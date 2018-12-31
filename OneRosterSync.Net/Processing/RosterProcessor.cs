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
using OneRosterSync.Net.Extensions;
using OneRosterSync.Net.Models;
using OneRosterSync.Net.Utils;

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

        /// <summary>
        /// Process a district's OneRoster CSV feed
        /// </summary>
        /// <param name="districtId">District Id</param>
        /// <param name="cancellationToken">Token to cancel operation (not currently used)</param>
        public async Task ProcessDistrict(int districtId, CancellationToken cancellationToken)
        {
            using (var scope = Services.CreateScope())
            {
                using (var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
                {
                    DistrictRepo repo = new DistrictRepo(Logger, db, districtId);
                    District district = repo.District;
                    district.Touch();
                    await repo.Committer.Invoke();

                    switch (district.ProcessingStatus)
                    {
                        case ProcessingStatus.Scheduled:
                            DateTime start = DateTime.UtcNow;
                            await Load(repo);
                            await Analyze(repo); // must pass start time before Load!!!
                            break;

                        case ProcessingStatus.Approved:
                            await Apply(repo);
                            break;

                        default:
                            Logger.Here().LogError($"Unexpected Processing status {district.ProcessingStatus} for District {district.Name} ({district.DistrictId})");
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Load the District CSV data into the database
        /// This is the first step of the Processing
        /// </summary>
        private async Task Load(DistrictRepo repo)
        {
            DataSyncHistory history = null;
            try
            {
                history = repo.PushHistory();
                repo.District.ProcessingStatus = ProcessingStatus.Loading;
                await repo.Committer.Invoke();

                var loader = new Loader(Logger, repo, @"CSVSample\", history);

                await loader.LoadFile<CsvOrg>(@"orgs.csv");
                await loader.LoadFile<CsvCourse>(@"courses.csv");
                await loader.LoadFile<CsvAcademicSession>(@"academicSessions.csv");
                await loader.LoadFile<CsvClass>(@"classes.csv");
                await loader.LoadFile<CsvUser>(@"users.csv");
                await loader.LoadFile<CsvEnrollment>(@"enrollments.csv");
            }
            catch (Exception ex)
            {
                Logger.Here().LogError(ex, "Error Loading District Data.");
            }
            finally
            {
                repo.District.ProcessingStatus = ProcessingStatus.LoadingDone;
                repo.District.Touch();
                //history.Completed = DateTime.UtcNow;
                await repo.Committer.Invoke();
            }
        }

        /// <summary>
        /// Load the District CSV data into the database
        /// This is the first step of the Processing
        /// </summary>
        private async Task Analyze(DistrictRepo repo)
        {
            DataSyncHistory history = null;
            try
            {
                history = repo.CurrentHistory;
                repo.District.ProcessingStatus = ProcessingStatus.Loading;
                await repo.Committer.Invoke();

                var analyzer = new Analyzer(Logger, repo);
                await analyzer.MarkDeleted(history.Started);
                await analyzer.Analyze();
            }
            catch (Exception ex)
            {
                Logger.Here().LogError(ex, "Error Analyzing District Data.");
            }
            finally
            {
                repo.District.ProcessingStatus = ProcessingStatus.AnalyzingDone;
                repo.District.Touch();
                //history.Completed = DateTime.UtcNow;
                await repo.Committer.Invoke();
            }
        }


        private async Task Apply(DistrictRepo repo)
        {
            try
            {
                repo.District.ProcessingStatus = ProcessingStatus.Applying;
                repo.District.Touch();
                await repo.Committer.Invoke();

                using (var api = new ApiManager(Logger))
                {
                    var applier = new Applier(Logger, repo, api);

                    await applier.ApplyLines<CsvOrg>();
                    await applier.ApplyLines<CsvCourse>();
                    await applier.ApplyLines<CsvAcademicSession>();
                    await applier.ApplyLines<CsvClass>();
                    await applier.ApplyLines<CsvUser>();
                    await applier.ApplyLines<CsvEnrollment>();
                }
            }
            catch (Exception ex)
            {
                Logger.Here().LogError(ex, "Error Applying District Data");
                throw;
            }
            finally
            {
                repo.District.ProcessingStatus = ProcessingStatus.ApplyingDone;
                repo.District.Touch();
                await repo.Committer.Invoke();
            }
        }


    }
}