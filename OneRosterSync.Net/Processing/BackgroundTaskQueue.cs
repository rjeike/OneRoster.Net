using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace OneRosterSync.Net.Processing
{
    /// <summary>
    /// Taken from the following:
    /// https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-2.1
    /// </summary>
    public interface IBackgroundTaskQueue
    {
        void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem);

        Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
    }

    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private ConcurrentQueue<Func<CancellationToken, Task>> WorkItems =
            new ConcurrentQueue<Func<CancellationToken, Task>>();

        private SemaphoreSlim Signal = new SemaphoreSlim(0);

        public void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            WorkItems.Enqueue(workItem);
            Signal.Release();
        }

        public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        {
            await Signal.WaitAsync(cancellationToken);
            WorkItems.TryDequeue(out var workItem);

            return workItem;
        }
    }
}
