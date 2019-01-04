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
            ApplicationLogging.ConfigureSerilog();

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
    }
}