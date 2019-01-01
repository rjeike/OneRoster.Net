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
    public class LoadException : Exception
    {
        public LoadException(ILogger logger, string message, Exception innerException = null)
            : base(message, innerException)
        {
            logger.LogError(message, innerException);
        }
    }

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
                    int errors = 0;
                    for (int i = 0; await csv.ReadAsync(); i++)
                    {
                        try
                        {
                            var record = csv.GetRecord<T>();
                            await ProcessRecord(record, table, now);
                        }
                        catch (Exception)
                        {
                            errors++;
                        }

                        // enough errors, give up
                        if (errors > 100)
                            break;

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
            if (string.IsNullOrEmpty(record.sourcedId))
                throw new LoadException(Logger.Here(), $"Record contains no SourcedId: {JsonConvert.SerializeObject(record)}");

            DataSyncLine line;

            try
            {
                line = await Repo.Lines<T>().SingleOrDefaultAsync(l => l.SourcedId == record.sourcedId);
            }
            catch (InvalidOperationException ex)
            {
                throw new LoadException(Logger.Here(), $"Multiple records of type {typeof(T).Name} for sourcedId {record.sourcedId} for {JsonConvert.SerializeObject(record)}", ex);
            }

            bool newRecord = line == null;

            History.NumRows++;
            string data = JsonConvert.SerializeObject(record);

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
                    SourcedId = record.sourcedId,
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
            line.SourcedId = record.sourcedId;
            line.SyncStatus = SyncStatus.Loaded;

            return newRecord;
        }
    }
}