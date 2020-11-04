using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OAuth;
using OneRosterSync.Net.Common;
using OneRosterSync.Net.DAL;
using OneRosterSync.Net.Extensions;
using OneRosterSync.Net.Models;
using OneRosterSync.Net.Utils;

namespace OneRosterSync.Net.Processing
{
    public class Loader
    {
        private readonly ILogger Logger = ApplicationLogging.Factory.CreateLogger<Loader>();
        private readonly DistrictRepo Repo;
        private readonly string BasePath;
        private readonly TextInfo textInfo;

        public string LastEntity { get; private set; }

        public Loader(DistrictRepo repo, string basePath)
        {
            Repo = repo;
            BasePath = basePath;
            textInfo = new CultureInfo("en-US", false).TextInfo;
        }

        public async Task LoadFile<T>(string filename) where T : CsvBaseObject
        {
            LastEntity = typeof(T).Name; // kludge
            DateTime now = DateTime.UtcNow;
            string filePath = Path.Combine(BasePath, filename);
            string table = typeof(T).Name;

            if (File.Exists(filePath))
            {
                using (var file = File.OpenText(filePath))
                {
                    using (var csv = new CsvHelper.CsvReader(file))
                    {
                        csv.Configuration.MissingFieldFound = null;
                        csv.Configuration.HasHeaderRecord = true;
                        csv.Configuration.PrepareHeaderForMatch = (string header) => header.ToLower();
                        if (typeof(T) == typeof(CsvOrg))
                        {
                            csv.Configuration.RegisterClassMap<CsvOrgClassMap>();
                        }
                        else if (typeof(T) == typeof(CsvUser))
                        {
                            csv.Configuration.RegisterClassMap<CsvUserClassMap>();
                        }

                        csv.Read();
                        csv.ReadHeader();
                        for (int i = 0; await csv.ReadAsync(); i++)
                        {
                            T record = null;
                            try
                            {
                                record = csv.GetRecord<T>();
                                await ProcessRecord(record, table, now);
                                //commenting because it might slow down load process, considering houston isd
                                //if (i > 2 && i % 100 == 0)
                                //{
                                //    if (Repo.GetStopFlag(Repo.DistrictId))
                                //    {
                                //        throw new ProcessingException(Logger, $"Current action is stopped by the user.");
                                //    }
                                //}
                                //no need to invoke
                                //await Repo.Committer.InvokeIfChunk(5000);
                            }
                            catch (Exception ex)
                            {
                                if (ex is ProcessingException)
                                    throw;

                                string o = record == null ? "(null)" : JsonConvert.SerializeObject(record);
                                throw new ProcessingException(Logger.Here(), $"Unhandled error processing {typeof(T).Name}: {o}", ex);
                            }
                        }

                        await Repo.Committer.InvokeIfChunk();

                        // commit any last changes
                        await Repo.Committer.InvokeIfAny();
                        GC.Collect();
                    }
                    Logger.Here().LogInformation($"Processed Csv file {filePath}");
                }
            }
            else
            {
                Repo.District.FTPFilesLastLoadedOn = null;
                await Repo.Committer.Invoke();
                Logger.Here().LogInformation($"Csv file for {LastEntity} not found {filePath}");
                throw new ProcessingException(Logger.Here(), $"Csv file for {LastEntity} not found {filePath}");
            }
        }

