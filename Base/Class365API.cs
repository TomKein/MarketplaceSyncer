using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using Newtonsoft.Json;
using Selen.Base;
using Selen.Tools;

namespace Selen {
    public delegate Task SyncAllEventDelegate();
    public class Class365API {
        static readonly string _secret = @"A9PiFXQcbabLyOolTRRmf4RR8zxWLlVb";
        static readonly string _appId = "768289";
        static string _l = "class365: ";
        private static Uri _baseAdr = new Uri("https://action_37041.business.ru/api/rest/");
        static RootResponse _rr = new RootResponse();
        static Dictionary<string, string> _data = new Dictionary<string, string>();
        private static HttpClient _hc = new HttpClient();
        public static List<RootGroupsObject> _busGroups = new List<RootGroupsObject>();
        public static List<RootObject> _bus = new List<RootObject>();
        public static List<RootObject> lightSyncGoods = new List<RootObject>();
        public static readonly string _busFileName = @"..\bus.json";
        public static readonly int _pageLimitBase = 250;
        public static bool _isBusinessNeedRescan = false;
        public static bool _isBusinessCanBeScan = false;
        public static DateTime _syncStartTime;
        public static DateTime _scanTime;
        public static DateTime _lastScanTime;
        public static string _labelBusText;
        static Random _rnd = new Random();
        private static object _locker = new object();
        private static bool _flag = false;
        private static System.Timers.Timer _timer = new System.Timers.Timer();

        public static event SyncAllEventDelegate syncAllEvent = null;

        static Class365API() {
            _timer.Interval = 20000;
            _timer.Elapsed += timer_sync_Tick;
            _timer.Start();
        }
        private static async void timer_sync_Tick(object sender, ElapsedEventArgs e) {
            if (!_isBusinessCanBeScan ||
                DateTime.Now.Hour < DB.GetParamInt("syncStartHour") ||
                DateTime.Now.Hour >= DB.GetParamInt("syncStopHour"))
                return;
            _timer.Stop();
            await GoLiteSync();
            if (_isBusinessNeedRescan)
                await BaseGetAsync();
            _timer.Start();
        }
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
        public class RootResponse {
            public string status { get; set; }
            public object result { get; set; }
            public string token { get; set; }
            public string app_psw { get; set; }
            public string request_count { get; set; }
        }
        private static string GetMd5(byte[] hashenc) {
            StringBuilder md5_str = new StringBuilder();
            foreach (var b in hashenc) {
                md5_str.Append(b.ToString("x2"));
            }
            return md5_str.ToString();
        }
        public static async Task BaseGetAsync() {
            _isBusinessNeedRescan = true;
            _isBusinessCanBeScan = false;
            _syncStartTime = DateTime.Now;
            Log.Add(_l+"старт полного цикла синхронизации");
            await GetBusGroupsAsync();
            await GetBusGoodsAsync2();
            var tlog = _bus.Count + "/" + _bus.Count(c => c.images.Count > 0 && c.amount > 0 && c.price > 0);
            Log.Add(_l+"получено товаров с остатками " + tlog);
            _labelBusText = tlog;
            await SaveBusAsync();
            _isBusinessNeedRescan = false;
            await syncAllEvent();
            Log.Add(_l+"полный цикл синхронизации завершен");
        }
        public static async Task GetBusGoodsAsync2() {
            int lastScan;
            do {
                lastScan = await DB.GetParamIntAsync("controlBus");
                _bus.Clear();
                try {
                    for (int i = 1; i > 0; i++) {
                        string s = await RequestAsync("get", "goods", new Dictionary<string, string>{
                            {"archive", "0"},
                            {"type", "1"},
                            {"limit", _pageLimitBase.ToString()},
                            {"page", i.ToString()},
                            {"with_additional_fields", "1"},
                            {"with_attributes", "1"},
                            {"with_remains", "1"},
                            {"with_prices", "1"},
                            {"type_price_ids[0]","75524" }
                        });
                        if (s.Contains("name")) {
                            s = s
                                .Replace("\"209334\":", "\"drom\":")
                                .Replace("\"209360\":", "\"vk\":")
                                .Replace("\"833179\":", "\"kp\":")
                                .Replace("\"854872\":", "\"gde\":")
                                .Replace("\"854879\":", "\"ozon\":");
                            _bus.AddRange(JsonConvert.DeserializeObject<List<RootObject>>(s));
                            Log.Add(_l+ "GetBusGoodsAsync2 - получено " +_bus.Count.ToString()+ " товаров");
                        } else
                            break;
                    }
                } catch (Exception x) {
                    Log.Add("business.ru: ошибка при запросе товаров из базы!!! - " + x.Message);
                    await Task.Delay(60000);
                }
                await DB.SetParamAsync("controlBus", _bus.Count.ToString());
            } while (_bus.Count == 0 || Math.Abs(lastScan - _bus.Count) > 400);
            Log.Add("business.ru: получено товаров " + _bus.Count);
        }

