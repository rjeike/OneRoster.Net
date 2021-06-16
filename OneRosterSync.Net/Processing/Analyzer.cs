﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OneRosterSync.Net.DAL;
using OneRosterSync.Net.Extensions;
using OneRosterSync.Net.Models;

namespace OneRosterSync.Net.Processing
{
    public class Analyzer
    {
        private readonly ILogger Logger = ApplicationLogging.Factory.CreateLogger<Analyzer>();
        private readonly DistrictRepo Repo;

        public Analyzer(ILogger logger, DistrictRepo repo)
        {
            Repo = repo;
        }

        /// <summary>
        /// Identifies records that were missing from the feed and marks them as Deleted
        /// </summary>
        public async Task MarkDeleted(DateTime start)
        {
            foreach (var line in await Repo.Lines().Where(l => l.LastSeen < start).ToListAsync())
            {
                line.LoadStatus = LoadStatus.Deleted;
                line.IncludeInSync = false;
                line.Error = $"Deleted from analyze in MarkDeleted method. Last seen: {line.LastSeen.ToString()}. start: {start.ToString()}";
                await Repo.Committer.InvokeIfChunk(500);
            }
            await Repo.Committer.InvokeIfAny();
        }

        /// <summary>
        /// This is used to determine if any change needs to be pushed to the LMS and is included in sync.
        /// Basically if a record has changed OR has never been Applied
        /// which can happen if a record is loaded and later caused to be included in the Sync
        /// </summary>
        private static bool IsUnappliedChange(DataSyncLine line) =>
            line.IncludeInSync &&
            (line.LoadStatus != LoadStatus.NoChange ||
            line.SyncStatus != SyncStatus.Applied);

        private static bool IsUnappliedChangeWithoutIncludedInSync(DataSyncLine line) =>
            (line.LoadStatus != LoadStatus.NoChange ||
             line.SyncStatus != SyncStatus.Applied);

        /// <summary>
        /// Helper to mark a record to be included in the next push to LMS
        /// </summary>
        private void IncludeReadyTouch(DataSyncLine line)
        {
            line.IncludeInSync = true;
            line.SyncStatus = SyncStatus.ReadyToApply;
            line.Touch();

            Repo.PushLineHistory(line, isNewData: false);
        }

