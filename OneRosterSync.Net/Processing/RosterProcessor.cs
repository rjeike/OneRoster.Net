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
        private int DistrictId;
        private ApplicationDbContext Db;
        private DistrictRepo Repo;

        public RosterProcessor(
            IServiceProvider services,
            ILogger logger)
        {
            Services = services;
            Logger = logger;
        }

        private void CreateContext()
        {
            DestroyContext();
            //            using (var scope = Services.CreateScope())
            var scope = Services.CreateScope();
            {
                Db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Repo = new DistrictRepo(Logger, Db, DistrictId);
            }
        }

        private void DestroyContext()
        {
            if (Db != null)
            {
                Db.Dispose();
                Db = null;
                Repo = null;
            }
        }

        /// <summary>
        /// Process a district's OneRoster CSV feed
        /// </summary>
        /// <param name="districtId">District Id</param>
        /// <param name="cancellationToken">Token to cancel operation (not currently used)</param>
        public async Task Process(int districtId, CancellationToken cancellationToken)
        {
            try
            {
                DistrictId = districtId;
                CreateContext();
                District district = Repo.District;
                ProcessingAction action = district.ProcessingAction;
                district.ProcessingAction = ProcessingAction.None; // clear the action out
                district.Touch();
                await Repo.Committer.Invoke();

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
                        await Load();
                        break;

                    case ProcessingAction.Analyze:
                        await Analyze();
                        break;

                    case ProcessingAction.Apply:
                        await Apply();
                        break;

                    case ProcessingAction.FullProcess:
                        await Load();
                        await Analyze();
                        await Apply();
                        break;
                }
            }
            finally
            {
                DestroyContext();
            }
        }


        /// <summary>
        /// Load the District CSV data into the database
        /// This is the first step of the Processing
        /// </summary>
        private async Task Load()
        {
            DataSyncHistory history = Repo.PushHistory();
            var loader = new Loader(Logger, Repo, Repo.District.BasePath, history);
            await Repo.Committer.Invoke();

            try
            {
                Repo.RecordProcessingStart(ProcessingStage.Load);
                await Repo.Committer.Invoke();

                await loader.LoadFile<CsvOrg>(@"orgs.csv");
                await loader.LoadFile<CsvCourse>(@"courses.csv");
                await loader.LoadFile<CsvAcademicSession>(@"academicSessions.csv");
                await loader.LoadFile<CsvClass>(@"classes.csv");
                await loader.LoadFile<CsvUser>(@"users.csv");
                await loader.LoadFile<CsvEnrollment>(@"enrollments.csv");
            }
            catch (Exception ex)
            {
                CreateContext();
                var pe = (ex as ProcessingException)
                    ?? new ProcessingException(Logger.Here(), ProcessingStage.Load, 
                        $"Exception Loading data for {loader.LastEntity}.  Possible duplicate sourcedId. " + ex.Message, ex);
                Repo.RecordProcessingError(pe);
            }
            finally
            {
                Repo.RecordProcessingStop(ProcessingStage.Load);
                await Repo.Committer.Invoke();
            }
        }


        /// <summary>
        /// Load the District CSV data into the database
        /// This is the first step of the Processing
        /// </summary>
        private async Task Analyze()
        {
            try
            {
                DataSyncHistory history = Repo.CurrentHistory;
                if (!string.IsNullOrEmpty(history.LoadError))
                {
                    var pe = new ProcessingException(Logger.Here(), ProcessingStage.Analyze, "Can't Analyze with active LoadError.  Reload first.");
                    Repo.RecordProcessingError(pe);
                    return;
                }

                Repo.RecordProcessingStart(ProcessingStage.Analyze);
                await Repo.Committer.Invoke();

                var analyzer = new Analyzer(Logger, Repo);
                await analyzer.MarkDeleted(history.Started);
                await analyzer.Analyze();
            }
            catch (Exception ex)
            {
                CreateContext();
                var pe = (ex as ProcessingException)
                    ?? new ProcessingException(Logger.Here(), ProcessingStage.Analyze, $"Unhandled exception Analyzing data.", ex);
                Repo.RecordProcessingError(pe);
            }
            finally
            {
                Repo.RecordProcessingStop(ProcessingStage.Analyze);
                await Repo.Committer.Invoke();
            }
        }


        private async Task Apply()
        {
            try
            {
                if (!string.IsNullOrEmpty(Repo.CurrentHistory.LoadError) ||
                    !string.IsNullOrEmpty(Repo.CurrentHistory.AnalyzeError))
                {
                    var pe = new ProcessingException(Logger.Here(), ProcessingStage.Apply, "Can't Apply with active LoadError or AnalyzeError");
                    Repo.RecordProcessingError(pe);
                    return;
                }

                Repo.RecordProcessingStart(ProcessingStage.Apply);
                await Repo.Committer.Invoke();

                using (var api = new ApiManager(Logger, Repo.District.LmsApiEndpoint))
                {
                    var applier = new Applier(Logger, Repo, api);

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
                CreateContext();
                var pe = (ex as ProcessingException) ?? 
                    new ProcessingException(Logger.Here(), ProcessingStage.Apply, $"Unhandled exception Applying data.", ex);
                Repo.RecordProcessingError(pe);
            }
            finally
            {
                Repo.RecordProcessingStop(ProcessingStage.Apply);
                await Repo.Committer.Invoke();
            }
        }
    }
}