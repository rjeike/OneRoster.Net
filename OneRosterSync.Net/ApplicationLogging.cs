using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace OneRosterSync.Net
{
    public class ApplicationLogging
    {
        public static ILoggerFactory Factory { get; set; }

        public static void ConfigureSerilog()
        {
            const string outputTemplate =
                "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}" +
                "{Message}{NewLine}" +
                "in Method {MemberName} at {FilePath}:{LineNumber}{NewLine}" +
                "{Exception}{NewLine}";

            const string processingOutputTemplate =
                "[{Timestamp:HH:mm:ss.fff} {Level}] {Message}{NewLine}" +
                "in Method {MemberName} at {FilePath}:{LineNumber}{NewLine}" +
                "{Exception}{NewLine}";

            const string apiOutputTemplate =
                "[{Timestamp:HH:mm:ss.fff}] {Message}{NewLine}";

            // TODO - a better way to generate default directory?
            //string basePath = @"C:\temp\logs\OneRoster.Net\";
            string basePath = @"Logs\";

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()

                // Default logger (to console)
                .WriteTo.Logger(l => l
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                    .WriteTo.Console())

                // Errors
                .WriteTo.Logger(l => l
                    .MinimumLevel.Error()
                    .WriteTo.File(
                        path: basePath + @"Errors.log",
                        outputTemplate: outputTemplate,
                        rollingInterval: RollingInterval.Day))

                // Processor Logger
                .WriteTo.Logger(l => l
                    .Filter.ByIncludingOnly("SourceContext like 'OneRosterSync.Net.Processing%'")
                    .WriteTo.File(
                        path: basePath + @"Processing.log",
                        outputTemplate: processingOutputTemplate,
                        rollingInterval: RollingInterval.Day))

                // MockAPI Logger
                .WriteTo.Logger(l => l
                    .Filter.ByIncludingOnly("SourceContext = 'OneRosterSync.Net.Controllers.MockApiController'")
                    .WriteTo.File(
                        path: basePath + @"MockApi.log",
                        restrictedToMinimumLevel: LogEventLevel.Information,
                        outputTemplate: apiOutputTemplate,
                        rollingInterval: RollingInterval.Day))

                .CreateLogger();
        }
    }
}