        public async Task LoadClassLinkData<T>() where T : CsvBaseObject
        {
            LastEntity = typeof(T).Name; // kludge
            string table = typeof(T).Name;
            DateTime now = DateTime.UtcNow;
            int responseCount = 0;
            int offset = 0, limit = 5000;

            if (Repo.District.IsApiValidated)
            {
                do
                {
                    // Creating a new instance with a helper method
                    OAuthRequest client = OAuthRequest.ForRequestToken(AesOperation.DecryptString(Constants.EncryptKey, Repo.District.ClassLinkConsumerKey),
                        AesOperation.DecryptString(Constants.EncryptKey, Repo.District.ClassLinkConsumerSecret));

                    if (typeof(T) == typeof(CsvUser))
                    {
                        client.RequestUrl = $"{Repo.District.ClassLinkUsersApiUrl}?offset={offset}&limit={limit}&sort=dateLastModified";
                        if (!string.IsNullOrEmpty(Repo.District.UsersLastDateModified))
                            client.RequestUrl += $"&filter=dateLastModified>'{Repo.District.UsersLastDateModified}'";
                    }
                    else if (typeof(T) == typeof(CsvOrg))
                    {
                        client.RequestUrl = $"{Repo.District.ClassLinkOrgsApiUrl}?offset={offset}&limit={limit}&sort=dateLastModified";
                    }
                    // Using HTTP header authorization
                    string auth = client.GetAuthorizationHeader();
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(client.RequestUrl);

                    request.Headers.Add("Authorization", auth);
                    HttpWebResponse httpWebResponse = (HttpWebResponse)await request.GetResponseAsync();
                    using (Stream dataStream = httpWebResponse.GetResponseStream())
                    {
                        // Open the stream using a StreamReader for easy access.
                        StreamReader reader = new StreamReader(dataStream);
                        string strResponse = await reader.ReadToEndAsync();

                        if (typeof(T) == typeof(CsvUser))
                        {
                            var hashSet = new HashSet<string>();
                            var classLinkUsers = JsonConvert.DeserializeObject<ClassLinkUsers>(strResponse);
                            responseCount = classLinkUsers.users.Count;
                            if (responseCount > 0 && responseCount < limit)
                                Repo.District.UsersLastDateModified = classLinkUsers.users.Select(s => s.dateLastModified).LastOrDefault();

                            foreach (var user in classLinkUsers.users)
                            {
                                var csvUser = GetCsvUserClasslink(user);
                                if (user.grades.Length > 0) foreach (var grade in user.grades) hashSet.Add(grade);
                                else
                                    hashSet.Add(string.Empty);
                                await ProcessRecord(csvUser, table, now);
                            }
                            foreach (var grade in hashSet)
                            {
                                await Repo.PushFilterAsync(FilterType.Grades, grade);
                            }

                        }
                        else if (typeof(T) == typeof(CsvOrg))
                        {
                            var classLinkOrgs = JsonConvert.DeserializeObject<ClassLinkOrgs>(strResponse);
                            responseCount = classLinkOrgs.orgs.Count;
                            foreach (var org in classLinkOrgs.orgs)
                            {
                                await ProcessRecord(org as CsvOrg, table, now);
                            }
                        }
                    }

                    offset += limit;
                    await Repo.Committer.InvokeIfChunk();
                    await Repo.Committer.InvokeIfAny(); // commit any last changes
                } while (responseCount == limit);
                GC.Collect();
            }
            else
            {
                await Repo.Committer.Invoke();
                Logger.Here().LogInformation($"Classlink API for district '{Repo.District}' is not validated.");
                throw new ProcessingException(Logger.Here(), $"Classlink API is not validated.");
            }
        }

