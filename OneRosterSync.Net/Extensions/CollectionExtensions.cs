using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OneRosterSync.Net.Extensions
{
    public static class CollectionExtensions
    {
        /// <summary>
        /// Takes a collection and breaks it into "chunks" according to chunkSize
        /// and passes each chunk on to the delegate action for processing.
        /// </summary>
        /// <param name="collection">original colletion to chunk</param>
        /// <param name="chunkSize">size of each chunk</param>
        /// <param name="action">delegate function to process each chunk</param>
        public static void ForEachChunk<T>(this IQueryable<T> collection, int chunkSize, Action<List<T>> action)
        {
            for (int chunk = 0; ; chunk++)
            {
                List<T> currentChunk = collection
                   .Skip(chunk * chunkSize)
                   .Take(chunkSize)
                   .ToList();

                if (!currentChunk.Any())
                    break;

                action.Invoke(currentChunk);
            }
        }

        public static async Task ForEachChunkAsync<T>(this IQueryable<T> collection, int chunkSize, Func<List<T>, Task> action)
        {
            for (int chunk = 0; ; chunk++)
            {
                List<T> currentChunk = collection
                   .Skip(chunk * chunkSize)
                   .Take(chunkSize)
                   .ToList();

                if (!currentChunk.Any())
                    break;

                await action(currentChunk);
            }
        }



        /// <summary>
        /// This is equivalent to the standard ForEach extension, but divides the
        /// original collection into chunks rather than pulling it all in one query.
        /// This uses ForEachChunk helper to break it into chunks.
        /// </summary>
        /// <typeparam name="T">collection entity type</typeparam>
        /// <param name="collection">collection to process (this)</param>
        /// <param name="chunkSize">size of the chunks to process at one time</param>
        /// <param name="action">delegate function to process each individual entity</param>
        /// <param name="onChunkComplete">delete to call after each chunk is processed</param>
        public static void ForEachInChunks<T>(this IQueryable<T> collection, int chunkSize, Action<T> action, Action onChunkComplete)
        {
            collection.ForEachChunk(chunkSize, chunk =>
            {
                chunk.ForEach(action);
                onChunkComplete.Invoke();
            });
        }

        public static async Task ForEachInChunksAsync<T>(this IQueryable<T> collection, int chunkSize, Action<T> action, Func<Task> onChunkComplete)
        {
            await collection.AsQueryable().ForEachChunkAsync(chunkSize, async (chunk) =>
            {
                chunk.ForEach(action);
                await onChunkComplete();
            });
        }

        /*
        public static async Task ForEachInChunksAsync<T>(this IQueryable<T> collection, int chunkSize, Func<T, Task> action, Func<Task> onChunkComplete)
        {
            await collection.AsQueryable().ForEachChunkAsync(chunkSize, async (chunk) =>
            {
                await chunk.AsQueryable().ForEachAsync(async item => await action(item));
                await onChunkComplete();
            });
        }
        */
    }
}