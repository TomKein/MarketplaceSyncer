using System;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Net;

namespace Selen.Tools {
    static class SmtpMailClient {
        public static async Task SendAsync(string filename, string adress) {
            try {
                Log.Add("SmtpMailClient: готовлю письмо...");
                MailAddress from = new MailAddress("benderrrrrrrrrrrr@mail.ru", "Bender");
                MailAddress to = new MailAddress(adress);
                MailMessage m = new MailMessage(from, to);
                m.Subject = "price for euroauto from autotehnoshik";
                m.Body = "Hello! New price attached!";
                m.IsBodyHtml = true;
                Log.Add("SmtpMailClient: прикрепляю файл выгрузки " + filename + " ...");
                m.Attachments.Add(new Attachment(filename));
                SmtpClient smtp = new SmtpClient("smtp.mail.ru", 587);
                smtp.Credentials = new NetworkCredential("benderrrrrrrrrrrr@mail.ru", "drum122122");
                smtp.EnableSsl = true;
                Log.Add("SmtpMailClient: отправляю " + filename + " на почту " + adress + "...");
                await smtp.SendMailAsync(m);
                Log.Add("SmtpMailClient: " + filename + " успешно отправлен!");
            } catch (Exception x) {
                Log.Add("SmtpMailClient: ошибка! "+ filename + " не отправлен! - "+x.Message);
            }
        }
    }
}
