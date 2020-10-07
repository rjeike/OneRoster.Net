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

namespace OneRosterSync.Net.DAL
{
    public class DistrictRepo
    {
        private readonly ILogger Logger = ApplicationLogging.Factory.CreateLogger<DistrictRepo>();
        private readonly ApplicationDbContext Db;
        private District district;
        private DataSyncHistory currentHistory;

        public DistrictRepo(ApplicationDbContext db, int districtId, int chunkSize = 50)
        {
            Db = db;
            DistrictId = districtId;
            ChunkSize = chunkSize;

            Committer = new ActionCounter(async () =>
            {
                await db.SaveChangesAsync();

                // clear local cache of objects
                district = null;
                currentHistory = null;

            }, chunkSize: ChunkSize);
        }

        public int ChunkSize { get; }

        public int DistrictId { get; }

        /// <summary>
        /// Use this to save changes in chunks
        /// </summary>
        public ActionCounter Committer { get; }

        /// <summary>
        /// District Loaded into memory, or null if not found
        /// Is cached until the next commit
        /// </summary>
        public District District => district ?? (district = Db.Districts.Find(DistrictId));

        public IQueryable<DistrictFilter> DistrictFilters => Db.DistrictFilters.Where(w => w.DistrictId == DistrictId);

        /// <summary>
        /// All DataSyncLines assocated with the District
        /// </summary>
        public IQueryable<DataSyncLine> Lines() => Db.DataSyncLines.Where(l => l.DistrictId == DistrictId);

        /// <summary>
        /// All DataSyncLines for a specific Entity Type
        /// </summary>
        public IQueryable<DataSyncLine> Lines<T>() where T : CsvBaseObject => Lines().Where(l => l.Table == typeof(T).Name);

        /// <summary>
        /// Add a DataSyncLine to the db and associated with this District
        /// </summary>
        public void AddLine(DataSyncLine line)
        {
            line.DistrictId = DistrictId;
            Db.DataSyncLines.Add(line);
        }

        public IQueryable<DataSyncHistory> DataSyncHistories =>
            Db.DataSyncHistories
                .Where(history => history.DistrictId == District.DistrictId);

        public DataSyncHistory CurrentHistory => currentHistory ?? (currentHistory =
            this.DataSyncHistories
                .OrderByDescending(h => h.Created)
                .FirstOrDefault());

        public async Task<DateTime?> GetLastLoadTime() =>
            await this.DataSyncHistories
                .AsNoTracking()
                .OrderByDescending(h => h.Created)
                .Where(h => h.LoadStarted.HasValue)
                .Select(h => h.LoadStarted)
                .FirstOrDefaultAsync();

        private DataSyncHistory PushHistory()
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


        public async Task DeleteDistrict()
        {
            if (District == null)
                return;

            // remove history
            await RemoveHistory();

            // now delete the lines
            await DeleteLines();

            // finally, delete the district itself!
            Db.Districts.Remove(District);
            await Committer.Invoke();
        }

        public async Task RemoveHistory()
        {
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
        }

        public async Task DeleteLines()
        {
            for (; ; )
            {
                var lines = await Lines().Take(5000).ToListAsync();
                if (!lines.Any())
                    break;
                Db.DataSyncLines.RemoveRange(lines);
                await Committer.Invoke();
            }
        }

        public void RecordProcessingError(string message, ProcessingStage processingStage)
        {
            DataSyncHistory history = CurrentHistory;
            if (history == null)
            {
                Logger.Here().LogError($"No current history.  Developer: create History record before Processing.");
                return;
            }

            switch (processingStage)
            {
                case ProcessingStage.Load: CurrentHistory.LoadError = message; break;
                case ProcessingStage.Analyze: CurrentHistory.AnalyzeError = message; break;
                case ProcessingStage.Apply: CurrentHistory.ApplyError = message; break;
                default:
                    Logger.Here().LogError($"Unexpected Processing Stage in exception: {processingStage}");
                    break;
            }
        }

