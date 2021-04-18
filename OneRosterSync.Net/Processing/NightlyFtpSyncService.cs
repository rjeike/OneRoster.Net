using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneRosterSync.Net.Controllers;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Models;
using OneRosterSync.Net.Utils;
using TimeZoneConverter;

namespace OneRosterSync.Net.Processing
{
    public interface INightlyFtpSyncService
    {
        Task RunNightlyFtpSync(string cronExp);
    }
    public class NightlyFtpSyncService : INightlyFtpSyncService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<DataSyncController> _logger;
        private readonly IHostingEnvironment _hostingEnvironment;
        public NightlyFtpSyncService(ApplicationDbContext db, ILogger<DataSyncController> logger, IHostingEnvironment hostingEnvironment)
        {
            _db = db; _logger = logger; _hostingEnvironment = hostingEnvironment;
        }

        public async Task Run(string cronExp, IJobCancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                await RunNightlyFtpSync(cronExp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error running nightly sync: {ex.Message}.");
            }
        }

        public async Task RunNightlyFtpSync(string cronExp)
        {
            try
            {
                DataSyncController controller = new DataSyncController(_db, _logger, _hostingEnvironment);
                await controller.LoadFiles(_logger);
                var districts = _db.Districts.Where(w => w.NightlySyncEnabled && w.ReadyForNightlySync && w.CronExpression.Equals(cronExp)).ToList();
                foreach (var district in districts)
                {
                    try
                    {
                        await controller.FullProcess(district.DistrictId);
                        district.ReadyForNightlySync = false;
                        _db.Entry(district).State = EntityState.Modified;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing in nightly sync for district: {district.DistrictId}. Error: {ex.Message}.");
                        throw ex;
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                _db.SaveChanges();
            }
        }

        public async Task SendConsolidatedSyncErrorsEmail(IJobCancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                var districtsHistories = await _db.DataSyncHistories
                   .Where(w => w.District.NightlySyncEnabled && w.Created.Date == DateTime.Today && (w.LoadError != null || w.AnalyzeError != null || w.ApplyError != null))
                   .GroupBy(g => g.DistrictId)
                   .Select(s => s.OrderByDescending(c => c.DistrictId).FirstOrDefault()).Select(s => s.District.Name)
                   .ToListAsync();

                if (districtsHistories?.Count > 0)
                {
                    string districtWithErrors = string.Join("\n", districtsHistories);

                    var CSTZone = TZConvert.GetTimeZoneInfo("Central Standard Time");
                    string time = $"{DateTime.UtcNow.ToString("dddd, dd MMMM yyyy HH:mm:ss")} UTC";
                    if (CSTZone != null)
                        time = $"{TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CSTZone).ToString("dddd, dd MMMM yyyy HH:mm:ss")} CST";
                    var emailConfig = _db.EmailConfigs.FirstOrDefault();
                    if (emailConfig != null && emailConfig.IsActive)
                    {
                        string subject = $"{emailConfig.Subject} Nightly Sync Error(s)";
                        string body = $"You are receiving this email at {time} because error(s) occurred in tonight's nightly sync in OneRoster.\n\n";
                        body += $"District(s):\n\n{districtWithErrors}";
                        EmailManager.SendEmail(emailConfig.Host, emailConfig.From, emailConfig.Password, emailConfig.DisplayName, emailConfig.To,
                            emailConfig.Cc, emailConfig.Bcc, subject, body);
                    }
                    else
                    {
                        _logger.LogError($"Email configuration not found in database. Not sending email.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error running consolidated sync errors job: {ex.Message}.");
            }
        }
    }
}
