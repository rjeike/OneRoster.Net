using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneRosterSync.Net.DAL;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Models;
using OneRosterSync.Net.Processing;

namespace OneRosterSync.Net.Components
{
    public class DataSyncHistories : ViewComponent
    {
        private readonly ApplicationDbContext db;

        public DataSyncHistories(ApplicationDbContext db)
        {
            this.db = db;
        }

        public async Task<IViewComponentResult> InvokeAsync(int districtId, bool current)
        {
            var repo = new DistrictRepo(db, districtId);

            var model = await repo.DataSyncHistories
                .AsNoTracking()
                .OrderByDescending(h => h.Modified)
                .Take(20)
                .ToListAsync();

            string view = current ? "CurrentHistory" : "ListOfHistories";

            return View(viewName: view, model: model);
        }
    }
}