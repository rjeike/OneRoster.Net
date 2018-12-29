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
    public class CsvFileProcessor
    {
        public ApplicationDbContext Db { get; set; }
        public int DistrictId { get; set; }
        public string BasePath { get; set; }
        public DataSyncHistory History { get; set; }
        public ILogger Logger;
        public int ChunkSize { get; set; } = 50;

        public async Task ProcessFile<T>(string filename) where T : CsvBaseObject
        {
            string filePath = BasePath + filename;

            using (var file = System.IO.File.OpenText(filePath))
            {
                DateTime now = DateTime.UtcNow;
                using (var csv = new CsvHelper.CsvReader(file))
                {
                    csv.Configuration.MissingFieldFound = null;
                    csv.Configuration.HasHeaderRecord = true;

                    string table = null;

                    csv.Read();
                    csv.ReadHeader();
                    for (int i = 0; await csv.ReadAsync(); i++)
                    {
                        var record = csv.GetRecord<T>();
                        table = table ?? record.GetType().Name;

                        bool newRecord = await ProcessRecord(record, table, now);

                        if (newRecord || i > ChunkSize)
                        {
                            await Db.SaveChangesAsync();
                            i = 0;
                        }
                    }

                    // commit any last changes
                    await Db.SaveChangesAsync();
                }
                Logger.Here().LogInformation($"Processed Csv file {filePath}");
            }
        }

        private async Task<bool> ProcessRecord<T>(T record, string table, DateTime now) where T : CsvBaseObject
        {
            History.NumRows++;
            string data = JsonConvert.SerializeObject(record);

            DataSyncLine line = await Db.DataSyncLines.SingleOrDefaultAsync(l => l.DistrictId == DistrictId && l.SourceId == record.sourcedId);
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
                    DistrictId = DistrictId,
                    LoadStatus = LoadStatus.Added,
                    LastSeen = now,
                    Table = table,
                };
                Db.DataSyncLines.Add(line);
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
            Db.DataSyncHistoryDetails.Add(detail);

            line.RawData = data;
            line.SourceId = record.sourcedId;
            line.SyncStatus = SyncStatus.Loaded;

            return newRecord;
        }
    }
}