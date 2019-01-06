using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneRosterSync.Net.DAL;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Extensions;
using OneRosterSync.Net.Models;

namespace OneRosterSync.Net.Processing
{
    public class RosterScheduler : BackgroundService
    {
        /// <summary>
        /// How long to sleep between scanning for Districts to process
        /// </summary>
        private const int DelayBetweenProcessingMS = 3 * 1000;

        private readonly ILogger Logger = ApplicationLogging.Factory.CreateLogger<RosterScheduler>();
        private readonly IServiceProvider Services;
        private readonly IBackgroundTaskQueue TaskQueue;

        public RosterScheduler(IBackgroundTaskQueue taskQueue, IServiceProvider services)
        {
            TaskQueue = taskQueue;
            Services = services;
        }


        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                Logger.Here().LogInformation("Starting RosterScheduler");

                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(DelayBetweenProcessingMS, cancellationToken);
                    using (var scope = Services.CreateScope())
                    using (var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
                        await ScanForDistrictsToProcess(db);
                }
            }
            finally
            {
                Logger.Here().LogInformation("Stopping RosterScheduler");
            }
        }


        private async Task ScanForDistrictsToProcess(ApplicationDbContext db)
        {
            var now = DateTime.UtcNow;

            // First find districts scheduled and mark them "FullProcess"
            var districts = await db.Districts.Where(d => d.NextProcessingTime.HasValue && d.NextProcessingTime <= now).ToListAsync();
            if (districts.Any())
            {
                foreach (var d in districts)
                    d.ProcessingAction = ProcessingAction.FullProcess;
                await db.SaveChangesAsync();
            }

            // Now find any district that has any ProcessingAction
            districts = await db.Districts.Where(d => d.ProcessingAction != ProcessingAction.None).ToListAsync();

            // walk the districts ready to be processed
            foreach (var district in districts)
            {
                var worker = new RosterProcessorWorker(district.DistrictId, Services, district.ProcessingAction);
                TaskQueue.QueueBackgroundWorkItem(async token => await worker.Invoke(token));

                // clear the action out and reset the next processing time so it won't get picked up again
                district.ProcessingAction = ProcessingAction.None;
                DistrictRepo.UpdateNextProcessingTime(district);
                district.Touch();
                await db.SaveChangesAsync();
            }
        }


        class RosterProcessorWorker
        {
            private readonly ILogger Logger = ApplicationLogging.Factory.CreateLogger<RosterScheduler>();

            readonly int DistrictId;
            readonly IServiceProvider Services;
            readonly ProcessingAction ProcessingAction;

            public RosterProcessorWorker(int districtId, IServiceProvider services, ProcessingAction processingAction)
            {
                DistrictId = districtId;
                Services = services;
                ProcessingAction = processingAction;
            }

            public async Task Invoke(CancellationToken cancellationToken)
            {
                Logger.Here().LogInformation($"Begin processing District {DistrictId}.");

                using (var processor = new RosterProcessor(Services, DistrictId, cancellationToken))
                    await processor.Process(ProcessingAction);

                Logger.Here().LogInformation($"Done processing District {DistrictId}.");
            }
        }
    }
}