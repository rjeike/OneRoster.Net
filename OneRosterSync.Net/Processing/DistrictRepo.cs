using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Models;
using OneRosterSync.Net.Utils;

namespace OneRosterSync.Net.Processing
{
    public class DistrictRepo
    {
        private readonly ILogger Logger;
        private readonly ApplicationDbContext Db;
        private District district;

        public DistrictRepo(ILogger logger, ApplicationDbContext db, int districtId)
        {
            Logger = logger;
            Db = db;
            DistrictId = districtId;

            Committer = new ActionCounter(
                asyncAction: async () => { await db.SaveChangesAsync(); },
                chunkSize: 50);
        }

        public ActionCounter Committer { get; private set; }

        public int DistrictId { get; private set; }

        public District District => district ?? (district = Db.Districts.Find(DistrictId));

        /// <summary>
        /// All DataSyncLines assocated with the District
        /// </summary>
        public IQueryable<DataSyncLine> Lines() => Db.DataSyncLines.Where(l => l.DistrictId == DistrictId);

        /// <summary>
        /// All DataSyncLines for a specific Entity Type
        /// </summary>
        public IQueryable<DataSyncLine> Lines<T>() where T : CsvBaseObject => Lines().Where(l => l.Table == typeof(T).Name); 

        public void AddLine(DataSyncLine line)
        {
            line.DistrictId = DistrictId;
            Db.DataSyncLines.Add(line);
        }

        private DataSyncHistory currentHistory;

        public DataSyncHistory CurrentHistory => currentHistory ?? (currentHistory =
            Db.DataSyncHistories
                .Where(h => h.DistrictId == DistrictId)
                .OrderByDescending(h => h.Created)
                .FirstOrDefault());

        public DataSyncHistory PushHistory()
        {
            var currentHistory = new DataSyncHistory
            {
                DistrictId = DistrictId,
                Started = DateTime.UtcNow,
            };
            currentHistory.Touch();
            Db.DataSyncHistories.Add(currentHistory);
            return currentHistory;
        }

        public void PushHistoryDetail(DataSyncHistoryDetail detail)
        {
            // must apply to current history
            detail.DataSyncHistoryId = CurrentHistory.DataSyncHistoryId;
            Db.DataSyncHistoryDetails.Add(detail);
        }
    }
}