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

            return completed.HasValue ? result : result + " (still running)";
        }

        public static string PrintTimeSpan(TimeSpan t)
        {
            if (t.TotalMilliseconds < 3000)
                return string.Format("{0}ms", (int)t.TotalMilliseconds);

            if (t.TotalMinutes < 1.0)
                return string.Format("{0}s", (int)t.Seconds);

            if (t.TotalHours < 1.0)
                return string.Format("{0}m:{1:D2}s", t.Minutes, t.Seconds);

            return string.Format("{0}h:{1:D2}m:{2:D2}s", (int)t.TotalHours, t.Minutes, t.Seconds);
        }
    }
}
