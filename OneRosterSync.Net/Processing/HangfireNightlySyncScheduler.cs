using Hangfire;
using Hangfire.Dashboard;
using Hangfire.SqlServer;
using Hangfire.Storage;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using TimeZoneConverter;

namespace OneRosterSync.Net.Processing
{
    public class HangfireNightlySyncScheduler
    {
        public static void ScheduleNightlySync(string conString, List<string> cronExpressions)
        {
            JobStorage.Current = new SqlServerStorage(conString);
            var CSTZone = TZConvert.GetTimeZoneInfo("Central Standard Time");
            if (CSTZone != null)
            {
                // remove all existing jobs as new will be created
                var recurringJobs = JobStorage.Current.GetConnection().GetRecurringJobs();
                foreach (var job in recurringJobs)
                {
                    RecurringJob.RemoveIfExists(job.Id);
                }
                //RecurringJob.RemoveIfExists(nameof(NightlyFtpSyncService));
                int i = 0;
                cronExpressions.ForEach((cronExp) =>
                {
                    try
                    {
                        i++;
                        //RecurringJob.RemoveIfExists($"{nameof(NightlyFtpSyncService)}_{i}");
                        RecurringJob.AddOrUpdate<NightlyFtpSyncService>($"{nameof(NightlyFtpSyncService)}_{i}", job => job.Run(cronExp, JobCancellationToken.Null),
                            cronExp, CSTZone);
                    }
                    catch (Exception ex)
                    {
                    }
                });

                RecurringJob.AddOrUpdate<NightlyFtpSyncService>(nameof(NightlyFtpSyncService), job => job.SendConsolidatedSyncErrorsEmail(JobCancellationToken.Null),
                   "0 7 * * *", CSTZone);
                //RecurringJob.AddOrUpdate<NightlyFtpSyncService>(nameof(NightlyFtpSyncService), job => job.Run(JobCancellationToken.Null),
                //   Cron.Daily(1), CSTZone);
                //RecurringJob.AddOrUpdate<NightlyFtpSyncService>(nameof(NightlyFtpSyncService), job => job.Run(JobCancellationToken.Null),
                //    Cron.Daily(7, 20), CSTZone);
                //RecurringJob.AddOrUpdate<NightlyFtpSyncService>(nameof(NightlyFtpSyncService), job => job.Run(JobCancellationToken.Null),
                //   $"*/{10} * * * *", TimeZoneInfo.Local);
            }
        }
    }

    public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        public HangfireDashboardAuthorizationFilter(SignInManager<IdentityUser> signInManager)
        {
            _signInManager = signInManager;
        }

        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();
            return _signInManager.IsSignedIn(httpContext.User);
        }
    }
}
