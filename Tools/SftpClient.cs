using Selen.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WinSCP;

namespace Selen.Base {
    static class SftpClient {

        //выгрузка SFTP - новый протокол
        public static void Upload(string fname) {
            try {
                Log.Add("SftpClient: " + fname + " - отправляю файл на сервер sftp://35.185.57.11/ ...");
                SessionOptions sessionOptions = new SessionOptions {
                    Protocol = Protocol.Sftp,
                    HostName = "35.185.57.11",
                    UserName = "bitnami",
                    SshHostKeyFingerprint = "ssh-rsa 2048 5LaZLFR2u+1xdE9SWnc3KzPksfjDNL2FEFcDM8jztKo=",
                    SshPrivateKeyPath = "google_sftp.ppk",                          //SshPrivateKeyPath = Application.StartupPath + "\\" + "google_sftp.ppk",
                };
                using (Session session = new Session()) {
                    session.Open(sessionOptions);
                    if (session.Opened) {
                        var res = session.PutFileToDirectory(
                            fname, "/opt/bitnami/apps/wordpress/htdocs");           //Application.StartupPath + "\\" + fname, "/opt/bitnami/apps/wordpress/htdocs");
                        Log.Add("SftpClient: " + fname + " - успешно отправлен!");
                    }
                }
            } catch (Exception x) {
                Log.Add("SftpClient: " + fname + " - ошибка отправки sftp! - " + x.Message);
            }
        }
        //выгрузка FTP
        public static async Task FtpUploadAsync(string fname) {
            System.Net.WebClient ftp = new System.Net.WebClient();
            //ftp.Credentials = new NetworkCredential("u1633438", "HIJv3W71Xmftd61D"); 
            ftp.Credentials = new NetworkCredential("u1723083", "e6sb0JTTrc0IV4sW");
            for (int f = 1; f <= 5; f++) {
                try {
                    await Task.Factory.StartNew(() => {
                        //ftp.UploadFile(new Uri("ftp://31.31.198.99:21/www/avtotehnoshik.tk/" 
                        ftp.UploadFile(new Uri("ftp://37.140.192.251:21/www/avtotehnoshik.ru/"
                            + (fname.Contains("\\") ? fname.Split('\\').Last() : fname)), "STOR", fname);
                    });
                    Log.Add("SftpClient: " + fname + " - успешно отправлен!");
                    break;
                } catch (Exception ex) {
                    Log.Add("SftpClient: " + fname + " - ошибка отправки FTP (" + f + ") - "
                        + ex.Message);
                    await Task.Delay(60000);
                }
            }
        }
    }
}
