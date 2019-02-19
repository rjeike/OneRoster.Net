using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Models;
using OneRosterSync.Net.DAL;
using OneRosterSync.Net.Extensions;
using ReflectionIT.Mvc.Paging;

namespace OneRosterSync.Net.Controllers
{
    public class DataSyncController : Controller
    {
        private readonly ApplicationDbContext db;
        private readonly ILogger Logger;

	    private readonly IHostingEnvironment _hostingEnvironment;


		public DataSyncController(ApplicationDbContext db, ILogger<DataSyncController> logger, IHostingEnvironment hostingEnvironment)
        {
            this.db = db;
            Logger = logger;
	        _hostingEnvironment = hostingEnvironment;
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
	        // create default values
			var district = new District
	        {
		        BasePath = @"CSVFiles",
		        LmsApiBaseUrl = @"https://localhost:44312/api/mockapi/",
		        LmsOrgEndPoint = @"org",
		        LmsCourseEndPoint = @"course",
		        LmsClassEndPoint = @"class",
		        LmsUserEndPoint = @"user",
		        LmsEnrollmentEndPoint = @"enrollment",
		        LmsAcademicSessionEndPoint = @"academicSession",
				SyncAcademicSessions = true,
				SyncClasses = true,
				SyncCourses = true,
				SyncEnrollment = true,
				SyncOrgs = true,
				SyncUsers = true
	        };

	        return View(district);
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

            return RedirectToAction(nameof(DistrictList)).WithSuccess("District deleted successfully");
        }

		/// <summary>
		/// This is fore testing and debugging purposes only
		/// </summary>
		/// <param name="districtId"></param>
		/// <returns></returns>
		[HttpPost, ValidateAntiForgeryToken]
		public IActionResult DistrictClone(int districtId)
		{
			var repo = new DistrictRepo(db, districtId);

			var clonedDistrict = repo.District.ShallowCopy();
			clonedDistrict.DistrictId = 0;
			clonedDistrict.Name = $"Clone of {clonedDistrict.Name}";
			clonedDistrict.ProcessingStatus = ProcessingStatus.None;
			clonedDistrict.ProcessingAction = ProcessingAction.None;
			clonedDistrict.Created = DateTime.Now;
			clonedDistrict.Modified = DateTime.Now;

			db.Add(clonedDistrict);
			db.SaveChanges();

			clonedDistrict.BasePath = $"CSVFiles/{clonedDistrict.DistrictId}";

			db.SaveChanges();

			return RedirectToAction(nameof(DistrictList)).WithSuccess($"District cloned as {clonedDistrict.Name}");
		}

