using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public async Task Process(int districtId, CancellationToken cancellationToken)
        {
            using (var scope = Services.CreateScope())
            {
                using (var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
                {
                    DistrictRepo repo = new DistrictRepo(Logger, db, districtId);
                    District district = repo.District;
                    ProcessingAction action = district.ProcessingAction;
                    district.ProcessingAction = ProcessingAction.None; // clear the action out
                    district.Touch();
                    await repo.Committer.Invoke();

                    switch (district.ProcessingStatus)
                    {
                        case ProcessingStatus.Loading:
                        case ProcessingStatus.Applying:
                        case ProcessingStatus.Analyzing:
                            // already in process, bail out?
                            Logger.Here().LogError($"Unexpected Processing status {district.ProcessingStatus} for District {district.Name} ({district.DistrictId})");
                            break;

                        default:
                            break;
                    }

                    switch (action)
                    {
                        case ProcessingAction.None:
                            break;

                        case ProcessingAction.LoadSample:
                            throw new NotImplementedException();

                        case ProcessingAction.Load:
                            await Load(repo);
                            break;

                        case ProcessingAction.Analyze:
                            await Analyze(repo);
                            break;

                        case ProcessingAction.Apply:
                            await Apply(repo);
                            break;

                        case ProcessingAction.FullProcess:
                            await Load(repo);
                            await Analyze(repo);
                            await Apply(repo);
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
            DataSyncHistory history = repo.PushHistory();
            var loader = new Loader(Logger, repo, repo.District.BasePath, history);
            await repo.Committer.Invoke();

            try
            {
                repo.RecordProcessingStart(ProcessingStage.Load);
                await repo.Committer.Invoke();

                await loader.LoadFile<CsvOrg>(@"orgs.csv");
                await loader.LoadFile<CsvCourse>(@"courses.csv");
                await loader.LoadFile<CsvAcademicSession>(@"academicSessions.csv");
                await loader.LoadFile<CsvClass>(@"classes.csv");
                await loader.LoadFile<CsvUser>(@"users.csv");
                await loader.LoadFile<CsvEnrollment>(@"enrollments.csv");
            }
            catch (Exception ex)
            {
                var pe = (ex as ProcessingException)
                    ?? new ProcessingException(Logger.Here(), ProcessingStage.Load, $"Unhandled exception Loading data for {loader.LastEntity}.", ex);
                repo.RecordProcessingError(pe);
            }
            finally
            {
                repo.RecordProcessingStop(ProcessingStage.Load);
                try
                {
                    await repo.Committer.Invoke();
                }
                catch (Exception ex)
                {
                    int i = 0;
                    string s = ex.Message;
                }
            }
        }


        /// <summary>
        /// Load the District CSV data into the database
        /// This is the first step of the Processing
        /// </summary>
        private async Task Analyze(DistrictRepo repo)
        {
            try
            {
                DataSyncHistory history = repo.CurrentHistory;
                if (!string.IsNullOrEmpty(history.LoadError))
                {
                    var pe = new ProcessingException(Logger.Here(), ProcessingStage.Analyze, "Can't Analyze with active LoadError.  Reload first.");
                    repo.RecordProcessingError(pe);
                    return;
                }

                repo.RecordProcessingStart(ProcessingStage.Analyze);
                await repo.Committer.Invoke();

                var analyzer = new Analyzer(Logger, repo);
                await analyzer.MarkDeleted(history.Started);
                await analyzer.Analyze();
            }
            catch (Exception ex)
            {
                var pe = (ex as ProcessingException)
                    ?? new ProcessingException(Logger.Here(), ProcessingStage.Analyze, $"Unhandled exception Analyzing data.", ex);
                repo.RecordProcessingError(pe);
            }
            finally
            {
                repo.RecordProcessingStop(ProcessingStage.Analyze);
                await repo.Committer.Invoke();
            }
        }


        private async Task Apply(DistrictRepo repo)
        {
            try
            {
                if (!string.IsNullOrEmpty(repo.CurrentHistory.LoadError) ||
                    !string.IsNullOrEmpty(repo.CurrentHistory.AnalyzeError))
                {
                    var pe = new ProcessingException(Logger.Here(), ProcessingStage.Apply, "Can't Apply with active LoadError or AnalyzeError");
                    repo.RecordProcessingError(pe);
                    return;
                }

                repo.RecordProcessingStart(ProcessingStage.Apply);
                await repo.Committer.Invoke();

                using (var api = new ApiManager(Logger, repo.District.LmsApiEndpoint))
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
                var pe = (ex as ProcessingException) ?? 
                    new ProcessingException(Logger.Here(), ProcessingStage.Apply, $"Unhandled exception Applying data.", ex);
                repo.RecordProcessingError(pe);
            }
            finally
            {
                repo.RecordProcessingStop(ProcessingStage.Apply);
                await repo.Committer.Invoke();
            }
        }
    }
}