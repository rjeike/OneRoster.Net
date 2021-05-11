using OneRosterSync.Net.Data;
using OneRosterSync.Net.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using TimeZoneConverter;

namespace OneRosterSync.Net.Utils
{
    public class EmailManager
    {
        public static void SendEmail(string host, string from, string password, string displayName, string To, string Cc, string Bcc, string subject, string body, bool isBodyHtml = false)
        {
            var smtp = new SmtpClient
            {
                Host = host,
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(from, password),
            };
            using (var mail = new MailMessage()
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = isBodyHtml
            })
            {
                mail.From = new MailAddress(from, displayName);
                foreach (var to in To.Split(","))
                    mail.To.Add(new MailAddress(to.Trim()));
                if (!string.IsNullOrEmpty(Cc))
                    foreach (var cc in Cc.Split(","))
                        mail.CC.Add(new MailAddress(cc.Trim()));
                if (!string.IsNullOrEmpty(Bcc))
                    foreach (var bcc in Bcc.Split(","))
                        mail.Bcc.Add(new MailAddress(bcc.Trim()));

                smtp.Send(mail);
            }
        }

        public static string GenerateConsolidatedEmailBody(ApplicationDbContext db, List<DataSyncHistory> districtsHistory)
        {
            var CSTZone = TZConvert.GetTimeZoneInfo("Central Standard Time");
            string time = $"{DateTime.UtcNow.ToString("dddd, dd MMMM yyyy HH:mm:ss")} UTC";
            if (CSTZone != null)
            {
                time = $"{TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CSTZone).ToString("dddd, dd MMMM yyyy HH:mm:ss")} CST";
            }
            string body = $"You are receiving this email at {time} because error(s) occurred in tonight's nightly sync in OneRoster.<br/><br/>";
            body += "District(s):<br/>";
            foreach (var dbHistory in districtsHistory)
            {
                body += $"<strong>{dbHistory.District.Name}</strong><br/>";
                if (!string.IsNullOrEmpty(dbHistory.LoadError))
                {
                    string districtError = dbHistory.LoadError;
                    if (string.IsNullOrEmpty(districtError))
                    {
                        var csvError = db.DistrictCsvErrors.Where(l => l.DistrictId == dbHistory.DistrictId && l.Created.Date >= DateTime.Today).FirstOrDefault();
                        if (csvError != null)
                        {
                            districtError = csvError.Error;
                        }
                    }
                    body += $"<ul><li>{districtError}</li></ul>";
                }
                else if (!string.IsNullOrEmpty(dbHistory.AnalyzeError))
                {
                    body += $"<ul><li>{dbHistory.AnalyzeError}</li></ul>";
                }
                else if (!string.IsNullOrEmpty(dbHistory.ApplyError))
                {
                    var successCodes = new string[] { "126", "128", "129" };
                    var groupErrors = db.DataSyncLines.Where(w => w.DistrictId == dbHistory.DistrictId && w.LoadStatus != LoadStatus.Deleted
                            && (w.ErrorCode == null || !successCodes.Contains(w.ErrorCode))
                            && w.Error != null 
                            && !w.Error.StartsWith("User (")
                            && !w.Error.StartsWith("Deleted from analyze in MarkDeleted method."))
                        .GroupBy(g => g.Error).ToList();
                    if (groupErrors.Count > 0)
                    {
                        body += $"<ul>";
                        foreach (var err in groupErrors)
                        {
                            int count = err.Count();
                            body += $"<li>{err.Key} ({count} {(count > 1 ? "records" : "record")})</li>";
                        }
                        body += $"</ul>";
                    }
                }
            }
            body += "<br/><br/>SummitK12 OneRoster";
            return body;
        }
    }
}
