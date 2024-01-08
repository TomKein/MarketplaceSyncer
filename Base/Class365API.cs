using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Selen.Tools;

namespace Selen {

    public class RootResponse {
        public string status { get; set; }
        public object result { get; set; }
        public string token { get; set; }
        public string app_psw { get; set; }
        public string request_count { get; set; }
    }

    public class Class365API {
        //поля
        private static string _secret = @"A9PiFXQcbabLyOolTRRmf4RR8zxWLlVb";
        private static string _appId = "768289";
        private static Uri _baseAdr = new Uri("https://action_37041.business.ru/api/rest/");
        static RootResponse _rr = new RootResponse();
        static Dictionary<string, string> _data = new Dictionary<string, string>();
        private static HttpClient _hc = new HttpClient();
        private static object _locker = new object();
        private static bool _flag = false;

        //конструктор
        //public static Class365API() { }

        //правильная сортировка на будущее
        //string[] sortedKeys = form.AllKeys.OrderBy(x => x, StringComparer.Ordinal).ToArray();

        public static async Task RepairAsync()
        {
            await Task.Delay(1000);
            //1
            _data["app_id"] = _appId;
            //2
            var sb = new StringBuilder();
            foreach (var key in _data.Keys.OrderBy(x => x, StringComparer.Ordinal)) {
                sb.Append("&")
                  .Append(key)
                  .Append("=")
                  .Append(_data[key]);
            }
            //3
            sb.Remove(0,1);//убираем первый &
            string ps = sb.ToString();
            //4 - кодировка в мд5
            byte[] hash = Encoding.ASCII.GetBytes(_secret + ps);
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] hashenc = md5.ComputeHash(hash);
            //5
            ps += "&app_psw=" + GetMd5(hashenc);
            //6
            Uri url = new Uri(_baseAdr, "repair.json?" + ps);
            HttpResponseMessage res = await _hc.GetAsync(url);
            var js = await res.Content.ReadAsStringAsync();

            if (res.StatusCode != HttpStatusCode.OK || js.Contains("Превышение лимита")) {
                _rr.token = "";
                Log.Add("business.ru: ошибка получения токена авторизации! - " + res.StatusCode.ToString());
                await Task.Delay(60000);
            } else {
                //сохраним токен
                _rr = JsonConvert.DeserializeObject<RootResponse>(js);
                Log.Add("business.ru: получен новый токен!");
            }
            await Task.Delay(1000);
        }

        public static async Task<string> RequestAsync(string action, string model, Dictionary<string, string> par)
        {
            while (_flag) { await Task.Delay(5000); }
            lock (_locker) {
                _flag = true; }
            HttpResponseMessage httpResponseMessage = null;

            //1.добавляем к словарю параметров app_id
            par["app_id"] = _appId;
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
                    while (_rr == null || _rr.token == "" || _rr.token == null) {
                        await RepairAsync();
                    }
                    //3.считаем хэш строку
                    byte[] hash = Encoding.UTF8.GetBytes(_rr.token + _secret + qstr);
                    MD5 md5 = new MD5CryptoServiceProvider();
                    byte[] hashenc = md5.ComputeHash(hash);
                    //4.прибавляем полученный пароль к строке запроса
                    qstr += "&app_psw=" + GetMd5(hashenc);

                    //5.готовим ссылку
                    string url = _baseAdr + model + ".json";

                    //6.выполняем соответствующий запрос
                    if (action.ToUpper() == "GET") {
                        httpResponseMessage = await _hc.GetAsync(url + "?" + qstr);
                    } else if (action.ToUpper() == "PUT") {
                        HttpContent content = new StringContent(qstr);//, Encoding.UTF8, "application/json");
                        httpResponseMessage = await _hc.PutAsync(url, content);
                    } else if (action.ToUpper() == "POST") {
                        HttpContent content = new StringContent(qstr, Encoding.UTF8, "application/x-www-form-urlencoded");
                        httpResponseMessage = await _hc.PostAsync(url, content);
                    } else if (action.ToUpper() == "DELETE") {
                        //HttpContent content = new StringContent(qstr);//, Encoding.UTF8, "application/x-www-form-urlencoded");
                        httpResponseMessage = await _hc.DeleteAsync(url + "?" + qstr);
                    }
                    if (httpResponseMessage.StatusCode == HttpStatusCode.OK) {
                        var js = await httpResponseMessage.Content.ReadAsStringAsync();
                        _rr = JsonConvert.DeserializeObject<RootResponse>(js);
                        //todo добавить параметр в настройки
                        Thread.Sleep(100);
                        _flag = false;
                        return _rr != null ? JsonConvert.SerializeObject(_rr.result) : "";
                    } 
                    Log.Add("business.ru: ошибка запроса - " + httpResponseMessage.StatusCode.ToString());
                    await RepairAsync();
                    qstr = qstr.Contains("&app_psw=") ? qstr.Replace("&app_psw=", "|").Split('|')[0] : qstr;
                    await Task.Delay(20000);
                } catch (Exception x){
                    Log.Add("business.ru: ошибка запроса к бизнес.ру - " + x.Message);
                    await Task.Delay(30000);
                    qstr = qstr.Contains("&app_psw=") ? qstr.Replace("&app_psw=", "|").Split('|')[0] : qstr;
                    _rr.token = "";
                    _flag = false;
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