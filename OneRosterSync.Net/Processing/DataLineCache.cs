using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneRosterSync.Net.Extensions;
using OneRosterSync.Net.Models;

namespace OneRosterSync.Net.Processing
{
    /// <summary>
    /// Helper class to cache DataLine records in memory
    /// </summary>
    public class DataLineCache
    {
        private Dictionary<string, Dictionary<string, DataSyncLine>> Cache = 
            new Dictionary<string, Dictionary<string, DataSyncLine>>();

        ILogger Logger;

        public DataLineCache(ILogger logger)
        {
            Logger = logger;
        }

        /// <summary>
        /// Load specific Csv entities into memory
        /// </summary>
        /// <param name="filter">Pass District DataSyncLines</param>
        /// <param name="tables">List of tables to load</param>
        public async Task Load(IQueryable<DataSyncLine> filter, IEnumerable<string> tables)
        {
            Cache.Clear();

            Logger.Here().LogInformation("Begin Loading DataLineCache");

            var sw = new Stopwatch();
            sw.Start();

            // could possibly use GroupBy instead, but this might be fine
            //string[] tables = await filter.Select(l => l.Table).Distinct().ToArrayAsync();
            foreach (string table in tables)
                Cache[table] = await filter
                    .Where(l => l.Table == table)
                    .ToDictionaryAsync(l => l.SourcedId, l => l);

            sw.Stop();
            Logger.Here().LogInformation($"Done Loading DataLineCache.  It took {sw.ElapsedMilliseconds} milliseconds.");
        }

        /// <summary>
        /// Get a mapping between sourcedId ==> cached table of DataLines
        /// </summary>
        /// <typeparam name="T">Csv entity type</typeparam>
        public Dictionary<string, DataSyncLine> GetMap<T>() where T : CsvBaseObject
        {
            string key = typeof(T).Name;
            return Cache.ContainsKey(key) 
                ? Cache[key] 
                : new Dictionary<string, DataSyncLine>();
        }
    }
}
