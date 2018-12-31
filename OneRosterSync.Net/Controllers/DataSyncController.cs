using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Extensions;
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
        private readonly ILogger Logger;

        public DataSyncController(
            IBackgroundTaskQueue taskQueue,
            ApplicationDbContext db,
            ILogger<DataSyncController> logger)
        {
            TaskQueue = taskQueue;
            this.db = db;
            Logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> DistrictList()
        {
            var model = await db.Districts.Select(d => new DistrictViewModel
            {
                DistrictId = d.DistrictId,
                Name = d.Name,
                NumRecords = db.DataSyncLines.Count(l => l.DistrictId == d.DistrictId),
                TimeOfDay = d.DailyProcessingTime.ToString(),
                ProcessingStatus = d.ProcessingStatus.ToString(),
                Modified = d.Modified.ToLocalTime().ToString(),
            })
            .ToListAsync();
                
            return View(model);
        }

        [HttpGet]
        public IActionResult DistrictCreate()
        {
            return View();
        }

        /// <summary>
        /// Delete a District and all associated records
        /// Should NOT BE AVAILABLE to regular users
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DistrictDelete(int districtId)
        {
            var district = db.Districts.Find(districtId);

            // retrieve entire list of histories
            var histories = await db.DataSyncHistories
                .Where(h => h.DistrictId == districtId)
                .ToListAsync();

            // delete the histories in chunks
            await histories
                .AsQueryable()
                .ForEachInChunksAsync(
                    chunkSize: 50,
                    action: history =>
                    {
                        // for each history, delete the details associated with it as well
                        var details = db.DataSyncHistoryDetails
                            .Where(d => d.DataSyncHistoryId == history.DataSyncHistoryId)
                            .ToList();
                        db.DataSyncHistoryDetails.RemoveRange(details);
                    }, 
                    // commit changes after each chunk
                    onChunkComplete: async () => await db.SaveChangesAsync());

            db.DataSyncHistories.RemoveRange(histories);
            await db.SaveChangesAsync();

            // now delete the lines
            var lines = await db.DataSyncLines
                .Where(l => l.DistrictId == districtId)
                .ToListAsync();
            db.DataSyncLines.RemoveRange(lines);
            await db.SaveChangesAsync();

            // finally, delete the district itself!
            db.Districts.Remove(district);
            await db.SaveChangesAsync();

            return RedirectToAction(nameof(DistrictList));
        }

        private IActionResult RedirectToDistrict(int districtId) =>
            RedirectToAction(nameof(DistrictDashboard), new { districtId });

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
        public IActionResult DistrictDashboard(int districtId)
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

        /*
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DistrictProcess(District district)
        {
            District d = db.Districts.Find(district.DistrictId);
            d.ProcessingStatus = ProcessingStatus.Scheduled;
            d.Touch();
            await db.SaveChangesAsync();

            return RedirectToDistrict(district.DistrictId);
        }
        */

        /// <summary>
        /// Display all matching records for the District (DataSyncLines)
        /// </summary>
        /// <param name="districtId"></param>
        /// <param name="page"></param>
        /// <param name="table">CSV Table to filter</param>
        /// <param name="filter">Filter for Source and/or Target Id</param>
        /// <param name="loadStatus"></param>
        /// <param name="syncStatus"></param>
        [HttpGet]
        public async Task<IActionResult> DataSyncLines(int districtId, 
            int page = 1, string table = null, string filter = null, 
            LoadStatus? loadStatus = null, SyncStatus? syncStatus = null)
        {
            District district = await db.Districts.FindAsync(districtId);
            if (district == null)
                return NotFound($"District {districtId} not found");

            ViewData["DistrictName"] = district.Name;

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
            DataSyncHistory history = await db.DataSyncHistories
                .Include(h => h.District)
                .SingleOrDefaultAsync(h => h.DataSyncHistoryId == dataSyncHistoryId);

            ViewData["DistrictName"] = history.District.Name;

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

        /*
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveChanges(int districtId)
        {
            District district = db.Districts.Find(districtId);
            district.ProcessingStatus = ProcessingStatus.Approved;
            district.Touch();
            await db.SaveChangesAsync();

            return RedirectToDistrict(districtId);
        }
        */

        private async Task<IActionResult> Process(int districtId, ProcessingAction processingAction)
        {
            District district = db.Districts.Find(districtId);

            district.ProcessingAction = processingAction;
            await db.SaveChangesAsync();

            return RedirectToDistrict(districtId);
        }

        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Load(int districtId) => await Process(districtId, ProcessingAction.Load);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> LoadSample(int districtId) => await Process(districtId, ProcessingAction.LoadSample);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Analyze(int districtId) => await Process(districtId, ProcessingAction.Analyze);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Apply(int districtId) => await Process(districtId, ProcessingAction.Apply);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> FullProcess(int districtId) => await Process(districtId, ProcessingAction.FullProcess);
    }
}