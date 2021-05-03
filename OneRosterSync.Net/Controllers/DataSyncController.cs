using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OAuth;
using OneRosterSync.Net.Common;
using OneRosterSync.Net.DAL;
using OneRosterSync.Net.Data;
using OneRosterSync.Net.Extensions;
using OneRosterSync.Net.Utils;
using OneRosterSync.Net.Models;
using ReflectionIT.Mvc.Paging;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimeZoneConverter;
using CsvHelper;
using Microsoft.AspNetCore.Authorization;

namespace OneRosterSync.Net.Controllers
{
    [Authorize]
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

            // Seeding database
            DbSeeder.SeedDb(db);
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> DistrictList()
        {
            var CSTZone = TZConvert.GetTimeZoneInfo("Central Standard Time");
            var model = await db.Districts.Select(d => new DistrictViewModel
            {
                DistrictId = d.DistrictId,
                Name = d.Name,
                NumRecords = db.DataSyncLines.Count(l => l.DistrictId == d.DistrictId && l.LoadStatus != LoadStatus.Deleted),
                //TimeOfDay = d.DailyProcessingTime.ToString(),
                ProcessingStatus = d.ProcessingStatus.ToString(),
                dtModified = CSTZone == null ? d.Modified.ToLocalTime() : TimeZoneInfo.ConvertTimeFromUtc(d.Modified, CSTZone),
                Modified = CSTZone == null ? d.Modified.ToLocalTime().ToString() : TimeZoneInfo.ConvertTimeFromUtc(d.Modified, CSTZone).ToString(),
                NightlySyncEnabled = d.NightlySyncEnabled,
                IsCsvBased = d.IsCsvBased,
                IsApiValidated = d.IsApiValidated,
                LastCsvUploadedOn = d.FTPFilesLastLoadedOn.HasValue ? (CSTZone == null ? d.FTPFilesLastLoadedOn.Value.ToLocalTime().ToString() : TimeZoneInfo.ConvertTimeFromUtc(d.FTPFilesLastLoadedOn.Value, CSTZone).ToString()) : string.Empty,
                RosteringSource = d.RosteringApiSource,
            })
            .OrderByDescending(d => d.dtModified)
            .ToListAsync();

            //SampleClasslinkApiCall();
            return View(model);
        }

        public async Task<IActionResult> DistrictValidateApi(int districtId)
        {
            string message = string.Empty;
            var district = db.Districts.FirstOrDefault(w => w.DistrictId == districtId && !w.IsApiValidated);
            if (district == null)
                return NotFound("District not found");

            bool isValid = false;
            if (district.RosteringApiSource == eRosteringApiSource.Classlink)
            {
                var consumerKey = AesOperation.DecryptString(Constants.EncryptKey, district.ClassLinkConsumerKey);
                var consumerSecret = AesOperation.DecryptString(Constants.EncryptKey, district.ClassLinkConsumerSecret);
                isValid = await ClasslinkApiCallAsync(consumerKey, consumerSecret, district.ClassLinkOrgsApiUrl);
                if (isValid)
                    isValid = await ClasslinkApiCallAsync(consumerKey, consumerSecret, district.ClassLinkUsersApiUrl);
            }
            else
            {
                var oAuthToken = AesOperation.DecryptString(Constants.EncryptKey, district.CleverOAuthToken);
                isValid = await CleverApiCallAsync(oAuthToken, district.ClassLinkOrgsApiUrl);
                //if (isValid)
                //    isValid = await CleverApiCallAsync(oAuthToken, district.ClassLinkUsersApiUrl);
            }

            district.IsApiValidated = isValid;
            db.SaveChanges();
            if (isValid)
                return RedirectToAction(nameof(DistrictList)).WithSuccess($"API for district '{district.Name}' has been validated successfully.");
            else
                return RedirectToAction(nameof(DistrictList)).WithDanger($"API validation for district '{district.Name}' has failed. Please check if orgs and users endpoints are correct or contact your administrator.");
        }

