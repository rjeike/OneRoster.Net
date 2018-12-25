using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace OneRosterSync.Net
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string outputTemplate = 
                "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message}{NewLine}in method {MemberName} at {FilePath}:{LineNumber}{NewLine}{Exception}{NewLine}";

            // TODO - a better way to generate default directory?
            //string basePath = @"C:\temp\logs\OneRoster.Net\";
            string basePath = @"Logs\";

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()

                // default logger - everything
                .WriteTo.File(
                    path: basePath + @"Default.log", 
                    outputTemplate: outputTemplate, 
                    rollingInterval: RollingInterval.Day)

                // log just MockApiController
                .WriteTo.Logger(lc1 => lc1
                    .Filter.ByIncludingOnly("SourceContext = 'OneRosterSync.Net.Controllers.MockApiController'")
                        .WriteTo.File(
                            path: basePath + @"MockAPI.log",
                            restrictedToMinimumLevel: LogEventLevel.Information,
                            outputTemplate: outputTemplate,
                            rollingInterval: RollingInterval.Day))

                .CreateLogger();

            try
            {
                Log.Information("Starting web host");
                WebHost.CreateDefaultBuilder(args)
                    .UseStartup<Startup>()
                    .UseSerilog()
                    .Build()
                    .Run();

                return;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        /*
        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
               .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddDebug();
                    logging.AddSerilog();
                });
        */
    }
}
