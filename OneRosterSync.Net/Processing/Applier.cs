﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OneRosterSync.Net.Authentication;
using OneRosterSync.Net.DAL;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Extensions;
using OneRosterSync.Net.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OneRosterSync.Net.Processing
{
    public class Applier
    {
        private readonly ILogger Logger = ApplicationLogging.Factory.CreateLogger<Analyzer>();

        private readonly IServiceProvider Services;
        private readonly int DistrictId;
        private List<string> listInvalidSchoolIDs;

        /// <summary>
        /// How many APIs should we call in parallel?
        /// TODO: make a property of the District
        /// </summary>
        public int ParallelChunkSize { get; set; } = 50;

        public Applier(IServiceProvider services, int districtId)
        {
            Services = services;
            DistrictId = districtId;
            listInvalidSchoolIDs = new List<string>();
        }

        /// <summary>
        /// Apply all records of a given entity type to the LMS
        /// </summary>
        public async Task ApplyLines<T>() where T : CsvBaseObject
        {
            for (int last = 0; ;)
            {
                using (var scope = Services.CreateScope())
                using (var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
                {
                    var repo = new DistrictRepo(db, DistrictId);

                    // filter on all lines that are included and ready to be applied or apply was failed
                    IQueryable<DataSyncLine> lines;
                    if (typeof(T) == typeof(CsvUser))
                    {
                        var gradeFilters = repo.DistrictFilters.Where(w => w.FilterType == FilterType.Grades && w.ShouldBeApplied).ToList();
                        var orgsIds = repo.Lines<CsvOrg>().Where(w => w.IncludeInSync && w.LoadStatus != LoadStatus.Deleted)
                            .Select(s => s.SourcedId).ToList();

                        lines = repo.Lines<T>().Where(l => l.IncludeInSync && l.LoadStatus != LoadStatus.Deleted
                           && (l.SyncStatus == SyncStatus.ReadyToApply || l.SyncStatus == SyncStatus.ApplyFailed || l.SyncStatus == SyncStatus.ReadyToEnroll));

                        if (listInvalidSchoolIDs.Count > 0 && listInvalidSchoolIDs.Count < orgsIds.Count)
                        {
                            listInvalidSchoolIDs.All(a => orgsIds.Remove(a));
                        }

                        lines = lines.Where(w => orgsIds.Any(a => w.RawData.Contains($"\"orgSourcedIds\":\"{a}\"")));
                        if (gradeFilters.Count > 0)
                        {
                            var linesData = lines.Select(s => new
                            {
                                line = s,
                                user = JsonConvert.DeserializeObject<CsvUser>(s.RawData),
                            }).Select(s => new
                            {
                                s.line,
                                s.user,
                                grades = s.user.grades.Split(",", StringSplitOptions.None),
                            });
                            lines = linesData.Where(w => gradeFilters.Any(a => w.grades.Contains($"{a.FilterValue}"))).Select(s => s.line);
                        }
                    }
                    else
                    {
                        lines = repo.Lines<T>().Where(l => l.IncludeInSync && l.LoadStatus != LoadStatus.Deleted
                            && (l.SyncStatus == SyncStatus.ReadyToApply || l.SyncStatus == SyncStatus.ApplyFailed));
                    }

                    lines = lines.OrderBy(o => o.SyncStatus).ThenByDescending(o => o.ErrorCode).ThenBy(c => Guid.NewGuid());

                    // how many records are remaining to process?
                    int curr = await lines.CountAsync();
                    if (curr == 0)
                        break;

                    // after each process, the remaining record count should go down
                    // this avoids and infinite loop in case there is an problem processing
                    // basically, we bail if no progress is made at all
                    if (last > 0 && last <= curr)
                        throw new ProcessingException(Logger, "Apply failed to update SyncStatus of applied record. This indicates that some apply calls are failing and hence the apply process was aborted.");
                    last = curr;

                    if (repo.GetStopFlag(DistrictId))
                    {
                        throw new ProcessingException(Logger, $"Current action is stopped by the user.");
                    }

                    // process chunks of lines in parallel
                    IEnumerable<Task> tasks = await lines
                        .AsNoTracking()
                        .Take(ParallelChunkSize)
                        .Select(line => ApplyLineParallel<T>(line))
                        .ToListAsync();

                    await Task.Run(() => Parallel.ForEach(tasks,
                        parallelOptions: new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount * 10 },
                        body: (task) => task.Wait()));
                    //await Task.WhenAll(tasks);
                }
            }
        }

        private async Task ApplyLineParallel<T>(DataSyncLine line) where T : CsvBaseObject
        {
            // we need a new DataContext to avoid concurrency issues
            using (var scope = Services.CreateScope())
            using (var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
            {
                // re-create the Repo and Data pulled from it
                var repo = new DistrictRepo(db, DistrictId);
                var newLine = await repo.Lines<T>().SingleAsync(l => l.DataSyncLineId == line.DataSyncLineId);
                await ApplyLine<T>(repo, newLine);
                await repo.Committer.Invoke();
            }
        }

        private async Task ApplyLine<T>(DistrictRepo repo, DataSyncLine line) where T : CsvBaseObject
        {
            switch (line.LoadStatus)
            {
                case LoadStatus.None:
                    Logger.Here().LogWarning($"None should not be flagged for Sync: {line.RawData}");
                    return;
            }

            ApiPostBase data;
            var apiManager = new ApiManager(repo.District.LmsApiBaseUrl)
            {
                ApiAuthenticator = ApiAuthenticatorFactory.GetApiAuthenticator(repo.District.LmsApiAuthenticatorType,
                    repo.District.LmsApiAuthenticationJsonData)
            };
            // Commenting for changing process of enrollment
            /*
            if (line.Table == nameof(CsvEnrollment))
            {
                var enrollment = new ApiEnrollmentPost(line.RawData);

                CsvEnrollment csvEnrollment = JsonConvert.DeserializeObject<CsvEnrollment>(line.RawData);
                DataSyncLine cls = repo.Lines<CsvClass>().SingleOrDefault(l => l.SourcedId == csvEnrollment.classSourcedId);
                DataSyncLine usr = repo.Lines<CsvUser>().SingleOrDefault(l => l.SourcedId == csvEnrollment.userSourcedId);
                DataSyncLine org = repo.Lines<CsvOrg>().SingleOrDefault(l => l.SourcedId == csvEnrollment.schoolSourcedId); //Sandesh

                var ncesMapping = repo.GetNCESMapping(org.SourcedId);
                //var orgCsv = JsonConvert.DeserializeObject<CsvOrg>(org.RawData);

                var map = new EnrollmentMap
                {
                    //Sandesh
                    //classTargetId = cls?.TargetId,
                    //userTargetId = usr?.TargetId,
                    user_id = usr?.TargetId,
                    nces_schoolid = ncesMapping?.ncesId //orgCsv?.identifier
                };

                if (!(org?.IncludeInSync ?? false))
                {
                    // Is NCES school ID given?
                    line.SyncStatus = SyncStatus.ApplyFailed;
                    if (string.IsNullOrEmpty(map.nces_schoolid))
                    {
                        line.Error = "NCES school ID not found";
                    }
                    else
                    {
                        line.Error = $"CsvOrg line ID {org.DataSyncLineId} is not marked to sync with LMS.";
                    }
                    line.Touch();
                    repo.PushLineHistory(line, isNewData: false);
                    return;
                }

                // set nces school id in enrollment object
                csvEnrollment.nces_schoolid = ncesMapping?.ncesId; //orgCsv?.identifier;
                enrollment.Data.nces_schoolid = ncesMapping?.ncesId; //orgCsv?.identifier;
                enrollment.Data.user_id = usr?.TargetId;

                // this provides a mapping of LMS TargetIds (rather than sourcedId's)
                enrollment.EnrollmentMap = map;
                //enrollment.ClassTargetId = cls?.TargetId;
                //enrollment.UserTargetId = usr?.TargetId;
                enrollment.user_id = usr?.TargetId;
                enrollment.nces_schoolid = ncesMapping?.ncesId; //orgCsv?.identifier;

                // cache map in the database (for display/troubleshooting only)
                line.EnrollmentMap = JsonConvert.SerializeObject(map);

                data = enrollment;
            }
            else
            */
            if (line.Table == nameof(CsvClass))
            {
                var classCsv = JsonConvert.DeserializeObject<CsvClass>(line.RawData);

                // Get course & school of this class
                var course = repo.Lines<CsvCourse>().SingleOrDefault(l => l.SourcedId == classCsv.courseSourcedId);
                var courseCsv = JsonConvert.DeserializeObject<CsvCourse>(course.RawData);

                // Get Term of this class
                // TODO: Handle multiple terms, termSourceIds can be a comma separated list of terms.
                var term = repo.Lines<CsvAcademicSession>().SingleOrDefault(s => s.SourcedId == classCsv.termSourcedIds);

                var org = repo.Lines<CsvOrg>().SingleOrDefault(o => o.SourcedId == classCsv.schoolSourcedId);

                var _class = new ApiClassPost(line.RawData)
                {
                    CourseTargetId = course.TargetId,
                    SchoolTargetId = org.TargetId,
                    TermTargetId = string.IsNullOrWhiteSpace(term.TargetId) ? "2019" : term.TargetId, //TODO: Add a default term setting in District Entity
                    Period = classCsv.periods
                };

                data = _class;
            }
            else if (line.Table == nameof(CsvUser))
            {
                var userCsv = JsonConvert.DeserializeObject<CsvUser>(line.RawData);
                DataSyncLine org = repo.Lines<CsvOrg>().SingleOrDefault(l => l.SourcedId == userCsv.orgSourcedIds);
                if (org == null || !org.IncludeInSync)
                {
                    return;
                }
                //if (string.IsNullOrEmpty(line.TargetId))
                //{
                userCsv.email = userCsv.email.ToLower();
                if (repo.District.EmailFieldNameForUserAPI.Equals(nameof(userCsv.email)))
                {
                    userCsv.username = userCsv.email.ToLower();
                }
                else if (repo.District.EmailFieldNameForUserAPI.Equals(nameof(userCsv.username)))
                {
                    userCsv.username = userCsv.username.ToLower();
                    if (string.IsNullOrEmpty(userCsv.email))
                    {
                        userCsv.email = userCsv.username.ToLower();
                    }
                }

                if (repo.District.PasswordFieldNameForUserAPI.Equals(nameof(userCsv.sourcedId)))
                {
                    userCsv.password = line.SourcedId;
                }
                else if (repo.District.PasswordFieldNameForUserAPI.Equals(nameof(userCsv.password)))
                {
                    userCsv.password = userCsv.password;
                }
                else if (repo.District.PasswordFieldNameForUserAPI.Equals(nameof(userCsv.identifier)))
                {
                    userCsv.password = userCsv.identifier;
                }
                else if (repo.District.PasswordFieldNameForUserAPI.Equals(nameof(userCsv.username)))
                {
                    userCsv.password = userCsv.username;
                }
                //if (string.IsNullOrEmpty(userCsv.password))
                //{
                //    userCsv.password = line.SourcedId;
                //}
                data = new ApiPost<T>(JsonConvert.SerializeObject(userCsv));
                //}
                //else
                //{
                //    await ApplyEnrollment(line, repo, apiManager);
                //    return;
                //}
            }
            else
            {
                data = new ApiPost<T>(line.RawData);
            }

            data.DistrictId = repo.District.DistrictId.ToString(); //repo.District.TargetId;
            data.DistrictName = repo.District.Name;
            data.LastSeen = line.LastSeen;
            data.SourcedId = line.SourcedId;
            data.TargetId = line.TargetId;
            data.Status = line.LoadStatus.ToString();

            var response = await apiManager.Post(GetEntityEndpoint(data.EntityType.ToLower(), repo), data);
            ReadResponse(line, repo, response, false);
            if (response.Success && line.Table == nameof(CsvUser))
            {
                await ApplyEnrollment(line, repo, apiManager);
            }
        }

        private string GetEntityEndpoint(string entityType, DistrictRepo repo)
        {
            switch (entityType)
            {
                case "org":
                    return repo.District.LmsOrgEndPoint;
                case "course":
                    return repo.District.LmsCourseEndPoint;
                case "class":
                    return repo.District.LmsClassEndPoint;
                case "user":
                    return repo.District.LmsUserEndPoint;
                case "enrollment":
                    return repo.District.LmsEnrollmentEndPoint;
                case "academicsession":
                    return repo.District.LmsAcademicSessionEndPoint;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entityType), entityType, "An unknown entity was provided for which there is not endpoint.");
            }
        }

        private async Task ApplyEnrollment(DataSyncLine line, DistrictRepo repo, ApiManager apiManager)
        {
            if (string.IsNullOrEmpty(repo.District.NCESDistrictID))
            {
                line.SyncStatus = SyncStatus.ApplyFailed;
                line.Error = $"NCES District ID is not provided.";
                line.Touch();
                repo.PushLineHistory(line, isNewData: false);
                throw new ProcessingException(Logger, $"NCES District ID is not provided. Force stopping current action.");
            }

            var currentDistrict = repo.District;
            var csvUser = JsonConvert.DeserializeObject<CsvUser>(line.RawData);
            DataSyncLine org = repo.Lines<CsvOrg>().SingleOrDefault(l => l.SourcedId == csvUser.orgSourcedIds);
            if (org == null || !org.IncludeInSync)
            {
                return;
            }

            var orgCsv = JsonConvert.DeserializeObject<CsvOrg>(org.RawData);
            string ncesId = orgCsv.identifier;
            ncesId = string.IsNullOrEmpty(ncesId) || !ncesId.StartsWith(currentDistrict.NCESDistrictID) ? orgCsv.sourcedId : ncesId;
            // Is NCES school ID given?
            if (string.IsNullOrEmpty(ncesId) || !ncesId.StartsWith(currentDistrict.NCESDistrictID))
            {
                NCESMappingModel ncesMapping = null;
                if (!string.IsNullOrEmpty(orgCsv.identifier))
                    ncesMapping = repo.GetNCESMapping(orgCsv.identifier);
                if (ncesMapping == null)
                    ncesMapping = repo.GetNCESMapping(csvUser.orgSourcedIds);

                if (ncesMapping != null && !string.IsNullOrEmpty(ncesMapping.ncesId)) ncesId = ncesMapping.ncesId;
                else ncesId = string.Empty;
            }

            if (string.IsNullOrEmpty(ncesId))
            {
                line.SyncStatus = SyncStatus.ApplyFailed;
                line.Error = $"NCES school ID not found for {orgCsv.sourcedId}|{orgCsv.name}";
                line.Touch();
                repo.PushLineHistory(line, isNewData: false);
                if (!listInvalidSchoolIDs.Contains(org.SourcedId))
                {
                    listInvalidSchoolIDs.Add(org.SourcedId);
                }
                return;
            }
            else if (!ncesId.StartsWith(currentDistrict.NCESDistrictID))
            {
                line.SyncStatus = SyncStatus.ApplyFailed;
                line.Error = $"NCES school ID {ncesId} is invalid for {orgCsv.sourcedId}|{orgCsv.name} as it does not start with NCES District ID {currentDistrict.NCESDistrictID}";
                line.Touch();
                repo.PushLineHistory(line, isNewData: false);
                if (!listInvalidSchoolIDs.Contains(org.SourcedId))
                {
                    listInvalidSchoolIDs.Add(org.SourcedId);
                }
                return;
            }

            var enrollment = new CsvEnrollment
            {
                user_id = line.TargetId,
                nces_schoolid = ncesId
            };

            var data = new ApiPost<CsvEnrollment>(JsonConvert.SerializeObject(enrollment));

            data.DistrictId = repo.District.DistrictId.ToString();
            data.DistrictName = repo.District.Name;
            data.LastSeen = line.LastSeen;
            data.SourcedId = line.SourcedId;
            data.TargetId = line.TargetId;
            data.Status = line.LoadStatus.ToString();

            if (!listInvalidSchoolIDs.Contains(csvUser.orgSourcedIds))
            {
                line.EnrollmentMap = JsonConvert.SerializeObject(enrollment);
                var response = await apiManager.Post(GetEntityEndpoint(data.EntityType.ToLower(), repo), data);
                ReadResponse(line, repo, response, true, csvUser.orgSourcedIds);
            }
        }

        private void ReadResponse(DataSyncLine line, DistrictRepo repo, ApiResponse response, bool fromEnrollment, string orgSourcedId = null)
        {
            if (response.Success)
            {
                if (!fromEnrollment && line.Table == nameof(CsvUser))
                {
                    line.SyncStatus = SyncStatus.ReadyToEnroll;
                }
                else
                {
                    line.SyncStatus = SyncStatus.Applied;
                }
            }
            else
            {
                line.SyncStatus = SyncStatus.ApplyFailed;
                var ErrorCode = string.IsNullOrEmpty(response.ErrorCode) ? string.Empty : response.ErrorCode;
                if (fromEnrollment && ErrorCode.Equals("106") && orgSourcedId != null && !listInvalidSchoolIDs.Contains(orgSourcedId))
                {
                    response.ErrorMessage = string.IsNullOrEmpty(response.ErrorMessage) ? response.ErrorMessage : $"{response.ErrorMessage} (orgSourcedId: {orgSourcedId})";
                    listInvalidSchoolIDs.Add(orgSourcedId);
                }
            }

            line.Error = response.ErrorMessage;
            line.ErrorCode = response.ErrorCode;
            // The Lms can send false success if the entity already exist. In such a case we read the targetId
            if (!string.IsNullOrEmpty(response.TargetId) && !fromEnrollment)
                line.TargetId = response.TargetId;

            line.Touch();
            repo.PushLineHistory(line, isNewData: false);
        }
    }
}