using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Extensions;
using OneRosterSync.Net.Models;
using OneRosterSync.Net.Utils;

namespace OneRosterSync.Net.Processing
{
    public class DistrictRepo
    {
        private readonly ILogger Logger;
        private readonly ApplicationDbContext Db;
        private District district;

        private readonly int ChunkSize;

        public DistrictRepo(ILogger logger, ApplicationDbContext db, int districtId, int chunkSize = 50)
        {
            Logger = logger;
            Db = db;
            DistrictId = districtId;
            ChunkSize = chunkSize;

            Committer = new ActionCounter(async () => await db.SaveChangesAsync(), chunkSize: ChunkSize);
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

        public async Task DeleteDistrict()
        {
            if (District == null)
                return;

            // retrieve entire list of histories
            var histories = await Db.DataSyncHistories
                .Where(h => h.DistrictId == DistrictId)
                .ToListAsync();

            // delete the histories in chunks
            await histories
                .AsQueryable()
                .ForEachInChunksAsync(
                    chunkSize: ChunkSize,
                    action: async history =>
                    {
                        // for each history, delete the details associated with it as well
                        var details = await Db.DataSyncHistoryDetails
                            .Where(d => d.DataSyncHistoryId == history.DataSyncHistoryId)
                            .ToListAsync();
                        Db.DataSyncHistoryDetails.RemoveRange(details);
                    },
                    // commit changes after each chunk
                    onChunkComplete: async () => await Committer.Invoke());

            Db.DataSyncHistories.RemoveRange(histories);
            await Committer.Invoke();

            // now delete the lines
            for (; ; )
            {
                var lines = await Lines().Take(ChunkSize).ToListAsync();
                if (!lines.Any())
                    break;
                Db.DataSyncLines.RemoveRange(lines);
                await Committer.Invoke();
            }

            // finally, delete the district itself!
            Db.Districts.Remove(District);
            await Committer.Invoke();
        }

        public void RecordProcessingError(ProcessingException pe)
        {
            DataSyncHistory history = CurrentHistory;
            if (history == null)
            {
                Logger.Here().LogError($"No current history.  Developer: create History record before Processing.");
                return;
            }
           
            switch (pe.ProcessingStage)
            {
                case ProcessingStage.Load: CurrentHistory.LoadError = pe.Message; break;
                case ProcessingStage.Analyze: CurrentHistory.AnalyzeError = pe.Message; break;
                case ProcessingStage.Apply: CurrentHistory.ApplyError = pe.Message; break;
                default:
                    Logger.Here().LogError($"Unexpected Processing Stage in exception: {pe.ProcessingStage}");
                    break;
            }
        }

        public void RecordProcessingStart(ProcessingStage processingStage)
        {
            var now = DateTime.UtcNow;
            var history = CurrentHistory;
            District.Touch();

            switch (processingStage)
            {
                case ProcessingStage.Load:
                    District.ProcessingStatus = ProcessingStatus.Loading;
                    history.LoadError = null; // clear error
                    history.LoadStarted = now;
                    break;

                case ProcessingStage.Analyze:
                    District.ProcessingStatus = ProcessingStatus.Analyzing;
                    history.AnalyzeError = null; // clear error
                    history.AnalyzeStarted = now;
                    break;

                case ProcessingStage.Apply:
                    District.ProcessingStatus = ProcessingStatus.Applying;
                    history.ApplyError = null; // clear error
                    history.ApplyStarted = now;
                    break;

                default:
                    Logger.Here().LogError($"Unexpected Processing Stage: {processingStage}");
                    break;
            }
        }

        public void RecordProcessingStop(ProcessingStage processingStage)
        {
            var now = DateTime.UtcNow;
            var history = CurrentHistory;
            District.Touch();

            switch (processingStage)
            {
                case ProcessingStage.Load:
                    District.ProcessingStatus = ProcessingStatus.LoadingDone;
                    history.LoadCompleted = now;
                    break;

                case ProcessingStage.Analyze:
                    District.ProcessingStatus = ProcessingStatus.AnalyzingDone;
                    history.AnalyzeCompleted = now;
                    break;

                case ProcessingStage.Apply:
                    District.ProcessingStatus = ProcessingStatus.ApplyingDone;
                    history.ApplyCompleted = now;
                    break;

                default:
                    Logger.Here().LogError($"Unexpected Processing Stage: {processingStage}");
                    break;
            }
        }

    }
}