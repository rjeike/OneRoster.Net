using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OneRosterSync.Net.Extensions;
using OneRosterSync.Net.Models;
using OneRosterSync.Net.Utils;

namespace OneRosterSync.Net.Processing
{
    public class Applier
    {
        private readonly ILogger Logger;
        private readonly DistrictRepo Repo;
        private readonly ApiManager Api;

        public Applier(ILogger logger, DistrictRepo repo, ApiManager api)
        {
            Logger = logger;
            Repo = repo;
            Api = api;
        }

        /// <summary>
        /// Apply all records of a given Csv type the LMS
        /// </summary>
        public async Task ApplyLines<T>() where T : CsvBaseObject
        {
            var lines = Repo.Lines<T>().Where(l => l.IncludeInSync && l.SyncStatus == SyncStatus.ReadyToApply);

            foreach (DataSyncLine line in await lines.ToListAsync())
            {
                switch (line.LoadStatus)
                {
                    default:
                        await ApplyLine<T>(line);
                        break;

                    case LoadStatus.None:
                        Logger.Here().LogWarning($"None should not be flagged for Sync: {line.RawData}");
                        break;
                }
            }
        }

        private async Task ApplyLine<T>(DataSyncLine line) where T : CsvBaseObject
        {
            ApiPostBase data;
            
            if (line.Table == nameof(CsvEnrollment))
            {
                var enrollment = new ApiEnrollmentPost(line.RawData);
               
                CsvEnrollment csvEnrollment = JsonConvert.DeserializeObject<CsvEnrollment>(line.RawData);
                DataSyncLine cls = Repo.Lines<CsvClass>().SingleOrDefault(l => l.SourceId == csvEnrollment.classSourcedId);
                DataSyncLine usr = Repo.Lines<CsvUser>().SingleOrDefault(l => l.SourceId == csvEnrollment.userSourcedId);

                var map = new EnrollmentMap
                {
                    classTargetId = cls?.TargetId,
                    userTargetId = usr?.TargetId,
                };

                enrollment.EnrollmentMap = map; // API data - give LMS IDs in it's own system
                line.EnrollmentMap = JsonConvert.SerializeObject(map); // cache it in the database - for display only

                data = enrollment;
            }
            else
            {
                data = new ApiPost<T>(line.RawData);
            }
                
            data.DistrictId = Repo.DistrictId.ToString();
            data.DistrictName = Repo.District.Name;
            data.LastSeen = line.LastSeen;
            data.SourceId = line.SourceId;
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
            await Repo.Committer.Invoke();
        }
    }
}