        public void RecordProcessingStart(ProcessingStage processingStage)
        {
            var now = DateTime.UtcNow;
            var history = CurrentHistory ?? PushHistory();
            District.Touch();

            switch (processingStage)
            {
                case ProcessingStage.Load:
                    if (history.LoadStarted.HasValue ||
                        history.AnalyzeStarted.HasValue ||
                        history.ApplyStarted.HasValue)
                        history = PushHistory();
                    District.ProcessingStatus = ProcessingStatus.Loading;
                    history.LoadStarted = now;
                    break;

                case ProcessingStage.Analyze:
                    District.ProcessingStatus = ProcessingStatus.Analyzing;
                    if (history.AnalyzeStarted.HasValue)
                        history = PushHistory();
                    history.AnalyzeStarted = now;
                    break;

                case ProcessingStage.Apply:
                    District.ProcessingStatus = ProcessingStatus.Applying;
                    if (history.ApplyStarted.HasValue)
                        history = PushHistory();
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

        /// <summary>
        /// Computes the NextProcessingTime after NOW based on DailyProcessingTime
        /// Assigns to NextProcessingTime
        /// </summary>
        public static void UpdateNextProcessingTime(District district)
        {
            if (!district.DailyProcessingTime.HasValue)
            {
                district.NextProcessingTime = null;
                return;
            }

            var now = DateTime.UtcNow;

            // update the next processing to be the time of day called for either today or tomorrow if already passed
            DateTime next = now.Date.Add(district.DailyProcessingTime.Value);

            // if the time to process has already passed, then tomorrow
            if (next <= now)
                next = next.AddDays(1);

            district.NextProcessingTime = next;
        }

        public void PushLineHistory(DataSyncLine line, bool isNewData)
        {
            Db.DataSyncHistoryDetails.Add(new DataSyncHistoryDetail
            {
                DataSyncLineId = line.DataSyncLineId,
                DataSyncHistoryId = CurrentHistory.DataSyncHistoryId,
                DataNew = isNewData ? line.RawData : (string)null,
                IncludeInSync = line.IncludeInSync,
                LoadStatus = line.LoadStatus,
                SyncStatus = line.SyncStatus,
                Table = line.Table,
            });
        }

        public NCESMappingModel GetNCESMapping(string SchoolId)
        {
            var ncesMap = Db.NCESMappings
                .Where(w => w.StateID.EndsWith($"-{SchoolId}"))
                .Select(s => new NCESMappingModel()
                {
                    ncesId = s.NCESId,
                    stateSchoolId = s.StateID
                }).FirstOrDefault();
            return ncesMap;
        }

        public bool GetStopFlag(int DistrictID)
        {
            bool flag = false;
            var district = Db.Districts.FirstOrDefault(w => w.DistrictId == DistrictID);
            Db.Entry(district).ReloadAsync().Wait();
            if (district != null)
                flag = district.StopCurrentAction;

            return flag;
        }

        //public void SetStopFlag(int DistrictID, bool flag)
        //{
        //    var district = Db.Districts.FirstOrDefault(w => w.DistrictId == DistrictID);
        //    if (district != null)
        //        district.StopCurrentAction = flag;
        //}

        public async Task PushFilterAsync(FilterType filterType, string value)
        {
            var districtFilter = await Db.DistrictFilters.FirstOrDefaultAsync(w => w.DistrictId == DistrictId && w.FilterType == filterType && w.FilterValue == value.Trim());
            if (districtFilter == null)
            {
                await Db.DistrictFilters.AddAsync(new DistrictFilter()
                {
                    FilterValue = value.Trim(),
                    DistrictId = DistrictId,
                    FilterType = filterType,
                });
            }
        }

        public async Task UpdateFiltersAsync(FilterType filterType, IEnumerable<string> selectedGrades)
        {
            var districtFilters = await Db.DistrictFilters.Where(w => w.DistrictId == DistrictId && w.FilterType == filterType).ToListAsync();
            foreach (var districtFilter in districtFilters)
            {
                bool shouldBeApplied = selectedGrades.Contains(districtFilter.FilterValue);
                if (districtFilter.ShouldBeApplied == shouldBeApplied)
                    continue;
                else
                    districtFilter.ShouldBeApplied = shouldBeApplied;
                districtFilter.Touch();
            }
        }

        public async Task EmptyFiltersAsync()
        {
            var filters = await Db.DistrictFilters.Where(w => w.DistrictId == DistrictId).ToListAsync();
            if (filters.Any())
                Db.DistrictFilters.RemoveRange(filters);
            await Committer.Invoke();
        }
    }
}