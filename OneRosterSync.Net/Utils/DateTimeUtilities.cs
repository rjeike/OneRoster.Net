using System;

namespace OneRosterSync.Net.Utils
{
    public static class DateTimeUtilities
    {
        public static string FormatTimespan(DateTime? started, DateTime? completed)
        {
            if (!started.HasValue)
                return "";

            DateTime end = completed ?? DateTime.UtcNow;

            TimeSpan ts = end - started.Value;

            string result = PrintTimeSpan(ts);

            return completed.HasValue ? result : result + " (pending)";
        }

        public static string PrintTimeSpan(TimeSpan t)
        {
            if (t.TotalMilliseconds < 5000)
                return $"{t.TotalMilliseconds.ToString("N0")}ms";

            if (t.TotalMinutes < 1.0)
                return $"{t.Seconds.ToString("N0")}s";

            if (t.TotalHours < 1.0)
                return $"{t.Minutes.ToString("D2")}m:{t.Seconds.ToString("D2")}s";

            return string.Format("{0}h:{1:D2}m:{2:D2}s", (int)t.TotalHours, t.Minutes, t.Seconds);
        }
    }
}
