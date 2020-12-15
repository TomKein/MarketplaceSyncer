using System;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Net;

namespace Selen.Tools {
    static class SmtpMailClient {
        public static async Task SendAsync() {
            try {
                Log.Add("SmtpMailClient: готовлю письмо...");
                MailAddress from = new MailAddress("benderrrrrrrrrrrr@mail.ru", "Bender");
                MailAddress to = new MailAddress("prices@euroauto.ru");
                MailMessage m = new MailMessage(from, to);
                m.Subject = "price for euroauto from autotehnoshik";
                m.Body = "Hello! New price attached!";
                m.IsBodyHtml = true;
                Log.Add("SmtpMailClient: прикрепляю файл выгрузки ea_export.csv...");
                m.Attachments.Add(new Attachment("ea_export.csv"));
                SmtpClient smtp = new SmtpClient("smtp.mail.ru", 587);
                smtp.Credentials = new NetworkCredential("benderrrrrrrrrrrr@mail.ru", "drum122122");
                smtp.EnableSsl = true;
                Log.Add("SmtpMailClient: отправляю prices@euroauto.ru...");
                await smtp.SendMailAsync(m);
                Log.Add("SmtpMailClient: письмо успешно отправлено!");
            } catch (Exception x) {
                Log.Add("SmtpMailClient: ошибка! письмо не отправлено!");
            }
        }
    }
}
