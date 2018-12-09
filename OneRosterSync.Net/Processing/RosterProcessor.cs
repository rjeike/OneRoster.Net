using System;
using System.Collections.Generic;
using System.Linq;
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
                    district.ProcessingStatus = ProcessingStatus.LoadProcessing;
                    district.Touch();
                    await db.SaveChangesAsync();

                    DataSyncHistory history = new DataSyncHistory
                    {
                        DistrictId = districtId,
                        Started = DateTime.UtcNow,
                    };
                    db.DataSyncHistories.Add(history);
                    await db.SaveChangesAsync();

                    try
                    {
                        DateTime start = DateTime.UtcNow;
                        string basePath = @"C:\dev\SummitK12\CSVSample\";
                        await ProcessCsvFile<CsvOrg>(db, districtId, basePath + @"orgs.csv", 50, history);
                        await ProcessCsvFile<CsvCourse>(db, districtId, basePath + @"courses.csv", 50, history);
                        await ProcessCsvFile<CsvAcademicSession>(db, districtId, basePath + @"academicSessions.csv", 50, history);
                        await ProcessCsvFile<CsvClass>(db, districtId, basePath + @"classes.csv", 50, history);
                        await ProcessCsvFile<CsvEnrollment>(db, districtId, basePath + @"enrollments.csv", 50, history);
                        await ProcessCsvFile<CsvUser>(db, districtId, basePath + @"users.csv", 50, history);

                        await MarkDeleted(db, districtId, start);

                        await Analyze(db, districtId);
                    }
                    catch (Exception /* ex*/)
                    {
                        //history.Error = ex.Message;
                    }
                    finally
                    {
                        district.ProcessingStatus = ProcessingStatus.Finished;
                        history.Completed = DateTime.UtcNow;
                        await db.SaveChangesAsync();
                    }
                }
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
            foreach (var _class in classes.Where(c => c.LoadStatus != LoadStatus.NoChange))
            {
                CsvClass csvClass = Newtonsoft.Json.JsonConvert.DeserializeObject<CsvClass>(_class.RawData);
                if (courses.Select(c => c.SourceId).Contains(csvClass.courseSourcedId))
                {
                    _class.SyncStatus = SyncStatus.ReadyToApply;
                    _class.Touch();
                }
            }
            await db.SaveChangesAsync();

            int i = 0;
            var enrollments = await lines
                .Where(l => l.Table == "CsvEnrollment")
                .ToListAsync();
            var users = await lines
                .Where(l => l.Table == nameof(CsvUser))
                .Where(u => u.LoadStatus != LoadStatus.NoChange)
                .ToListAsync();

            foreach (var enrollment in enrollments.Where(e => e.LoadStatus != LoadStatus.NoChange))
            {
                CsvEnrollment csvEnrollment = Newtonsoft.Json.JsonConvert.DeserializeObject<CsvEnrollment>(enrollment.RawData);
                if (classes.Select(c => c.SourceId).Contains(csvEnrollment.classSourcedId))
                {
                    enrollment.SyncStatus = SyncStatus.ReadyToApply;
                    enrollment.Touch();

                    foreach (var user in users.Where(u => u.SourceId == csvEnrollment.userSourcedId).ToList())
                    {
                        user.SyncStatus = SyncStatus.ReadyToApply;
                        user.Touch();
                    }

                    if (++i > 50)
                    {
                        await db.SaveChangesAsync();
                        i = 0;
                    }
                }
            }
            await db.SaveChangesAsync();
        }

        private async Task ProcessCsvFile<T>(ApplicationDbContext db, int districtId, string filePath, int chunkCommitSize, DataSyncHistory history) 
            where T : CsvBaseObject
        {
            using (var file = System.IO.File.OpenText(filePath))
            {
                DateTime now = DateTime.UtcNow;
                var csv = new CsvHelper.CsvReader(file);

                string table = null;

                csv.Read();
                csv.ReadHeader();
                int i = 0;
                for (; await csv.ReadAsync(); i++)
                {
                    var record = csv.GetRecord<T>();
                    table = table ?? record.GetType().Name;
                    history.NumRows++;
                    string data = JsonConvert.SerializeObject(record);

                    DataSyncLine line = db.DataSyncLines.SingleOrDefault(l => l.DistrictId == districtId && l.SourceId == record.sourcedId);
                    bool newRecord = line == null;

                    if (newRecord)
                    {
                        // already deleted
                        if (record.isDeleted)
                        {
                            history.NumDeleted++;
                            continue;
                        }

                        history.NumAdded++;
                        line = new DataSyncLine
                        {
                            SourceId = record.sourcedId,
                            DistrictId = districtId,
                            LoadStatus = LoadStatus.Added,
                            LastSeen = now,
                            Table = table,
                        };
                        db.DataSyncLines.Add(line);
                        //await db.SaveChangesAsync();
                        i = 0;
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
                            continue;
                        }

                        // status should be deleted
                        if (record.isDeleted)
                        {
                            history.NumDeleted++;
                            line.LoadStatus = LoadStatus.Deleted;
                        }
                        else if (line.SyncStatus == SyncStatus.Loaded && line.LoadStatus == LoadStatus.Added)
                        {
                            history.NumAdded++;
                            line.LoadStatus = LoadStatus.Added; // if added, leave added
                        }
                        else
                        {
                            history.NumModified++;
                            line.LoadStatus = LoadStatus.Modified;
                        }
                    }

                    DataSyncHistoryDetail detail = new DataSyncHistoryDetail
                    {
                        DataOrig = line.RawData,
                        DataNew = data,
                        DataSyncLine = line,
                        DataSyncHistory = history,
                        LoadStatus = line.LoadStatus,
                        Table = table,
                    };
                    db.DataSyncHistoryDetails.Add(detail);

                    line.RawData = data;
                    line.SourceId = record.sourcedId;
                    line.SyncStatus = SyncStatus.Loaded;

                    if (i > chunkCommitSize)
                    {
                        await db.SaveChangesAsync();
                        i = 0;
                    }
                }

                // commit any last changes
                if (i > 0)
                    await db.SaveChangesAsync();

                Logger.LogInformation($"Processed Csv file {filePath}");
            }
        }
    }
}