using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Extensions;
using OneRosterSync.Net.Models;

namespace OneRosterSync.Net.Processing
{
    public class Applier
    {
        private readonly ILogger Logger = ApplicationLogging.Factory.CreateLogger<Analyzer>();

        private readonly IServiceProvider Services;
        private readonly int DistrictId;
        private readonly ApiManager Api;

        /// <summary>
        /// How many APIs should we call in parallel?
        /// TODO: make a property of the District
        /// </summary>
        public int ParallelChunkSize { get; set; } = 10;

        public Applier(IServiceProvider services, int districtId, ApiManager api)
        {
            Services = services;
            DistrictId = districtId;
            Api = api;
        }

        /// <summary>
        /// Apply all records of a given entity type to the LMS
        /// </summary>
        public async Task ApplyLines<T>() where T : CsvBaseObject
        {
            for (int last = 0; ; )
            {
                using (var scope = Services.CreateScope())
                using (var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
                {
                    var repo = new DistrictRepo(db, DistrictId);

                    // filter on all lines that are included and ready to be applied
                    var lines = repo.Lines<T>().Where(l => l.IncludeInSync && l.SyncStatus == SyncStatus.ReadyToApply);

                    // how many records are remaining to process?
                    int curr = await lines.CountAsync();
                    if (curr == 0)
                        break;

                    // after each process, the remaining record count should go down
                    // this avoids and infinite loop in case there is an problem processing
                    // basically, we bail if no progress is made at all
                    if (last > 0 && last <= curr)
                        throw new ProcessingException(Logger, "Apply failed to update SyncStatus of applied record.");
                    last = curr;

                    // process chunks of lines in parallel
                    IEnumerable<Task> tasks = await lines
                        .AsNoTracking()
                        .Take(ParallelChunkSize)
                        .Select(line => ApplyLineParallel<T>(line))
                        .ToListAsync();

                    await Task.WhenAll(tasks);
                }
            }
        }

        private async Task ApplyLineParallel<T>(DataSyncLine line) where T : CsvBaseObject
        {
            // we need a new DataContext to avoid concurrency issues
            using (var scope = Services.CreateScope())
            using (var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
            {
                // re-create the Repo and Data pulled from it
                var repo = new DistrictRepo(db, DistrictId);
                var newLine = await repo.Lines<T>().SingleAsync(l => l.DataSyncLineId == line.DataSyncLineId);
                await ApplyLine<T>(repo, newLine);
                await repo.Committer.Invoke();
            }
        }

        private async Task ApplyLine<T>(DistrictRepo repo, DataSyncLine line) where T : CsvBaseObject
        {
            switch (line.LoadStatus)
            {
                case LoadStatus.None:
                    Logger.Here().LogWarning($"None should not be flagged for Sync: {line.RawData}");
                    return;
            }

            ApiPostBase data;

            if (line.Table == nameof(CsvEnrollment))
            {
                var enrollment = new ApiEnrollmentPost(line.RawData);
               
                CsvEnrollment csvEnrollment = JsonConvert.DeserializeObject<CsvEnrollment>(line.RawData);
                DataSyncLine cls = repo.Lines<CsvClass>().SingleOrDefault(l => l.SourcedId == csvEnrollment.classSourcedId);
                DataSyncLine usr = repo.Lines<CsvUser>().SingleOrDefault(l => l.SourcedId == csvEnrollment.userSourcedId);

                var map = new EnrollmentMap
                {
                    classTargetId = cls?.TargetId,
                    userTargetId = usr?.TargetId,
                };

                // this provides a mapping of LMS TargetIds (rather than sourcedId's)
                enrollment.EnrollmentMap = map;

                // cache map in the database (for display/troubleshooting only)
                line.EnrollmentMap = JsonConvert.SerializeObject(map); 

                data = enrollment;
            }
            else
            {
                data = new ApiPost<T>(line.RawData);
            }
                
            data.DistrictId = repo.DistrictId.ToString();
            data.DistrictName = repo.District.Name;
            data.LastSeen = line.LastSeen;
            data.SourcedId = line.SourcedId;
            data.TargetId = line.TargetId;
            data.Status = line.LoadStatus.ToString();

            ApiResponse response = await Api.Post(data.EntityType.ToLower(), data);
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

            line.Touch();
        }
    }
}