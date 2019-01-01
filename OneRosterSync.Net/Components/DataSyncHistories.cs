using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Models;

namespace OneRosterSync.Net.Components
{
    public class DataSyncHistories : ViewComponent
    {
        private readonly ApplicationDbContext db;

        public DataSyncHistories(ApplicationDbContext db)
        {
            this.db = db;
        }

        public IViewComponentResult Invoke(District district)
        {
            var model = db.DataSyncHistories
                .Where(history => history.DistrictId == district.DistrictId)
                .OrderByDescending(h => h.Modified)
                .Take(20)
                .ToList();

            return View(viewName: "Histories", model: model);
        }
    }
}