        public static async Task GetBusGroupsAsync() {
            Log.Add(_l + "GetBusGroupsAsync - получаю список групп...");
            do {
                _busGroups.Clear();
                try {
                    var tmp = await RequestAsync("get", "groupsofgoods", new Dictionary<string, string> {
                        //{"parent_id", "205352"} // БУ запчасти
                    });
                    var tmp2 = JsonConvert.DeserializeObject<List<RootGroupsObject>>(tmp);
                    _busGroups.AddRange(tmp2);
                } catch (Exception x) {
                    Log.Add(_l + "GetBusGroupsAsync - ошибка запроса групп товаров из базы!!! - " + x.Message + " - " + x.InnerException?.Message);
                    await Task.Delay(60000);
                }
            } while (_busGroups.Count < 30);
            Log.Add(_l+ "GetBusGroupsAsync - получено " + _busGroups.Count + " групп товаров");
            RootObject.Groups = _busGroups;
        }
        public static async Task GoLiteSync() {
            _isBusinessCanBeScan = false;
            var stage = "";
            try {
                _syncStartTime = DateTime.Now;
                Log.Add(_l+ "GoLiteSync - запрос изменений...");
                var liteScanTimeShift = await DB.GetParamIntAsync("liteScanTimeShift");
                var lastLiteScanTime = await DB.GetParamStrAsync("liteScanTime");
                _lastScanTime = await DB.GetParamDateTimeAsync("lastScanTime");
                var lastWriteBusFile = File.GetLastWriteTime(_busFileName);

                var isBusFileOld = lastWriteBusFile.AddMinutes(5) < _lastScanTime;
                //если файл базы устарел - полный рескан
                if (isBusFileOld || _bus.Count <= 0 || _isBusinessNeedRescan) { 
                    Log.Add(@"{_l} GoLiteSync - будет произведен запрос полной базы товаров... isBusFileOld {isBusFileOld}, goods.Count {_bus.Count}, _isBusinessNeedRescan {_isBusinessNeedRescan}");
                    _isBusinessNeedRescan = true;
                    _isBusinessCanBeScan = true;
                    return;
                }
                //запросим новые/измененные карточки товаров
                string s = await RequestAsync("get", "goods", new Dictionary<string, string>{
                            //{"archive", "0"},
                            {"type", "1"},
                            //{"limit", pageLimitBase.ToString()},
                            //{"page", i.ToString()},
                            {"with_additional_fields", "1"},
                            {"with_attributes", "1"},
                            {"with_remains", "1"},
                            {"with_prices", "1"},
                            {"type_price_ids[0]","75524" },
                            {"updated[from]", lastLiteScanTime}
                    });
                s = s.Replace("\"209334\":", "\"drom\":")
                    .Replace("\"209360\":", "\"vk\":")
                    .Replace("\"833179\":", "\"kp\":")
                    .Replace("\"854872\":", "\"gde\":")
                    .Replace("\"854879\":", "\"ozon\":");
                lightSyncGoods.Clear();
                if (s.Length > 3)
                    lightSyncGoods.AddRange(JsonConvert.DeserializeObject<RootObject[]>(s));

                stage = "текущие остатки...";
                var ids = JsonConvert.DeserializeObject<List<GoodIds>>(
                    await RequestAsync("get", "storegoods", new Dictionary<string, string>{
                        {"updated[from]", lastLiteScanTime}
                    }));

                stage = "текущие цены...";
                ids.AddRange(JsonConvert.DeserializeObject<List<GoodIds>>(
                    await RequestAsync("get", "salepricelistgoods", new Dictionary<string, string>{
                        {"updated[from]", lastLiteScanTime},
                        {"price_type_id","75524" } //интересуют только отпускные цены
                    })));

                stage = "запросим реализации...";
                ids.AddRange(JsonConvert.DeserializeObject<List<GoodIds>>(
                    await RequestAsync("get", "realizationgoods", new Dictionary<string, string>{
                        {"updated[from]", lastLiteScanTime}
                    })));
                //добавляю в запрос карточки без атрибутов - беру рандом 10 штук за раз
                //(костыль от глюка бизнес.ру, который иногда отдает пустые атрибуты)
                var ids2 = _bus.Where(w => w.attributes == null && w.images.Count > 0 && w.amount > 0)
                               .Select(p => new GoodIds { good_id = p.id }).ToList();
                Log.Add(_l+"GoLiteSync карточек с пустыми атрибутами - " + ids2.Count);
                if (ids2.Count > 0) {
                    Log.Add(_l + "GoLiteSync:" + _bus.Where(w => w.attributes == null && w.images.Count > 0 && w.amount > 0)
                           .Select(sel => sel.name).Aggregate((a, b) => a + "\n" + b));
                    var i = _rnd.Next(ids2.Count > 10 ? ids2.Count - 10 : ids2.Count);
                    ids.AddRange(ids2.Skip(i).Take(10));
                }

                //stage = "запросим изменения атрибутов...";
                //ids.AddRange(JsonConvert.DeserializeObject<List<GoodIds>>(
                //    await Class365API.RequestAsync("get", "goodsattributes", new Dictionary<string, string>{
                //        {"updated[from]", lastLiteScanTime}
                //    })));

                //добавляем к запросу карточки, привязанные к тиу, но с нулевой ценой. решает глюк нулевой цены после поступления
                //ids.AddRange(bus.Where(w => w.tiu.Contains("http") && w.price == 0).Select(_ => new GoodIds { good_id = _.id }));

                stage = "подгружаем карточки ...";

                if (ids.Count > 0) {
                    var distinctIds = new List<string>();
                    foreach (var id in ids) {
                        if (lightSyncGoods.FindIndex(f => f.id == id.good_id) == -1 && !distinctIds.Contains(id.good_id)) {
                            distinctIds.Add(id.good_id);
                        }
                    }
                    if (distinctIds.Count > 0)
                        lightSyncGoods.AddRange(await GetBusGoodsAsync(distinctIds));
                }
                //если изменений слишком много или сменился день - нужен полный рескан базы
                if (lightSyncGoods.Count >= 250 ||
                    _lastScanTime.Day != DateTime.Now.Day) {
                    Log.Add(_l + "будет произведен запрос полной базы товаров...");
                    _isBusinessNeedRescan = true;
                    _isBusinessCanBeScan = true;
                    return;
                }
                Log.Add(_l + "изменены карточки: " + lightSyncGoods.Count + " (с фото, ценой и остатками: " +
                    lightSyncGoods.Count(c => c.amount > 0 && c.price > 0 && c.images.Count > 0) + ")");
                foreach (var item in lightSyncGoods) {
                    Log.Add(_l + item.name + "(цена " + item.price + ", кол. " + item.amount + ")");
                }
                stage = "...";
                //переносим обновления в загруженную базу
                foreach (var lg in lightSyncGoods) {
                    var ind = _bus.FindIndex(f => f.id == lg.id);
                    if (ind != -1) { //индекс карточки найден - заменяем
                        _bus[ind] = lg;
                        //время обновления карточки сдвигаю на время начала цикла синхронизации
                        //для того чтобы обновлялись карточки, у которых изменился только остаток или цена
                        _bus[ind].updated = _syncStartTime.ToString();
                    } else { //иначе добавляем в коллекцию
                        _bus.Add(lg);
                    }
                }

                //база теперь должна быть в актуальном состоянии
                //сохраняем время, на которое база актуальна (только liteScanTime! 
                //lastScanTime сохраним когда перенесем изменения в объявления!!)
                //когда реализуем перенос изменения сразу - можно будет оперировать только одной переменной                

                _labelBusText = _bus.Count + "/" + _bus.Count(c => c.amount > 0 && c.price > 0 && c.images.Count > 0);
                if (!isBusFileOld) {
                    await DB.SetParamAsync("liteScanTime", _syncStartTime.AddMinutes(-liteScanTimeShift).ToString());
                    await DB.SetParamAsync("controlBus", _bus.Count.ToString());
                }

                ///вызываем событие в котором сообщаем о том, что в базе есть изменения... на будущее, когда будем переходить на событийную модель
                ///пока события не реализованы в проекте, поэтому пока мы лишь обновили уже загруженной базе только те карточки, 
                ///которые изменились с момента последнего запроса
                ///и дальше сдвинем контрольное время актуальности базы
                ///а дальше всё как обычно, только сайты больше не парсим,
                ///только вызываем методы обработки изменений и подъема упавших

                if (DateTime.Now.Minute == 55 || _lastScanTime.AddHours(1) < DateTime.Now) {
                    await SaveBusAsync();
                    await syncAllEvent.Invoke();
                }
                _isBusinessCanBeScan = true;
                Log.Add(_l+"цикл синхронизации завершен");
            } catch (Exception ex) {
                Log.Add(_l + "ошибка синхронизации: " + ex.Message + "\n"
                    + ex.Source + "\n"
                    + ex.InnerException?.Message + "\n"
                    + stage);
                _isBusinessCanBeScan = true;
                _isBusinessNeedRescan = true;
            }
        }

