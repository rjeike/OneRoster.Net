using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Models;
using OneRosterSync.Net.Processing;
using ReflectionIT.Mvc.Paging;

namespace OneRosterSync.Net.Controllers
{
    public class WhatsNewViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext db;

        public WhatsNewViewComponent(ApplicationDbContext db)
        {
            this.db = db;
        }

        public IViewComponentResult Invoke(District district)
        {
            //District district = db.Districts.Find(districtId);
            var model = db.DataSyncHistories
                .Where(history => history.DistrictId == district.DistrictId)
                .OrderByDescending(h => h.Modified)
                .Take(20)
                .ToList();

            return View(viewName: "Histories", model: model);
        }
    }

    public class DataSyncController : Controller
    {
        private readonly ApplicationDbContext db;
        private readonly IBackgroundTaskQueue TaskQueue;

        public DataSyncController(
            IBackgroundTaskQueue taskQueue,
            ApplicationDbContext db)
        {
            TaskQueue = taskQueue;
            this.db = db;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult DistrictList()
        {
            return View(db.Districts.ToList());
        }

        [HttpGet]
        public IActionResult DistrictCreate()
        {
            return View();
        }

        private IActionResult RedirectToDistrict(int districtId) =>
            RedirectToAction(nameof(DistrictEdit), new { districtId });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DistrictCreate([Bind("Name")] District district)
        {
            if (!ModelState.IsValid)
                return View(district);

            db.Add(district);
            await db.SaveChangesAsync();
            return RedirectToDistrict(district.DistrictId);
        }

        [HttpGet]
        public IActionResult DistrictEdit(int districtId)
        {
            District district = db.Districts.Find(districtId);
            if (district == null)
                return NotFound($"District {districtId} not found");

            return View(district);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DistrictEdit(District district)
        {
            if (!ModelState.IsValid)
                return View(district);

            if (district.DailyProcessingTime.HasValue)
            {
                TimeSpan t = district.DailyProcessingTime.Value;
                TimeSpan min = new TimeSpan(0);
                TimeSpan max = new TimeSpan(hours: 24, minutes: 0, seconds: 0);
                if (t < min || t > max)
                {
                    ModelState.AddModelError(
                        key: nameof(district.DailyProcessingTime), 
                        errorMessage: "Invalid Daily Processing Time.  Please enter a time between 0:0:0 and 23:59:59.  Or clear to disable daily processing.");
                    return View(district);
                }
            }

            District d = db.Districts.Find(district.DistrictId);

            d.Name = district.Name;
            d.DailyProcessingTime = district.DailyProcessingTime;
            d.Touch();

            await db.SaveChangesAsync();

            return RedirectToDistrict(district.DistrictId);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DistrictProcess(District district)
        {
            District d = db.Districts.Find(district.DistrictId);
            //d.NextProcessingTime = DateTime.UtcNow.AddSeconds(1);
            d.ProcessingStatus = ProcessingStatus.ManuallyScheduled;
            d.Touch();
            await db.SaveChangesAsync();

            return RedirectToDistrict(district.DistrictId);
        }

        [HttpGet]
        public async Task<IActionResult> DataSyncLines(int districtId, int page = 1, string table = null, string filter = null, LoadStatus? loadStatus = null, SyncStatus? syncStatus = null)
        {
            var query = db.DataSyncLines
                .Where(l => l.DistrictId == districtId);

            if (!string.IsNullOrEmpty(table))
                query = query.Where(l => l.Table == table);

            if (!string.IsNullOrEmpty(filter))
                query = query.Where(l => l.SourceId.Contains(filter) || l.TargetId.Contains(filter));

            if (loadStatus.HasValue)
                query = query.Where(l => l.LoadStatus == loadStatus.Value);

            if (syncStatus.HasValue)
                query = query.Where(l => l.SyncStatus == syncStatus.Value);

            var orderedQuery = query.OrderByDescending(l => l.LastSeen);

            var model = await PagingList.CreateAsync(orderedQuery, 10, page);

            model.Action = nameof(DataSyncLines);
            model.RouteValue = new RouteValueDictionary
            {
                { "districtId", districtId }
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult HistoryInfo(int dataSyncHistoryId)
        {
            var model = db.DataSyncHistories
                .Include(h => h.District)
                .SingleOrDefault(h => h.DataSyncHistoryId == dataSyncHistoryId);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> HistoryDetailsList(int dataSyncHistoryId)
        {
            var model = await db.DataSyncHistoryDetails
                .Where(d => d.DataSyncHistoryId == dataSyncHistoryId) 
                .OrderByDescending(d => d.Modified)
                .Take(20)
                .ToListAsync();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> SelectCourses(int districtId)
        {
            var model = await db.DataSyncLines
                .Where(l => l.DistrictId == districtId && l.Table == "CsvCourse")
                .ToListAsync();

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectCourses(int districtId, IEnumerable<string> SelectedCourses)
        {
            var model = await db.DataSyncLines
                .Where(l => l.DistrictId == districtId && l.Table == "CsvCourse")
                .ToListAsync();

            foreach (var course in model)
            {
                bool include = SelectedCourses.Contains(course.SourceId);
                if (course.IncludeInSync == include)
                    continue;
                course.IncludeInSync = include;
                course.Touch();
            }

            await db.SaveChangesAsync();

            return RedirectToDistrict(districtId);
        }


        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(int districtId)
        {
            var query = db.DataSyncLines
                .Where(l => l.DistrictId == districtId && l.SyncStatus == SyncStatus.ReadyToApply);

            int i = 0;
            foreach (var line in query)
            {
                // TODO call API and handle result
                if (string.IsNullOrEmpty(line.TargetId))
                    line.TargetId = Guid.NewGuid().ToString();

                line.SyncStatus = SyncStatus.Applied;
                line.Touch();

                if (++i > 50)
                {
                    await db.SaveChangesAsync();
                    i = 0;
                }
            }
            await db.SaveChangesAsync();

            return RedirectToDistrict(districtId);
        }


    }
}