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

namespace OneRosterSync.Net.Processing
{
    public class RosterScheduler : BackgroundService
    {
        private const int DelayBetweenProcessingMS = 5 * 1000;
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
                        DateTime now = DateTime.UtcNow;
                        var districts = await db.Districts
                            .Where(d => d.NextProcessingTime.HasValue && d.NextProcessingTime <= now 
                                || d.ProcessingStatus == Models.ProcessingStatus.Scheduled
                                || d.ProcessingStatus == Models.ProcessingStatus.Approved)
                            .ToListAsync();

                        // walk the districts ready to be processed
                        foreach (var district in districts)
                        {
                            TaskQueue.QueueBackgroundWorkItem(async token =>
                            {
                                Logger.Here().LogInformation($"Begin processing District {district.DistrictId}.");
                                var rosterProcessor = new RosterProcessor(Services, Logger);
                                await rosterProcessor.ProcessDistrict(district.DistrictId, cancellationToken);
                                Logger.Here().LogInformation($"Done processing District {district.DistrictId}.");
                            });

                            // update the next processing to be the time of day called for either today or tomorrow if already passed
                            DateTime? next = null;
                            if (district.DailyProcessingTime.HasValue)
                            {
                                // today at the processing time
                                next = now.Date.Add(district.DailyProcessingTime.Value);

                                // if the time to process has already passed, then tomorrow
                                if (next <= now)
                                    next = next.Value.AddDays(1);
                            }
                            district.NextProcessingTime = next;
                            //district.ProcessingStatus = Models.ProcessingStatus.Queued;
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