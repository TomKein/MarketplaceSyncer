using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClosedXML.Excel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Web;

namespace Selen {

    public class RootResponse {
        public string status { get; set; }
        public object result { get; set; }
        public string token { get; set; }
        public string app_psw { get; set; }
        public int request_count { get; set; }
    }

    static class Class365API {
        //поля
        private static string secret = @"A9PiFXQcbabLyOolTRRmf4RR8zxWLlVb";

        private static string app_id = "768289";

        private static Uri baseAdr = new Uri("https://action_37041.business.ru/api/rest/");

        static RootResponse rr = new RootResponse();

        static Dictionary<string, string> data = new Dictionary<string, string>();

        private static HttpClient hc = new HttpClient();

        private static object locker = new object();
        private static bool flag = false;

        //конструктор
        //public static Class365API() { }

        //правильная сортировка на будущее
        //string[] sortedKeys = form.AllKeys.OrderBy(x => x, StringComparer.Ordinal).ToArray();

        public static async Task RepairAsync()
        {
            await Task.Delay(1000);
            //1
            data["app_id"] = app_id;
            //2
            var sb = new StringBuilder();
            foreach (var key in data.Keys.OrderBy(x => x, StringComparer.Ordinal)) {
                sb.Append("&")
                  .Append(key)
                  .Append("=")
                  .Append(data[key]);
            }
            //3
            sb.Remove(0,1);//убираем первый &
            string ps = sb.ToString();
            //4 - кодировка в мд5
            byte[] hash = Encoding.ASCII.GetBytes(secret + ps);
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] hashenc = md5.ComputeHash(hash);
            //5
            ps += "&app_psw=" + GetMd5(hashenc);
            //6
            Uri url = new Uri(baseAdr, "repair.json?" + ps);

            HttpResponseMessage res = await hc.GetAsync(url);

            var js = await res.Content.ReadAsStringAsync();

            if (res.StatusCode != HttpStatusCode.OK || js.Contains("Превышение лимита")) {
                rr.token = "";
                await Task.Delay(60000);
            }
            else
                //сохраним токен
                rr = JsonConvert.DeserializeObject<RootResponse>(js);
            await Task.Delay(1000);
        }

        public static async Task<string> RequestAsync(string action, string model, Dictionary<string, string> par)
        {
            while (flag) { await Task.Delay(5000); }
            lock (locker) {
                flag = true; }
            HttpResponseMessage httpResponseMessage = null;

            //1.добавляем к словарю параметров app_id
            par["app_id"] = app_id;
            //2.сортиовка параметров
            SortedDictionary<string, string> parSorted = new SortedDictionary<string, string>(new PhpKSort());
            foreach (var key in par.Keys) {
                parSorted.Add(key, par[key]);
            }
            //3.создаем строку запроса
            string qstr = QueryStringBuilder.BuildQueryString(parSorted);
            do {
                //проверяем токен
                try {
                    while (rr == null || rr.token == "" || rr.token == null) {
                        await RepairAsync();
                    }
                    //3.считаем хэш строку
                    byte[] hash = Encoding.UTF8.GetBytes(rr.token + secret + qstr);
                    MD5 md5 = new MD5CryptoServiceProvider();
                    byte[] hashenc = md5.ComputeHash(hash);
                    //4.прибавляем полученный пароль к строке запроса
                    qstr += "&app_psw=" + GetMd5(hashenc);

                    //5.готовим ссылку
                    string url = baseAdr + model + ".json";

                    //6.выполняем соответствующий запрос
                    if (action.ToUpper() == "GET") {
                        httpResponseMessage = await hc.GetAsync(url + "?" + qstr);
                    } else if (action.ToUpper() == "PUT") {
                        //HttpContent content = new StringContent(qstr, Encoding.UTF8, "application/json");
                        HttpContent content = new StringContent(qstr);//, Encoding.UTF8, "application/json");
                        httpResponseMessage = await hc.PutAsync(url, content);
                    } else if (action.ToUpper() == "POST") {
                        HttpContent content = new StringContent(qstr, Encoding.UTF8, "application/x-www-form-urlencoded");
                        httpResponseMessage = await hc.PostAsync(url, content);
                    }
                    if (httpResponseMessage.StatusCode == HttpStatusCode.OK) {
                        var js = await httpResponseMessage.Content.ReadAsStringAsync();
                        rr = JsonConvert.DeserializeObject<RootResponse>(js);
                        Thread.Sleep(rr.request_count*10);
                        flag = false;
                        return JsonConvert.SerializeObject(rr.result);
                    }

                    await RepairAsync();
                    qstr = qstr.Contains("&app_psw=") ? qstr.Replace("&app_psw=", "|").Split('|')[0] : qstr;
                    await Task.Delay(20000);
                } catch //(Exception e)
                {
                    await Task.Delay(30000);
                    qstr = qstr.Contains("&app_psw=") ? qstr.Replace("&app_psw=", "|").Split('|')[0] : qstr;
                    rr.token = "";
                    flag = false;
                }
            } while (true);
        }

        private static string GetMd5(byte[] hashenc) {
            StringBuilder md5_str = new StringBuilder();
            foreach (var b in hashenc) {
                md5_str.Append(b.ToString("x2"));
            }

            return md5_str.ToString();
        }
    }
}