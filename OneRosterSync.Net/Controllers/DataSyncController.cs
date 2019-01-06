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
using OneRosterSync.Net.DAL;
using ReflectionIT.Mvc.Paging;

namespace OneRosterSync.Net.Controllers
{
    public class DataSyncController : Controller
    {
        private readonly ApplicationDbContext db;
        private readonly ILogger Logger;

        public DataSyncController(ApplicationDbContext db, ILogger<DataSyncController> logger)
        {
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
            .OrderByDescending(d => d.Modified)
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
            var repo = new DistrictRepo(db, districtId);

            await repo.DeleteDistrict();

            return RedirectToAction(nameof(DistrictList));
        }

        private IActionResult RedirectToDistrict(int districtId) =>
            RedirectToAction(nameof(DistrictDashboard), new { districtId });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DistrictCreate([Bind("Name")] District district)
        {
            if (!ModelState.IsValid)
                return View(district);

            // create default values
            district.BasePath = @"CSVSample";
            district.LmsApiEndpoint = @"https://localhost:44312/api/mockapi/";

            db.Add(district);
            await db.SaveChangesAsync();
            return RedirectToDistrict(district.DistrictId);
        }

        [HttpGet]
        public async Task<IActionResult> DistrictDashboard(int districtId)
        {
            District district = await db.Districts.FindAsync(districtId);
            if (district == null)
                return NotFound($"District {districtId} not found");

            return View(district);
        }


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
            var repo = new DistrictRepo(db, districtId);
            if (repo.District == null)
                return NotFound($"District {districtId} not found");

            ViewData["DistrictName"] = repo.District.Name;

            var query = repo.Lines().AsNoTracking();

            if (!string.IsNullOrEmpty(table))
                query = query.Where(l => l.Table == table);

            if (!string.IsNullOrEmpty(filter))
                query = query.Where(l => l.SourcedId.Contains(filter) || l.TargetId.Contains(filter));

            if (loadStatus.HasValue)
                query = query.Where(l => l.LoadStatus == loadStatus.Value);

            if (syncStatus.HasValue)
                query = query.Where(l => l.SyncStatus == syncStatus.Value);

            var orderedQuery = query.OrderByDescending(l => l.LastSeen);

            var model = await PagingList.CreateAsync(orderedQuery, 10, page);

            model.Action = nameof(DataSyncLines);
            model.RouteValue = new RouteValueDictionary
            {
                { "districtId", districtId },
                { "table", table },
                { "filter", filter },
            };

            if (loadStatus.HasValue) model.RouteValue["loadStatus"] = (int)loadStatus.Value;
            if (syncStatus.HasValue) model.RouteValue["syncStatus"] = (int)syncStatus.Value;

            // kludge to remove empty values
            foreach (var kvp in model.RouteValue.Where(kvp => kvp.Value == null).ToList())
                model.RouteValue.Remove(kvp.Key);

            return View(model);
        }


