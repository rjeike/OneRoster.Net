using Hangfire;
using Hangfire.Annotations;
using Hangfire.Dashboard;
using Hangfire.SqlServer;
using OneRosterSync.Net.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OneRosterSync.Net.Processing
{
    public class HangfireNightlySyncScheduler
    {
        public static void ScheduleNightlySync(string conString)
        {
            JobStorage.Current = new SqlServerStorage(conString);
            var CSTZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
            if (CSTZone != null)
            {
                RecurringJob.RemoveIfExists(nameof(NightlyFtpSyncService));
                RecurringJob.AddOrUpdate<NightlyFtpSyncService>(nameof(NightlyFtpSyncService), job => job.Run(JobCancellationToken.Null),
                    Cron.Daily(1), CSTZone);
                //RecurringJob.AddOrUpdate<NightlyFtpSyncService>(nameof(NightlyFtpSyncService), job => job.Run(JobCancellationToken.Null),
                //    Cron.Daily(7, 20), CSTZone);
                //RecurringJob.AddOrUpdate<NightlyFtpSyncService>(nameof(NightlyFtpSyncService), job => job.Run(JobCancellationToken.Null),
                //   $"*/{10} * * * *", TimeZoneInfo.Local);
            }
        }
    }

}
