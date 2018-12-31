using System;
using System.Threading.Tasks;

namespace OneRosterSync.Net.Utils
{
    /// <summary>
    /// Helper class to count requests and only Invoke and only once a 
    /// "chunk" of requests has been submitted
    /// </summary>
    public class ActionCounter
    {
        private readonly int ChunkSize;
        private readonly Func<Task> AsyncAction;

        private int Count = 0;

        /// <summary>
        /// Bookkeeping of how many times we actually Invoked
        /// </summary>
        public int TotalInvokes { get; private set; }

        /// <summary>
        /// ActionCounter Constructor
        /// </summary>
        /// <param name="asyncAction">async Action to be called on Invoke</param>
        /// <param name="chunkSize">number of calls to InvokeIfChunk before actually Invoking</param>
        public ActionCounter(Func<Task> asyncAction, int chunkSize = 50)
        {
            ChunkSize = chunkSize;
            AsyncAction = asyncAction;
        }

        /// <summary>
        /// Increment counter, then Invoke Action and reset counter if we have reached ChunkSize 
        /// </summary>
        public async Task InvokeIfChunk()
        {
            if (++Count >= ChunkSize)
                await Invoke();
        }

        /// <summary>
        /// Invoke the Action if Counter is greater than zero and reset counter
        /// </summary>
        public async Task InvokeIfAny()
        {
            if (Count > 0)
                await Invoke();
        }

        /// <summary>
        /// Invoke the Action and reset the counter
        /// </summary>
        public async Task Invoke()
        {
            await AsyncAction.Invoke();
            TotalInvokes++;
            Count = 0;
        }
    }
}