        public async Task LoadCleverData<T>() where T : CsvBaseObject
        {
            LastEntity = typeof(T).Name; // kludge
            string table = typeof(T).Name;
            DateTime now = DateTime.UtcNow;
            int responseCount = 0;
            int limit = 5000;

            if (Repo.District.IsApiValidated)
            {
                // Creating a new instance with a helper method
                string url = string.Empty, baseUrl = string.Empty;

                if (typeof(T) == typeof(CsvUser))
                {
                    var orgs = await Repo.Lines<CsvOrg>().Where(w => w.IncludeInSync).ToListAsync();
                    if (orgs.Count > 1)
                        throw new ProcessingException(Logger.Here(), $"Cannot process more than one school for Clever Rostering.");
                    else if (orgs.Count < 1)
                        throw new ProcessingException(Logger.Here(), $"At least one school must be selected for Clever Rostering.");
                    else
                    {
                        url = $"{Repo.District.ClassLinkUsersApiUrl.Replace("[school_id]", orgs.FirstOrDefault().SourcedId)}?limit={limit}";
                    }
                }
                else if (typeof(T) == typeof(CsvOrg))
                    url = $"{Repo.District.ClassLinkOrgsApiUrl}?limit={limit}";

                var uri = new Uri(url);
                baseUrl = $"{uri.Scheme}://{uri.Host}";

                do
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                    request.Headers.Add("Authorization", $"Bearer {AesOperation.DecryptString(Constants.EncryptKey, Repo.District.CleverOAuthToken)}");
                    HttpWebResponse httpWebResponse = (HttpWebResponse)await request.GetResponseAsync();
                    using (Stream dataStream = httpWebResponse.GetResponseStream())
                    {
                        // Open the stream using a StreamReader for easy access.
                        StreamReader reader = new StreamReader(dataStream);
                        string strResponse = await reader.ReadToEndAsync();

                        if (typeof(T) == typeof(CsvUser))
                        {
                            var hashSet = new HashSet<string>();
                            var cleverUsers = JsonConvert.DeserializeObject<CleverUsers>(strResponse);
                            url = $"{baseUrl}{cleverUsers.links.Where(w => w.rel == "next").Select(s => s.uri).FirstOrDefault()}";
                            responseCount = cleverUsers.data.Count;

                            foreach (var user in cleverUsers.data)
                            {
                                var csvUser = GetCsvUserClever(user);
                                if (user.data.grade != null) hashSet.Add(user.data.grade);
                                await ProcessRecord(csvUser, table, now);
                            }
                            foreach (var grade in hashSet)
                            {
                                await Repo.PushFilterAsync(FilterType.Grades, grade);
                            }

                        }
                        else if (typeof(T) == typeof(CsvOrg))
                        {
                            var cleverOrgs = JsonConvert.DeserializeObject<CleverOrgs>(strResponse);
                            url = $"{baseUrl}{cleverOrgs.links.Where(w => w.rel == "next").Select(s => s.uri).FirstOrDefault()}";
                            responseCount = cleverOrgs.data.Count;
                            foreach (var org in cleverOrgs.data)
                            {
                                var csvOrg = GetCsvOrgClever(org);
                                await ProcessRecord(csvOrg, table, now);
                            }
                        }
                    }

                    await Repo.Committer.InvokeIfChunk();
                    await Repo.Committer.InvokeIfAny(); // commit any last changes
                } while (responseCount == limit);
                GC.Collect();
            }
            else
            {
                await Repo.Committer.Invoke();
                Logger.Here().LogInformation($"Clever API for district '{Repo.District}' is not validated.");
                throw new ProcessingException(Logger.Here(), $"Clever API is not validated.");
            }
        }