        private async Task<bool> ClasslinkApiCallAsync(string key, string secret, string url)
        {
            try
            {
                // Creating a new instance with a helper method
                OAuthRequest client = OAuthRequest.ForRequestToken(key, secret);
                client.RequestUrl = url;

                // Using HTTP header authorization
                string auth = client.GetAuthorizationHeader();
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(client.RequestUrl);

                request.Headers.Add("Authorization", auth);
                HttpWebResponse httpWebResponse = (HttpWebResponse)await request.GetResponseAsync();
                using (Stream dataStream = httpWebResponse.GetResponseStream())
                {
                    // Open the stream using a StreamReader for easy access.
                    StreamReader reader = new StreamReader(dataStream);
                    string strResponse = reader.ReadToEnd();
                    //var repsonse = JsonConvert.DeserializeObject<ClassLinkUsers>(strResponse);
                }

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private async Task<bool> CleverApiCallAsync(string token, string url)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create($"{url}?count=true");

                request.Headers.Add("Authorization", $"Bearer {token}");
                HttpWebResponse httpWebResponse = (HttpWebResponse)await request.GetResponseAsync();
                using (Stream dataStream = httpWebResponse.GetResponseStream())
                {
                    // Open the stream using a StreamReader for easy access.
                    StreamReader reader = new StreamReader(dataStream);
                    string strResponse = reader.ReadToEnd();
                }

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        [HttpGet]
        public async Task<IActionResult> DistrictSyncLineErrors(int districtId, int page = 1)
        {
            var query = db.DataSyncLines.Where(w => w.DistrictId == districtId && w.SyncStatus == SyncStatus.ApplyFailed
                && w.LoadStatus != LoadStatus.Deleted
                && !string.IsNullOrEmpty(w.Error)).Select(d => new DataSyncLineViewModel
                {
                    DistrictId = d.DistrictId,
                    Created = d.Created,
                    Data = d.Data,
                    DataSyncLineId = d.DataSyncLineId,
                    EnrollmentMap = d.EnrollmentMap,
                    Error = d.Error,
                    ErrorCode = d.ErrorCode,
                    IncludeInSync = d.IncludeInSync,
                    LastSeen = d.LastSeen,
                    LoadStatus = d.LoadStatus,
                    Modified = d.Modified,
                    RawData = d.RawData,
                    SourcedId = d.SourcedId,
                    SyncStatus = d.SyncStatus,
                    Table = d.Table,
                    TargetId = d.TargetId,
                    Version = d.Version
                });

            var orderedQuery = query.OrderByDescending(l => l.Version);

            var model = await PagingList.CreateAsync(orderedQuery, 100, page);
            model.Action = nameof(DistrictSyncLineErrors);
            model.RouteValue = new RouteValueDictionary
            {
                { "districtId", districtId },
            };

            ViewBag.CurrentDistrict = db.Districts.FirstOrDefault(w => w.DistrictId == districtId);

            GC.Collect();
            return View(model);
        }

        [HttpGet]
        public async Task LoadFiles(ILogger logger)
        {
            try
            {
                var districts = db.Districts.Where(w => w.NightlySyncEnabled).ToList();
                foreach (var district in districts)
                {
                    try
                    {
                        if (district.IsCsvBased)
                        {
                            var tuple = await DownloadFile(district, logger);
                            district.ReadyForNightlySync = tuple.Item2;
                        }
                        else
                        {
                            try { await DownloadFile(district, logger); }
                            catch { }
                            district.ReadyForNightlySync = district.IsApiValidated;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Here().LogError(ex, ex.Message);
                    }
                    finally
                    {
                        db.Entry(district).State = EntityState.Modified;
                    }
                }

                db.SaveChanges();
                logger.Here().LogInformation($"FTP fetch complete.{Environment.NewLine}");
                GC.Collect();
            }
            catch (Exception ex)
            {
                Logger.Here().LogError(ex, ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> LoadFilesForDistrict(int districtId)
        {
            try
            {
                var district = db.Districts.FirstOrDefault(w => w.DistrictId == districtId);
                if (district != null)
                {
                    try
                    {
                        var tuple = await DownloadFile(district, Logger);
                        district.ReadyForNightlySync = tuple.Item2;
                        TempData["Message"] = tuple.Item1;
                        TempData["SuccessFlag"] = tuple.Item2;
                    }
                    catch (Exception ex)
                    {
                        Logger.Here().LogError(ex, ex.Message);
                        TempData["Message"] = ex.Message;
                        TempData["SuccessFlag"] = false;
                    }
                    finally
                    {
                        db.Entry(district).State = EntityState.Modified;
                    }
                }
                else
                {
                    TempData["Message"] = $"District not found with ID {districtId}";
                }

                db.SaveChanges();
                Logger.Here().LogInformation($"FTP fetch complete.{Environment.NewLine}");
                GC.Collect();
            }
            catch (Exception ex)
            {
                TempData["SuccessFlag"] = false;
                TempData["Message"] = ex.Message;
                Logger.Here().LogError(ex, ex.Message);
            }

            return RedirectToAction(nameof(DistrictList));
        }

        public async Task<IActionResult> DeleteFiles(int districtId)
        {
            var district = await db.Districts.FindAsync(districtId);
            string orgsFilePath = Path.Combine(district.BasePath, "orgs.csv");
            string usersFilePath = Path.Combine(district.BasePath, "users.csv");
            try
            {
                if (System.IO.File.Exists(orgsFilePath))
                    System.IO.File.Delete(orgsFilePath);
                if (System.IO.File.Exists(usersFilePath))
                    System.IO.File.Delete(usersFilePath);
                return RedirectToAction(nameof(DistrictEdit), new { id = districtId }).WithSuccess("CSV Files deleted successfully");
            }
            catch (Exception ex)
            {
                return RedirectToAction(nameof(DistrictEdit), new { id = districtId }).WithDanger($"Failed to delete CSV files. {ex.Message}");
            }
        }

        public async Task<Tuple<string, bool>> DownloadFile(District district, ILogger logger)
        {
            string Host = "sftp.summitk12.com", Message = string.Empty;
            bool SuccessFlag = false;

            if (string.IsNullOrEmpty(district.FTPUsername) || string.IsNullOrEmpty(district.FTPPassword) || string.IsNullOrEmpty(district.FTPPath))
            {
                Message += $"FTP information is missing for district '{district.Name}' with district ID {district.DistrictId}.{Environment.NewLine}";
            }
            else
            {
                try
                {
                    string BaseFolder = district.BasePath.Split('/')[0]; //"CSVFiles";
                    if (!Directory.Exists(Path.GetFullPath(BaseFolder)))
                    {
                        CreateCSVDirectory(Path.GetFullPath(BaseFolder), false);
                    }

                    string Username = district.FTPUsername, Password = district.FTPPassword,
                        FTPFilePath = district.FTPPath, SubFolder = "",//district.DistrictId.ToString(),
                        FullPath = Path.Combine(district.BasePath, SubFolder);

                    var connectionInfo = new PasswordConnectionInfo(Host, Username, Password);
                    using (var sftp = new SftpClient(connectionInfo))
                    {
                        sftp.Connect();
                        string[][] csvFiles;
                        bool isZipFile = false;
                        if (FTPFilePath.ToLower().EndsWith(".zip"))
                        {
                            if (sftp.Exists(FTPFilePath))
                            {
                                isZipFile = true;
                                csvFiles = new string[][] { new string[1] { FTPFilePath } };
                            }
                            else
                            {
                                logger.Here().LogInformation($"FTP file path '{FTPFilePath}' is incorrect for district '{district.Name}' with district ID {district.DistrictId}. Looking for csv files in this folder.{Environment.NewLine}");
                                FTPFilePath = FTPFilePath.Substring(0, FTPFilePath.LastIndexOf("/") + 1);
                                csvFiles = new string[][] { new string[4] { "orgs.csv", "Orgs.csv", "org.csv", "Org.csv" },
                                    new string[4] { "users.csv", "Users.csv", "user.csv", "User.csv" } }; //"courses.csv", "academicSessions.csv", "classes.csv", "enrollments.csv"
                            }
                        }
                        else
                        {
                            if (!FTPFilePath.EndsWith("/"))
                                FTPFilePath = FTPFilePath + "/";

                            csvFiles = new string[][] { new string[4] { "orgs.csv", "Orgs.csv", "org.csv", "Org.csv" },
                                new string[4] { "users.csv", "Users.csv", "user.csv", "User.csv" } }; //"courses.csv", "academicSessions.csv", "classes.csv", "enrollments.csv"
                        }

                        foreach (var csvFile in csvFiles)
                        {
                            bool existsFlag = false;
                            foreach (var file in csvFile)
                            {
                                string localFile = isZipFile ? "csv_files.zip" : csvFile[0],
                                    localFilePath = Path.GetFullPath($@"{Path.Combine(FullPath, localFile)}"),
                                    ftpFile = isZipFile ? FTPFilePath : $"{FTPFilePath}{file}";

                                if (sftp.Exists(ftpFile))
                                {
                                    var dtFtpFile = sftp.GetLastWriteTime(ftpFile);
                                    // Check if zip file exists and is updated
                                    if (isZipFile && district.FTPFilesLastLoadedOn != null && dtFtpFile.CompareTo(district.FTPFilesLastLoadedOn.Value) == 0 && System.IO.File.Exists(Path.GetFullPath($@"{localFilePath}")))
                                    {
                                        if (isZipFile)
                                        {
                                            sftp.Disconnect();
                                            district.ReadyForNightlySync = false;
                                        }
                                        Message += $"FTP file '{ftpFile}' is not changed for district '{district.Name}' with district ID {district.DistrictId}.{Environment.NewLine}";
                                    }
                                    // Check if csv files exist and are updated
                                    else if (!isZipFile && district.FTPFilesLastLoadedOn != null && dtFtpFile.CompareTo(district.FTPFilesLastLoadedOn.Value) <= 0 && System.IO.File.Exists(Path.GetFullPath($@"{localFilePath}")))
                                    {
                                        Message += $"FTP file '{ftpFile}' is not changed for district '{district.Name}' with district ID {district.DistrictId}.{Environment.NewLine}";
                                    }
                                    // else download then=m
                                    else
                                    {
                                        MemoryStream outputSteam = new MemoryStream();
                                        sftp.DownloadFile($"{ftpFile}", outputSteam);

                                        CreateCSVDirectory(Path.GetFullPath($@"{FullPath}"), Directory.Exists(Path.GetFullPath($@"{FullPath}")), isZipFile);

                                        using (var fileStream = new FileStream(localFilePath, FileMode.Create))
                                        {
                                            outputSteam.Seek(0, SeekOrigin.Begin);
                                            await outputSteam.CopyToAsync(fileStream);
                                        }

                                        if (isZipFile)
                                        {
                                            sftp.Disconnect();
                                            ZipFile.ExtractToDirectory(Path.GetFullPath($@"{Path.Combine(FullPath, "csv_files.zip")}"), Path.GetFullPath($@"{FullPath}"));
                                            // Rename files from User.csv and Orgs.csv to users.csv and orgs.csv
                                            try
                                            {
                                                string usersPath = Path.GetFullPath($@"{Path.Combine(FullPath, "Users.csv")}"),
                                                    orgsPath = Path.GetFullPath($@"{Path.Combine(FullPath, "Orgs.csv")}");

                                                if (System.IO.File.Exists(usersPath))
                                                    System.IO.File.Move(usersPath, Path.GetFullPath($@"{Path.Combine(FullPath, "users.csv")}"));
                                                if (System.IO.File.Exists(orgsPath))
                                                    System.IO.File.Move(orgsPath, Path.GetFullPath($@"{Path.Combine(FullPath, "orgs.csv")}"));
                                            }
                                            catch { } // Just renaming the files so that files can load without handling case
                                        }

                                        district.LastSyncedOn = DateTime.Now;
                                        if (district.FTPFilesLastLoadedOn == null || dtFtpFile.CompareTo(district.FTPFilesLastLoadedOn.Value) > 0)
                                            district.FTPFilesLastLoadedOn = dtFtpFile;
                                        district.ReadyForNightlySync = true;
                                        SuccessFlag = true;

                                        if (isZipFile)
                                            Message += $"FTP fetch successful for district '{district.Name}' with district ID {district.DistrictId}.{Environment.NewLine}";
                                        else
                                            Message += $"FTP file '{ftpFile}' fetch for district '{district.Name}' successful.{Environment.NewLine}";
                                    }
                                    existsFlag = true;
                                    break;
                                }
                            }
                            if (!existsFlag)
                            {
                                string filePath = isZipFile ? FTPFilePath : $"{FTPFilePath}{csvFile[0]}";
                                Message += $"FTP file path '{filePath}' is incorrect for district '{district.Name}' with district ID {district.DistrictId}.{Environment.NewLine}";
                            }
                        }

                        logger.Here().LogInformation($"{Message}.{ Environment.NewLine}");
                    }
                }
                catch (Exception ex)
                {
                    Message += $"FTP fetch failed for district '{district.Name}' with district ID {district.DistrictId}.{Environment.NewLine}";
                    throw new Exception(Message, ex);
                }
            }

            return new Tuple<string, bool>(Message, SuccessFlag);
        }

        public void CreateCSVDirectory(string FolderName, bool Exists, bool isZipFile = false)
        {
            if (Exists)
            {
                if (isZipFile)
                {
                    Directory.Delete(FolderName, true);
                    Thread.Sleep(1500);
                    CreateCSVDirectory(FolderName, false, isZipFile);
                }
            }
            else
            {
                Directory.CreateDirectory(FolderName);
            }
        }

        [HttpGet]
        public IActionResult DistrictCreate()
        {
            // create default values
            var district = new District
            {
                //BasePath = @"CSVFiles",
                //LmsApiBaseUrl = @"https://localhost:44312/api/mockapi/",
                //LmsOrgEndPoint = @"org",
                //LmsCourseEndPoint = @"course",
                //LmsClassEndPoint = @"class",
                //LmsUserEndPoint = @"user",
                //LmsEnrollmentEndPoint = @"enrollment",
                //LmsAcademicSessionEndPoint = @"academicSession",
                //SyncAcademicSessions = true,
                //SyncClasses = true,
                //SyncCourses = true,
                //SyncEnrollment = true,
                //SyncOrgs = true,
                //SyncUsers = true
                BasePath = @"CSVFiles",
                LmsApiBaseUrl = @"https://lms.summitk12.com/webservice/rest/server.php",
                LmsOrgEndPoint = @"org",
                LmsCourseEndPoint = @"course",
                LmsClassEndPoint = @"class",
                LmsUserEndPoint = @"?wstoken=d9b4c39098bc096c72a99fc447d41cb2&wsfunction=local_oneroster_user&moodlewsrestformat=json",
                LmsEnrollmentEndPoint = @"?wstoken=d9b4c39098bc096c72a99fc447d41cb2&wsfunction=local_oneroster_enrollment&moodlewsrestformat=json",
                LmsAcademicSessionEndPoint = @"academicSession",
                ClassLinkUsersApiUrl = "[base_url]/ims/oneroster/v1p1/users",
                ClassLinkOrgsApiUrl = "[base_url]/ims/oneroster/v1p1/orgs",
                SyncAcademicSessions = true,
                SyncClasses = true,
                SyncCourses = true,
                SyncEnrollment = true,
                SyncOrgs = false,
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
            clonedDistrict.LastSyncedOn = null;
            clonedDistrict.ReadyForNightlySync = false;
            clonedDistrict.FTPFilesLastLoadedOn = null;

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

            if (!district.IsCsvBased)
            {
                if (!string.IsNullOrEmpty(district.ClassLinkConsumerKey))
                    district.ClassLinkConsumerKey = AesOperation.EncryptString(Constants.EncryptKey, district.ClassLinkConsumerKey);
                else
                    district.ClassLinkConsumerKey = null;

                if (!string.IsNullOrEmpty(district.ClassLinkConsumerKey))
                    district.ClassLinkConsumerSecret = AesOperation.EncryptString(Constants.EncryptKey, district.ClassLinkConsumerSecret);
                else
                    district.ClassLinkConsumerSecret = null;

                if (!string.IsNullOrEmpty(district.CleverOAuthToken))
                    district.CleverOAuthToken = AesOperation.EncryptString(Constants.EncryptKey, district.CleverOAuthToken);
                else
                    district.CleverOAuthToken = null;
            }
            else
            {
                district.ClassLinkConsumerKey = null;
                district.ClassLinkConsumerSecret = null;
                district.CleverOAuthToken = null;
            }

            db.Add(district);
            await db.SaveChangesAsync();

            // Set the CSV Folder path
            district.BasePath = $"CSVFiles/{district.DistrictId}";
            await db.SaveChangesAsync();

            //return RedirectToDistrict(district.DistrictId).WithSuccess($"Successfully created District {district.Name}");
            return RedirectToAction(nameof(DistrictList), district.DistrictId).WithSuccess($"Successfully created District {district.Name}");
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

            // TODO: Add config to determine how to pick up courses.

            // Direct Mapping: Get all courses that belong to this school
            //var model = repo.Lines<CsvCourse>().Where(c =>
            //	 JsonConvert.DeserializeObject<CsvCourse>(c.RawData).orgSourcedId == orgSourceId).ToList();

            // Indirect Mapping: Get classes that belong to this School,
            // then get the courses for them that match the sourceId.
            // then apply unique on courseSourcedId

            // TODO: Optimize the following
            var courseSourcedIds = repo.Lines<CsvClass>()
                .Where(c => JsonConvert.DeserializeObject<CsvClass>(c.RawData).schoolSourcedId == orgSourceId)
                .Select(c => JsonConvert.DeserializeObject<CsvClass>(c.RawData).courseSourcedId);

            var courses = repo.Lines<CsvCourse>()
                .Where(cr => courseSourcedIds.Contains(cr.SourcedId))
                .OrderBy(cr => cr.SourcedId)
                .Distinct();

            return View(courses);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectCourses(int districtId, string orgSourceId, IEnumerable<string> SelectedCourses)
        {
            var repo = new DistrictRepo(db, districtId);
            var model = await repo.Lines<CsvCourse>()
                //.Where(c => JsonConvert.DeserializeObject<CsvCourse>(c.RawData).orgSourcedId == orgSourceId.ToString())
                .Where(c => SelectedCourses.Contains(c.SourcedId))
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

            return RedirectToAction(nameof(SelectCourses), new { districtId, orgSourceId }).WithSuccess("Courses saved successfully");
        }

        [HttpGet]
        public async Task<IActionResult> SelectOrgs(int districtId)
        {
            var repo = new DistrictRepo(db, districtId);
            var model = await repo.Lines<CsvOrg>().Where(w => w.LoadStatus != LoadStatus.Deleted).ToListAsync();
            ViewBag.districtId = districtId;
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectOrgs(int districtId, IEnumerable<string> SelectedOrgs)
        {
            var repo = new DistrictRepo(db, districtId);
            var model = await repo.Lines<CsvOrg>().Where(w => w.LoadStatus != LoadStatus.Deleted).ToListAsync();

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


        [HttpGet]
        public async Task<IActionResult> SelectGrades(int districtId)
        {
            var repo = new DistrictRepo(db, districtId);
            var grades = await repo.DistrictFilters.Where(w => w.FilterType == FilterType.Grades)
                .OrderBy(o => o.FilterValue)
                .ToListAsync();
            ViewBag.districtId = districtId;
            return View(grades);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectGrades(int districtId, IEnumerable<string> SelectedGrades)
        {
            var repo = new DistrictRepo(db, districtId);
            SelectedGrades = SelectedGrades.Select(s => s ?? string.Empty); // replace null with empty
            await repo.UpdateFiltersAsync(FilterType.Grades, SelectedGrades);
            await repo.Committer.Invoke();
            return RedirectToAction(nameof(SelectGrades), new { districtId }).WithSuccess("Grades filter saved successfully");
        }

        private async Task<IActionResult> Process(int districtId, ProcessingAction processingAction)
        {
            District district = await db.Districts.FindAsync(districtId);
            district.StopCurrentAction = false;
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

            int NewEnrollments = await lines.Where(w => w.ErrorCode == "129").CountAsync();
            int Transfers = await lines.Where(w => w.ErrorCode == "128").CountAsync();

            var result = new DataSyncLineReportLine
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

                NewEnrollments = NewEnrollments,
                Transferred = Transfers,
                TotalRecords = await lines.CountAsync(),
            };

            return result;
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
        public async Task<IActionResult> EnrollmentSyncDetails(int districtId, string filter, int page = 1, bool AppliedFlag = false,
                bool Transfers = false, bool NewEnrollments = false, bool DownloadExcel = false, string sortExpression = "Name")
        {
            var repo = new DistrictRepo(db, districtId);
            if (repo.District == null)
                return NotFound($"District {districtId} not found");

            ViewBag.districtId = districtId;
            ViewBag.AppliedFlag = AppliedFlag;
            ViewBag.Transfers = Transfers;
            ViewBag.NewEnrollments = NewEnrollments;
            ViewBag.CurrentDistrict = repo.District;

            var SyncHistory = await repo.DataSyncHistories.Where(w => w.DistrictId == districtId)
                    .OrderByDescending(o => o.DataSyncHistoryId)
                    .FirstOrDefaultAsync();

            ViewBag.SyncLoadError = SyncHistory?.LoadError;
            ViewBag.SyncAnalyzeError = SyncHistory?.AnalyzeError;
            ViewBag.SyncApplyError = SyncHistory?.ApplyError;

            var allOrgs = repo.Lines<CsvOrg>().AsNoTracking().Where(w => w.LoadStatus != LoadStatus.Deleted).ToList();
            var orgs = allOrgs.Where(w => w.IncludeInSync).ToList();
            var orgsIds = orgs.Select(s => s.SourcedId).ToList();

            ViewBag.OrgsCountLabel = $"{orgs.Count}/{allOrgs.Count}";
            var query = repo.Lines<CsvUser>().AsNoTracking()
                .Where(w => w.DistrictId == districtId && w.LoadStatus != LoadStatus.Deleted
                        && orgsIds.Any(a => w.RawData.Contains($"\"orgSourcedIds\":\"{a}\"")));

            if (Transfers)
            {
                query = query.Where(w => w.ErrorCode == "128");
            }
            else if (NewEnrollments)
            {
                query = query.Where(w => w.ErrorCode == "129");
            }
            else
            {
                query = query.Where(w => ((!AppliedFlag && w.SyncStatus == SyncStatus.ReadyToApply)
                              || (AppliedFlag && w.SyncStatus == SyncStatus.ApplyFailed)
                              || (AppliedFlag && w.SyncStatus == SyncStatus.Applied)));
            }

            string nameOfUsername = nameof(CsvUser.username),
                nameOfEmail = nameof(CsvUser.email),
                nameOfPassword = nameof(CsvUser.password),
                nameOfSourcedId = nameof(CsvUser.sourcedId),
                nameOfIdentifier = nameof(CsvUser.identifier),
                EmailFieldNameForUserAPI = repo.District.EmailFieldNameForUserAPI,
                PasswordFieldNameForUserAPI = repo.District.PasswordFieldNameForUserAPI;

            var gradeFilters = repo.DistrictFilters.Where(w => w.FilterType == FilterType.Grades && w.ShouldBeApplied).ToList();
            var filterQuery = query.Select(s => new
            {
                line = s,
                user = JsonConvert.DeserializeObject<CsvUser>(s.RawData),
            })
            .Select(s => new
            {
                s.line,
                s.user,
                grades = s.user.grades == null ? new string[0] : s.user.grades.Split(",", StringSplitOptions.None),
                org = JsonConvert.DeserializeObject<CsvOrg>
                         (orgs.SingleOrDefault(l => l.SourcedId == s.user.orgSourcedIds).RawData)
            });

            if (gradeFilters.Count > 0)
            {
                filterQuery = filterQuery.Where(w => gradeFilters.Count > 0 && gradeFilters.Any(a => w.grades.Contains($"{a.FilterValue}")));
            }

            if (!string.IsNullOrWhiteSpace(filter))
            {
                filterQuery = filterQuery.Where(w => w.line.RawData.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || w.org.name.Contains(filter, StringComparison.OrdinalIgnoreCase));
            }

            ViewBag.TotalRecordsCount = await filterQuery.CountAsync();
            var selectQuery = filterQuery.Select(s => new EnrollmentSyncLineViewModel
            {
                Created = s.line.Created,
                DataSyncLineId = s.line.DataSyncLineId,
                DistrictId = s.line.DistrictId,
                Email = EmailFieldNameForUserAPI.Equals(nameOfUsername) ? s.user.username :
                    (EmailFieldNameForUserAPI.Equals(nameOfEmail) ? s.user.email : s.user.email),
                Password = PasswordFieldNameForUserAPI.Equals(nameOfPassword) ? s.user.password :
                    (PasswordFieldNameForUserAPI.Equals(nameOfSourcedId) ? s.user.sourcedId :
                    (PasswordFieldNameForUserAPI.Equals(nameOfIdentifier) ? s.user.identifier :
                    (PasswordFieldNameForUserAPI.Equals(nameOfUsername) ? s.user.username : s.user.username))),
                Name = $"{s.user.givenName} {s.user.familyName}",
                Version = s.line.Version,
                SyncStatus = s.line.SyncStatus,
                Error = s.line.Error,
                IncludeInSync = s.line.IncludeInSync,
                Table = s.line.Table,
                SchoolName = s.org.name,
            });


            if (DownloadExcel)
            {
                string fileName = $"sync_details_{districtId}.csv";
                var stream = await GenerateExcelFile(selectQuery, districtId, fileName);
                return File(stream, "text/csv", fileName);
            }

            var orderedQuery = selectQuery.Take(500).OrderByDescending(l => l.SchoolName);
            var model = await PagingList.CreateAsync(orderedQuery, 50, page, sortExpression, "Name");
            model.Action = nameof(EnrollmentSyncDetails);
            model.RouteValue = new RouteValueDictionary
            {
                { "districtId", districtId },
                { "filter", filter},
                { "AppliedFlag", AppliedFlag },
                { "Transfers", Transfers },
                { "NewEnrollments",NewEnrollments }
            };

            GC.Collect();
            return View(model);
        }

        private async Task<FileStream> GenerateExcelFile(IQueryable<EnrollmentSyncLineViewModel> orderedQuery, int districtId, string fileName)
        {
            string dir = Path.Combine(_hostingEnvironment.ContentRootPath, "DownloadFiles");

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var filePath = Path.Combine(dir, fileName);
            using (StreamWriter sw = new StreamWriter(filePath, false, new UTF8Encoding(true)))
            {
                using (CsvWriter cw = new CsvWriter(sw))
                {
                    cw.WriteHeader<EnrollmentSyncLineViewModel>();
                    cw.NextRecord();
                    foreach (var detail in await orderedQuery.ToListAsync())
                    {
                        cw.WriteRecord(detail);
                        cw.NextRecord();
                    }
                }
            }
            return System.IO.File.Open(filePath, FileMode.Open);
        }

        [HttpPost]
        public async Task<IActionResult> StopCurrentAction(int districtId, string ViewName = "EnrollmentSyncDetails")
        {
            District district = await db.Districts.FindAsync(districtId);
            district.StopCurrentAction = true;
            await db.SaveChangesAsync();
            return RedirectToAction(ViewName, new { districtId });
        }

        [HttpPost]
        public async Task<IActionResult> LoadEnrollmentSyncDetails(int districtId)
        {
            await Process(districtId, ProcessingAction.Load);
            return RedirectToAction(nameof(EnrollmentSyncDetails), new { districtId }).WithSuccess("Load has been queued."); ;
        }

        [HttpPost]
        public async Task<IActionResult> AnalyzeEnrollmentSyncDetails(int districtId)
        {
            await Process(districtId, ProcessingAction.Analyze);
            return RedirectToAction(nameof(EnrollmentSyncDetails), new { districtId }).WithSuccess("Analyze has been queued."); ;
        }

        [HttpPost]
        public async Task<IActionResult> ApplyEnrollmentSyncDetails(int districtId)
        {
            await Process(districtId, ProcessingAction.Apply);
            return RedirectToAction(nameof(EnrollmentSyncDetails), new { districtId }).WithSuccess("Apply has been queued.");
        }

        [HttpPost]
        public async Task<JsonResult> ToggleIncludeInSyncFlag(bool flag, int LineId)
        {
            var responseFlag = true;
            try
            {
                var line = db.DataSyncLines.FirstOrDefault(w => w.DataSyncLineId == LineId);
                line.IncludeInSync = flag;
                await DataSyncLineEdit(line);

            }
            catch (Exception ex)
            {
                responseFlag = false;
            }

            return Json(responseFlag);
        }

        public DataSyncLineViewModel GetDataSyncLineViewModel<T>(DataSyncLine Data) where T : CsvBaseObject
        {
            var ViewModel = new DataSyncLineViewModel
            {
                Created = Data.Created,
                Data = Data.Data,
                DataSyncLineId = Data.DataSyncLineId,
                DeserializedRawData = JsonConvert.DeserializeObject<T>(Data.RawData),
                EnrollmentMap = Data.EnrollmentMap,
                DistrictId = Data.DistrictId,
                Error = Data.Error,
                ErrorCode = Data.ErrorCode,
                IncludeInSync = Data.IncludeInSync,
                LastSeen = Data.LastSeen,
                LoadStatus = Data.LoadStatus,
                Modified = Data.Modified,
                RawData = Data.RawData,
                SourcedId = Data.SourcedId,
                SyncStatus = Data.SyncStatus,
                Table = Data.Table,
                TargetId = Data.TargetId,
                Version = Data.Version
            };

            if (Data.Table == "CsvEnrollment" && !string.IsNullOrEmpty(Data.EnrollmentMap))
            {
                ViewModel.DeserializedEnrollmentMap = JsonConvert.DeserializeObject<EnrollmentMap>(Data.EnrollmentMap);
            }

            return ViewModel;
        }

        [HttpGet]
        public IActionResult DistrictEntityMapping(int districtId)
        {
            var repo = new DistrictRepo(db, districtId);
            return View(repo.District);
        }

        [HttpPost]
        public async Task<IActionResult> UploadMappingFiles(IFormFile mappingFile, int districtId, string tableName)
        {
            var path = Path.GetTempFileName();
            var repo = new DistrictRepo(db, districtId);
            var mapCount = 0;
            var lineCount = 0;

            if (string.IsNullOrWhiteSpace(tableName) || mappingFile == null)
            {
                return View(nameof(DistrictEntityMapping), repo.District)
                    .WithDanger($"Please select Table Name and select a file first.");
            }

            using (var stream = new FileStream(path, FileMode.Create))
            {
                await mappingFile.CopyToAsync(stream);
            }

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

                            mapCount++;

                            var line = repo.Lines()
                                .Where(l => l.SourcedId == sourcedId && l.Table == tableName)
                                .FirstOrDefault();

                            if (line != null)
                            {
                                line.TargetId = targetId;
                                line.Touch();

                                lineCount++;
                            }

                        }
                        catch (Exception ex)
                        {
                            Logger.Here().LogError(ex, ex.Message);
                            return View(nameof(DistrictEntityMapping), repo.District)
                                .WithDanger($"Failed to apply Mappings. {ex.Message}");
                        }
                    }

                    await repo.Committer.Invoke();
                }
            }

            return View(nameof(DistrictEntityMapping), repo.District)
                .WithSuccess($"Successfully Processed Mapping for {tableName}. Mapping applied to {lineCount} records out of {mapCount} mapping records.");
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
            if (!string.IsNullOrEmpty(model.ClassLinkConsumerKey))
                model.ClassLinkConsumerKey = AesOperation.DecryptString(Constants.EncryptKey, model.ClassLinkConsumerKey);
            if (!string.IsNullOrEmpty(model.ClassLinkConsumerSecret))
                model.ClassLinkConsumerSecret = AesOperation.DecryptString(Constants.EncryptKey, model.ClassLinkConsumerSecret);
            if (!string.IsNullOrEmpty(model.CleverOAuthToken))
                model.CleverOAuthToken = AesOperation.DecryptString(Constants.EncryptKey, model.CleverOAuthToken);

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

            bool deleteLines = postedDistrict.IsCsvBased != district.IsCsvBased;

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

            district.NCESDistrictID = postedDistrict.NCESDistrictID;
            district.FTPUsername = postedDistrict.FTPUsername;
            district.FTPPassword = postedDistrict.FTPPassword;
            district.FTPPath = postedDistrict.FTPPath;
            district.NightlySyncEnabled = postedDistrict.NightlySyncEnabled;
            district.EmailFieldNameForUserAPI = postedDistrict.EmailFieldNameForUserAPI;
            district.PasswordFieldNameForUserAPI = postedDistrict.PasswordFieldNameForUserAPI;

            if (!postedDistrict.IsCsvBased)
            {
                if (!string.IsNullOrEmpty(postedDistrict.ClassLinkConsumerKey))
                    district.ClassLinkConsumerKey = AesOperation.EncryptString(Constants.EncryptKey, postedDistrict.ClassLinkConsumerKey);
                else
                    district.ClassLinkConsumerKey = null;

                if (!string.IsNullOrEmpty(postedDistrict.ClassLinkConsumerKey))
                    district.ClassLinkConsumerSecret = AesOperation.EncryptString(Constants.EncryptKey, postedDistrict.ClassLinkConsumerSecret);
                else
                    district.ClassLinkConsumerSecret = null;

                if (!string.IsNullOrEmpty(postedDistrict.CleverOAuthToken))
                    district.CleverOAuthToken = AesOperation.EncryptString(Constants.EncryptKey, postedDistrict.CleverOAuthToken);
                else
                    district.CleverOAuthToken = null;

                district.IsApiValidated = false;
            }
            else
            {
                district.ClassLinkConsumerKey = null;
                district.ClassLinkConsumerSecret = null;
                district.CleverOAuthToken = null;
            }

            district.RosteringApiSource = postedDistrict.RosteringApiSource;
            district.IsCsvBased = postedDistrict.IsCsvBased;
            district.ClassLinkUsersApiUrl = postedDistrict.ClassLinkUsersApiUrl;
            district.ClassLinkOrgsApiUrl = postedDistrict.ClassLinkOrgsApiUrl;

            DistrictRepo.UpdateNextProcessingTime(district);

            district.Touch();

            await db.SaveChangesAsync();

            if (deleteLines)
            {
                district.UsersLastDateModified = null;
                var repo = new DistrictRepo(db, postedDistrict.DistrictId);
                await repo.RemoveHistory();
                await repo.DeleteLines();
                await repo.EmptyFiltersAsync();
            }

            //return RedirectToDistrict(district.DistrictId);
            return RedirectToAction(nameof(DistrictList)).WithSuccess("District updated successfully.");
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

        /// <summary>
        /// Display all matching records for the District (DataSyncLines)
        /// </summary>
        /// <param name="districtId"></param>
        /// <param name="page"></param>
        /// <param name="startDate">Start date filter</param>
        /// <param name="endDate">End date filter</param>
        [HttpGet]
        public async Task<IActionResult> DistrictCsvErrors(int districtId,
            int page = 1, string startDate = null, string endDate = null)
        {
            var repo = new DistrictRepo(db, districtId);
            if (repo.District == null)
                return NotFound($"District {districtId} not found");
            ViewData["DistrictName"] = repo.District.Name;

            var query = repo.DistrictCsvErrors.AsNoTracking();

            if (!string.IsNullOrEmpty(startDate))
            {
                var dtStart = Convert.ToDateTime(startDate);
                query = query.Where(l => l.Created.Date >= dtStart.Date);
            }

            if (!string.IsNullOrEmpty(endDate))
            {
                var dtEnd = Convert.ToDateTime(endDate);
                query = query.Where(l => l.Created.Date <= dtEnd.Date);
            }

            var orderedQuery = query.OrderByDescending(l => l.Created);

            const int perPage = 30;
            var model = await PagingList.CreateAsync(orderedQuery, perPage, page);
            model.Action = nameof(DistrictCsvErrors);
            model.RouteValue = new RouteValueDictionary
            {
                { "districtId", districtId },
                { "startDate", startDate},
                { "endDate", endDate},
            };

            //// kludge to remove empty values
            foreach (var kvp in model.RouteValue.Where(kvp => kvp.Value == null).ToList())
                model.RouteValue.Remove(kvp.Key);

            return View(model);
        }

        [HttpGet]
        public IActionResult ClearDistrictCsvErrors(int districtId)
        {
            return RedirectToAction(nameof(DistrictCsvErrors), new { districtId });
        }
    }
}