        static async Task<List<RootObject>> GetBusGoodsAsync(List<string> ids) {
            int uMax = 200;
            var iMax = ids.Count % uMax > 0 ? ids.Count / uMax + 1 : ids.Count / uMax;
            List<RootObject> lro = new List<RootObject>();

            for (int i = 0; i < iMax; i++) {
                var d = new Dictionary<string, string>();
                for (int u = 0; u < uMax; u++) {
                    if (u + i * uMax < ids.Count)
                        d.Add("id[" + u + "]", ids[u + i * uMax]);
                    else
                        break;
                }
                //d.Add("archive", "0");
                //d.Add("type", "1");
                //d.Add("limit", pageLimitBase.ToString());
                //d.Add("page", i.ToString());
                d.Add("with_attributes", "1");
                d.Add("with_additional_fields", "1");
                d.Add("with_remains", "1");
                d.Add("with_prices", "1");
                d.Add("type_price_ids[0]", "75524");
                int err = 0;
                do {
                    string s = "";
                    try {
                        s = await RequestAsync("get", "goods", d);
                        s = s
                            .Replace("\"209334\":", "\"drom\":")
                            .Replace("\"209360\":", "\"vk\":")
                            .Replace("\"833179\":", "\"kp\":")
                            .Replace("\"854872\":", "\"gde\":")
                            .Replace("\"854879\":", "\"ozon\":");
                        lro.AddRange(JsonConvert.DeserializeObject<List<RootObject>>(s));
                        await Task.Delay(1000);
                        break;
                    } catch (Exception x) {
                        Log.Add(_l + "ошибка запроса товаров!!! - " + d + " - " + x.Message + " - " + s);
                        err++;
                        await Task.Delay(60000);
                    }
                } while (err < 10);
            }
            return lro;
        }
        public static async Task SaveBusAsync() {
            try {
                await Task.Factory.StartNew(() => {
                    if (DB.GetParamBool("saveDataBaseLocal")) {
                        File.WriteAllText(_busFileName, JsonConvert.SerializeObject(_bus));
                        Log.Add(_l + "bus.json - сохранение успешно");
                        File.SetLastWriteTime(_busFileName, _syncStartTime);
                    }
                });
            } catch (Exception x) {
                Log.Add(_l + "ошибка сохранения локальной базы данных bus.json - " + x.Message);
            }
        }
        public static bool IsWorkTime() {
            var dt = DateTime.Now;
            if (dt.DayOfWeek == DayOfWeek.Sunday)
                return false;
            if (dt.Hour >= 17 || (dt.AddMinutes(30).Hour < 10)) //c 9-30 до 17
                return false;
            if (dt.DayOfWeek == DayOfWeek.Saturday && dt.Hour >= 15) // суб до 15
                return false;
            return true;
        }
        public static async Task AddPartNumsAsync() {
            try {
                var i = 0;
                foreach (var item in _bus.Where(w => (!string.IsNullOrEmpty(w.description)
                                                    && w.description.Contains("№")
                                                    && string.IsNullOrEmpty(w.Part)))) {
                    Log.Add("business.ru: обнаружен пустой артикул - " + item.name);
                    var nums = item.GetDescriptionNumbers();
                    if (nums.Count > 0) {
                        item.part = nums[0];
                        await RequestAsync("put", "goods", new Dictionary<string, string>{
                                {"id", item.id},
                                {"name", item.name},
                                {"part", item.part}
                        });
                        Log.Add("business.ru: добавлен артикул - " + item.part);
                        Thread.Sleep(1000);
                    }
                    i++;
                    //if (i > 10) break;
                }
            } catch (Exception x) {
                Log.Add("business.ru: ошибка при добавлении артикула - " + x.Message);
            }

        }
        public static async Task CheckUrls() {
            for (int b = 0; b < _bus.Count; b++) {
                if (_bus[b].GroupName().Contains("РАЗБОРКА") &&
                    _bus[b].amount == 0 &&
                    _bus[b].price == 0 &&
                    (!string.IsNullOrEmpty(_bus[b].drom) ||
                    !string.IsNullOrEmpty(_bus[b].vk) ||
                    !string.IsNullOrEmpty(_bus[b].kp) ||
                    !string.IsNullOrEmpty(_bus[b].ozon) ||
                    !string.IsNullOrEmpty(_bus[b].gde))) {
                    _bus[b].drom = "";
                    _bus[b].vk = "";
                    _bus[b].kp = "";
                    _bus[b].gde = "";
                    _bus[b].ozon = "";
                    await RequestAsync("put", "goods", new Dictionary<string, string>{
                        {"id", _bus[b].id},
                        {"name", _bus[b].name},
                        {"209334", ""},
                        {"209360", ""},
                        {"833179", ""},
                        {"854872", ""},
                        {"854879", ""}
                    });
                    Log.Add(_bus[b].name + " - удалены ссылки черновика");
                }
            }
        }
        public static async Task CheckArhiveStatusAsync() {
            try {
                foreach (var item in _bus.Where(w => w.amount > 0 && w.archive)) {
                    Log.Add("business.ru: ошибка! карточка с положительным остатком в архиве! - " + item.name);
                    await RequestAsync("put", "goods", new Dictionary<string, string>{
                                {"id", item.id},
                                {"name", item.name},
                                {"archive", "0"}
                        });
                    Log.Add("business.ru: архивный статус отменен! - " + item.name);
                    Thread.Sleep(1000);
                }
            } catch (Exception x) {
                Log.Add("business.ru: ошибка при изменении архивного статуса! - " + x.Message);
            }
        }
        public static async Task CheckBu() => await Task.Factory.StartNew(() => {
            foreach (var b in _bus) {
                var n = b.name;
                if (n.StartsWith(@"Б/У"))
                    b.name = b.name.Replace(@"Б/У", "").Trim() + " Б/У";
                if (n.StartsWith(@"б/у"))
                    b.name = b.name.Replace(@"б/у", "").Trim() + " Б/У";
                if (n.StartsWith(@"Б\У"))
                    b.name = b.name.Replace(@"Б\У", "").Trim() + " Б/У";
                if (n.StartsWith(@"б\у"))
                    b.name = b.name.Replace(@"б\у", "").Trim() + " Б/У";
                if (n.StartsWith(@"Б.У."))
                    b.name = b.name.Replace(@"Б.У.", "").Trim() + " Б/У";
                if (n.StartsWith(@"б.у."))
                    b.name = b.name.Replace(@"б.у.", "").Trim() + " Б/У";
                if (n.StartsWith(@"Б.У"))
                    b.name = b.name.Replace(@"Б.У", "").Trim() + " Б/У";
                if (n.StartsWith(@"Б.у"))
                    b.name = b.name.Replace(@"Б.у", "").Trim() + " Б/У";
                if (n.StartsWith(@"Бу "))
                    b.name = b.name.Replace(@"Бу ", "").Trim() + " Б/У";
                if (n.StartsWith(@"бу "))
                    b.name = b.name.Replace(@"бу ", "").Trim() + " Б/У";
                if (n != b.name)
                    Log.Add("business: исправлено наименование " + n + " -> " + b.name);
            }
        });
        public static async Task CheckDublesAsync() {
            try {
                var count = await DB.GetParamIntAsync("checkDublesCount");
                for (int i = DateTime.Now.Second, cnt = 0; i < _bus.Count && cnt < count; i += 30) {
                    //название без кавычек
                    var tmpName = _bus[i].name.TrimEnd(new[] { '`', ' ', '\'', '.' });
                    //признак дублирования
                    var haveDub = _bus.Count(c => c.name.Contains(tmpName) && c.amount > 0) > 1;
                    //если есть дубли, но нет на остатках и НЕ заканчивается . - переименовываю
                    if (_bus[i].amount <= 0
                        && haveDub
                        && !_bus[i].name.EndsWith(".")) {
                        _bus[i].name = tmpName + " .";
                        await RequestAsync("put", "goods", new Dictionary<string, string> {
                            { "id" , _bus[i].id},
                            { "name" , _bus[i].name}
                        });
                        Log.Add("CheckDublesAsync: переименование [" + ++cnt + "] нет в наличии: " + _bus[i].name);
                        await Task.Delay(1000);
                    }
                    //если есть дубли, остаток и в конце ` или '
                    if (_bus[i].amount > 0
                        && (haveDub && _bus[i].name.EndsWith("`")
                            || _bus[i].name.EndsWith("'")
                            || _bus[i].name.EndsWith("."))) {
                        //проверяем свободные имена
                        while (_bus.Count(c => c.name == tmpName && c.amount > 0) > 0)
                            tmpName += tmpName.EndsWith("`") ? "`"
                                                             : " `";
                        //если новое имя короче или если старое заканчивается ' или .
                        if (tmpName.Length < _bus[i].name.Length || _bus[i].name.EndsWith("'") || _bus[i].name.EndsWith(".")) {
                            Log.Add("CheckDublesAsync: сокращаю наименование [" + ++cnt + "] " + _bus[i].name + " => " + tmpName);
                            _bus[i].name = tmpName;
                            await RequestAsync("put", "goods", new Dictionary<string, string> {
                                { "id" , _bus[i].id},
                                { "name" , _bus[i].name}
                            });
                        }
                    }
                    await Task.Delay(50);
                }
            } catch (Exception x) {
                Log.Add("CheckDublesAsync: ошибка! " + x.Message);
            }
        }
        public static async Task CheckMultipleApostropheAsync() {
            try {
                foreach (var item in _bus.Where(w => (w.name.Contains("''''") || w.name.Contains("' `")) && w.amount > 0)) {
                    Log.Add("business.ru: обнаружено название с множеством апострофов - " + item.name);
                    var s = item.name;
                    while (s.EndsWith("'") || s.EndsWith("`"))
                        s = s.TrimEnd('\'').TrimEnd('`').TrimEnd(' ');
                    item.name = s;
                    await RequestAsync("put", "goods", new Dictionary<string, string>{
                                {"id", item.id},
                                {"name", item.name}
                    });
                    Log.Add("business.ru: исправлено имя с множеством апострофов - " + item.name);
                    Thread.Sleep(1000);
                }
            } catch (Exception x) {
                Log.Add("business.ru: ошибка при переименовании множественных апострофов - " + x.Message);
            }
        }
        public static async Task ArtCheckAsync() {
            if (await DB.GetParamBoolAsync("articlesClear")){
                for (int b = 0; b < _bus.Count; b++) {
                    try {
                        var desc = !String.IsNullOrEmpty(_bus[b].description) ? _bus[b].description.ToLowerInvariant() : "";

                        //проверим на нулевое значение
                        _bus[b].part = _bus[b].part ?? "";
                        _bus[b].store_code = _bus[b].store_code ?? "";
                        if (_bus[b].part.Contains(" ")
                            || _bus[b].part.Contains(".")
                            //|| _bus[b].part.Contains("/") && _bus[b].images.Count > 0
                            //|| _bus[b].part.Contains(",") && _bus[b].images.Count > 0
                            || _bus[b].part.Contains("_")
                            || _bus[b].store_code.Contains(" ")
                            || _bus[b].store_code.Contains(".")
                            || _bus[b].store_code.Contains("_")
                            || _bus[b].store_code.Contains("(")
                            || _bus[b].name.StartsWith(" ")
                            || _bus[b].name.EndsWith(" ")
                            || _bus[b].name.Contains("  ")
                            || _bus[b].name.Contains(";")
                            //|| _bus[b].name.Contains(@"\")
                            || _bus[b].name.Contains("!")
                            || _bus[b].name.Contains("\t")
                            || _bus[b].name.Contains("\u00a0")

                            //|| ((bus[b].name.Contains(" l ") || bus[b].name.Contains(" L ")) && desc.Contains("лев"))
                            //|| ((bus[b].name.Contains(" r ") || bus[b].name.Contains(" R ")) && desc.Contains("прав"))
                            //|| bus[b].name.Contains(" l/r ")
                            //|| bus[b].name.Contains(" L/R ")
                            //|| bus[b].name.Contains(@" l\r ")
                            //|| bus[b].name.Contains(@" L\R ")

                            //или артикул написан маленькими буквами
                            || _bus[b].part.ToUpper() != _bus[b].part

                            //или описание содержит названия месенджеров (запрещено на многих сайтах)
                            || desc.Contains("viber") || desc.Contains("вайбер")
                        ) {
                            //словарь аргументов
                            var args = new Dictionary<string, string>();

                            //удаляем упоминание в тексте мессенжеров
                            if (!String.IsNullOrEmpty(_bus[b].description)) {
                                var newDesc = _bus[b].description
                                                    .Replace("на вайбер", "")
                                                    .Replace("на Вайбер", "")
                                                    .Replace("на viber", "")
                                                    .Replace("на Viber", "")
                                                    .Replace("НА VIBER", "")
                                                    .Replace("НА ВАЙБЕР", "")
                                                    .Replace("viber", "")
                                                    .Replace("Viber", "")
                                                    .Replace("VIBER", "")
                                                    .Replace("вайбер", "")
                                                    .Replace("Вайбер", "")
                                                    .Replace("ВАЙБЕР", "");
                                //если не совпадает - были изменения, значит надо добавить в набор параметров
                                if (newDesc != _bus[b].description) {
                                    args.Add("description", newDesc);
                                    _bus[b].description = newDesc;
                                }
                            }
                            var new_part = _bus[b].part.Replace(" ", "")
                                .Replace(".", "")
                                .Replace("_", "")
                                .ToUpper();

                            var new_store_code = _bus[b].store_code.Replace(" ", "")
                                .Replace(".", "")
                                //.Replace("-", "")
                                .Replace("_", "");

                            var new_name = _bus[b].name.Trim()
                                .Replace("\u00a0", " ")
                                .Replace("  ", " ")
                                .Replace(@"\", " ")
                                .Replace("!", " ")
                                .Replace(";", ",")
                                .Replace("\t", " ");
                            args.Add("id", _bus[b].id);
                            args.Add("name", new_name);
                            args.Add("part", new_part);
                            args.Add("store_code", new_store_code);

                            await RequestAsync("put", "goods", args);

                            Log.Add("исправлена карточка\n" +
                                  _bus[b].name + "\n" +
                                  new_name + "\n" +
                                  _bus[b].part + "\t" + _bus[b].store_code + "\n" +
                                  new_part + "\t" + new_store_code);

                            _bus[b].part = new_part;
                            _bus[b].store_code = new_store_code;
                            _bus[b].name = new_name;
                            await Task.Delay(1000);
                        }
                    } catch (Exception x) {
                        Log.Add("business.ru: " + _bus[b].name + " - ошибка при обработке артикулов! - " + x.Message);
                        Thread.Sleep(10000);
                    }
                }
            }
        }
        public static async Task GroupsMoveAsync() {  //TODO добавиить перемещение по группам карточек с ценами из группы черновики
            for (int b = 0; b < _bus.Count; b++) {
                //перемещение в группу Заказы всех карточек, непривязанных к группам
                if (_bus[b].group_id == "1") {
                    _bus[b].group_id = "209277";
                    await RequestAsync("put", "goods", new Dictionary<string, string>
                    {
                        {"id", _bus[b].id},
                        {"name", _bus[b].name},
                        {"group_id", _bus[b].group_id}
                    });
                    Log.Add("business.ru: " + _bus[b].name + " --> в группу Заказы");
                    await Task.Delay(1000);
                }
            }
        }
        public static async Task PhotoClearAsync() {
            var cnt = await DB.GetParamIntAsync("photosCheckCount");
            var days = await DB.GetParamIntAsync("photosCheckDays");
            if (cnt == 0)
                return;
            //список карточек с фото, но без остатка, с ценой и с поступлениями на карточку, отсортированный с самых старых
            //для новых товаров оставляем фото в карточке в 30 раз дольше (30 дней -> 900)
            var buschk = _bus.Where(b => b.images.Count > 0 &&
                                        b.amount <= 0 &&
                                        b.price > 0 &&
                                        b.remains.Count > 0 &&
                                        DateTime.Now.AddDays(-days * (b.IsNew()?30:1)) > DateTime.Parse(b.updated))
                .OrderBy(o => DateTime.Parse(o.updated))
                .ToList();
            Log.Add("PhotoClearAsync: карточек с фото и ценой без остатка, обновленных более месяца назад: " + buschk.Count);
            var lastDate = DateTime.Now.AddYears(-2).ToString();
            for (int b = 0; cnt > 0 && b < buschk.Count; b++) {
                try {
                    //количество реализаций товара за 2 года
                    var s = await RequestAsync("get", "realizationgoods", new Dictionary<string, string>(){
                                    {"good_id", buschk[b].id},
                                    {"updated[from]", lastDate}
                                });
                    var realizations = JsonConvert.DeserializeObject<List<realizationgoods>>(s)
                                                  .OrderBy(o => DateTime.Parse(o.updated));
                    DateTime controlDate;
                    if (realizations.Any())
                        //за каждую реализацию добавляю 10 дней.
                        controlDate = DateTime.Parse(realizations.Last().updated).AddDays(days + 10 * realizations.Count());
                    else
                        controlDate = DateTime.Parse(buschk[b].updated).AddDays(days);

                    Log.Add("PhotoClearAsync: " + buschk[b].id + " - " + buschk[b].name + ", updated: " + buschk[b].updated + ", реализаций: " + realizations.Count() + ", контрольная дата: " + controlDate);
                    if (DateTime.Now < controlDate) {
                        Log.Add("PhotoClearAsync: пропуск - дата не подошла (Now < controlDate)");
                        continue;
                    }

                    //удаляю фото
                    await RequestAsync("put", "goods", new Dictionary<string, string>(){
                                    {"id", buschk[b].id},
                                    {"name", buschk[b].name},
                                    {"images", "[]"}
                                });
                    Log.Add("PhotoClearAsync: " + buschk[b].name + " - удалены фото из карточки! (" + buschk[b].images.Count + ")");
                    buschk[b].images.Clear();
                    cnt--;
                } catch (Exception x) {
                    Log.Add("ошибка при удалении фото из карточки! - " + _bus[b].name + " - " + x.Message);
                }
            }
        }
        public static async Task ArchivateAsync() {
            var cnt = await DB.GetParamIntAsync("archivateCount");
            if (cnt == 0)
                return;
            //список не архивных карточек без фото, без остатка, отсортированный с самых старых
            var buschk = _bus.Where(w => w.images.Count == 0 && w.amount == 0 && !w.archive)
                .OrderBy(o => DateTime.Parse(o.updated))
                .ToList();
            Log.Add("ArchivateAsync: карточек без фото и остатка: " + buschk.Count);
            for (int b = 0; b < cnt && b < buschk.Count; b++) {
                try {
                    //пропускаю карточки которые обновлялись в течении полугода
                    if (DateTime.Now.AddMonths(-6) < DateTime.Parse(buschk[b].updated))
                        continue;
                    //архивирую карточку
                    await RequestAsync("put", "goods", new Dictionary<string, string>(){
                                    {"id", buschk[b].id},
                                    {"name", buschk[b].name},
                                    {"archive", "1"}
                                });
                    Log.Add("ArchivateAsync: " + buschk[b].name + " - карточка перемещена в архив! (фото " +
                        buschk[b].images.Count + ", остаток " + buschk[b].amount + ", updated " + buschk[b].updated + ")");
                    buschk[b].archive = true;
                } catch (Exception x) {
                    Log.Add("ошибка архивирования карточки! - " + _bus[b].name + " - " + x.Message);
                }
            }
        }
        public static async Task CheckDescriptions() {
            for (int i = _bus.Count - 1; i > -1; i--) {
                if (_bus[i].name.Contains("test")) { await Task.Delay(10); }
                if (_bus[i].description?.Contains("\n") ?? false) {
                    _bus[i].description = _bus[i].description.Replace("\n", "<br />");
                    await RequestAsync("put", "goods", new Dictionary<string, string>() {
                                {"id", _bus[i].id},
                                {"name", _bus[i].name},
                                {"description", _bus[i].description}
                            });
                    Log.Add("CheckDescriptions: " + _bus[i].name + " - описание обновлено\n" + _bus[i].description);
                }
            }
        }
        public static async Task CheckRealisationsAsync() {
            //запрашиваю реализации за последние 2 часа от последней синхронизации
            var s = await RequestAsync("get", "realizations", new Dictionary<string, string>{
                        {"extended", "true"},
                        {"with_goods", "true"},
                        {"updated[from]", _scanTime.AddHours(-1).ToString()},
                    });
            var realizations = JsonConvert.DeserializeObject<List<Realization>>(s);
            //проверяю статус
            foreach (var realization in realizations) {
                if (realization.status.id == "363" || realization.status.name == "ЯНДЕКС МАРКЕТ" ||
                 realization.status.id == "365" || realization.status.name == "ОЗОН") {
                    //если озон или маркет, проверяю цены
                    foreach (var good in realization.goods) {
                        //цена, которая должна быть в реализации
                        var busPrice = _bus.First(f => f.id == good.good.id).price;
                        var priceCorrected = (0.8 * busPrice).ToString("F0");
                        //если цена в реализации отличается, корректируем
                        if (good.price != priceCorrected) {
                            s= await RequestAsync("put", "realizationgoods", new Dictionary<string, string> {
                                { "id", good.id },
                                { "realization_id", realization.id },
                                { "good_id", good.good.id },
                                { "amount", good.amount },
                                { "price", priceCorrected }
                            });
                            if (s.Contains("updated"))
                                Log.Add("CheckRealisationsAsync: "+realization.number+" - " + good.good.name + 
                                    " - цена в отгрузке скорректирована: " + busPrice + " -> " + priceCorrected);
                        }
                    }
                }
            }
        }
        public static async Task LoadBusJSON() {
            if (File.Exists(_busFileName)) {
                await GetBusGroupsAsync();
                Log.Add("business.ru: загружаю список товаров...");
                await Task.Factory.StartNew(() => {
                    var s = File.ReadAllText(_busFileName);
                    _bus = JsonConvert.DeserializeObject<List<RootObject>>(s);
                });
                Log.Add("business.ru: загружено " + _bus.Count + " карточек товаров");
                _labelBusText = _bus.Count + "/" + _bus.Count(c => c.images.Count > 0 && c.price > 0 && c.amount > 0);
                
            }
            _isBusinessCanBeScan = true;
        }
        public static async Task DescriptionsEdit() {
            //загрузим словарь
            List<string> eng = new List<string>();
            List<string> rus = new List<string>();
            List<string> file = new List<string>(File.ReadAllLines(@"..\dict.txt", Encoding.UTF8));
            foreach (var s in file) {
                var ar = s.Split(',');
                eng.Add(ar[0]);
                rus.Add(ar[1]);
            }
            file.Clear();
            //количество изменений за один раз
            var n = await DB.GetParamIntAsync("descriptionEditCount");
            //пробегаемся по описаниям карточек базы
            for (int i = _bus.Count - 1; i > -1 && n > 0; i--) {
                //если в карточке есть фото и остатки
                if (_bus[i].images.Count == 0 /*&& bus[i].amount > 0*/)
                    continue;
                bool flag_need_formEdit = false;
                //старое название, нужно обрезать
                if (_bus[i].description.Contains("Есть и другие")) {
                    _bus[i].description = _bus[i].description.Replace("Есть и другие", "|").Split('|')[0];
                    flag_need_formEdit = true;
                }
                //для каждого слова из словаря проверим, содержится ли оно в описании
                for (int d = 0; d < eng.Count; d++) {
                    //если содержит английское написание И не содержит такого же на русском ИЛИ содержит
                    if (_bus[i].description.Contains(eng[d]) && !_bus[i].description.Contains(rus[d])) {
                        _bus[i].description = _bus[i].description.Replace(eng[d], eng[d] + " / " + rus[d]);
                        flag_need_formEdit = true;
                        break;
                    }
                }
                if (flag_need_formEdit) {
                    Form f4 = new FormEdit(_bus[i]);
                    f4.Owner = Form.ActiveForm;
                    f4.ShowDialog();
                    if (f4.DialogResult == DialogResult.OK) {
                        var s = await RequestAsync("put", "goods", new Dictionary<string, string>() {
                            {"id", _bus[i].id},
                            {"name", _bus[i].name},
                            {"description", _bus[i].description},
                        });
                        if (s.Contains("updated"))
                            Log.Add("business.ru: " + _bus[i].name + " - описание карточки обновлено - " + _bus[i].description + " (ост. " + --n + ")");
                        else
                            Log.Add("business.ru: ошибка сохранения изменений " + _bus[i].name + " - " + s + " (ост. " + --n + ")");
                    }
                    f4.Dispose();
                }
                
            }
            eng.Clear();
            rus.Clear();
        }
        public static async Task CheckPartnersDubles() {
            List<PartnerContactinfoClass> pc = new List<PartnerContactinfoClass>();
            do {
                pc.Clear();
                try {
                    for (int i = 1; i > 0; i++) {
                        string s = await RequestAsync("get", "partnercontactinfo", new Dictionary<string, string>
                        {
                            {"contact_info_type_id","1"},
                            {"limit", _pageLimitBase.ToString()},
                            {"page", i.ToString()}
                        });

                        if (s.Length > 3) {
                            pc.AddRange(JsonConvert.DeserializeObject<PartnerContactinfoClass[]>(s));
                            Log.Add(_l+ "CheckPartnersDubles "+ pc.Count.ToString());
                        } else {
                            break;
                        }
                    }
                } catch (Exception x){
                    Log.Add(_l+ "CheckPartnersDubles - ошибка при запросе информации о контрагентах из бизнес.ру!!" + x.Message+x.InnerException?.Message);
                }
            } while (pc.Count == 0);
            Log.Add(_l+ "CheckPartnersDubles - получено контрагентов " + pc.Count);
            var dub = pc.Where(w => pc.Count(c => c.contact_info
                                        .Contains(w.contact_info
                                            .Replace("-", "")
                                            .Replace(" ", "")
                                            .Replace("(", "")
                                            .Replace(")", "")
                                            .Replace("+", "")
                                            .TrimStart('7')
                                            .TrimStart('8'))
                                    ) > 1)
                        .Select(s=>s.contact_info)
                        .Aggregate((x1,x2)=>x1+"\n"+x2);
            Log.Add(_l+ "CheckPartnersDubles - найдены дубли:\n" + dub);
        }
        //массовая замена текста
        public static async Task ReplaceTextAsync(string checkText, string newText) {
            for (int b = 0; b < _bus.Count; b++) {
                if (_bus[b].images.Count == 0)
                    continue;
                if (!string.IsNullOrEmpty(_bus[b].name) && _bus[b].name.Contains(checkText)) {
                    var newName = _bus[b].name.Replace(checkText, newText);
                    var result = MessageBox.Show(_bus[b].name + "\n\n↓↓↓\n\n" + newName, "Изменить название?", MessageBoxButtons.YesNo);
                    if (result == DialogResult.Yes) {
                        _bus[b].name = newName;
                        var s = await RequestAsync("put", "goods", new Dictionary<string, string> {
                            {"id", _bus[b].id},
                            {"name", _bus[b].name},
                        });
                        if (s.Contains("updated"))
                            Log.Add("ReplaceTextAsync: [" + _bus[b].id + "] " + _bus[b].name + " - название обновлено");
                        else
                            Log.Add("ReplaceTextAsync: ошибка сохранения названия " + _bus[b].name);
                    }
                }
                if (!string.IsNullOrEmpty(_bus[b].description) && _bus[b].description.Contains(checkText)) {
                    var newDescription = _bus[b].description.Replace(checkText, newText);
                    var result = MessageBox.Show(_bus[b].description + "\n\n↓↓↓\n\n" + newDescription, "Изменить описание?", MessageBoxButtons.YesNo);
                    if (result == DialogResult.Yes) {
                        _bus[b].description = newDescription;
                        var s = await RequestAsync("put", "goods", new Dictionary<string, string> {
                            {"id", _bus[b].id},
                            {"name", _bus[b].name},
                            {"description", _bus[b].description},
                        });
                        if (s.Contains("updated"))
                            Log.Add("ReplaceTextAsync: " + _bus[b].name + " - карточка обновлено - " + _bus[b].description);
                        else
                            Log.Add("ReplaceTextAsync: ошибка сохранения изменений " + _bus[b].name + " - " + s);
                    }
                }
            }
        }

        //удаление атрибута в карточке
        public static async Task DeleteAttribute(int b, string atrId) {
            var s = await RequestAsync("get", "goodsattributes", new Dictionary<string, string> {
                                {"good_id",_bus[b].id},
                                {"attribute_id",atrId }
                            });
            var attr = JsonConvert.DeserializeObject<Goodsattributes[]>(s);
            //удаляю привязку по id
            if (attr.Any()) {
                s = await RequestAsync("delete", "goodsattributes", new Dictionary<string, string> {
                                {"id",attr[0].id},
                            });
                if (s.Contains("id")) {
                    Log.Add(b + ": " + _bus[b].name + " - атрибут " + atrId + " удален");
                    Thread.Sleep(360);
                } else {
                    Log.Add(b + ": " + _bus[b].name + "атрибут " + atrId + " - ошибка удаления");
                }
            }
        }
        //замена атрибутов с размерами на поля в карточке
        public static async Task AttributesChange(int i) {
            for (int b = 0; b < _bus.Count && i > 0 && !_isBusinessNeedRescan; b++) {
                try {
                    //удаляю атрибут tiuAttr 
                    if (_bus[b].attributes?.Any(f => f.Attribute.id == "77526") ?? false)
                        await DeleteAttribute(b, "77526");
                    //удаляю атрибут tiuId 
                    if (_bus[b].attributes?.Any(f => f.Attribute.id == "77371") ?? false)
                        await DeleteAttribute(b, "77371");
                    //удаляю атрибут tiuPhotos 
                    if (_bus[b].attributes?.Any(f => f.Attribute.id == "77480") ?? false)
                        await DeleteAttribute(b, "77480");
                    //удаляю атрибут Ключевые слова
                    if (_bus[b].attributes?.Any(f => f.Attribute.id == "77527") ?? false)
                        await DeleteAttribute(b, "77527");
                    //перенос значений атрибутов в новые поля
                    var width = _bus[b].attributes?.Find(f => f.Attribute.id == "2283757")?.Value.value;
                    var height = _bus[b].attributes?.Find(f => f.Attribute.id == "2283758")?.Value.value;
                    var length = _bus[b].attributes?.Find(f => f.Attribute.id == "2283759")?.Value.value;
                    if (!string.IsNullOrEmpty(width) && !string.IsNullOrEmpty(height) && !string.IsNullOrEmpty(length)) {
                        if (width != _bus[b].width || height != _bus[b].height || length != _bus[b].length) {
                            _bus[b].width = width;
                            _bus[b].height = height;
                            _bus[b].length = length;
                            var s = await RequestAsync("put", "goods", new Dictionary<string, string> {
                                    {"id", _bus[b].id},
                                    {"width", _bus[b].width},
                                    {"height", _bus[b].height},
                                    {"length", _bus[b].length}
                            });
                            if (s != null && s.Contains("updated")) {
                                Log.Add(_bus[b].name + " - размеры скопированы! [" + (i--) + "]");
                            } else
                                Log.Add(_bus[b].name + " - ошибка копирования размеров!");
                            await Task.Delay(400);
                        } else {
                            //удаляю атрибут width  
                            await DeleteAttribute(b, "2283757");
                            //удаляю атрибут height  
                            await DeleteAttribute(b, "2283758");
                            //удаляю атрибут length
                            await DeleteAttribute(b, "2283759");
                        }
                    }
                } catch (Exception x) {
                    Log.Add(x.Message);
                }
            }
        }

    }
}

//правильная сортировка на будущее
//string[] sortedKeys = form.AllKeys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
