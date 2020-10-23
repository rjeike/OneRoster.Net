using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneRosterSync.Net.Authentication;
using OneRosterSync.Net.DAL;
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

        private ProcessingAction CurrentProcessingAction = ProcessingAction.None;

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
            CurrentProcessingAction = action;
            switch (action)
            {
                default:
                case ProcessingAction.None:
                case ProcessingAction.LoadSample:
                    throw new NotImplementedException();

                case ProcessingAction.Load:
                    await ProcessStage(ProcessingStage.Load);
                    break;

                case ProcessingAction.Analyze:
                    await ProcessStage(ProcessingStage.Analyze);
                    break;

                case ProcessingAction.Apply:
                    await ProcessStage(ProcessingStage.Apply);
                    break;

                case ProcessingAction.FullProcess:
                    bool success = // rely on lazy eval...
                        await ProcessStage(ProcessingStage.Load) &&
                        await ProcessStage(ProcessingStage.Analyze) &&
                        await ProcessStage(ProcessingStage.Apply);
                    break;
            }
        }


        /// <summary>
        /// Process the specific stage and handle the errors
        /// </summary>
        /// <param name="processingStage">Stage to process</param>
        /// <returns>true iff successfully processed with error causing process to stop</returns>
        private async Task<bool> ProcessStage(ProcessingStage processingStage)
        {
            try
            {
                Repo.RecordProcessingStart(processingStage);
                await Repo.Committer.Invoke();
                var action = Repo.District.ProcessingAction;

                switch (processingStage)
                {
                    case ProcessingStage.Load: await Load(); return true;
                    case ProcessingStage.Analyze: await Analyze(); return true;
                    case ProcessingStage.Apply: await Apply(); return true;
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                RefreshContext();
                var pe = (ex as ProcessingException)
                    ?? new ProcessingException(Logger.Here(), $"Unhandled processing error.  {ex.Message}", ex);
                Repo.RecordProcessingError(pe.Message, processingStage);
                //Repo.SetStopFlag(Repo.DistrictId, false);
                await Repo.Committer.Invoke();
                return false;
            }
            finally
            {
                RefreshContext();
                Repo.RecordProcessingStop(processingStage);
                await Repo.Committer.Invoke();
            }
        }


        /// <summary>
        /// Load the District CSV data into the database
        /// This is the first step of the Processing
        /// </summary>
        private async Task Load()
        {
            var loader = new Loader(Repo, Repo.District.BasePath);

            try
            {
                if (Repo.District.IsCsvBased)
                {
                    await loader.LoadFile<CsvOrg>(@"orgs.csv");
                    //await loader.LoadFile<CsvCourse>(@"courses.csv");
                    //await loader.LoadFile<CsvAcademicSession>(@"academicSessions.csv");
                    //await loader.LoadFile<CsvClass>(@"classes.csv");
                    await loader.LoadFile<CsvUser>(@"users.csv");
                    //await loader.LoadFile<CsvEnrollment>(@"enrollments.csv"); // Enrollments csv is not available
                }
                else
                {
                    if (Repo.District.RosteringApiSource == eRosteringApiSource.Classlink)
                    {
                        await loader.LoadClassLinkData<CsvOrg>();
                        await loader.LoadClassLinkData<CsvUser>();
                    }
                    else if (Repo.District.RosteringApiSource == eRosteringApiSource.Clever)
                    {
                        await loader.LoadCleverData<CsvOrg>();
                        await loader.LoadCleverData<CsvUser>();
                    }
                }

                //if (CurrentProcessingAction == ProcessingAction.FullProcess)
                //{
                //    await IncludeInSyncOrgsNightlySync(Repo.DistrictId);
                //}
            }
            catch (Exception ex)
            {
                if (ex is ProcessingException)
                    throw;

                // catch unhandled exception and blame sourceId
                throw new ProcessingException(Logger.Here(),
                    $"An error occured while processing CSV file of {loader.LastEntity}.  Possible duplicate sourcedId. Error message: {ex.Message} Inner exception: {ex.InnerException?.Message}", ex);
            }
        }


        /// <summary>
        /// Load the District CSV data into the database
        /// This is the first step of the Processing
        /// </summary>
        private async Task Analyze()
        {
            if (!string.IsNullOrEmpty(Repo.CurrentHistory.LoadError))
                throw new ProcessingException(Logger.Here(), "Can't Analyze with active LoadError.  Reload first.");

            DateTime? lastLoaded = await Repo.GetLastLoadTime();
            if (!lastLoaded.HasValue)
                throw new ProcessingException(Logger.Here(), "Data has never been loaded.");

            var analyzer = new Analyzer(Logger, Repo);
            if (Repo.District.IsCsvBased)
                await analyzer.MarkDeleted(lastLoaded.Value);
            await analyzer.Analyze();
        }


        private async Task Apply()
        {
            if (!string.IsNullOrEmpty(Repo.CurrentHistory.LoadError) ||
                !string.IsNullOrEmpty(Repo.CurrentHistory.AnalyzeError))
                throw new ProcessingException(Logger.Here(), "Can't Apply with active LoadError or AnalyzeError");

            var applier = new Applier(Services, Repo.DistrictId);

            if (Repo.District.SyncOrgs)
            {
                await applier.ApplyLines<CsvOrg>();
            }

            //if (Repo.District.SyncCourses)
            //{
            // await applier.ApplyLines<CsvCourse>();
            //}

            //if (Repo.District.SyncAcademicSessions)
            //{
            // await applier.ApplyLines<CsvAcademicSession>();
            //}

            //if (Repo.District.SyncClasses)
            //{
            // await applier.ApplyLines<CsvClass>();
            //}

            if (Repo.District.SyncUsers)
            {
                await applier.ApplyLines<CsvUser>();
            }

            //if (Repo.District.SyncEnrollment)
            //{
            // await applier.ApplyLines<CsvEnrollment>();
            //}
        }

        public async Task IncludeInSyncOrgsNightlySync(int districtId)
        {
            District district = await Db.Districts.FindAsync(districtId);
            district.StopCurrentAction = false;
            var lines = await Db.DataSyncLines.Where(w => w.DistrictId == districtId && !w.IncludeInSync && w.Table == nameof(CsvOrg)).ToListAsync();
            lines.ForEach(i => i.IncludeInSync = true);
            await Db.SaveChangesAsync();
        }

    }
}