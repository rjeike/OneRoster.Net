using OneRosterSync.Net.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace OneRosterSync.Net.Utils
{
    public class EmailManager
    {
        public static void SendEmail(string host, string from, string password, string displayName, string To, string Cc, string Bcc, string subject, string body)
        {
            var smtp = new SmtpClient
            {
                Host = host,
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(from, password)
            };
            using (var mail = new MailMessage()
            {
                Subject = subject,
                Body = body,
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
    }
}
