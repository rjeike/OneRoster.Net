using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using Serilog.Context;

namespace OneRosterSync.Net.Extensions
{
    public static class LoggerExtensions
    {
        /// <summary>
        /// Adds information to the Serilog context, including:
        /// 1. Member Name
        /// 2. File Path
        /// 3. Line Number
        /// This is done at compile time for performance (rather than using reflection).
        /// </summary>
        public static ILogger Here(this ILogger logger,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            LogContext.PushProperty("MemberName", memberName);
            LogContext.PushProperty("FilePath", sourceFilePath);
            LogContext.PushProperty("LineNumber", sourceLineNumber);

            return logger;
        }
    }
}
