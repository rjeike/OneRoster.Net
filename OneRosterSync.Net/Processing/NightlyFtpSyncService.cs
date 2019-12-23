using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using OneRosterSync.Net.Controllers;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Models;

namespace OneRosterSync.Net.Processing
{
    public interface INightlyFtpSyncService
    {
        Task RunNightlyFtpSync();
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

        public async Task Run(IJobCancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                await RunNightlyFtpSync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error running nightly sync: {ex.Message}.");
            }
        }

        public async Task RunNightlyFtpSync()
        {
            try
            {
                DataSyncController controller = new DataSyncController(_db, _logger, _hostingEnvironment);
                await controller.LoadFiles(_logger);
                var districts = _db.Districts.Where(w => w.NightlySyncEnabled && w.ReadyForNightlySync).ToList();
                foreach (var district in districts)
                {
                    try
                    {
                        await controller.FullProcess(district.DistrictId);
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
        }
    }
}
