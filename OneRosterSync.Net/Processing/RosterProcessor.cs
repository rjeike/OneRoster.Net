using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Extensions;
using OneRosterSync.Net.Models;

namespace OneRosterSync.Net.Processing
{
    public class RosterProcessor : IDisposable
    {
        private readonly ILogger Logger = ApplicationLogging.Factory.CreateLogger<RosterProcessor>();

        private readonly IServiceProvider Services;
        private readonly int DistrictId;
        private readonly CancellationToken CancellationToken;

        private IServiceScope ServiceScope;
        private ApplicationDbContext Db;
        private DistrictRepo Repo;

        public RosterProcessor(
            IServiceProvider services,
            int districtId,
            CancellationToken cancellationToken)
        {
            Services = services;
            DistrictId = districtId;
            CancellationToken = cancellationToken;

            CreateContext();
        }

        public void Dispose()
        {
            DestroyContext();
        }

        private void CreateContext()
        {
            ServiceScope = Services.CreateScope();
            Db = ServiceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Repo = new DistrictRepo(Db, DistrictId);
        }

        private void DestroyContext()
        {
            Repo = null;
            Db.Dispose();
            Db = null;
            ServiceScope.Dispose();
            ServiceScope = null;
        }

        private void RefreshContext()
        {
            DestroyContext();
            CreateContext();
        }

        /// <summary>
        /// Process a district's OneRoster CSV feed
        /// </summary>
        public async Task Process(ProcessingAction action)
        {
            switch (action)
            {
                default:
                case ProcessingAction.None:
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


        /// <summary>
        /// Load the District CSV data into the database
        /// This is the first step of the Processing
        /// </summary>
        private async Task Load()
        {
            Repo.PushHistory();
            var loader = new Loader(Repo, Repo.District.BasePath);
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
                RefreshContext();
                var pe = (ex as ProcessingException)
                    ?? new ProcessingException(Logger.Here(), ProcessingStage.Load, 
                        $"Exception Loading data for {loader.LastEntity}.  Possible duplicate sourcedId. " + ex.Message, ex);
                Repo.RecordProcessingError(pe);
                await Repo.Committer.Invoke();
            }
            finally
            {
                RefreshContext();
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
                RefreshContext();
                var pe = (ex as ProcessingException)
                    ?? new ProcessingException(Logger.Here(), ProcessingStage.Analyze, $"Unhandled exception Analyzing data.", ex);
                Repo.RecordProcessingError(pe);
                await Repo.Committer.Invoke();
            }
            finally
            {
                RefreshContext();
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

                using (var api = new ApiManager(Repo.District.LmsApiEndpoint))
                {
                    var applier = new Applier(Services, Repo.DistrictId, api);

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
                RefreshContext();
                var pe = (ex as ProcessingException) ?? 
                    new ProcessingException(Logger.Here(), ProcessingStage.Apply, $"Unhandled exception Applying data.", ex);
                Repo.RecordProcessingError(pe);
                await Repo.Committer.Invoke();
            }
            finally
            {
                RefreshContext();
                Repo.RecordProcessingStop(ProcessingStage.Apply);
                await Repo.Committer.Invoke();
            }
        }
    }
}