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
    public class DataLineCache
    {
        private Dictionary<string, Dictionary<string, DataSyncLine>> Cache = 
            new Dictionary<string, Dictionary<string, DataSyncLine>>();

        ILogger Logger;

        public DataLineCache(ILogger logger)
        {
            Logger = logger;
        }

        public async Task Load(IQueryable<DataSyncLine> filter)
        {
            Cache.Clear();

            Logger.Here().LogInformation("Begin Loading DataLineCache");

            var sw = new Stopwatch();
            sw.Start();

            // could possibly use GroupBy instead, but this might be fine
            string[] tables = await filter.Select(l => l.Table).Distinct().ToArrayAsync();
            foreach (string table in tables)
                Cache[table] = await filter
                    .Where(l => l.Table == table)
                    .ToDictionaryAsync(l => l.SourceId, l => l);

            sw.Stop();
            Logger.Here().LogInformation($"Done Loading DataLineCache.  It took {sw.ElapsedMilliseconds} milliseconds.");
        }

        public Dictionary<string, DataSyncLine> GetMap<T>() where T : CsvBaseObject
        {
            string key = typeof(T).Name;
            return Cache.ContainsKey(key) 
                ? Cache[key] 
                : new Dictionary<string, DataSyncLine>();
        }
    }
}
