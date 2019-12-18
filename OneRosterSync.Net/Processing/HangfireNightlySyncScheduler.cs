using Hangfire;
using Hangfire.Dashboard;
using Hangfire.SqlServer;
using TimeZoneConverter;

namespace OneRosterSync.Net.Processing
{
    public class HangfireNightlySyncScheduler
    {
        public static void ScheduleNightlySync(string conString)
        {
            JobStorage.Current = new SqlServerStorage(conString);
            var CSTZone = TZConvert.GetTimeZoneInfo("Central Standard Time");
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

    public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            //var httpContext = context.GetHttpContext();
            return true;
        }
    }
}
