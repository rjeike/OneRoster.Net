using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Extensions;
using OneRosterSync.Net.Models;

namespace OneRosterSync.Net.Processing
{
    public class RosterScheduler : BackgroundService
    {
        private const int DelayBetweenProcessingMS = 3 * 1000;
        private readonly ILogger Logger;
        private readonly IServiceProvider Services;
        private readonly IBackgroundTaskQueue TaskQueue;

        public RosterScheduler(
            IBackgroundTaskQueue taskQueue,
            IServiceProvider services, 
            ILogger<RosterScheduler> logger)
        {
            TaskQueue = taskQueue;
            Services = services;
            Logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Logger.Here().LogInformation("Starting RosterScheduler");

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(DelayBetweenProcessingMS, cancellationToken);

                using (var scope = Services.CreateScope())
                {
                    using (var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
                    {
                        var now = DateTime.UtcNow;

                        // First find districts scheduled and mark them "FullProcess"
                        var districts = await db.Districts.Where(d => d.NextProcessingTime.HasValue && d.NextProcessingTime <= now).ToListAsync();
                        foreach (var d in districts)
                            d.ProcessingAction = ProcessingAction.FullProcess;
                        await db.SaveChangesAsync();

                        // Now find any district that has any ProcessingAction
                        districts = await db.Districts.Where(d => d.ProcessingAction != ProcessingAction.None).ToListAsync();

                        // walk the districts ready to be processed
                        foreach (var district in districts)
                        {
                            TaskQueue.QueueBackgroundWorkItem(async token =>
                            {
                                Logger.Here().LogInformation($"Begin processing District {district.DistrictId}.");
                                var rosterProcessor = new RosterProcessor(Services, Logger);
                                await rosterProcessor.Process(district.DistrictId, cancellationToken);
                                Logger.Here().LogInformation($"Done processing District {district.DistrictId}.");
                            });

                            DistrictRepo.UpdateNextProcessingTime(district);
                            district.Touch();
                            await db.SaveChangesAsync();
                        }
                    }
                }
            }

            Logger.Here().LogInformation("Stopping RosterScheduler");
        }
    }
}