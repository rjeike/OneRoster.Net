using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Extensions;
using OneRosterSync.Net.Models;
using OneRosterSync.Net.Utils;

namespace OneRosterSync.Net.Processing
{
    public class RosterProcessor
    {
        private readonly IServiceProvider Services;
        private readonly ILogger Logger;

        public RosterProcessor(
            IServiceProvider services,
            ILogger logger)
        {
            Services = services;
            Logger = logger;
        }

        /// <summary>
        /// Helper for saving data in chunks
        /// </summary>
        private static ActionCounter CreateCommitter(ApplicationDbContext db) =>
            new ActionCounter(
                asyncAction: async () => { await db.SaveChangesAsync(); },
                chunkSize: 50);

        /// <summary>
        /// Process a district's OneRoster CSV feed
        /// </summary>
        /// <param name="districtId">District Id</param>
        /// <param name="cancellationToken">Token to cancel operation (not currently used)</param>
        public async Task ProcessDistrict(int districtId, CancellationToken cancellationToken)
        {
            using (var scope = Services.CreateScope())
            {
                using (var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
                {
                    District district = db.Districts.Find(districtId);
                    district.Touch();
                    await db.SaveChangesAsync();

                    switch (district.ProcessingStatus)
                    {
                        case ProcessingStatus.Scheduled:
                            await LoadDistrictData(db, district);
                            break;

                        case ProcessingStatus.Approved:
                            await ApplyDistrictData(db, district);
                            break;

                        default:
                            Logger.Here().LogError($"Unexpected Processing status {district.ProcessingStatus} for District {district.Name} ({district.DistrictId})");
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Load the District CSV data into the database
        /// This is the first step of the Processing
        /// </summary>
        private async Task LoadDistrictData(ApplicationDbContext db, District district)
        {
            DataSyncHistory history = null;
            try
            {
                district.ProcessingStatus = ProcessingStatus.Loading;
                await db.SaveChangesAsync();

                history = new DataSyncHistory
                {
                    DistrictId = district.DistrictId,
                    Started = DateTime.UtcNow,
                };
                db.DataSyncHistories.Add(history);
                await db.SaveChangesAsync();

                DateTime start = DateTime.UtcNow;

                var processor = new CsvFileProcessor
                {
                    Db = db,
                    DistrictId = district.DistrictId,
                    BasePath = @"CSVSample\", // TODO pull this from the district
                    History = history,
                    Logger = Logger,
                    ChunkSize = 50,
                };

                await processor.ProcessFile<CsvOrg>(@"orgs.csv");
                await processor.ProcessFile<CsvCourse>(@"courses.csv");
                await processor.ProcessFile<CsvAcademicSession>(@"academicSessions.csv");
                await processor.ProcessFile<CsvClass>(@"classes.csv");
                await processor.ProcessFile<CsvUser>(@"users.csv");
                await processor.ProcessFile<CsvEnrollment>(@"enrollments.csv");

                await MarkDeleted(db, district.DistrictId, start);

                await Analyze(db, district.DistrictId);
            }
            catch (Exception ex)
            {
                Logger.Here().LogError(ex, "Error Loading District Data.");
            }
            finally
            {
                district.ProcessingStatus = ProcessingStatus.PendingApproval;
                district.Touch();
                if (history != null)
                    history.Completed = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        private async Task ApplyDistrictData(ApplicationDbContext db, District district)
        {
            try
            {
                district.ProcessingStatus = ProcessingStatus.Applying;
                district.Touch();
                await db.SaveChangesAsync();

                var lines = db.DataSyncLines
                    .Where(l => l.DistrictId == district.DistrictId)
                    .Where(l => l.IncludeInSync && l.SyncStatus == SyncStatus.ReadyToApply);

                var api = new ApiManager(Logger);

                string[] entities = new string[]
                {
                    "Org",
                    "Course",
                    "AcademicSession",
                    "Class",
                    "User",
                    "Enrollment",
                };

                foreach (string entity in entities)
                {
                    string csvEntity = $"Csv{entity}";
                    foreach (DataSyncLine line in await lines.Where(l => l.Table == csvEntity).ToListAsync())
                    {
                        switch (line.LoadStatus)
                        {
                            case LoadStatus.Added:
                            case LoadStatus.Modified:
                            case LoadStatus.Deleted:
                                ApiPostBase data = ApiPostBase.CreateApiPost(entity, line.RawData);

                                data.DistrictId = district.DistrictId.ToString();
                                data.DistrictName = district.Name;
                                data.LastSeen = line.LastSeen;
                                data.SourceId = line.SourceId;
                                data.TargetId = line.TargetId;
                                data.Status = line.LoadStatus.ToString();

                                ApiResponse response = await api.Post(data.EntityType.ToLower(), data);
                                if (response.Success)
                                {
                                    line.SyncStatus = SyncStatus.Applied;
                                    if (!string.IsNullOrEmpty(response.TargetId))
                                        line.TargetId = response.TargetId;
                                    line.Error = null;
                                }
                                else
                                {
                                    line.SyncStatus = SyncStatus.ApplyFailed;
                                    line.Error = response.ErrorMessage;
                                }
                                await db.SaveChangesAsync();
                                break;

                            case LoadStatus.None:
                            case LoadStatus.NoChange:
                                Logger.Here().LogWarning($"NoChange / None should not be flagged for Sync: {line.RawData}");
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Here().LogError(ex.Message);
                throw;
            }
            finally
            {
                district.ProcessingStatus = ProcessingStatus.Finished;
                district.Touch();
                await db.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Identifies records that were missing from the feed and marks them as Deleted
        /// </summary>
        private static async Task MarkDeleted(ApplicationDbContext db, int districtId, DateTime start)
        {
            var committer = CreateCommitter(db);
            var lines = db.DataSyncLines.Where(l => l.DistrictId == districtId);
            foreach (var line in await lines.Where(l => l.LastSeen < start).ToListAsync())
            {
                line.LoadStatus = LoadStatus.Deleted;
                await committer.InvokeIfChunk();
            }
            await committer.InvokeIfAny();
        }


        /// <summary>
        /// Analyze the records to determine which should be included in the feed
        /// based on dependencies.
        /// </summary>
        private async Task Analyze(ApplicationDbContext db, int districtId)
        {
            var committer = CreateCommitter(db);

            var lines = db.DataSyncLines.Where(l => l.DistrictId == districtId);

            // This loads the entire set of DataSyncLines associated with the district into memory
            // This should be comfortable for 50K students or so with a reasonable amount of computer memory.
            // Performance testing is needed...
            var cache = new DataLineCache(Logger);
            await cache.Load(lines);

            foreach (var org in cache.GetMap<CsvOrg>().Values)
            {
                org.SyncStatus = SyncStatus.ReadyToApply;
                org.Touch();
            }
            await committer.Invoke();

            var courses = cache.GetMap<CsvCourse>().Values.Where(l => l.IncludeInSync).ToList();
            foreach (var course in courses.Where(c =>  c.LoadStatus != LoadStatus.NoChange))
            {
                course.SyncStatus = SyncStatus.ReadyToApply;
                course.Touch();
            }
            await committer.Invoke();

            var classMap = cache.GetMap<CsvClass>();

            foreach (var _class in classMap.Values.Where(c => c.LoadStatus != LoadStatus.NoChange || !c.IncludeInSync))
            {
                CsvClass csvClass = JsonConvert.DeserializeObject<CsvClass>(_class.RawData);
                if (courses.Select(c => c.SourceId).Contains(csvClass.courseSourcedId))
                {
                    _class.IncludeInSync = true;
                    _class.SyncStatus = SyncStatus.ReadyToApply;
                    _class.Touch();
                }
                await committer.InvokeIfChunk();
            }
            await committer.InvokeIfAny();

            var enrollments = cache.GetMap<CsvEnrollment>().Values;
            var userMap = cache.GetMap<CsvUser>();

            foreach (var enrollment in enrollments)
            {
                // check if the enrollment is included in the classes, if not skip
                CsvEnrollment csvEnrollment = JsonConvert.DeserializeObject<CsvEnrollment>(enrollment.RawData);
                if (!classMap.Keys.Contains(csvEnrollment.classSourcedId))
                    continue;

                // if there is no change AND already included in the sync, skip
                //if (enrollment.LoadStatus == LoadStatus.NoChange && enrollment.IncludeInSync)
                //    continue;

                // check that the user referenced exists (should alway exist, perhaps should log error)
                DataSyncLine user = userMap.ContainsKey(csvEnrollment.userSourcedId) ? userMap[csvEnrollment.userSourcedId] : null;
                if (user == null)
                    continue;

                enrollment.IncludeInSync = true;
                enrollment.SyncStatus = SyncStatus.ReadyToApply;
                enrollment.Touch();

                if (user.LoadStatus != LoadStatus.NoChange || !user.IncludeInSync)
                {
                    user.IncludeInSync = true;
                    user.SyncStatus = SyncStatus.ReadyToApply;
                    user.Touch();
                }

                enrollment.EnrollmentMap = JsonConvert.SerializeObject(new EnrollmentMap
                {
                    classTargetId = classMap[csvEnrollment.classSourcedId].TargetId,
                    userTargetId = user.TargetId,
                });

                await committer.InvokeIfChunk();
            }
            await committer.InvokeIfAny();
        }
    }
}