        /// <summary>
        /// Analyze the records to determine which should be included in the feed
        /// based on dependencies.
        /// </summary>
        public async Task Analyze()
        {
            // load some small tables into memory for performance
            var cache = new DataLineCache();
            await cache.Load(Repo.Lines(), new[] { nameof(CsvOrg), nameof(CsvCourse), nameof(CsvClass) });

            var orgIds = new List<string>();
            // include Orgs that have been selected for sync
            foreach (var org in cache.GetMap<CsvOrg>().Values.Where(IsUnappliedChange))
            {
                orgIds.Add(org.SourcedId);
                IncludeReadyTouch(org);
            }
            await Repo.Committer.Invoke();

            // courses are manually marked for sync, so choose only those
            foreach (var course in cache.GetMap<CsvCourse>().Values.Where(l => l.IncludeInSync).Where(IsUnappliedChange))
                IncludeReadyTouch(course);
            await Repo.Committer.Invoke();

            // now walk the classes and include those which map to an included course and are part of the selected orgs.
            var classMap = cache.GetMap<CsvClass>();
            var orgMap = cache.GetMap<CsvOrg>();
            var courseIds = cache.GetMap<CsvCourse>()
                .Values
                .Where(l => l.IncludeInSync)
                .Select(l => l.SourcedId)
                .ToHashSet();

            foreach (var _class in classMap.Values.Where(IsUnappliedChangeWithoutIncludedInSync))
            {
                CsvClass csvClass = JsonConvert.DeserializeObject<CsvClass>(_class.RawData);
                if (courseIds.Contains(csvClass.courseSourcedId) && orgIds.Contains(csvClass.schoolSourcedId))
                    IncludeReadyTouch(_class);
                await Repo.Committer.InvokeIfChunk();
            }
            await Repo.Committer.InvokeIfAny();

            //Commented as districts do not share enrollments CSV
            // process enrollments in the database associated with the District based on the conditions below (in chunks of 200)
            //await Repo.Lines<CsvEnrollment>().ForEachInChunksAsync(chunkSize: 200,
            //    action: async (enrollment) =>
            //    {
            //        CsvEnrollment csvEnrollment = JsonConvert.DeserializeObject<CsvEnrollment>(enrollment.RawData);

            //        // figure out if we need to process this enrollment
            //        // Sandesh. commented because we have to enroll in school regardless of class
            //        //if (!classMap.ContainsKey(csvEnrollment.classSourcedId) ||      // look up class associated with enrollment
            //        //	!classMap[csvEnrollment.classSourcedId].IncludeInSync ||    // only include enrollment if the class is included
            //        //	!IsUnappliedChangeWithoutIncludedInSync(enrollment))        // only include if unapplied change in enrollment
            //        //	return;

            //        // check if organization is selected for enrollment
            //        var org = await Repo.Lines<CsvOrg>().FirstOrDefaultAsync(w => w.SourcedId == csvEnrollment.schoolSourcedId);
            //        if (org != null && !org.IncludeInSync)
            //            return;

            //        var user = await Repo.Lines<CsvUser>().SingleOrDefaultAsync(l => l.SourcedId == csvEnrollment.userSourcedId);
            //        if (user == null) // should never happen
            //        {
            //            enrollment.Error = $"Missing user for {csvEnrollment.userSourcedId}";
            //            Logger.Here().LogError($"Missing user for enrollment for line {enrollment.DataSyncLineId}");
            //            return;
            //        }

            //        // mark enrollment for sync
            //        IncludeReadyTouch(enrollment);

            //        // mark user for sync
            //        //DataSyncLine user = userMap[csvEnrollment.userSourcedId];
            //        if (IsUnappliedChangeWithoutIncludedInSync(user))
            //            IncludeReadyTouch(user);
            //    },
            //    onChunkComplete: async () => await Repo.Committer.Invoke());

            var gradeFilters = Repo.DistrictFilters.Where(w => w.FilterType == FilterType.Grades && w.ShouldBeApplied).ToList();
            var orgs = Repo.Lines<CsvOrg>().Where(w => w.LoadStatus != LoadStatus.Deleted);
            await orgs.ForEachInChunksAsync(chunkSize: 200,
                action: async (org) =>
                {
                    if (!org.IncludeInSync) return;
                    CsvOrg csvOrg = JsonConvert.DeserializeObject<CsvOrg>(org.RawData);
                    string orgId = $"\"orgSourcedIds\":\"{csvOrg.sourcedId}\"";
                    var users = Repo.Lines<CsvUser>().Where(w => w.RawData.Contains(orgId)
                        && w.SyncStatus != SyncStatus.Applied && w.SyncStatus != SyncStatus.ReadyToApply)
                        .Select(s => new
                        {
                            line = s,
                            user = JsonConvert.DeserializeObject<CsvUser>(s.RawData),
                        }).Select(s => new
                        {
                            s.line,
                            s.user,
                            grades = s.user.grades.Split(",", StringSplitOptions.None),
                        });
                    if (gradeFilters.Count > 0)
                        users = users.Where(w => gradeFilters.Any(a => w.grades.Contains($"{a.FilterValue}")));

                    await users.Select(s => s.line).ForEachAsync(action: (user) =>
                      {
                          IncludeReadyTouch(user);
                      });
                    await Repo.Committer.Invoke();
                    if (Repo.GetStopFlag(Repo.DistrictId))
                    {
                        throw new ProcessingException(Logger, $"Current action is stopped by the user.");
                    }
                },
                onChunkComplete: async () =>
                {
                    await Repo.Committer.Invoke();
                });

            //GC.Collect();

            // now process any user changes we may have missed
            await Repo.Lines<CsvUser>()
                .Where(u => u.IncludeInSync
                && u.LoadStatus != LoadStatus.NoChange && u.LoadStatus != LoadStatus.Deleted
                && u.SyncStatus != SyncStatus.ReadyToApply
                && u.SyncStatus != SyncStatus.Applied)
                .ForEachInChunksAsync(chunkSize: 200,
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
                    action: async (user) =>
                    {
                        CsvUser csvUser = JsonConvert.DeserializeObject<CsvUser>(user.RawData);
                        //var org = await Repo.Lines<CsvOrg>().FirstOrDefaultAsync(w => w.SourcedId == csvUser.orgSourcedIds);
                        var org = orgs.FirstOrDefault(w => w.SourcedId == csvUser.orgSourcedIds);
                        if (org == null) // should never happen
                        {
                            user.Error = $"Missing org for {csvUser.orgSourcedIds}";
                            Logger.Here().LogError($"Missing org for enrollment for line {user.DataSyncLineId}");
                            return;
                        }
                        else if (org != null && !org.IncludeInSync)
                        {
                            user.IncludeInSync = false;
                            user.SyncStatus = org.SyncStatus = SyncStatus.Loaded;
                            return;
                        }

                        IncludeReadyTouch(user);
                    },
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
                    onChunkComplete: async () =>
                    {
                        await Repo.Committer.Invoke();
                        if (Repo.GetStopFlag(Repo.DistrictId))
                        {
                            throw new ProcessingException(Logger, $"Current action is stopped by the user.");
                        }
                    });


            await Repo.Committer.Invoke();
            GC.Collect();
        }
    }
}