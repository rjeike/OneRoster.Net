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

                var lines = db.DataSyncLines.Where(l => l.DistrictId == district.DistrictId);
                var analyzer = new Analyzer(Logger, district, lines, CreateCommitter(db));
                await analyzer.MarkDeleted(start);
                await analyzer.Analyze();
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

                DataLineCache cache = new DataLineCache(Logger);
                foreach (string entity in entities)
                {
                    /*
                    if (entity == "Enrollment")
                    {
                        //await Analyze(db, district.DistrictId);
                        await cache.Load(db.DataSyncLines.Where(l => l.DistrictId == district.DistrictId));
                    }
                    */

                    foreach (DataSyncLine line in await lines.Where(l => l.Table == $"Csv{entity}" && l.SyncStatus == SyncStatus.ReadyToApply).ToListAsync())
                    {
                        switch (line.LoadStatus)
                        {
                            case LoadStatus.Added:
                            case LoadStatus.Modified:
                            case LoadStatus.Deleted:
                            case LoadStatus.NoChange:
                                await ApplyLine(entity, line, api, db, district, cache);
                                break;

                            case LoadStatus.None:
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

        private static async Task ApplyLine(string entity, DataSyncLine line, ApiManager api, ApplicationDbContext db, District district, DataLineCache cache)
        {
            ApiPostBase data = ApiPostBase.CreateApiPost(entity, line.RawData);

            data.DistrictId = district.DistrictId.ToString();
            data.DistrictName = district.Name;
            data.LastSeen = line.LastSeen;
            data.SourceId = line.SourceId;
            data.TargetId = line.TargetId;
            data.Status = line.LoadStatus.ToString();

            if (entity == "Enrollment")
            {
                // kludge...
                //var classMap = cache.GetMap<CsvClass>();
                //var userMap = cache.GetMap<CsvUser>();
                CsvEnrollment csvEnrollment = JsonConvert.DeserializeObject<CsvEnrollment>(line.RawData);

                var lines = db.DataSyncLines.Where(l => l.DistrictId == district.DistrictId);
                DataSyncLine _class = lines.Where(l => l.Table == "CsvClass").SingleOrDefault(l => l.SourceId == csvEnrollment.classSourcedId);
                DataSyncLine user = lines.Where(l => l.Table == "CsvUser").SingleOrDefault(l => l.SourceId == csvEnrollment.userSourcedId);

                data.EnrollmentMap = new EnrollmentMap
                {
                    //classTargetId = classMap.ContainsKey(csvEnrollment.classSourcedId) ? classMap[csvEnrollment.classSourcedId].TargetId : null,
                    //userTargetId = userMap.ContainsKey(csvEnrollment.userSourcedId) ? userMap[csvEnrollment.userSourcedId].TargetId : null,
                    classTargetId = _class?.TargetId,
                    userTargetId = user?.TargetId,
                };
                // cache it in the database - for display only
                line.EnrollmentMap = JsonConvert.SerializeObject(data.EnrollmentMap);
            }

            ApiResponse response = await api.Post(data.EntityType.ToLower(), data);
            if (response.Success)
            {
                line.SyncStatus = SyncStatus.Applied;
                if (!string.IsNullOrEmpty(response.TargetId))
                    line.TargetId = response.TargetId;
                line.Error = null;

                /*
                switch (entity)
                {
                    case "Class":
                        //var enrollments = db.DataSyncLines.Where(l => l.DistrictId == district.DistrictId).Where(l => l.Table == nameof(CsvEnrollment)).Where(l => l.)
                        break;

                    case "User":
                        break;
                }
                */
            }
            else
            {
                line.SyncStatus = SyncStatus.ApplyFailed;
                line.Error = response.ErrorMessage;
            }

            line.Touch();
            await db.SaveChangesAsync();
        }
    }
}