		private IActionResult RedirectToDistrict(int districtId) =>
            RedirectToAction(nameof(DistrictDashboard), new { districtId });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DistrictCreate(District district)
        {
	        if (!ModelState.IsValid)
		        return View(district);

			db.Add(district);
            await db.SaveChangesAsync();

			// Set the CSV Folder path
	        district.BasePath = $"CSVFiles/{district.DistrictId}";
	        await db.SaveChangesAsync();

			return RedirectToDistrict(district.DistrictId).WithSuccess($"Successfully created District {district.Name}");
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
		public IActionResult SelectCourses(int districtId, string orgSourceId)
		{
			var repo = new DistrictRepo(db, districtId);
			ViewBag.districtId = districtId;
			ViewBag.orgSourceId = orgSourceId;

			// Get all courses that belong to this school
			var model = repo.Lines<CsvCourse>().Where(c =>
				 JsonConvert.DeserializeObject<CsvCourse>(c.RawData).orgSourcedId == orgSourceId).ToList();
			return View(model);
		}

		[HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectCourses(int districtId, string orgSourceId, IEnumerable<string> SelectedCourses)
        {
            var repo = new DistrictRepo(db, districtId);
            var model = await repo.Lines<CsvCourse>()
	            .Where(c => JsonConvert.DeserializeObject<CsvCourse>(c.RawData).orgSourcedId == orgSourceId.ToString())
	            .ToListAsync();

	        ViewBag.districtId = districtId;

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

	        return RedirectToAction(nameof(SelectCourses), new {districtId, orgSourceId}).WithSuccess("Courses saved successfully");
        }

	    [HttpGet]
	    public async Task<IActionResult> SelectOrgs(int districtId)
	    {
		    var repo = new DistrictRepo(db, districtId);
		    var model = await repo.Lines<CsvOrg>().ToListAsync();
		    ViewBag.districtId = districtId;
		    return View(model);
	    }

	    [HttpPost, ValidateAntiForgeryToken]
	    public async Task<IActionResult> SelectOrgs(int districtId, IEnumerable<string> SelectedOrgs)
	    {
		    var repo = new DistrictRepo(db, districtId);
		    var model = await repo.Lines<CsvOrg>().ToListAsync();

			foreach (var org in model)
		    {
			    bool include = SelectedOrgs.Contains(org.SourcedId);
			    if (org.IncludeInSync == include)
				    continue;
			    org.IncludeInSync = include;
			    org.Touch();
			    repo.PushLineHistory(org, isNewData: false);
		    }

		    await repo.Committer.Invoke();

		    return RedirectToAction(nameof(SelectOrgs), new { districtId }).WithSuccess("Orgs saved successfully");
	    }

		private async Task<IActionResult> Process(int districtId, ProcessingAction processingAction)
        {
            District district = await db.Districts.FindAsync(districtId);

            district.ProcessingAction = processingAction;
            await db.SaveChangesAsync();

            return RedirectToDistrict(districtId).WithSuccess($"{processingAction} has been queued");
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
	        ViewBag.districtId = districtId;

	        var org = await ReportLine<CsvOrg>(repo);
	        org.SyncEnabled = repo.District.SyncOrgs;

	        var course = await ReportLine<CsvCourse>(repo);
	        course.SyncEnabled = repo.District.SyncCourses;

			var academicSession = await ReportLine<CsvAcademicSession>(repo);
	        academicSession.SyncEnabled = repo.District.SyncAcademicSessions;

			var _class = await ReportLine<CsvClass>(repo);
	        _class.SyncEnabled = repo.District.SyncClasses;

			var user = await ReportLine<CsvUser>(repo);
	        user.SyncEnabled = repo.District.SyncUsers;

			var enrollment = await ReportLine<CsvEnrollment>(repo);
	        enrollment.SyncEnabled = repo.District.SyncEnrollment;

			var total = await ReportLine(repo.Lines().AsNoTracking(), "Totals");
	        total.SyncEnabled = true;

			var model = new[]
            {
                org,
				course,
				academicSession,
				_class,
				user,
				enrollment,
				total
            };

            return View(model);
        }

		[HttpGet]
	    public async Task<IActionResult> DistrictEntityMapping(int districtId)
	    {
		    var repo = new DistrictRepo(db, districtId);
			return View(repo.District);
	    }

	    [HttpPost]
	    public async Task<IActionResult> UploadMappingFiles(List<IFormFile> files, int districtId, string tableName)
	    {
		    var path = Path.GetTempFileName();

			// TODO: Restrict to just one file upload
		    foreach (var formFile in files)
		    {
			    if (formFile.Length <= 0) continue;
			    using (var stream = new FileStream(path, FileMode.Create))
			    {
				    await formFile.CopyToAsync(stream);
			    }
		    }

		    var repo = new DistrictRepo(db, districtId);

		    using (var file = System.IO.File.OpenText(path))
		    {
			    using (var csv = new CsvHelper.CsvReader(file))
			    {
				    csv.Configuration.MissingFieldFound = null;
				    csv.Configuration.HasHeaderRecord = true;

				    csv.Read();
				    csv.ReadHeader();

				    for (int i = 0; await csv.ReadAsync(); i++)
				    {
					    dynamic record = null;
					    try
					    {
						    record = csv.GetRecord<dynamic>();
						    string sourcedId = record.sourcedId;
						    string targetId = record.targetId;

							// TODO: Pick line based on type of mapping file
						    var line = repo.Lines<CsvCourse>().Where(c => c.SourcedId == sourcedId).FirstOrDefault();

						    if (line != null)
						    {
							    line.TargetId = targetId;
								line.Touch();
							}

						}
					    catch (Exception ex)
					    {
							Logger.Here().LogError(ex, ex.Message);
					    }
				    }

				    await repo.Committer.Invoke();
			    }
		    }

		    return View(nameof(DistrictEntityMapping), repo.District).WithSuccess($"Uploaded {tableName} Mapping file.");
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

            return RedirectToAction(nameof(DataSyncLineEdit), line.DataSyncLineId).WithSuccess("Dataline updated successfully");
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
            district.LmsApiBaseUrl = postedDistrict.LmsApiBaseUrl;
            district.Name = postedDistrict.Name;
            district.TargetId = postedDistrict.TargetId;
	        district.LmsApiAuthenticatorType = postedDistrict.LmsApiAuthenticatorType;
	        district.LmsApiAuthenticationJsonData = postedDistrict.LmsApiAuthenticationJsonData;

	        district.LmsOrgEndPoint = postedDistrict.LmsOrgEndPoint;
	        district.LmsCourseEndPoint = postedDistrict.LmsCourseEndPoint;
	        district.LmsClassEndPoint = postedDistrict.LmsClassEndPoint;
	        district.LmsUserEndPoint = postedDistrict.LmsUserEndPoint;
	        district.LmsEnrollmentEndPoint = postedDistrict.LmsEnrollmentEndPoint;
	        district.LmsAcademicSessionEndPoint = postedDistrict.LmsAcademicSessionEndPoint;

	        district.SyncEnrollment = postedDistrict.SyncEnrollment;
	        district.SyncAcademicSessions = postedDistrict.SyncAcademicSessions;
	        district.SyncClasses = postedDistrict.SyncClasses;
	        district.SyncCourses = postedDistrict.SyncCourses;
	        district.SyncOrgs = postedDistrict.SyncOrgs;
	        district.SyncUsers = postedDistrict.SyncUsers;

			DistrictRepo.UpdateNextProcessingTime(district);
            
            district.Touch();

            await db.SaveChangesAsync();

            return RedirectToDistrict(district.DistrictId);
        }

	    [HttpPost]
	    public async Task<IActionResult> UploadFiles(List<IFormFile> files, int districtId)
	    {
		    var path = Path.Combine(_hostingEnvironment.ContentRootPath, "CSVFiles", districtId.ToString());
		    Directory.CreateDirectory(path);

			foreach (var formFile in files)
		    {
			    if (formFile.Length <= 0) continue;
			    using (var stream = new FileStream(Path.Combine(path, formFile.FileName), FileMode.Create))
			    {
				    await formFile.CopyToAsync(stream);
			    }
		    }

			return RedirectToDistrict(districtId).WithSuccess($"Uploaded {files.Count} files successfully");
		}
	}
}