        private async Task<bool> ProcessRecord<T>(T record, string table, DateTime now) where T : CsvBaseObject
        {
            if (string.IsNullOrEmpty(record.sourcedId))
                throw new ProcessingException(Logger.Here(),
                    $"Record of type {typeof(T).Name} contains no SourcedId: {JsonConvert.SerializeObject(record)}");

            DataSyncLine line = await Repo.Lines<T>().SingleOrDefaultAsync(l => l.SourcedId == record.sourcedId);
            bool isNewRecord = line == null;

            if (typeof(T) == typeof(CsvUser))
            {
                CsvUser rec = record as CsvUser;
                if (string.IsNullOrEmpty(rec.orgSourcedIds))
                {
                    throw new ProcessingException(Logger, $"orgSourcedIds cannot be empty in CsvUser for sourcedId {record.sourcedId}");
                }

                if (string.IsNullOrEmpty(rec.enabledUser))
                    rec.enabledUser = "true";
                if (string.IsNullOrEmpty(rec.familyName))
                    rec.familyName = string.Empty;
                if (isNewRecord && !Helper.ToBoolean(rec.enabledUser))
                    return false;
                if (!string.IsNullOrEmpty(rec.role) && !rec.role.Trim().ToLower().Equals("student"))
                    return false;

                rec.familyName = textInfo.ToTitleCase(rec.familyName.Trim().ToLower());
                rec.givenName = textInfo.ToTitleCase(rec.givenName.Trim().ToLower());
                rec.middleName = textInfo.ToTitleCase(rec.middleName.Trim().ToLower());
                rec.password = rec.password.Trim();
                rec.email = rec.email.Trim();
                rec.username = rec.username.Trim();

                record = rec as T;
            }

            Repo.CurrentHistory.NumRows++;
            string data = JsonConvert.SerializeObject(record);

            if (isNewRecord)
            {
                // already deleted
                if (record.isDeleted)
                {
                    Repo.CurrentHistory.NumDeleted++;
                    return false;
                }

                Repo.CurrentHistory.NumAdded++;
                line = new DataSyncLine
                {
                    SourcedId = record.sourcedId,
                    DistrictId = Repo.DistrictId,
                    LoadStatus = LoadStatus.Added,
                    LastSeen = now,
                    Table = table,
                };
                Repo.AddLine(line);
            }
            else // existing record, check if it has changed
            {
                line.LastSeen = now;
                line.Touch();

                if (typeof(T) == typeof(CsvUser)) // if enabled false, do nothing
                {
                    bool enabledUser = Helper.ToBoolean((record as CsvUser).enabledUser);
                    if (!enabledUser)
                    {
                        Repo.CurrentHistory.NumDeleted++;
                        line.LoadStatus = LoadStatus.Deleted;
                        line.IncludeInSync = false;
                        line.Error = null;
                        line.ErrorCode = null;
                        return false;
                    }
                }

                // no change to the data, skip!
                if (line.RawData == data)
                {
                    if (line.SyncStatus != SyncStatus.Loaded)
                        line.LoadStatus = LoadStatus.NoChange;
                    return false;
                }

                // status should be deleted
                if (record.isDeleted)
                {
                    Repo.CurrentHistory.NumDeleted++;
                    line.LoadStatus = LoadStatus.Deleted;
                    line.IncludeInSync = false;
                    line.Error = null;
                    line.ErrorCode = null;
                }
                else if (line.SyncStatus == SyncStatus.Loaded && line.LoadStatus == LoadStatus.Added)
                {
                    Repo.CurrentHistory.NumAdded++;
                    line.LoadStatus = LoadStatus.Added; // if added, leave added
                }
                else
                {
                    Repo.CurrentHistory.NumModified++;
                    line.LoadStatus = LoadStatus.Modified;
                    line.Error = null;
                    line.ErrorCode = null;
                }
            }

            line.RawData = data;
            line.SourcedId = record.sourcedId;
            line.SyncStatus = SyncStatus.Loaded;

            Repo.PushLineHistory(line, isNewData: true);

            return isNewRecord;
        }

        private CsvUser GetCsvUserClasslink(ClassLinkUser classLinkUser)
        {
            return new CsvUser()
            {
                sourcedId = classLinkUser.sourcedId,
                dateLastModified = classLinkUser.dateLastModified,
                status = classLinkUser.status,
                enabledUser = classLinkUser.enabledUser,
                orgSourcedIds = classLinkUser.orgs.Select(s => s.sourcedId).FirstOrDefault(),
                role = classLinkUser.role,
                username = classLinkUser.username,
                givenName = classLinkUser.givenName,
                familyName = classLinkUser.familyName,
                middleName = classLinkUser.middleName,
                password = classLinkUser.password,
                email = classLinkUser.email,
                grades = string.Join(",", classLinkUser.grades),
                identifier = classLinkUser.identifier,
            };
        }

        private CsvOrg GetCsvOrgClever(CleverOrg cleverOrg)
        {
            return new CsvOrg()
            {
                sourcedId = cleverOrg.data.id,
                dateLastModified = cleverOrg.data.last_modified,
                status = "active",
                name = cleverOrg.data.name,
                identifier = cleverOrg.data.state_id,
                type = "school",
            };
        }

        private CsvUser GetCsvUserClever(CleverUser cleverUser)
        {
            return new CsvUser()
            {
                sourcedId = cleverUser.data.id,
                dateLastModified = cleverUser.data.last_modified,
                status = "active",
                enabledUser = "true",
                orgSourcedIds = cleverUser.data.schools.FirstOrDefault(),
                role = "student",
                username = cleverUser.data.email,
                givenName = cleverUser.data.name.first,
                familyName = cleverUser.data.name.last,
                middleName = cleverUser.data.name.middle,
                password = cleverUser.data.sis_id,
                email = cleverUser.data.email,
                grades = cleverUser.data.grade,
                identifier = cleverUser.data.sis_id
            };
        }
    }
}