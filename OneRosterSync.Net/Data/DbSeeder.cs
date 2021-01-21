using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OneRosterSync.Net.Models;
using OneRosterSync.Net.Processing;
using Renci.SshNet;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace OneRosterSync.Net.Data
{
    public static class DbSeeder
    {
        public static void SeedDb(IApplicationBuilder app)
        {
            using (var serviceScope = app.ApplicationServices.CreateScope())
            {
                var dbContext = serviceScope.ServiceProvider.GetService<ApplicationDbContext>();
                dbContext.Database.EnsureCreated();

                if (!dbContext.NCESMappings.Any())
                {
                    CopyFile();
                    SeedNCESMapping(dbContext);
                }
                InsertTestSchoolID(dbContext);

                if (!dbContext.EmailConfigs.Any())
                {
                    var emailConfig = new EmailConfig()
                    {
                        Host = "smtp.mailgun.org",
                        From = "admin@lms.summitk12.com",
                        DisplayName = "OneRoster SummitK12",
                        To = "oliver.huang@summitk12.com",
                        Cc = "sandeshkumar@folio3.com,john@summitk12.com,szaman@folio3.com",
                        Bcc = string.Empty,
                        Subject = "ALARM: OneRoster Sync",
                        Password = "4XYj2Tq4qEAlRn1psrT4"
                    };
                    dbContext.EmailConfigs.Add(emailConfig);
                    dbContext.SaveChanges();
                }
            }
        }

        public static void SeedDb(ApplicationDbContext dbContext)
        {
            dbContext.Database.EnsureCreated();

            if (!dbContext.NCESMappings.Any())
            {
                CopyFile();
                SeedNCESMapping(dbContext);
            }
            InsertTestSchoolID(dbContext);
        }

        public static void CopyFile()
        {
            string BaseFolder = "CSVSample", Host = "sftp.summitk12.com", Username = "sftpd30", Password = "kRg92eceJGNd",
             FTPFilePath = "/files/CSV_NCES.zip", localFilePath = Path.GetFullPath($@"{Path.Combine(BaseFolder, "csv_nces.zip")}");
            if (!Directory.Exists(Path.GetFullPath(BaseFolder)))
            {
                Directory.CreateDirectory(Path.GetFullPath(BaseFolder));
            }
            else
            {
                if (File.Exists(localFilePath))
                {
                    File.Delete(localFilePath);
                }
            }

            var connectionInfo = new PasswordConnectionInfo(Host, Username, Password);
            using (var sftp = new SftpClient(connectionInfo))
            {
                sftp.Connect();
                MemoryStream outputSteam = new MemoryStream();
                sftp.DownloadFile($"{FTPFilePath}", outputSteam);
                sftp.Disconnect();

                using (var fileStream = new FileStream(localFilePath, FileMode.Create))
                {
                    outputSteam.Seek(0, SeekOrigin.Begin);
                    outputSteam.CopyTo(fileStream);
                }

                ZipFile.ExtractToDirectory(Path.GetFullPath($@"{Path.Combine(BaseFolder, "csv_nces.zip")}"), Path.GetFullPath($@"{BaseFolder}"), true);
            }
        }

        private static void SeedNCESMapping(ApplicationDbContext dbContext)
        {
            try
            {
                string filePath = Path.Combine("CSVSample", "state_nces_2019.csv");

                using (var file = File.OpenText(filePath))
                {
                    using (var csv = new CsvHelper.CsvReader(file))
                    {
                        csv.Configuration.MissingFieldFound = null;
                        csv.Configuration.HasHeaderRecord = true;

                        csv.Read();
                        csv.ReadHeader();
                        List<NCESMapping> listMappings = new List<NCESMapping>();
                        for (int i = 0; csv.Read(); i++)
                        {
                            NCESMappingModel record = null;
                            try
                            {
                                record = csv.GetRecord<NCESMappingModel>();
                                NCESMapping obj = new NCESMapping()
                                {
                                    NCESId = record.ncesId,
                                    StateID = record.stateSchoolId,
                                    Version = 1
                                };
                                listMappings.Add(obj);
                            }
                            catch (Exception ex)
                            {
                                //if (ex is ProcessingException)
                                //    throw;

                                string o = record == null ? "(null)" : JsonConvert.SerializeObject(record);
                                throw new Exception($"Unhandled error processing {typeof(NCESMapping).Name}: {o}", ex);
                            }
                        }

                        //for (int i = 0; i < listMappings.Count; i++)
                        //{
                        //    dbContext.NCESMappings.Add(listMappings[i]);
                        //    dbContext.SaveChanges();
                        //}
                        //await dbContext.NCESMappings.AddRangeAsync(listMappings);
                        //await dbContext.SaveChangesAsync();
                        dbContext.NCESMappings.AddRange(listMappings);
                        dbContext.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Unhandled error processing method SeedNCESMapping", ex);
            }
        }

        public static void InsertTestSchoolID(ApplicationDbContext dbContext)
        {
            var objTestSchool = dbContext.NCESMappings.FirstOrDefault(w => w.NCESId == "890000000000");
            if (objTestSchool == null)
            {
                NCESMapping obj = new NCESMapping()
                {
                    NCESId = "890000000000",
                    StateID = "-890000000000",
                    Version = 1
                };
                dbContext.NCESMappings.Add(obj);
                dbContext.SaveChanges();
            }
        }
    }
}
