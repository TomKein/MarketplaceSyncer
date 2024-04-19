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
    public delegate Task SyncEventDelegate();
    public enum CustomerOrderStatus {
            New = 106,
            Process = 108,
            Complete = 110,
            Canceled = 112,
            Ready = 301,
            Markets = 367
        };
    public enum Source {
            Ozon = 1,
            YandexMarket = 2,
            Avito = 3,
            Drom = 4
        };
    public class Class365API {
        static readonly string SECRET = @"A9PiFXQcbabLyOolTRRmf4RR8zxWLlVb";
        static readonly string APP_ID = "768289";
        static readonly string _l = "class365: ";
        static readonly Uri _baseAdr = new Uri("https://action_37041.business.ru/api/rest/");
        static RootResponse _rr = new RootResponse();
        static Dictionary<string, string> _data = new Dictionary<string, string>();
        static HttpClient _hc = new HttpClient();
        public static List<GoodGroupsObject> _busGroups = new List<GoodGroupsObject>();
        public static List<GoodObject> _bus = new List<GoodObject>();
        public static List<GoodObject> lightSyncGoods = new List<GoodObject>();
        private static System.Timers.Timer _timer = new System.Timers.Timer();
        static readonly string BUS_FILE_NAME = @"..\bus.json";
        public static readonly int PAGE_LIMIT_BASE = 250;
        static readonly string ORGANIZATION_ID = "75519";           //ип радченко
        static readonly string RESPONSIBLE_EMPLOYEE_ID = "76221";   //рогачев  76197-радченко
        static readonly string AUTHOR_EMPLOYEE_ID = "76221";        //рогачев  76197-радченко
        static readonly string PARTNER_ID = "1511892";              //клиент с маркетплейса
        public static bool IsBusinessNeedRescan { set; get; } = false;
        public static bool IsBusinessCanBeScan { set; get; } = false;
        public static DateTime SyncStartTime { set; get; }
        public static DateTime ScanTime { set; get; }
        public static DateTime LastScanTime {  set; get; } 
        static Random _rnd = new Random();
        static object _locker = new object();
        static bool _flag = false;
        static string _labelBusText;
        public static string LabelBusText {
            get {
                return _labelBusText;
            }
            set {
                _labelBusText = value;
                updatePropertiesEvent.Invoke();
            }
        }

        public static event SyncEventDelegate syncAllEvent = null;
        public static event SyncEventDelegate updatePropertiesEvent = null;


        static Class365API() {
            _timer.Interval = 20000;
            _timer.Elapsed += timer_sync_Tick;
            _timer.Start();
            syncAllEvent += SyncAllHandlerAsync;
        }
        private static async void timer_sync_Tick(object sender, ElapsedEventArgs e) {
            if (!IsBusinessCanBeScan ||
                DateTime.Now.Hour < DB.GetParamInt("syncStartHour") ||
                DateTime.Now.Hour >= DB.GetParamInt("syncStopHour"))
                return;
            _timer.Stop();
            await GoLiteSync();
            if (IsBusinessNeedRescan)
                await BaseGetAsync();
            _timer.Start();
        }
        public static async Task RepairAsync() {
            await Task.Delay(1000);
            //1
            _data["app_id"] = APP_ID;
            //2
            var sb = new StringBuilder();
            foreach (var key in _data.Keys.OrderBy(x => x, StringComparer.Ordinal)) {
                sb.Append("&")
                  .Append(key)
                  .Append("=")
                  .Append(_data[key]);
            }
            //3
            sb.Remove(0, 1);//убираем первый &
            string ps = sb.ToString();
            //4 - кодировка в мд5
            byte[] hash = Encoding.ASCII.GetBytes(SECRET + ps);
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
        public static async Task<string> RequestAsync(string action, string model, Dictionary<string, string> par) {
            while (_flag) { await Task.Delay(5000); }
            lock (_locker) {
                _flag = true;
            }
            HttpResponseMessage httpResponseMessage = null;

            //1.добавляем к словарю параметров app_id
            par["app_id"] = APP_ID;
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
                    byte[] hash = Encoding.UTF8.GetBytes(_rr.token + SECRET + qstr);
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
                        Thread.Sleep(500);
                        _flag = false;
                        return _rr != null ? JsonConvert.SerializeObject(_rr.result) : "";
                    }
                    Log.Add("business.ru: ошибка запроса - " + httpResponseMessage.StatusCode.ToString());
                    await RepairAsync();
                    qstr = qstr.Contains("&app_psw=") ? qstr.Replace("&app_psw=", "|").Split('|')[0] : qstr;
                    await Task.Delay(20000);
                } catch (Exception x) {
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
        static async Task SyncAllHandlerAsync() {
            await Class365API.AddPartNumsAsync();                   //добавление артикулов из описания
            await Class365API.CheckArhiveStatusAsync();             //проверка архивного статуса
            await Class365API.CheckBu();
            await Class365API.CheckUrls();                          //удаление ссылок из черновиков
            if (Class365API.SyncStartTime.Minute >= 55) {
                await Class365API.CheckDublesAsync();               //проверка дублей
                await Class365API.CheckMultipleApostropheAsync();   //проверка лишних аппострофов
                await Class365API.ArtCheckAsync();                  //очистка артикулов
                await Class365API.GroupsMoveAsync();                //проверка групп
                await Class365API.PhotoClearAsync();                //удаление старых фото
                await Class365API.ArchivateAsync();                 //архивирование карточек
                await Class365API.CheckDescriptions();              //корректировка описаний
            }
            await Class365API.CheckRealisationsAsync();             //проверка реализаций, добавление расходов
            await Class365API.ChangePostingsPrices();               //корректировка цены закупки в оприходованиях
        }

        public static async Task BaseGetAsync() {
            IsBusinessNeedRescan = true;
            IsBusinessCanBeScan = false;
            SyncStartTime = DateTime.Now;
            Log.Add(_l + "старт полного цикла синхронизации");
            await GetBusGroupsAsync();
            await GetBusGoodsAsync2();
            var tlog = _bus.Count + "/" + _bus.Count(c => c.images.Count > 0 && c.Amount > 0 && c.Price > 0);
            Log.Add(_l + "получено товаров с остатками " + tlog);
            LabelBusText = tlog;
            await SaveBusAsync();
            IsBusinessNeedRescan = false;
            await syncAllEvent();
            IsBusinessCanBeScan = true;
            Log.Add(_l + "полный цикл синхронизации завершен");
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
                            {"limit", PAGE_LIMIT_BASE.ToString()},
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
                                .Replace("\"854879\":", "\"ozon\":");
                            _bus.AddRange(JsonConvert.DeserializeObject<List<GoodObject>>(s));
                            Log.Add(_l + "GetBusGoodsAsync2 - получено " + _bus.Count.ToString() + " товаров");
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
                    var tmp2 = JsonConvert.DeserializeObject<List<GoodGroupsObject>>(tmp);
                    _busGroups.AddRange(tmp2);
                } catch (Exception x) {
                    Log.Add(_l + "GetBusGroupsAsync - ошибка запроса групп товаров из базы!!! - " + x.Message + " - " + x.InnerException?.Message);
                    await Task.Delay(60000);
                }
            } while (_busGroups.Count < 30);
            Log.Add(_l + "GetBusGroupsAsync - получено " + _busGroups.Count + " групп товаров");
            GoodObject.Groups = _busGroups;
        }
        public static async Task GoLiteSync() {
            IsBusinessCanBeScan = false;
            try {
                SyncStartTime = DateTime.Now;
                Log.Add(_l + "GoLiteSync - запрос изменений...");
                var liteScanTimeShift = await DB.GetParamIntAsync("liteScanTimeShift");
                var lastLiteScanTime = await DB.GetParamStrAsync("liteScanTime");
                LastScanTime = await DB.GetParamDateTimeAsync("lastScanTime");
                var lastWriteBusFile = File.GetLastWriteTime(BUS_FILE_NAME);
                var isBusFileOld = lastWriteBusFile.AddMinutes(5) < LastScanTime;
                //если файл базы устарел - полный рескан
                if (isBusFileOld || _bus.Count <= 0 || IsBusinessNeedRescan) {
                    Log.Add(@"{_l} GoLiteSync - будет произведен запрос полной базы товаров... isBusFileOld {isBusFileOld}, goods.Count {_bus.Count}, _isBusinessNeedRescan {_isBusinessNeedRescan}");
                    IsBusinessNeedRescan = true;
                    IsBusinessCanBeScan = true;
                    return;
                }
                lightSyncGoods.Clear();
                //новые или измененные карточки товаров
                string s = await RequestAsync("get", "goods", new Dictionary<string, string>{
                            {"type", "1"},
                            {"with_additional_fields", "1"},
                            {"with_attributes", "1"},
                            {"with_remains", "1"},
                            {"with_prices", "1"},
                            {"type_price_ids[0]","75524" },
                            {"updated[from]", lastLiteScanTime}
                    });
                s = s.Replace("\"209334\":", "\"drom\":")
                    .Replace("\"209360\":", "\"vk\":")
                    .Replace("\"854879\":", "\"ozon\":");
                if (s.Length > 3)
                    lightSyncGoods.AddRange(JsonConvert.DeserializeObject<GoodObject[]>(s));
                //карточки товаров c измененным остатком или ценой
                s = await RequestAsync("get", "goods", new Dictionary<string, string>{
                            {"type", "1"},
                            {"with_additional_fields", "1"},
                            {"with_attributes", "1"},
                            {"with_remains", "1"},
                            {"with_prices", "1"},
                            {"type_price_ids[0]","75524" },
                            {"updated_remains_prices[from]", lastLiteScanTime}
                    });
                s = s.Replace("\"209334\":", "\"drom\":")
                    .Replace("\"209360\":", "\"vk\":")
                    .Replace("\"854879\":", "\"ozon\":");
                if (s.Length > 3) {
                    var goods = JsonConvert.DeserializeObject<GoodObject[]>(s);
                    foreach (var good in goods) {
                        if (lightSyncGoods.Any(g => g.id == good.id))
                            continue;
                        lightSyncGoods.Add(good);
                    }
                }
                //если изменений слишком много или сменился день - нужен полный рескан базы
                if (lightSyncGoods.Count >= 250 ||
                    LastScanTime.Day != DateTime.Now.Day) {
                    Log.Add(_l + "будет произведен запрос полной базы товаров...");
                    IsBusinessNeedRescan = true;
                    IsBusinessCanBeScan = true;
                    return;
                }
                Log.Add(_l + "изменены карточки: " + lightSyncGoods.Count + " (с фото, ценой и остатками: " +
                    lightSyncGoods.Count(c => c.Amount > 0 && c.Price > 0 && c.images.Count > 0) + ")");
                foreach (var item in lightSyncGoods) {
                    Log.Add($"{_l} {item.name} (цена { item.Price}, своб. остаток { item.Amount}, резерв {item.Reserve })");
                }
                //переносим обновления в загруженную базу
                foreach (var lg in lightSyncGoods) {
                    var ind = _bus.FindIndex(f => f.id == lg.id);
                    if (ind != -1) { //индекс карточки найден - заменяем
                        _bus[ind] = lg;
                        //время обновления карточки сдвигаю на время начала цикла синхронизации
                        //для того чтобы обновлялись карточки, у которых изменился только остаток или цена
                        _bus[ind].updated = SyncStartTime.ToString();
                    } else { //иначе добавляем в коллекцию
                        if(!lg.name.EndsWith("(копия)")) //пропускаем карточки с признаком Копия! (ломают синхронизацию из-за ссылок)
                            _bus.Add(lg);
                    }
                }

                //база теперь должна быть в актуальном состоянии
                //сохраняем время, на которое база актуальна (только liteScanTime! 
                //lastScanTime сохраним когда перенесем изменения в объявления!!)
                //когда реализуем перенос изменения сразу - можно будет оперировать только одной переменной                

                LabelBusText = _bus.Count + "/" + _bus.Count(c => c.Amount > 0 && c.Price > 0 && c.images.Count > 0);
                await DB.SetParamAsync("liteScanTime", SyncStartTime.AddMinutes(-liteScanTimeShift).ToString());
                await DB.SetParamAsync("controlBus", _bus.Count.ToString());

                ///вызываем событие в котором сообщаем о том, что в базе есть изменения... на будущее, когда будем переходить на событийную модель
                ///пока события не реализованы в проекте, поэтому пока мы лишь обновили уже загруженной базе только те карточки, 
                ///которые изменились с момента последнего запроса
                ///и дальше сдвинем контрольное время актуальности базы
                ///а дальше всё как обычно, только сайты больше не парсим,
                ///только вызываем методы обработки изменений и подъема упавших

                if (DateTime.Now.Minute % 15 > 10 && (DateTime.Now.AddMinutes(-5) > ScanTime) ||
                    LastScanTime.AddHours(1) < DateTime.Now) {
                    await SaveBusAsync();
                    await syncAllEvent.Invoke();
                }
                IsBusinessCanBeScan = true;
                Log.Add(_l + "цикл синхронизации завершен");
            } catch (Exception ex) {
                Log.Add(_l + "ошибка синхронизации: " + ex.Message + "\n"
                    + ex.Source + "\n"
                    + ex.InnerException?.Message);
                IsBusinessCanBeScan = true;
                IsBusinessNeedRescan = true;
            }
        }

        static async Task<List<GoodObject>> GetBusGoodsAsync(List<string> ids) {
            int requestLimit = 10;
            List<GoodObject> lro = new List<GoodObject>();
            while (ids.Count > 0) {
                var requestId = ids.Take(requestLimit).ToList();
                ids = ids.Skip(requestLimit).ToList();
                var d = new Dictionary<string, string>();
                for (int i = 0; i < requestId.Count; i++) 
                    d.Add("id[" + i + "]", requestId[i]);
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
                            .Replace("\"854879\":", "\"ozon\":");
                        lro.AddRange(JsonConvert.DeserializeObject<List<GoodObject>>(s));
                        await Task.Delay(100);
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
                        File.WriteAllText(BUS_FILE_NAME, JsonConvert.SerializeObject(_bus));
                        Log.Add(_l + "bus.json - сохранение успешно");
                        File.SetLastWriteTime(BUS_FILE_NAME, SyncStartTime);
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
                if ((_bus[b].GroupName().Contains("РАЗБОРКА")||
                    _bus[b].name.EndsWith("(копия)") ||
                    (_bus[b].Amount == 0 && _bus[b].Reserve == 0 && _bus[b].Price==0))
                    &&
                    (!string.IsNullOrEmpty(_bus[b].drom) ||
                    !string.IsNullOrEmpty(_bus[b].vk) ||
                    !string.IsNullOrEmpty(_bus[b].ozon))) {
                    _bus[b].drom = "";
                    _bus[b].vk = "";
                    _bus[b].ozon = "";
                    await RequestAsync("put", "goods", new Dictionary<string, string>{
                        {"id", _bus[b].id},
                        {"name", _bus[b].name},
                        {"209334", ""},
                        {"209360", ""},
                        {"854879", ""}
                    });
                    Log.Add(_bus[b].name + " - удалены ссылки черновика");
                }
            }
        }
        public static async Task CheckArhiveStatusAsync() {
            try {
                foreach (var item in _bus.Where(w => (w.Amount > 0 || w.images.Count>0) && w.archive)) {
                    Log.Add("business.ru: ошибка! карточка с положительным остатком или с фото в архиве! - " + item.name);
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
                //todo use regex instead!! regex.replace...
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
                    var haveDub = _bus.Count(c => c.name.Contains(tmpName) && c.Amount > 0) > 1;
                    //если есть дубли, но нет на остатках и НЕ заканчивается . - переименовываю
                    if (_bus[i].Amount <= 0
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
                    if (_bus[i].Amount > 0
                        && (haveDub && _bus[i].name.EndsWith("`")
                            || _bus[i].name.EndsWith("'")
                            || _bus[i].name.EndsWith("."))) {
                        //проверяем свободные имена
                        while (_bus.Count(c => c.name == tmpName && c.Amount > 0) > 0)
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
                foreach (var item in _bus.Where(w => (w.name.Contains("''''") || w.name.Contains("' `")) && w.Amount > 0)) {
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
            if (await DB.GetParamBoolAsync("articlesClear")) {
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
            //todo упростить метод удаления фото, с учетом updated_remains_prices
            var cnt = await DB.GetParamIntAsync("photosCheckCount");
            var days = await DB.GetParamIntAsync("photosCheckDays");
            if (cnt == 0)
                return;
            //список карточек с фото, но без остатка, с ценой и с поступлениями на карточку, отсортированный с самых старых
            //для новых товаров оставляем фото в карточке в 30 раз дольше (30 дней -> 900)
            var buschk = _bus.Where(b => b.images.Count > 0 &&
                                        b.Amount <= 0 &&
                                        b.Reserve <= 0 &&
                                        b.Price > 0 &&
                                        b.remains.Count > 0 &&
                                        DateTime.Now.AddDays(-days * (b.IsNew() ? 30 : 1)) > DateTime.Parse(b.updated)&&
                                        DateTime.Now.AddDays(-days * (b.IsNew() ? 30 : 1)) > DateTime.Parse(b.updated_remains_prices))
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

                    Log.Add($"{_l} PhotoClearAsync: {buschk[b].name} ост. {buschk[b].Amount} рез. {buschk[b].Reserve} фото {buschk[b].images.Count} " +
                        $"upd. {buschk[b].updated} / {buschk[b].updated_remains_prices} реал. {realizations.Count()}, конт. дата: {controlDate}");
                    if (DateTime.Now < controlDate) {
                        Log.Add("PhotoClearAsync: пропуск - дата не подошла");
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
            //todo упростить метод архивирования, с учетом updated_remains_prices
            var cnt = await DB.GetParamIntAsync("archivateCount");
            var index = await DB.GetParamIntAsync("archivateLastIndex");
            if (cnt == 0)
                return;
            //список не архивных карточек без фото, без остатка, отсортированный с самых старых
            var busQuery = _bus.Where(w => w.images.Count == 0 &&
                                      w.Amount <= 0 &&
                                      w.Reserve <= 0 &&
                                      !w.archive &&
                                      DateTime.Now > DateTime.Parse(w.updated).AddMonths(6)&&
                                      DateTime.Now > DateTime.Parse(w.updated_remains_prices).AddMonths(6))
                               .OrderBy(o => DateTime.Parse(o.updated))
                               .Skip(index);
            var queryCount = busQuery.Count();
            Log.Add("ArchivateAsync: карточек для архивирования: " + queryCount);
            if (queryCount == 0)
                index = 0;
            foreach (var b in busQuery) {
                try {
                    //количество реализаций товара за 2 года
                    var s = await RequestAsync("get", "realizationgoods", new Dictionary<string, string>(){
                                    {"good_id", b.id},
                                    {"updated[from]", DateTime.Now.AddYears(-2).ToString()}
                                });
                    var realizations = JsonConvert.DeserializeObject<List<realizationgoods>>(s);
                    Log.Add(b.id + " " + b.name + " реализаций " + realizations.Count);
                    if (realizations.Any()) {
                        index++;
                        continue;
                    }
                    //архивирую карточку
                    await RequestAsync("put", "goods", new Dictionary<string, string>(){
                                    {"id", b.id},
                                    {"name", b.name},
                                    {"archive", "1"}
                                });
                    Log.Add("ArchivateAsync: " + b.id + " " + b.name + " - карточка перемещена в архив! (updated " + b.updated + ") " + cnt);
                    b.archive = true;
                    if (--cnt == 0)
                        break;
                } catch (Exception x) {
                    Log.Add("ошибка архивирования карточки! - " + b.name + " - " + x.Message);
                }
            }
            await DB.SetParamAsync("archivateLastIndex", index.ToString());
        }
        public static async Task CheckDescriptions() {
            var descChkCnt = await DB.GetParamIntAsync("descriptionsCheckCount");
            for (int i = _bus.Count - 1; i > -1 && descChkCnt > 0; i--) {
                try {

                    var needUpdate = false;
                    if (_bus[i].description?.Contains("\t") ?? false) {
                        _bus[i].description = _bus[i].description.Replace("\t", " ");
                        needUpdate = true;
                        Log.Add("CheckDescriptions: " + i + " " + _bus[i].name + " - удалена табуляция");
                    }
                    if (_bus[i].description?.Contains("!!!") ?? false) {
                        _bus[i].description = _bus[i].description.Replace("!!!", "!!");
                        needUpdate = true;
                        Log.Add("CheckDescriptions: " + i + " " + _bus[i].name + " - удалены лишние восклицания");
                    }
                    if (_bus[i].description?.Contains("</p><br />") ?? false) {
                        _bus[i].description = _bus[i].description.Replace("</p><br />", "</p>");
                        needUpdate = true;
                        Log.Add("CheckDescriptions: " + i + " " + _bus[i].name + " - удалены лишние переносы");
                    }
                    if (_bus[i].description?.Contains("<br><br>") ?? false) {
                        _bus[i].description = _bus[i].description.Replace("<br><br>", "<br>");
                        needUpdate = true;
                        Log.Add("CheckDescriptions: " + i + " " + _bus[i].name + " - удалены лишние переносы");
                    }
                    if (_bus[i].description?.Contains("<br /><br />") ?? false) {
                        _bus[i].description = _bus[i].description.Replace("<br /><br />", "<br>");
                        needUpdate = true;
                        Log.Add("CheckDescriptions: " + i + " " + _bus[i].name + " - удалены лишние переносы");
                    }
                    if (_bus[i].description?.Contains("<p></p>") ?? false) {
                        _bus[i].description = _bus[i].description.Replace("<p></p>", "");
                        needUpdate = true;
                        Log.Add("CheckDescriptions: " + i + " " + _bus[i].name + " - удалены лишние переносы");
                    }
                    if (_bus[i].description?.Contains("<br></p>") ?? false) {
                        _bus[i].description = _bus[i].description.Replace("<br></p>", "</p>");
                        needUpdate = true;
                        Log.Add("CheckDescriptions: " + i + " " + _bus[i].name + " - удалены лишние переносы");
                    }
                    if (_bus[i].description?.Contains("</p>\n") ?? false) {
                        _bus[i].description = _bus[i].description.Replace("</p>\n", "</p>");
                        needUpdate = true;
                        Log.Add("CheckDescriptions: " + i + " " + _bus[i].name + " - удалены лишние переносы");
                    }
                    if (_bus[i].description?.Contains("&nbsp;") ?? false) {
                        _bus[i].description = _bus[i].description.Replace("&nbsp;", " ");
                        needUpdate = true;
                        Log.Add("CheckDescriptions: " + i + " " + _bus[i].name + " - удалены мягкие пробелы");
                    }
                    if (_bus[i].description?.Contains("&ndash;") ?? false) {
                        _bus[i].description = _bus[i].description.Replace("&ndash;", "-");
                        needUpdate = true;
                        Log.Add("CheckDescriptions: " + i + " " + _bus[i].name + " - замена длинного тире");
                    }
                    if (_bus[i].description?.Contains("&gt;") ?? false) {
                        _bus[i].description = _bus[i].description.Replace("&gt;", "-");
                        needUpdate = true;
                        Log.Add("CheckDescriptions: " + i + " " + _bus[i].name + " - замена угловой скобки");
                    }
                    if (_bus[i].description?.Contains("&lt;") ?? false) {
                        _bus[i].description = _bus[i].description.Replace("&lt;", "-");
                        needUpdate = true;
                        Log.Add("CheckDescriptions: " + i + " " + _bus[i].name + " - замена угловой скобки");
                    }
                    if (needUpdate) {
                        await RequestAsync("put", "goods", new Dictionary<string, string>() {
                            {"id", _bus[i].id},
                            {"name", _bus[i].name},
                            {"description", _bus[i].description}
                        });
                        Log.Add("CheckDescriptions: " + _bus[i].description);
                        descChkCnt--;
                    }
                } catch (Exception x) {
                    Log.Add(x.Message);
                }
            }
        }
        public static async Task CheckRealisationsAsync() {
            //запрашиваю реализации за последний час от последней синхронизации
            var s = await RequestAsync("get", "realizations", new Dictionary<string, string>{
                        {"extended", "true"},
                        {"with_goods", "true"},
                        {"updated[from]", ScanTime.AddMinutes(-30).ToString()},
                    });
            var realizations = JsonConvert.DeserializeObject<List<Realization>>(s);
            //проверяю статус
            foreach (var realization in realizations) {
                if (realization.status.id == "363" || realization.status.name == "ЯНДЕКС МАРКЕТ" ||
                 realization.status.id == "365" || realization.status.name == "ОЗОН") {
                    //если озон или маркет, проверяю цены
                    foreach (var good in realization.goods) {
                        //цена, которая должна быть в реализации
                        var busPrice = _bus.First(f => f.id == good.good.id).Price;
                        var priceCorrected = (0.75 * busPrice).ToString("F0");
                        //если цена в реализации отличается, корректируем
                        if (good.price != priceCorrected) {
                            s = await RequestAsync("put", "realizationgoods", new Dictionary<string, string> {
                                { "id", good.id },
                                { "realization_id", realization.id },
                                { "good_id", good.good.id },
                                { "amount", good.amount },
                                { "price", priceCorrected }
                            });
                            if (s.Contains("updated"))
                                Log.Add("CheckRealisationsAsync: " + realization.number + " - " + good.good.name +
                                    " - цена в отгрузке скорректирована: " + busPrice + " -> " + priceCorrected);
                        }
                    }
                }
            }
        }
        public static async Task LoadBusJSON() {
            if (File.Exists(BUS_FILE_NAME)) {
                await GetBusGroupsAsync();
                Log.Add("business.ru: загружаю список товаров...");
                await Task.Factory.StartNew(() => {
                    var s = File.ReadAllText(BUS_FILE_NAME);
                    _bus = JsonConvert.DeserializeObject<List<GoodObject>>(s);
                });
                Log.Add("business.ru: загружено " + _bus.Count + " карточек товаров");
                LabelBusText = _bus.Count + "/" + _bus.Count(c => c.images.Count > 0 && c.Price > 0 && c.Amount > 0);

            }
            IsBusinessCanBeScan = true;
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

                if (_bus[i].description
                           .Split('<')
                           .Skip(1)
                           .Any(w => !w.StartsWith("p>") && 
                                     !w.StartsWith("br") && 
                                     !w.StartsWith("/p"))) 
                    flag_need_formEdit = true;
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
                            {"limit", PAGE_LIMIT_BASE.ToString()},
                            {"page", i.ToString()}
                        });

                        if (s.Length > 3) {
                            pc.AddRange(JsonConvert.DeserializeObject<PartnerContactinfoClass[]>(s));
                            Log.Add(_l + "CheckPartnersDubles " + pc.Count.ToString());
                        } else {
                            break;
                        }
                    }
                } catch (Exception x) {
                    Log.Add(_l + "CheckPartnersDubles - ошибка при запросе информации о контрагентах из бизнес.ру!!" + x.Message + x.InnerException?.Message);
                }
            } while (pc.Count == 0);
            Log.Add(_l + "CheckPartnersDubles - получено контрагентов " + pc.Count);
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
                        .Select(s => s.contact_info)
                        .Aggregate((x1, x2) => x1 + "\n" + x2);
            Log.Add(_l + "CheckPartnersDubles - найдены дубли:\n" + dub);
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
            for (int b = 0; b < _bus.Count && i > 0 && !IsBusinessNeedRescan; b++) {
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
        //массовое изменение цен закупки на товары введенных на остатки
        public static async Task ChangeRemainsPrices(int procent = 80) { //TODO переделать, чтобы метод получал список измененных карточек, а не перебирал все
            //цикл для пагинации запросов
            for (int i = 1; ; i++) {
                //запрашиваю товары из документов "ввод на остатки"
                var s = await Class365API.RequestAsync("get", "remaingoods", new Dictionary<string, string> {
                        {"limit", Class365API.PAGE_LIMIT_BASE.ToString()},
                        {"page", i.ToString()},
                    });
                //если запрос товаров вернул пустой ответ - товары закончились - прерываю цикл
                if (s.Length <= 4)
                    break;
                //десериализую json в список товаров
                var remainGoods = JsonConvert.DeserializeObject<List<RemainGoods>>(s);
                //перебираю товары из списка
                foreach (var rg in remainGoods) {
                    //индекс карточки товара
                    var indBus = Class365API._bus.FindIndex(f => f.id == rg.good_id);
                    //если индекс и остаток положительный
                    if (indBus > -1 && Class365API._bus[indBus].Amount > 0) {
                        //цена ввода на остатки (цена закупки)
                        var priceIn = rg.FloatPrice;
                        //цена отдачи (розничная)
                        var priceOut = Class365API._bus[indBus].Price;
                        //процент цены закупки от цены отдачи
                        var procentCurrent = 100 * priceIn / priceOut;
                        //если процент различается более чем на 5%
                        if (Math.Abs(procentCurrent - procent) > 5) {
                            //новая цена закупки
                            var newPrice = priceOut * procent * 0.01;
                            //- меняем цену закупки
                            s = await Class365API.RequestAsync("put", "remaingoods", new Dictionary<string, string> {
                                { "id", rg.id },
                                { "remain_id",rg.remain_id },
                                { "good_id", rg.good_id},
                                { "price", newPrice.ToString("#.##")},
                            });
                            if (!string.IsNullOrEmpty(s) && s.Contains("updated"))
                                Log.Add("business.ru: " + Class365API._bus[indBus].name + " - цена закупки изменена с " + priceIn + " на " + newPrice.ToString("#.##"));
                            await Task.Delay(50);
                        }
                    }
                }
            }
        }
        //массовое изменение цен закупки в оприходованиях
        public static async Task ChangePostingsPrices(int procent = 80) {//TODO переделать, чтобы метод получал список измененных карточек, а не перебирал все
            //цикл для пагинации запросов
            for (int i = 1; ; i++) {
                //запрашиваю товары из документов "Оприходования"
                var s = await Class365API.RequestAsync("get", "postinggoods", new Dictionary<string, string> {
                        {"limit", Class365API.PAGE_LIMIT_BASE.ToString()},
                        {"updated[from]", DateTime.Now.AddHours(-10).ToString("dd.MM.yyyy")},
                        {"page", i.ToString()},
                    });
                //если запрос товаров вернул пустой ответ - товары закончились - прерываю цикл
                if (s.Length <= 4)
                    break;
                //десериализую json в список товаров
                var postingGoods = JsonConvert.DeserializeObject<List<PostingGoods>>(s);
                //перебираю товары из списка
                foreach (var pg in postingGoods) {
                    //индекс карточки товара
                    var indBus = _bus.FindIndex(f => f.id == pg.good_id);
                    //если индекс и остаток положительный
                    if (indBus > -1 && _bus[indBus].Amount > 0 && _bus[indBus].Price > 0) {
                        //цена ввода на остатки (цена закупки)
                        var priceIn = pg.Price;
                        //цена отдачи (розничная)
                        var priceOut = _bus[indBus].Price;
                        //процент цены закупки от цены отдачи
                        var procentCurrent = 100 * priceIn / priceOut;
                        //если процент различается более чем на 5%
                        if (Math.Abs(procentCurrent - procent) > 5) {
                            //новая цена закупки
                            var newPrice = priceOut * procent * 0.01;
                            //- меняем цену закупки
                            s = await RequestAsync("put", "postinggoods", new Dictionary<string, string> {
                                { "id", pg.id },
                                { "posting_id",pg.posting_id },
                                { "good_id", pg.good_id},
                                { "price", newPrice.ToString("#.##")},
                            });
                            if (!string.IsNullOrEmpty(s) && s.Contains("updated"))
                                Log.Add("business.ru: " + _bus[indBus].name + " - цена оприходования изменена с " + priceIn + " на " + newPrice.ToString("#.##"));
                            await Task.Delay(1000);
                        }
                    }
                }
            }
        }
        //отчет остатки по уровню цен
        public static async Task PriceLevelsRemainsReport() => await Task.Factory.StartNew(() => {
            var priceLevelsStr = DB.GetParamStr("priceLevelsForRemainsReport");
            var priceLevels = JsonConvert.DeserializeObject<int[]>(priceLevelsStr);
            foreach (var price in priceLevels) {
                var x = _bus.Count(w => w.images.Count > 0 && w.Price >= price && w.Amount > 0);
                Log.Add("позиций фото, положительным остатком и ценой " + price + "+ : " + x);
            }
        });
        //создать резерв товара
        public static async Task<bool> MakeReserve(Source source, string comment, Dictionary<string,int> goods, string dt = null) {
            if (dt==null) dt = DateTime.Now.ToString();
            Log.Add("MakeReserve - проверка резерва товаров для заказа с "+source+": " 
                + goods.Select(g => g.Key).Aggregate((a, b) => a + ", " + b));
            if (goods.Count <= 0) return false;
            //проверка резерва перед созданием
            var s = await RequestAsync("get", "customerorders", new Dictionary<string, string> {
                                { "request_source_id", ((int)source).ToString() },              //источник заказа
                                { "comment", comment },                                         //комментарий
                            });
            if (s == null || s.Length > 4) {
                Log.Add("MakeReserve - заказ уже существует, действий не требуется!");
                return true;
            }
            Log.Add("MakeReserve - заказ не найден, создаем заказ...");
            //создаем заказ покупателя
            s = await RequestAsync("post", "customerorders", new Dictionary<string, string> {
                                { "author_employee_id", AUTHOR_EMPLOYEE_ID },                   //автора документа
                                { "responsible_employee_id", RESPONSIBLE_EMPLOYEE_ID },         //ответственный за документ
                                { "organization_id", ORGANIZATION_ID },                         //организация
                                { "partner_id", PARTNER_ID },                                   //ссылка на контрагента
                                { "status_id", ((int)CustomerOrderStatus.Markets).ToString() }, //статус заказа покупателя
                                { "comment", comment },                                         //комментарий
                                { "request_source_id", ((int)source).ToString() },              //источник заказа
                                { "date",  dt}
                            });
            if (s == null || !s.Contains("updated")) {
                Log.Add(_l+"MakeReserve - ошибка создания заказа!");
                return false;
            }
            Log.Add("MakeReserve - заказ создан");
            var order = JsonConvert.DeserializeObject<Response>(s);
            //создание резерва
            s = await RequestAsync("post", "reservations", new Dictionary<string, string> {
                { "author_employee_id", AUTHOR_EMPLOYEE_ID },                                   //автора документа
                { "organization_id", ORGANIZATION_ID },                                         //организация
                { "partner_id", PARTNER_ID },                                                   //ссылка на контрагента
                { "customer_order_id", order.id },                                              //id заказа
                { "sync_with_order", "true" },                                                  //синхронизировать с заказом
                { "comment", comment },                                                         //комментарий
                { "date",  dt}
            });
            if (s==null || !s.Contains("updated")) {
                Log.Add(_l+"MakeReserve - ошибка создания резерва!");
                return false;
            }
            Log.Add("MakeReserve - резерв для заказа создан");
            //привязка товаров к заказу
            foreach (var good in goods) {
                s = await RequestAsync("post", "customerordergoods", new Dictionary<string, string> {
                                { "customer_order_id", order.id },                              //id заказа 
                                { "good_id",good.Key },                                         //id товара
                                { "amount", good.Value.ToString() },                            //количество товара
                                { "price", _bus.Find(f=>f.id==good.Key).Price.ToString() },     //цена товара
                            });
                if (s == null || !s.Contains("updated")) {
                    Log.Add(_l+ "MakeReserve - ошибка добавления товара в заказ! - "+good.Key);
                    return false;
                }
                Log.Add("MakeReserve - " + good.Key + " товар добавлен в заказ (" + good.Value+")");
                _bus.Find(f => f.id == good.Key).Reserve += good.Value;
                _bus.Find(f => f.id == good.Key).Amount -= good.Value;
            }
            return true;
        }

    }
}

//возможная правильная сортировка
//string[] sortedKeys = form.AllKeys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
