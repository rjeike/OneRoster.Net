using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Extensions;
using OneRosterSync.Net.Models;

namespace OneRosterSync.Net.Processing
{
    public class Loader
    {
        private readonly ILogger Logger;
        private readonly DistrictRepo Repo;

        private readonly string BasePath;
        private readonly DataSyncHistory History;

        public Loader(ILogger logger, DistrictRepo repo, string basePath, DataSyncHistory history)
        {
            Logger = logger;
            Repo = repo;
            BasePath = basePath;
            History = history;
        }

        public async Task LoadFile<T>(string filename) where T : CsvBaseObject
        {
            DateTime now = DateTime.UtcNow;
            string filePath = BasePath + filename;
            string table = typeof(T).Name;

            using (var file = System.IO.File.OpenText(filePath))
            {
                using (var csv = new CsvHelper.CsvReader(file))
                {
                    csv.Configuration.MissingFieldFound = null;
                    csv.Configuration.HasHeaderRecord = true;

                    csv.Read();
                    csv.ReadHeader();
                    for (int i = 0; await csv.ReadAsync(); i++)
                    {
                        var record = csv.GetRecord<T>();
                        bool newRecord = await ProcessRecord(record, table, now);
                        await Repo.Committer.InvokeIfChunk();
                    }

                    // commit any last changes
                    await Repo.Committer.InvokeIfAny();
                }
                Logger.Here().LogInformation($"Processed Csv file {filePath}");
            }
        }

        private async Task<bool> ProcessRecord<T>(T record, string table, DateTime now) where T : CsvBaseObject
        {
            History.NumRows++;
            string data = JsonConvert.SerializeObject(record);

            DataSyncLine line = await Repo.Lines<T>().SingleOrDefaultAsync(l => l.SourceId == record.sourcedId);
            bool newRecord = line == null;

            if (newRecord)
            {
                // already deleted
                if (record.isDeleted)
                {
                    History.NumDeleted++;
                    return false;
                }

                History.NumAdded++;
                line = new DataSyncLine
                {
                    SourceId = record.sourcedId,
                    DistrictId = Repo.DistrictId,
                    LoadStatus = LoadStatus.Added,
                    LastSeen = now,
                    Table = table,
                };
                Repo.AddLine(line);
            }
            else // existing record, check if it has changed
            {
                line.LastSeen = now;
                line.Touch();

                // no change to the data, skip!
                if (line.RawData == data)
                {
                    if (line.SyncStatus != SyncStatus.Loaded)
                        line.LoadStatus = LoadStatus.NoChange;
                    return false;
                }

                // status should be deleted
                if (record.isDeleted)
                {
                    History.NumDeleted++;
                    line.LoadStatus = LoadStatus.Deleted;
                }
                else if (line.SyncStatus == SyncStatus.Loaded && line.LoadStatus == LoadStatus.Added)
                {
                    History.NumAdded++;
                    line.LoadStatus = LoadStatus.Added; // if added, leave added
                }
                else
                {
                    History.NumModified++;
                    line.LoadStatus = LoadStatus.Modified;
                }
            }

            DataSyncHistoryDetail detail = new DataSyncHistoryDetail
            {
                DataOrig = line.RawData,
                DataNew = data,
                DataSyncLine = line,
                DataSyncHistory = History,
                LoadStatus = line.LoadStatus,
                Table = table,
            };
            Repo.PushHistoryDetail(detail);

            line.RawData = data;
            line.SourceId = record.sourcedId;
            line.SyncStatus = SyncStatus.Loaded;

            return newRecord;
        }
    }
}