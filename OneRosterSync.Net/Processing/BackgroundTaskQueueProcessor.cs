using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneRosterSync.Net.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OneRosterSync.Net.Processing
{
    public class BackgroundTaskQueueProcessor : BackgroundService
    {
        public IBackgroundTaskQueue TaskQueue { get; }
        private readonly ILogger Logger;

        public BackgroundTaskQueueProcessor(
            IBackgroundTaskQueue taskQueue, 
            ILoggerFactory loggerFactory)
        {
            TaskQueue = taskQueue;
            Logger = loggerFactory.CreateLogger<BackgroundTaskQueueProcessor>();
        }

        protected async override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Logger.Here().LogInformation("BackgroundTaskQueueProcessor is starting.");

            while (!cancellationToken.IsCancellationRequested)
            {
                var workItem = await TaskQueue.DequeueAsync(cancellationToken);

                try
                {
                    await workItem(cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.Here().LogError(ex, $"Error occurred executing {nameof(workItem)}.");
                }
            }

            Logger.Here().LogInformation("BackgroundTaskQueueProcessor is stopping.");
        }
    }
}
