using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OneRosterSync.Net.DAL;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Models;
using OneRosterSync.Net.Processing;

namespace OneRosterSync.Net.Components
{
    public class DataSyncStats : ViewComponent
    {
        private readonly ILogger Logger;
        private readonly ApplicationDbContext db;

        public DataSyncStats(ApplicationDbContext db, ILogger<DataSyncStats> logger)
        {
            this.db = db;
            Logger = logger;
        }

        public async Task<IViewComponentResult> InvokeAsync(int districtId, bool syncDetails)
        {
            var repo = new DistrictRepo(db, districtId);

            var model = await OneRosterSync.Net.Controllers.DataSyncController.ReportLine(repo.Lines(), "");
            string view = syncDetails ? "StatsSyncDetails" : "Stats";
            return View(viewName: view, model: model);
        }
    }
}