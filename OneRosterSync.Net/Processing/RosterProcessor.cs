using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Models;

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
                            Logger.LogError($"Unexpected Processing status {district.ProcessingStatus} for District {district.Name} ({district.DistrictId})");
                            break;
                    }
                }
            }
        }

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
                string basePath = @"C:\dev\SummitK12\CSVSample\";

                var processor = new CsvFileProcessor
                {
                    Db = db,
                    DistrictId = district.DistrictId,
                    BasePath = basePath,
                    History = history,
                    Logger = Logger,
                    ChunkSize = 50,
                };

                await processor.ProcessFile<CsvOrg>(@"orgs.csv");
                await processor.ProcessFile<CsvCourse>(@"courses.csv");
                await processor.ProcessFile<CsvAcademicSession>(@"academicSessions.csv");
                await processor.ProcessFile<CsvClass>(@"classes.csv");
                await processor.ProcessFile<CsvEnrollment>(@"enrollments.csv");
                await processor.ProcessFile<CsvUser>(@"users.csv");

                await MarkDeleted(db, district.DistrictId, start);

                await Analyze(db, district.DistrictId);
            }
            catch (Exception /* ex*/)
            {
                //history.Error = ex.Message;
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

                /* 
                 * Web Services:
                 * create class
                 * create user
                 * modify user
                 * deactivate user
                 * enroll user to class
                 * unenroll user from class
                 */

                var api = new ApiManager();

                foreach (DataSyncLine _class in await lines.Where(l => l.Table == "CsvClass").ToListAsync())
                {
                    switch (_class.LoadStatus)
                    {
                        case LoadStatus.Added:
                            await api.Update(_class);

                            // TODO update SyncStatus
                            break;

                        case LoadStatus.Modified:
                        case LoadStatus.Deleted:
                            // ignore modified and deleted
                            break;

                        case LoadStatus.None:
                        case LoadStatus.NoChange:
                            Logger.LogWarning($"NoChange / None should not be flagged for Sync: {_class.RawData}");
                            break;
                    }
                }

                foreach (DataSyncLine user in await lines.Where(l => l.Table == "CsvUser").ToListAsync())
                {
                }

                foreach (DataSyncLine enrollment in await lines.Where(l => l.Table == "CsvEnrollment").ToListAsync())
                {
                }

            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
                throw;
            }
            finally
            {
                await Task.Delay(5000);

                district.ProcessingStatus = ProcessingStatus.Finished;
                district.Touch();
                await db.SaveChangesAsync();
            }

        }

        private async Task MarkDeleted(ApplicationDbContext db, int districtId, DateTime start)
        {
            var lines = db.DataSyncLines.Where(l => l.DistrictId == districtId);

            int i = 0;
            foreach (var line in await lines.Where(l => l.LastSeen < start).ToListAsync())
            {
                line.LoadStatus = LoadStatus.Deleted;
                if (++i > 50)
                {
                    await db.SaveChangesAsync();
                    i = 0;
                }
            }

            if (i > 0)
                await db.SaveChangesAsync();
        }


        /// <summary>
        /// TODO Speed up performance
        /// </summary>
        /// <param name="db"></param>
        /// <param name="districtId"></param>
        /// <returns></returns>
        private async Task Analyze(ApplicationDbContext db, int districtId)
        {
            var lines = db.DataSyncLines.Where(l => l.DistrictId == districtId);

            var orgs = await lines
                .Where(l => l.Table == "CsvOrg")
                .ToListAsync();
            foreach (var org in orgs)
            {
                org.SyncStatus = SyncStatus.ReadyToApply;
                org.Touch();
            }
            await db.SaveChangesAsync();

            var courses = await lines
                .Where(l => l.Table == "CsvCourse" && l.IncludeInSync)
                .ToListAsync();
            foreach (var course in courses.Where(c =>  c.LoadStatus != LoadStatus.NoChange))
            {
                course.SyncStatus = SyncStatus.ReadyToApply;
                course.Touch();
            }
            await db.SaveChangesAsync();

            var classes = await lines
                .Where(l => l.Table == "CsvClass")
                .ToListAsync();

            foreach (var _class in classes.Where(c => c.LoadStatus != LoadStatus.NoChange || !c.IncludeInSync))
            {
                CsvClass csvClass = Newtonsoft.Json.JsonConvert.DeserializeObject<CsvClass>(_class.RawData);
                if (courses.Select(c => c.SourceId).Contains(csvClass.courseSourcedId))
                {
                    _class.IncludeInSync = true;
                    _class.SyncStatus = SyncStatus.ReadyToApply;
                    _class.Touch();
                }
            }
            await db.SaveChangesAsync();

            // set of ides that 
            HashSet<string> classIds = classes
                .Where(c => c.IncludeInSync)
                .Select(c => c.SourceId)
                .ToHashSet();

            int i = 0;
            var enrollments = await lines
                .Where(l => l.Table == "CsvEnrollment")
                .ToListAsync();

            List<DataSyncLine> users = await lines
                .Where(l => l.Table == nameof(CsvUser))
                //.Where(u => u.LoadStatus != LoadStatus.NoChange)
                .ToListAsync();

            var userMap = users.ToDictionary(k => k.SourceId, v => v);

            foreach (var enrollment in enrollments /*.Where(e => e.LoadStatus != LoadStatus.NoChange)*/)
            {
                CsvEnrollment csvEnrollment = Newtonsoft.Json.JsonConvert.DeserializeObject<CsvEnrollment>(enrollment.RawData);
                if (!classIds.Contains(csvEnrollment.classSourcedId))
                    continue;

                // no change AND already included in the sync
                if (enrollment.LoadStatus == LoadStatus.NoChange && enrollment.IncludeInSync)
                    continue;

                enrollment.IncludeInSync = true;
                enrollment.SyncStatus = SyncStatus.ReadyToApply;
                enrollment.Touch();

                DataSyncLine user = userMap.ContainsKey(csvEnrollment.userSourcedId) ? userMap[csvEnrollment.userSourcedId] : null;

                if (user != null)
                {
                    if (user.LoadStatus != LoadStatus.NoChange || !user.IncludeInSync)
                    {
                        user.IncludeInSync = true;
                        user.SyncStatus = SyncStatus.ReadyToApply;
                        user.Touch();
                    }
                }

                if (++i > 50)
                {
                    await db.SaveChangesAsync();
                    i = 0;
                }
            }
            await db.SaveChangesAsync();
        }
    }
}