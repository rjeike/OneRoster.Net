using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
            Logger.LogInformation("BackgroundTaskQueueProcessor is starting.");

            while (!cancellationToken.IsCancellationRequested)
            {
                var workItem = await TaskQueue.DequeueAsync(cancellationToken);

                try
                {
                    await workItem(cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"Error occurred executing {nameof(workItem)}.");
                }
            }

            Logger.LogInformation("BackgroundTaskQueueProcessor is stopping.");
        }
    }
}