        [HttpGet]
        public IActionResult ClearDataSyncLines(int districtId)
        {
            return RedirectToAction(nameof(DataSyncLines), new { districtId });
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
            var repo = new DistrictRepo(db, districtId);
            var model = await repo.Lines<CsvCourse>().ToListAsync();
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectCourses(int districtId, IEnumerable<string> SelectedCourses)
        {
            var repo = new DistrictRepo(db, districtId);
            var model = await repo.Lines<CsvCourse>().ToListAsync();

            foreach (var course in model)
            {
                bool include = SelectedCourses.Contains(course.SourcedId);
                if (course.IncludeInSync == include)
                    continue;
                course.IncludeInSync = include;
                course.Touch();
                repo.PushLineHistory(course, isNewData: false);
            }

            await repo.Committer.Invoke();

            return RedirectToDistrict(districtId);
        }

        private async Task<IActionResult> Process(int districtId, ProcessingAction processingAction)
        {
            District district = await db.Districts.FindAsync(districtId);

            district.ProcessingAction = processingAction;
            await db.SaveChangesAsync();

            return RedirectToDistrict(districtId);
        }

        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Load(int districtId) => await Process(districtId, ProcessingAction.Load);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> LoadSample(int districtId) => await Process(districtId, ProcessingAction.LoadSample);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Analyze(int districtId) => await Process(districtId, ProcessingAction.Analyze);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Apply(int districtId) => await Process(districtId, ProcessingAction.Apply);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> FullProcess(int districtId) => await Process(districtId, ProcessingAction.FullProcess);




        private static async Task<DataSyncLineReportLine> ReportLine<T>(DistrictRepo repo) where T : CsvBaseObject
        {
            var lines = repo.Lines<T>().AsNoTracking();
            return await ReportLine(lines, typeof(T).Name);
        }

        public static async Task<DataSyncLineReportLine> ReportLine(IQueryable<DataSyncLine> lines, string entity)
        {
            Dictionary<LoadStatus, int> loadStatusStats = await lines
                .GroupBy(l => l.LoadStatus)
                .Select(g => new { LoadStatus = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.LoadStatus, x => x.Count);

            Dictionary<SyncStatus, int> syncStatusStats = await lines
                .GroupBy(l => l.SyncStatus)
                .Select(g => new { SyncStatus = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SyncStatus, x => x.Count);

            return new DataSyncLineReportLine
            {
                Entity = entity,

                IncludeInSync = await lines.CountAsync(l => l.IncludeInSync),

                Added = loadStatusStats.GetValueOrDefault(LoadStatus.Added),
                Modified = loadStatusStats.GetValueOrDefault(LoadStatus.Modified),
                NoChange = loadStatusStats.GetValueOrDefault(LoadStatus.NoChange),
                Deleted = loadStatusStats.GetValueOrDefault(LoadStatus.Deleted),

                Loaded = syncStatusStats.GetValueOrDefault(SyncStatus.Loaded),
                ReadyToApply = syncStatusStats.GetValueOrDefault(SyncStatus.ReadyToApply),
                Applied = syncStatusStats.GetValueOrDefault(SyncStatus.Applied),
                AppliedFailed = syncStatusStats.GetValueOrDefault(SyncStatus.ApplyFailed),

                TotalRecords = await lines.CountAsync(),
            };
        }

        [HttpGet]
        public async Task<IActionResult> DistrictReport(int districtId)
        {
            var repo = new DistrictRepo(db, districtId);

            var model = new DataSyncLineReportLine[]
            {
                await ReportLine<CsvOrg>(repo),
                await ReportLine<CsvCourse>(repo),
                await ReportLine<CsvAcademicSession>(repo),
                await ReportLine<CsvClass>(repo),
                await ReportLine<CsvUser>(repo),
                await ReportLine<CsvEnrollment>(repo),
                await ReportLine(repo.Lines().AsNoTracking(), "Totals"),
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> DataSyncLineEdit(int id)
        {
            var model = await db.DataSyncLines
                .Include(l => l.District)
                .Include(l => l.DataSyncHistoryDetails)
                .SingleOrDefaultAsync(l => l.DataSyncLineId == id);

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DataSyncLineEdit(DataSyncLine postedLine)
        {
            var repo = new DistrictRepo(db, postedLine.DistrictId);
            var line = await repo.Lines().SingleOrDefaultAsync(l => l.DataSyncLineId == postedLine.DataSyncLineId);

            // not currently editable
            //bool isNewData = line.RawData != postedLine.RawData;

            line.TargetId = postedLine.TargetId;
            line.IncludeInSync = postedLine.IncludeInSync;
            line.LoadStatus = postedLine.LoadStatus;
            line.SyncStatus = postedLine.SyncStatus;
            line.Touch();

            repo.PushLineHistory(line, isNewData: false);

            await repo.Committer.Invoke();

            //return RedirectToDistrict(line.DistrictId);
            return RedirectToAction(nameof(DataSyncLineEdit), line.DataSyncLineId);
        }


        [HttpGet]
        public async Task<IActionResult> DistrictEdit(int id)
        {
            var model = await db.Districts.FindAsync(id);

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DistrictEdit(District postedDistrict)
        {
            if (!ModelState.IsValid)
                return View(postedDistrict);

            var district = await db.Districts.FindAsync(postedDistrict.DistrictId);

            if (postedDistrict.DailyProcessingTime.HasValue)
            {
                TimeSpan t = postedDistrict.DailyProcessingTime.Value;
                TimeSpan min = new TimeSpan(0);
                TimeSpan max = new TimeSpan(hours: 24, minutes: 0, seconds: 0);
                if (t < min || t > max)
                {
                    ModelState.AddModelError(
                        key: nameof(postedDistrict.DailyProcessingTime),
                        errorMessage: "Invalid Daily Processing Time.  Please enter a time between 0:0:0 and 23:59:59.  Or clear to disable daily processing.");
                    return View(postedDistrict);
                }
            }

            district.BasePath = postedDistrict.BasePath;
            district.DailyProcessingTime = postedDistrict.DailyProcessingTime;
            district.EmailsEachProcess = postedDistrict.EmailsEachProcess;
            district.EmailsOnChanges = postedDistrict.EmailsOnChanges;
            district.IsApprovalRequired = postedDistrict.IsApprovalRequired;
            district.LmsApiEndpoint = postedDistrict.LmsApiEndpoint;
            district.Name = postedDistrict.Name;
            district.TargetId = postedDistrict.TargetId;

            DistrictRepo.UpdateNextProcessingTime(district);
            
            district.Touch();

            await db.SaveChangesAsync();

            return RedirectToDistrict(district.DistrictId);
        }
    }
}