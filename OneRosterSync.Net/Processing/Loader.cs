using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly ILogger Logger = ApplicationLogging.Factory.CreateLogger<Loader>();
        private readonly DistrictRepo Repo;
        private readonly string BasePath;

        public string LastEntity { get; private set; }

        public Loader(DistrictRepo repo, string basePath)
        {
            Repo = repo;
            BasePath = basePath;
        }

        public async Task LoadFile<T>(string filename) where T : CsvBaseObject
        {
            LastEntity = typeof(T).Name; // kludge
            DateTime now = DateTime.UtcNow;
            string filePath = Path.Combine(BasePath, filename);
            string table = typeof(T).Name;

            using (var file = File.OpenText(filePath))
            {
                using (var csv = new CsvHelper.CsvReader(file))
                {
                    csv.Configuration.MissingFieldFound = null;
                    csv.Configuration.HasHeaderRecord = true;

                    csv.Read();
                    csv.ReadHeader();
                    for (int i = 0; await csv.ReadAsync(); i++)
                    {
                        T record = null;
                        try
                        {
                            record = csv.GetRecord<T>();
                            await ProcessRecord(record, table, now);
                        }
                        catch (Exception ex)
                        {
                            if (ex is ProcessingException)
                                throw;

                            string o = record == null ? "(null)" : JsonConvert.SerializeObject(record);
                            throw new ProcessingException(Logger.Here(), ProcessingStage.Load, 
                                $"Unhandled error processing {typeof(T).Name}: {o}", innerException: ex);
                        }

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
                throw new ProcessingException(Logger.Here(), ProcessingStage.Load, 
                    $"Record of type {typeof(T).Name} contains no SourcedId: {JsonConvert.SerializeObject(record)}");

            DataSyncLine line = await Repo.Lines<T>().SingleOrDefaultAsync(l => l.SourcedId == record.sourcedId);

            bool newRecord = line == null;

            Repo.CurrentHistory.NumRows++;
            string data = JsonConvert.SerializeObject(record);

            if (newRecord)
            {
                // already deleted
                if (record.isDeleted)
                {
                    Repo.CurrentHistory.NumDeleted++;
                    return false;
                }

                Repo.CurrentHistory.NumAdded++;
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
                    Repo.CurrentHistory.NumDeleted++;
                    line.LoadStatus = LoadStatus.Deleted;
                }
                else if (line.SyncStatus == SyncStatus.Loaded && line.LoadStatus == LoadStatus.Added)
                {
                    Repo.CurrentHistory.NumAdded++;
                    line.LoadStatus = LoadStatus.Added; // if added, leave added
                }
                else
                {
                    Repo.CurrentHistory.NumModified++;
                    line.LoadStatus = LoadStatus.Modified;
                }
            }

            DataSyncHistoryDetail detail = new DataSyncHistoryDetail
            {
                DataOrig = line.RawData,
                DataNew = data,
                DataSyncLine = line,
                DataSyncHistory = Repo.CurrentHistory,
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