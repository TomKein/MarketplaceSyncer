using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Selen.Base;
using Selen.Tools;

namespace Selen {
    public delegate Task SyncEventDelegate();
    public enum UrlCode {
        Drom = 209334,
        VK = 209360,
        Ozon = 854879,
        WB = 854882
    }
    public enum SyncStatus {
        Waiting,
        ActiveLite,
        ActiveFull,
        NeedUpdate
    }
    public class Class365API {
        static readonly string SETTINGS_FILE = @"..\data\settings.json";
        static readonly string BUS_FILE_NAME = @"..\data\bus.json";
        static readonly string L = "class365: ";
        public static readonly string VERSION;
        static readonly string SECRET;
        static readonly string APP_ID;
        public static readonly int PAGE_LIMIT_BASE;                                     //пагинация запросов к бизнес.ру
        public static string ORGANIZATION_ID;                                           //75519 ип
        static readonly string RESPONSIBLE_EMPLOYEE_ID;                                 //76221 рогачев, 76197 радченко
        static readonly string AUTHOR_EMPLOYEE_ID;                                      //76221 рогачев, 76197 радченко
        static readonly string PARTNER_ID;                                              //1511892 клиент с маркетплейса
        static readonly Uri BASE_URL;
        static RootResponse _rr = new RootResponse();
        static Dictionary<string, string> _data = new Dictionary<string, string>();
        static HttpClient _hc = new HttpClient();
        public static List<GoodGroupsObject> _busGroups = new List<GoodGroupsObject>();
        public static List<GoodObject> _bus = new List<GoodObject>();
        public static List<GoodObject> lightSyncGoods = new List<GoodObject>();
        public static List<AttributesForGoods> _attributesForGoods;
        //поиск товара по id
        public static GoodObject GetGoodById(string id) => _bus.Find(f => f.id == id);
        //источники заказа
        public static List<Class365Source> _source;
        public static Class365Source Source(string name) =>
            _source.Find(f => f.name == name);
        //статусы заказа
        public static List<Class365CustomerOrderStatus> _orderStatuses;
        public static Class365CustomerOrderStatus OrderStatus(string name) =>
            _orderStatuses.Find(f => f.name == name);
        //отпускные цены
        public static List<Class365Prices> _salePrices;
        public static Class365Prices SalePrice =>
            _salePrices.Find(f => f.name.StartsWith("Розничн") || f.name.StartsWith("Отпускн"));
        //закупочные цены
        public static List<Class365Prices> _buyPrices;
        public static Class365Prices BuyPrice =>
            _buyPrices.Find(f => f.name.StartsWith("Закуп"));
        //страны
        public static List<Class365Countries> _countries = new List<Class365Countries>();
        public static Class365Countries Country(string id) => _countries.Find(f => f.id == id);

        private static System.Timers.Timer _timer = new System.Timers.Timer();
        public static int _checkIntervalMinutes = 15;

        //статус синхронизации
        static SyncStatus status;
        public static SyncStatus Status {
            get {
                return status;
            }
            set {
                status = value;
                updatePropertiesEvent?.Invoke();
            }
        }
        //время запуска нового цикла
        public static DateTime SyncStartTime { set; get; }

        //время последнего обновления
        static DateTime lastScanTime;
        public static DateTime LastScanTime { set {
                lastScanTime = value;
                DB.SetParamAsync("lastScanTime", lastScanTime.ToString());
                _lastLiteScanTime = lastScanTime;
                //updatePropertiesEvent.Invoke();
            } get {
                return lastScanTime;
            } }
        //время последнего запроса изменений
        static DateTime _lastLiteScanTime;

        static DateTime lastErrorShowTime;
        public static DateTime LastErrorShowTime {
            set {
                lastErrorShowTime = value;
                DB.SetParamAsync("lastErrorShowTime", lastErrorShowTime.ToString());
            } get {
                return lastErrorShowTime;
            } }
        static DateTime lastWarningShowTime;
        public static DateTime LastWarningShowTime {
            set {
                lastWarningShowTime = value;
                DB.SetParamAsync("lastWarningShowTime", lastWarningShowTime.ToString());
            } get {
                return lastWarningShowTime;
            } }

        //ограничение времени цикла синхронизации (10 мин)
        public static bool IsTimeOver { get {
                return DateTime.Now > SyncStartTime.AddMinutes(_checkIntervalMinutes);
            } }
        static object _lockerRequests = new object();
        static bool _flagRequestActive = false;
        static string _labelBusText;
        public static string LabelBusText {
            get {
                return _labelBusText;
            }
            set {
                _labelBusText = value;
                updatePropertiesEvent?.Invoke();
            }
        }
        public static event SyncEventDelegate syncAllEvent = null;
        public static event SyncEventDelegate updatePropertiesEvent = null;

        static Class365API() {
            try {
                var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                    File.ReadAllText(SETTINGS_FILE));
                VERSION = dict["VERSION"];
                SECRET = dict["SECRET"];
                APP_ID = dict["APP_ID"];
                BASE_URL = new Uri(dict["BASE_URL"]);
                ORGANIZATION_ID = dict["ORGANIZATION_ID"];
                RESPONSIBLE_EMPLOYEE_ID = dict["RESPONSIBLE_EMPLOYEE_ID"];
                AUTHOR_EMPLOYEE_ID = dict["AUTHOR_EMPLOYEE_ID"];
                PARTNER_ID = dict["PARTNER_ID"];
                PAGE_LIMIT_BASE = int.Parse(dict["PAGE_LIMIT_BASE"]);
                _timer.Interval = int.Parse(dict["TIMER_INTERVAL"]);
                _timer.Elapsed += timer_sync_Tick;
                lastScanTime = DB.GetParamDateTime("lastScanTime");
                _lastLiteScanTime = lastScanTime;
                lastErrorShowTime = DB.GetParamDateTime("lastErrorShowTime");
                lastWarningShowTime = DB.GetParamDateTime("lastWarningShowTime");
                
            } catch (Exception ex) {
                Log.Add($"{L}ошибка чтения начальных параметров - {ex.Message}");
                new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
            }
        }
        public static async Task StartSync() {
            try {
                await GetAttributes();
                await GetOrderSources();
                await GetOrderStatuses();
                await GetPriceTypes();
                await GetCountries();
                await LoadGoodListFromFile();
            } catch (Exception x) {
                Log.Add($"{L}StartSync: ошибка загрузки карточек товаров из файла! - {x.Message}");
                Status = SyncStatus.NeedUpdate;
            }
#if DEBUG
//            return;
#endif
            _timer.Start();
        }
        private static async Task LoadGoodListFromFile() {
            if (File.Exists(BUS_FILE_NAME)) {
                await GetBusGroupsAsync();
                await Task.Factory.StartNew(() => {
                    var sb = File.ReadAllText(BUS_FILE_NAME);
                    _bus = JsonConvert.DeserializeObject<List<GoodObject>>(sb);
                });
                Log.Add($"{L}LoadGoodList: загружено " + _bus.Count + " карточек товаров");
                LabelBusText = _bus.Count + "/" + _bus.Count(c => c.images.Count > 0 && c.Price > 0 && c.Amount > 0);
                Status = SyncStatus.Waiting;
            } else {
                Log.Add($"{L}LoadGoodList: предупреждение - файл не найден, будет произведен запрос всех карточек товаров");
                Status = SyncStatus.NeedUpdate;
            }
        }
        private static async Task GetOrderStatuses() {
            if (_orderStatuses == null) {
                var stat = await RequestAsync("get", "customerorderstatus", new Dictionary<string, string>());
                if (stat.Length > 4)
                    _orderStatuses = JsonConvert.DeserializeObject<List<Class365CustomerOrderStatus>>(stat);
            }
        }
        private static async Task GetOrderSources() {
            if (_source == null) {
                var src = await RequestAsync("get", "requestsource", new Dictionary<string, string>());
                if (src.Length > 4)
                    _source = JsonConvert.DeserializeObject<List<Class365Source>>(src);
            }
        }
        private static async Task GetAttributes() {
            var sa = await RequestAsync("get", "attributesforgoods", new Dictionary<string, string>());
            if (sa.Length > 4)
                _attributesForGoods = JsonConvert.DeserializeObject<List<AttributesForGoods>>(sa);
        }
        private static async Task GetPriceTypes() {
            if (_salePrices == null) {
                var sp = await RequestAsync("get", "salepricetypes", new Dictionary<string, string>());
                if (sp.Length > 4)
                    _salePrices = JsonConvert.DeserializeObject<List<Class365Prices>>(sp);
            }
            if (_buyPrices == null) {
                var bp = await RequestAsync("get", "buypricetypes", new Dictionary<string, string>());
                if (bp.Length > 4)
                    _buyPrices = JsonConvert.DeserializeObject<List<Class365Prices>>(bp);
            }
        }
        private static async Task GetCountries() {
            if (_countries.Count == 0) {
                for (int i = 1; ; i++) {
                    var c = await RequestAsync("get", "countries", new Dictionary<string, string> {
                            {"page", i.ToString() },
                            {"limit","200" }
                        });
                    if (c.Length > 4)
                        _countries.AddRange(JsonConvert.DeserializeObject<List<Class365Countries>>(c));
                    else
                        break;
                }
            }
        }

        private static async void timer_sync_Tick(object sender, ElapsedEventArgs e) {
            if (DateTime.Now.Hour < DB.GetParamInt("syncStartHour") ||
                DateTime.Now.Hour >= DB.GetParamInt("syncStopHour"))
                return;
            _timer.Stop();
            if (Status == SyncStatus.NeedUpdate)
                await GoFullSync();
            else if (Status == SyncStatus.Waiting)
                await GoLiteSync();
            _timer.Start();
        }
        public static async Task RepairAsync() {
            await Task.Delay(1000);
            _data["app_id"] = APP_ID;
            var sb = new StringBuilder();
            foreach (var key in _data.Keys.OrderBy(x => x, StringComparer.Ordinal)) {
                sb.Append("&")
                  .Append(key)
                  .Append("=")
                  .Append(_data[key]);
            }
            sb.Remove(0, 1);//убираем первый &
            string ps = sb.ToString();
            byte[] hash = Encoding.ASCII.GetBytes(SECRET + ps);
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] hashenc = md5.ComputeHash(hash);
            ps += "&app_psw=" + GetMd5(hashenc);
            Uri url = new Uri(BASE_URL, "repair.json?" + ps);
            HttpResponseMessage res = await _hc.GetAsync(url);
            var js = await res.Content.ReadAsStringAsync();

            if (res.StatusCode != HttpStatusCode.OK || js.Contains("Превышение лимита")) {
                _rr.token = "";
                Log.Add($"{L}RepairAsync: ошибка получения токена! - {res.StatusCode.ToString()}");
                await Task.Delay(30000);
            } else {
                _rr = JsonConvert.DeserializeObject<RootResponse>(js);
                Log.Add($"{L}RepairAsync: получен новый токен!");
            }
            await Task.Delay(500);
        }
        public static async Task<string> RequestAsync(string action, string model, Dictionary<string, string> par) {
            while (_flagRequestActive) {
                await Task.Delay(3000);
            }
            lock (_lockerRequests) {
                _flagRequestActive = true;
            }
            try {
                HttpResponseMessage httpResponseMessage = null;
                par["app_id"] = APP_ID;
                SortedDictionary<string, string> parSorted = new SortedDictionary<string, string>(new PhpKSort());
                foreach (var key in par.Keys) {
                    parSorted.Add(key, par[key]);
                }
                string qstr = QueryStringBuilder.BuildQueryString(parSorted);
                for (int i = 0; i < 360; i++) {
                    try {
                        while (_rr == null || _rr.token == "" || _rr.token == null) {
                            await RepairAsync();
                        }
                        byte[] hash = Encoding.UTF8.GetBytes(_rr.token + SECRET + qstr);
                        MD5 md5 = new MD5CryptoServiceProvider();
                        byte[] hashenc = md5.ComputeHash(hash);
                        qstr += "&app_psw=" + GetMd5(hashenc);

                        string url = BASE_URL + model + ".json";

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
                            var delay = _rr.request_count * (_rr.request_count / 200);
                            if (_rr.request_count % 10 == 0)
                                Log.Add($"{L}RequestAsync: REQUEST_COUNT={_rr.request_count}, delay={delay}");
                            Thread.Sleep(_rr.request_count * 3);
                            _flagRequestActive = false;
                            return _rr != null ? JsonConvert.SerializeObject(_rr.result) : "";
                        }
                        Log.Add($"{L}RequestAsync: ошибка запроса - {httpResponseMessage.StatusCode.ToString()}");
                        await RepairAsync();
                        qstr = qstr.Contains("&app_psw=") ? qstr.Replace("&app_psw=", "|").Split('|')[0] : qstr;
                        await Task.Delay(20000);
                    } catch (Exception x) {
                        Log.Add($"{L}RequestAsync: ошибка запроса к бизнес.ру [{i}] - {x.Message}");
                        await Task.Delay(20000);
                        qstr = qstr.Contains("&app_psw=") ? qstr.Replace("&app_psw=", "|").Split('|')[0] : qstr;
                        _rr.token = "";
                        _flagRequestActive = false;
                    }
                };
            } catch (Exception x) {
                Log.Add($"{L}RequestAsync - ошибка запроса к бизнес.ру - {x.Message}");
                _flagRequestActive = false;
            }
            return "";
        }
        public class RootResponse {
            public string status { get; set; }
            public object result { get; set; }
            public string token { get; set; }
            public string app_psw { get; set; }
            public int request_count { get; set; }
        }
        private static string GetMd5(byte[] hashenc) {
            StringBuilder md5_str = new StringBuilder();
            foreach (var b in hashenc) {
                md5_str.Append(b.ToString("x2"));
            }
            return md5_str.ToString();
        }
        public static async Task GoFullSync() {
            Status = SyncStatus.ActiveFull;
            SyncStartTime = DateTime.Now;
            Log.Add($"{L}GoFullSync: старт полного цикла синхронизации");
            await GetBusGroupsAsync();
            await GetBusGoodsAsync2();
            var tlog = _bus.Count + "/" + _bus.Count(c => c.images.Count > 0 && c.Amount > 0 && c.Price > 0);
            Log.Add($"{L}GoFullSync: получено товаров с остатками {tlog}");
            LabelBusText = tlog;
            await SaveBusAsync();
            await syncAllEvent();
            Log.Add($"{L}GoFullSync: - полный цикл синхронизации завершен");
            Status = SyncStatus.Waiting;
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
                            {"type_price_ids[0]", SalePrice.id} //"75524"
                        });
                        if (s.Contains("name") && _bus.Count / (lastScan + 1) < 2) {
                            s = s
                                .Replace("\"209334\":", "\"drom\":")
                                .Replace("\"209360\":", "\"vk\":")
                                .Replace("\"854879\":", "\"ozon\":")
                                .Replace("\"854882\":", "\"wb\":");
                            _bus.AddRange(JsonConvert.DeserializeObject<List<GoodObject>>(s));
                            Log.Add($"{L}GetBusGoodsAsync2: получено {_bus.Count.ToString()} товаров");
                        } else
                            break;
                    }
                } catch (Exception x) {
                    Log.Add($"{L}GetBusGoodsAsync2: ошибка при запросе товаров из базы!!! - {x.Message}");
                    new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
                    await Task.Delay(60000);
                }
                await DB.SetParamAsync("controlBus", _bus.Count.ToString());
            } while (_bus.Count == 0 || Math.Abs(_bus.Count - lastScan) / _bus.Count > 0.01);
            Log.Add($"{L}GetBusGoodsAsync2: получено товаров {_bus.Count}");
        }
        public static async Task GetBusGroupsAsync() {
            do {
                _busGroups.Clear();
                try {
                    var tmp = await RequestAsync("get", "groupsofgoods", new Dictionary<string, string> {
                        //{"parent_id", "205352"} // БУ запчасти
                    });
                    var tmp2 = JsonConvert.DeserializeObject<List<GoodGroupsObject>>(tmp);
                    _busGroups.AddRange(tmp2);
                } catch (Exception x) {
                    Log.Add($"{L}GetBusGroupsAsync: ошибка запроса групп товаров из базы! - {x.Message} - {x.InnerException?.Message}");
                    new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
                    await Task.Delay(60000);
                }
            } while (_busGroups.Count < 3);
            Log.Add($"{L}GetBusGroupsAsync: получено {_busGroups.Count} групп товаров");
            GoodObject.Groups = _busGroups;
        }
        public static async Task GoLiteSync() {
            Status = SyncStatus.ActiveLite;
            try {
                SyncStartTime = DateTime.Now;
                Log.Add($"{L}GoLiteSync: запрос изменений...");
                _checkIntervalMinutes = await DB.GetParamIntAsync("checkIntervalMinutes");
                var liteScanTimeShift = await DB.GetParamIntAsync("liteScanTimeShift");
                var lastWriteBusFile = File.GetLastWriteTime(BUS_FILE_NAME);
                var isBusFileOld = lastWriteBusFile.AddMinutes(_checkIntervalMinutes * 2) < LastScanTime;
                //если файл базы устарел - полный рескан
                if (isBusFileOld || _bus.Count == 0) {
                    Log.Add($"{L}GoLiteSync: будет произведен запрос полной базы товаров... isBusFileOld={isBusFileOld}, goods.Count={_bus.Count}, Status={Status}");
                    Status = SyncStatus.NeedUpdate;
                    return;
                }
                lightSyncGoods.Clear();
                //новые или измененные карточки товаров

                string s;
                for (int i = 1; ; i++) {
                    s = await RequestAsync("get", "goods", new Dictionary<string, string>{
                            {"type", "1"},
                            {"with_additional_fields", "1"},
                            {"with_attributes", "1"},
                            {"with_remains", "1"},
                            {"with_prices", "1"},
                            {"type_price_ids[0]",SalePrice.id },
                            {"updated[from]", _lastLiteScanTime.ToString()},
                            {"page", i.ToString()},
                            {"limit", "250"},
                    });
                    s = s.Replace("\"209334\":", "\"drom\":")
                        .Replace("\"209360\":", "\"vk\":")
                        .Replace("\"854879\":", "\"ozon\":")
                        .Replace("\"854882\":", "\"wb\":");
                    if (s.Length > 3)
                        lightSyncGoods.AddRange(JsonConvert.DeserializeObject<GoodObject[]>(s));
                    else
                        break;
                }
                //карточки товаров c измененным остатком или ценой
                s = await RequestAsync("get", "goods", new Dictionary<string, string>{
                            {"type", "1"},
                            {"with_additional_fields", "1"},
                            {"with_attributes", "1"},
                            {"with_remains", "1"},
                            {"with_prices", "1"},
                            {"type_price_ids[0]",SalePrice.id },
                            {"updated_remains_prices[from]", _lastLiteScanTime.ToString()}
                    });
                s = s.Replace("\"209334\":", "\"drom\":")
                    .Replace("\"209360\":", "\"vk\":")
                    .Replace("\"854879\":", "\"ozon\":")
                    .Replace("\"854882\":", "\"wb\":");
                if (s.Length > 3) {
                    var goods = JsonConvert.DeserializeObject<GoodObject[]>(s);
                    foreach (var good in goods) {
                        if (lightSyncGoods.Any(g => g.id == good.id))
                            continue;
                        lightSyncGoods.Add(good);
                    }
                }
                ////если изменений слишком много или сменился день - нужен полный рескан базы
                //if (lightSyncGoods.Count >= 250 ||
                //    LastScanTime.Day != DateTime.Now.Day) {
                //    Log.Add($"{L}GoLiteSync: предупреждение - {lightSyncGoods.Count} изменений, " +
                //        $"{(DateTime.Now- LastScanTime).Hours} ч прошло с последней синхронизации, нужно полное обновление списка товаров...");
                //    Status = SyncStatus.NeedUpdate;
                //    return;
                //}
                Log.Add($"{L}GoLiteSync: изменены карточки: {lightSyncGoods.Count} (с фото, ценой и остатками: " +
                    $"{lightSyncGoods.Count(c => c.Amount > 0 && c.Price > 0 && c.images.Count > 0)})");
                foreach (var item in lightSyncGoods) {
                    Log.Add($"{L}GoLiteSync: {item.name} (цена {item.Price}, своб. остаток {item.Amount}, резерв {item.Reserve})");
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
                        if (!lg.name.EndsWith("(копия)")) //пропускаем карточки с признаком Копия! (ломают синхронизацию из-за ссылок)
                            _bus.Add(lg);
                    }
                }

                //база теперь должна быть в актуальном состоянии
                //сохраняем время, на которое база в кеше актуальна (только liteScanTime!) 
                //lastScanTime обновляем после переноса изменений на сайты!!)

                LabelBusText = _bus.Count + "/" + _bus.Count(c => c.Amount > 0 && c.Price > 0 && c.images.Count > 0);
                _lastLiteScanTime = SyncStartTime.AddMinutes(-liteScanTimeShift);
                await DB.SetParamAsync("controlBus", _bus.Count.ToString());

                if (LastScanTime.AddMinutes(_checkIntervalMinutes) < DateTime.Now || lightSyncGoods.Count>10) {
                    await SaveBusAsync();
                    await syncAllEvent.Invoke();
                }
                Status = SyncStatus.Waiting;
                Log.Add($"{L}GoLiteSync: цикл синхронизации завершен");
            } catch (Exception x) {
                new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
                Log.Add($"{L}GoLiteSync: ошибка синхронизации: {x.Message} - {x.Source} - {x.InnerException?.Message}");
                Status = SyncStatus.NeedUpdate;
            }
        }
        //обновление цен (костыль из-за бага бизнес.ру, убрать когда решится проблема назначения цен)
        private static async Task UpdatePrices() {
            var priceListType = new[] { "salepricelistgoodprices", "buypricelistgoodprices" };
            foreach (var type in priceListType) {
                string s = await RequestAsync("get", type, new Dictionary<string, string>{
                                {"updated[from]", _lastLiteScanTime.AddMinutes(-1).ToString()}
                            });
                var priceLists = JsonConvert.DeserializeObject<List<Class365PriceListGoodPrices>>(s);
                foreach (var priceList in priceLists) { 
                    //меняем цену на 1 копейку
                    s = await RequestAsync("put", type, new Dictionary<string, string>{
                                        {"id", priceList.id},
                                        {"price", (priceList.price+0.01).ToString("#.##").Replace(",",".")}
                        });
                    if (s.Contains("updated"))
                        Log.Add($"{L}UpdatePrices: назначение цен {priceList.id} обновлено => {(priceList.price + 0.01).ToString("#.##")}");
                    else
                        Log.Add($"{L}UpdatePrices: ошибка обновления назначения цен {priceList.id} => {(priceList.price + 0.01).ToString("#.##")}");
                    //теперь меняем обратно
                    s = await RequestAsync("put", type, new Dictionary<string, string>{
                                        {"id", priceList.id},
                                        {"price", priceList.price.ToString("#.##").Replace(",",".")}
                        });
                    if (s.Contains("updated"))
                        Log.Add($"{L}UpdatePrices: назначение цен {priceList.id} обновлено => {priceList.price.ToString("#.##")}");
                    else
                        Log.Add($"{L}UpdatePrices: ошибка обновления назначения цен {priceList.id} => {priceList.price.ToString("#.##")}");
                }
            }
        }
        public static async Task SaveBusAsync() {
            try {
                await Task.Factory.StartNew(() => {
                    if (DB.GetParamBool("saveDataBaseLocal")) {
                        File.WriteAllText(BUS_FILE_NAME, JsonConvert.SerializeObject(_bus));
                        Log.Add($"{L}SaveBusAsync: база товаров сохранена => bus.json");
                        File.SetLastWriteTime(BUS_FILE_NAME, SyncStartTime);
                    }
                });
            } catch (Exception x) {
                Log.Add($"{L}SaveBusAsync: ошибка сохранения базы товаров в bus.json - {x.Message}");
            }
        }
        public static async Task CheckReserve() {
            Log.Add($"{L}CheckReserve: проверяем резервы");
            var s = await RequestAsync("get", "reservations", new Dictionary<string, string>{
                {"responsible_employee_id", ""},
                {"updated[from]", SyncStartTime.AddHours(-2).ToString()},
            });
            var reserves = JsonConvert.DeserializeObject<List<reservations>>(s);
            Log.Add($"{L}CheckReserve: найдено резервов для проверки: {reserves.Count}");
            foreach (var reserve in reserves) {
                if (reserve.responsible_employee_id != null) continue;
                Log.Add($"{L}CheckReserve: проверяем резерв № {reserve.number}");
                s = await RequestAsync("get", "realizations", new Dictionary<string, string>{
                                    {"reservation_id", reserve.id}
                });
                var reals  = JsonConvert.DeserializeObject<List<Realization>>(s);
                if (reals.Any() && reals[0].held) {
                    Log.Add($"{L}CheckReserve: реализация № {reals[0].number} найдена и проведена");
                    //снимаем проведение с реализации
                    s = await RequestAsync("put", "realizations", new Dictionary<string, string> {
                        { "id", reals[0].id},
                        { "held", "0"}
                    });
                    if (s.Contains("update")) 
                        Log.Add($"{L}CheckReserve: проводка реализации отменена");
                    //теперь меняем ответственного в резерве
                    s = await RequestAsync("put", "reservations", new Dictionary<string, string>{
                                    {"id", reserve.id},
                                    {"responsible_employee_id", RESPONSIBLE_EMPLOYEE_ID}
                    });
                    if (s.Contains("update"))
                        Log.Add($"{L}CheckReserve: ответственный в реализации добавлен");
                    //проводим обратно реализацию
                    s = await RequestAsync("put", "realizations", new Dictionary<string, string> {
                        { "id", reals[0].id},
                        { "held", "1"}
                    });
                    if (s.Contains("update")) 
                        Log.Add($"{L}CheckReserve: проводка реализации возвращена");
                }
            }
        }
        public static async Task AddPartNums() {
            try {
                var i = 0;
                foreach (var item in _bus.Where(w => (!string.IsNullOrEmpty(w.description)
                                                    && w.description.Contains("№")
                                                    && string.IsNullOrEmpty(w.Part)))) {
                    if (Status == SyncStatus.NeedUpdate)
                        return;
                    Log.Add($"{L}AddPartNumsAsync: обнаружен пустой артикул - " + item.name);
                    var nums = item.GetDescriptionNumbers();
                    if (nums.Count > 0) {
                        item.part = nums[0];
                        await RequestAsync("put", "goods", new Dictionary<string, string>{
                                {"id", item.id},
                                {"name", item.name},
                                {"part", item.part}
                        });
                        Log.Add($"{L}AddPartNumsAsync: добавлен артикул - " + item.part);
                        Thread.Sleep(1000);
                    }
                    i++;
                }
            } catch (Exception x) {
                Log.Add($"{L}AddPartNumsAsync: ошибка при добавлении артикула - " + x.Message);
            }
        }
        public static async Task ClearOldUrls() {
            for (int b = 0; b < _bus.Count; b++) {
                if (Status == SyncStatus.NeedUpdate)
                    return;
                if ((_bus[b].GroupName.Contains("РАЗБОРКА") ||
                    _bus[b].name.EndsWith("(копия)") ||
                    (_bus[b].Amount == 0 && _bus[b].Reserve == 0 && _bus[b].Price == 0))
                    &&
                    (!string.IsNullOrEmpty(_bus[b].drom) ||
                    !string.IsNullOrEmpty(_bus[b].vk) ||
                    !string.IsNullOrEmpty(_bus[b].Ozon))) {
                    _bus[b].drom = "";
                    _bus[b].vk = "";
                    _bus[b].Ozon = "";
                    _bus[b].WB = "";
                    await RequestAsync("put", "goods", new Dictionary<string, string>{
                        {"id", _bus[b].id},
                        {"name", _bus[b].name},
                        {"209334", ""},
                        {"209360", ""},
                        {"854879", ""},
                        {"854882", ""}
                    });
                    Log.Add($"{L}ClearOldUrls: {_bus[b].id} {_bus[b].name} - удалены ссылки из карточки");
                }
            }
        }
        public static async Task CheckArhiveStatus() {
            try {
                foreach (var item in _bus.Where(w => (w.Amount > 0 /* || w.images.Count > 0*/) && w.archive)) {
                    if (Status == SyncStatus.NeedUpdate)
                        return;
                    Log.Add($"{L}CheckArhiveStatus: {item.name} - карточка в архиве! (ост.: {item.Amount}, фото: {item.images.Count})");
                    await RequestAsync("put", "goods", new Dictionary<string, string>{
                                {"id", item.id},
                                {"name", item.name},
                                {"archive", "0"}
                        });
                    Log.Add($"{L}CheckArhiveStatus: {item.name} - архивный статус отменен!");
                    Thread.Sleep(1000);
                }
            } catch (Exception x) {
                Log.Add($"{L}CheckArhiveStatus: ошибка при изменении архивного статуса! - {x.Message}");
            }
        }
        public static async Task CheckDubles() {
            try {
                var count = await DB.GetParamIntAsync("checkDublesCount");
                for (int i = DateTime.Now.Second, cnt = 0; i < _bus.Count && cnt < count; i += 30) {
                    if (Status == SyncStatus.NeedUpdate)
                        return;
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
                        Log.Add($"{L}CheckDublesAsync: переименование [{++cnt}] нет в наличии: {_bus[i].name}");
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
                            Log.Add($"{L}CheckDublesAsync: сокращаю наименование [{++cnt}] {_bus[i].name} => {tmpName}");
                            _bus[i].name = tmpName;
                            await RequestAsync("put", "goods", new Dictionary<string, string> {
                                { "id" , _bus[i].id},
                                { "name" , _bus[i].name}
                            });
                        }
                    }
                    await Task.Delay(5);
                }
            } catch (Exception x) {
                Log.Add($"{L}CheckDublesAsync: ошибка! {x.Message}");
            }
        }
        public static async Task CheckMultipleApostrophe() {
            try {
                foreach (var item in _bus.Where(w => (w.name.Contains("''''") || w.name.Contains("' `")) && w.Amount > 0)) {
                    if (Status == SyncStatus.NeedUpdate)
                        return;
                    Log.Add($"{L}CheckMultipleApostrophe: обнаружено название с множеством апострофов - {item.name}");
                    var s = item.name;
                    while (s.EndsWith("'") || s.EndsWith("`"))
                        s = s.TrimEnd('\'').TrimEnd('`').TrimEnd(' ');
                    item.name = s;
                    await RequestAsync("put", "goods", new Dictionary<string, string>{
                                {"id", item.id},
                                {"name", item.name}
                    });
                    Log.Add($"{L}CheckMultipleApostrophe: исправлено имя с множеством апострофов - {item.name}");
                    Thread.Sleep(1000);
                }
            } catch (Exception x) {
                Log.Add($"{L}CheckMultipleApostrophe: ошибка при переименовании множественных апострофов - {x.Message}");
            }
        }
        //удаление лишних символов из артикулов
        public static async Task PartsCorrection() {
            //todo use REGEX, replace to CheckGrammarOfTitlesAndDescriptions
            //todo use REGEX to replace cyrillic symbols to latin
            if (await DB.GetParamBoolAsync("articlesClear")) {
                for (int b = 0; b < _bus.Count; b++) {
                    if (Status == SyncStatus.NeedUpdate)
                        return;
                    try {
                        var desc = !String.IsNullOrEmpty(_bus[b].description) ? _bus[b].description.ToLowerInvariant() : "";

                        //проверим на нулевое значение
                        _bus[b].part = _bus[b].part ?? "";
                        _bus[b].store_code = _bus[b].store_code ?? "";
                        if (//_bus[b].part.Contains(" ")
                            //|| _bus[b].part.Contains(".")
                            //|| _bus[b].part.Contains("/") && _bus[b].images.Count > 0
                            //|| _bus[b].part.Contains(",") && _bus[b].images.Count > 0
                            //|| _bus[b].part.Contains("_")
                            //|| _bus[b].store_code.Contains(" ")
                            //|| _bus[b].store_code.Contains(".")
                            //|| _bus[b].store_code.Contains("_")
                            //|| _bus[b].store_code.Contains("(")
                            //||
                            _bus[b].name.StartsWith(" ")
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

                            Log.Add($"{L}ArtCheck: исправлена карточка {_bus[b].name} -> {new_name}, {_bus[b].part} -> {new_part}, {_bus[b].store_code} -> {new_store_code}");

                            _bus[b].part = new_part;
                            _bus[b].store_code = new_store_code;
                            _bus[b].name = new_name;
                            await Task.Delay(1000);
                        }
                    } catch (Exception x) {
                        Log.Add($"{L}ArtCheck: ошибка при обработке артикулов - {_bus[b].name} - {x.Message}");
                        Thread.Sleep(10000);
                    }
                }
            }
        }
        public static async Task GroupsMoveAsync() {  //TODO добавиить перемещение по группам карточек с ценами из группы черновики
            var gid = _busGroups.Find(f => f.name.ToLowerInvariant().Contains("заказы"))?.id;
            if (gid == null) {
                Log.Add($"{L}GroupsMoveAsync: ошибка - группа Заказы не найдена!");
                return;
            }
            for (int b = 0; b < _bus.Count; b++) {
                if (IsTimeOver || Status == SyncStatus.NeedUpdate)
                    return;
                //перемещение в группу Заказы всех карточек, не привязанных к группам
                if (_bus[b].group_id == "1") {
                    _bus[b].group_id = gid;
                    await RequestAsync("put", "goods", new Dictionary<string, string>{
                        {"id", _bus[b].id},
                        {"name", _bus[b].name},
                        {"group_id", _bus[b].group_id}
                    });
                    Log.Add($"{L}GroupsMoveAsync: {_bus[b].name} --> в группу Заказы");
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
                                        DateTime.Now.AddDays(-days * (b.New ? 30 : 1)) > b.Updated 
                                        //DateTime.Now.AddDays(-days * (b.New ? 30 : 1)) > DateTime.Parse(b.updated) &&
                                        //DateTime.Now.AddDays(-days * (b.New ? 30 : 1)) > DateTime.Parse(b.updated_remains_prices)
                                        )
                .OrderBy(o => DateTime.Parse(o.updated))
                .ToList();
            Log.Add($"{L}PhotoClearAsync: карточек с фото и ценой без остатка, обновленных более месяца назад: " + buschk.Count);
            var lastDate = DateTime.Now.AddYears(-2).ToString();
            for (int b = 0; cnt > 0 && b < buschk.Count; b++) {
                if (IsTimeOver || Status == SyncStatus.NeedUpdate)
                    return;
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

                    //Log.Add($"{_l} PhotoClearAsync: {buschk[b].name} ост. {buschk[b].Amount} рез. {buschk[b].Reserve} фото {buschk[b].images.Count} " +
                    //    $"upd. {buschk[b].updated} / {buschk[b].updated_remains_prices} реал. {realizations.Count()}, конт. дата: {controlDate}");
                    if (DateTime.Now < controlDate) {
                        //Log.Add("PhotoClearAsync: пропуск - дата не подошла");
                        continue;
                    }
                    await RequestAsync("put", "goods", new Dictionary<string, string>(){
                                    {"id", buschk[b].id},
                                    {"name", buschk[b].name},
                                    {"images", "[]"}
                                });
                    Log.Add($"{L}PhotoClearAsync: {buschk[b].name} - удалены фото из карточки! ({buschk[b].images.Count})");
                    buschk[b].images.Clear();
                    cnt--;
                } catch (Exception x) {
                    Log.Add($"{L}PhotoClearAsync: ошибка при удалении фото из карточки! - {_bus[b].name} - {x.Message}");
                }
            }
        }
        public static async Task ArchivateAsync() {
            var cnt = await DB.GetParamIntAsync("archivateCount");
            if (cnt == 0)
                return;
            //список не архивных карточек без фото, без остатка, отсортированный с самых старых
            var busQuery = _bus.Where(w => w.images.Count == 0 &&
                                      w.Amount <= 0 &&
                                      w.Reserve <= 0 &&
                                      !w.archive &&
                                      (DateTime.Now > w.Updated.AddYears(3) || (DateTime.Now>w.Updated.AddDays(1) && !w.New)))
                               .OrderBy(o => o.Updated);
            var queryCount = busQuery.Count();
            Log.Add($"{L}ArchivateAsync: карточек для архивирования: {queryCount}");
            foreach (var b in busQuery) {
                if (IsTimeOver || Status == SyncStatus.NeedUpdate)
                    return;
                try {
                    await RequestAsync("put", "goods", new Dictionary<string, string>(){
                                    {"id", b.id},
                                    {"name", b.name},
                                    {"archive", "1"}
                                });
                    Log.Add($"{L}ArchivateAsync: {b.name} id = {b.id}, Updated = {b.Updated} - карточка перемещена в архив! [{cnt}]");
                    b.archive = true;
                    if (--cnt == 0)
                        break;
                } catch (Exception x) {
                    Log.Add($"{L}ArchivateAsync: {b.name} id = {b.id} - ошибка архивирования карточки! - {x.Message}");
                }
            }
        }
        public static async Task CheckGrammarOfTitlesAndDescriptions() {
            var descChkCnt = await DB.GetParamIntAsync("descriptionsCheckCount");
            for (int i = _bus.Count - 1; i > -1 && descChkCnt > 0; i--) {
                if (IsTimeOver || Status == SyncStatus.NeedUpdate)
                    return;
                try {
                    if (_bus[i].description == null || _bus[i].name == null /*|| _bus[i].Amount == 0 || _bus[i].Price == 0*/)
                        continue;
                    var oldName = _bus[i].name;
                    var oldDesc = _bus[i].description;

                    //убираю апперкейс в заголовках
                    //_bus[i].name = Regex.Replace(_bus[i].name, @"\B[А-Я]", m=>m.ToString().ToLower());  //нужно тестить

                    //вынос б/у из начала заголовка объявления в конец
                    _bus[i].name = Regex.Replace(_bus[i].name, @"^([бБ][\.\\/][уУ]\.?|[бБ][уУ] )(.+)", "$2 Б/У");

                    //ставим пробелы перед скобкой ( (кроме пробел. симв. вначале и >)
                    _bus[i].name = Regex.Replace(_bus[i].name, @"([^\s>])\((\S)", "$1 ($2");
                    _bus[i].description = Regex.Replace(_bus[i].description, @"([^\s>])\((\S)", "$1 ($2");
                    //убираем пробелы после скобки (
                    _bus[i].name = Regex.Replace(_bus[i].name, @"\(\s+", "(");
                    _bus[i].description = Regex.Replace(_bus[i].description, @"\(\s+", "(");
                    //ставим пробелы после скобки ) (кроме пробельных символов, запятых, точек, воскл. знаков)
                    _bus[i].name = Regex.Replace(_bus[i].name, @"\)([^\s!:.,<])", ") $1");
                    _bus[i].description = Regex.Replace(_bus[i].description, @"\)([^\s!:.,<])", ") $1");
                    //убираем пробелы перед скобкой )
                    _bus[i].name = Regex.Replace(_bus[i].name, @"\s+\)", ")");
                    _bus[i].description = Regex.Replace(_bus[i].description, @"\s+\)", ")");
                    //добавляем пробел после запятой, кроме дробных чисел
                    _bus[i].name = Regex.Replace(_bus[i].name, @"(\S),([^0-9\s<])", "$1, $2");
                    _bus[i].description = Regex.Replace(_bus[i].description, @"(\S),([^0-9\s<])", "$1, $2");
                    //убираем пробелы перед запятой
                    _bus[i].name = Regex.Replace(_bus[i].name, @"\s+,", ",");
                    _bus[i].description = Regex.Replace(_bus[i].description, @"\s+,", ",");
                    //добавляем пробел после точки, кроме дробных чисел
                    _bus[i].name = Regex.Replace(_bus[i].name, @"(\S)\.([^0-9\s<])", "$1. $2");
                    _bus[i].description = Regex.Replace(_bus[i].description, @"(\S)\.([^0-9\s<])", "$1. $2");
                    //убираем в описания пробел перед точкой
                    //_bus[i].name = Regex.Replace(_bus[i].name, @"\s+\.", ".");
                    _bus[i].description = Regex.Replace(_bus[i].description, @"\s+\.", ".");
                    //добавляем пробел после № 
                    _bus[i].name = Regex.Replace(_bus[i].name, @"№(\S)", "№ $1");
                    _bus[i].description = Regex.Replace(_bus[i].description, @"№(\S)", "№ $1");
                    //добавляем пробел после :
                    _bus[i].name = Regex.Replace(_bus[i].name, @":([^\s<])", ": $1");
                    _bus[i].description = Regex.Replace(_bus[i].description, @":([^\s<])", ": $1");
                    //убираем пробел перед :
                    _bus[i].name = Regex.Replace(_bus[i].name, @"\s+:", ":");
                    _bus[i].description = Regex.Replace(_bus[i].description, @"\s+:", ":");
                    //добавляем пробел между числами и словами :
                    _bus[i].name = Regex.Replace(_bus[i].name, @"(\d+)([а-яА-Я])", "$1 $2");
                    _bus[i].description = Regex.Replace(_bus[i].description, @"(\d+)([а-яА-Я])", "$1 $2");
                    //добавляем пробел между словами и числами :
                    _bus[i].name = Regex.Replace(_bus[i].name, @"([а-я])(\d+)", "$1 $2");
                    _bus[i].description = Regex.Replace(_bus[i].description, @"([а-я])(\d+)", "$1 $2");
                    //меняем Г на г в годах :
                    _bus[i].name = Regex.Replace(_bus[i].name, @"(\d{2,4}) Г", "$1 г");
                    _bus[i].description = Regex.Replace(_bus[i].description, @"(\d{2,4}) Г", "$1 г");

                    _bus[i].Replace("\t");
                    _bus[i].Replace("!!!", "!!");
                    _bus[i].Replace("</p><br />", "</p>");
                    _bus[i].Replace("<br><br>", "<br>");
                    _bus[i].Replace("<br /><br />", "<br>");
                    _bus[i].Replace("<p></p>", "");
                    _bus[i].Replace("<br></p>", "</p>");
                    _bus[i].Replace("</p>\n", "</p>");
                    _bus[i].Replace("&nbsp;", " ");
                    _bus[i].Replace("&zwnj;", " ");
                    _bus[i].Replace("  ", " ");
                    _bus[i].Replace("&ndash;", "-");
                    _bus[i].Replace("&gt;", "-");
                    _bus[i].Replace("'", "`");
                    _bus[i].Replace("``", "`");
                    _bus[i].Replace("&lt;", "-");
                    _bus[i].Replace("&deg;", "град. ");

                    if (oldName != _bus[i].name || oldDesc != _bus[i].description) {
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
        //проверка и коррекция цен в реализациях для отгрузок на маркетплейсы
        public static async Task CheckRealisationsAsync() {
            if (!await DB.GetParamBoolAsync("checkRealisations"))
                return;
            for (int i = 1; ; i++) {
                var s = await RequestAsync("get", "realizations", new Dictionary<string, string>{
                        {"extended", "true"},
                        {"with_goods", "true"},
                        {"updated[from]", LastScanTime.AddMinutes(-10).ToString()},
                        {"limit","250" },
                        {"page",i.ToString() }
                    });
                var realizations = JsonConvert.DeserializeObject<List<Realization>>(s);
                if (realizations.Count == 0)
                    break;
                Log.Add($"CheckRealisationsAsync: проверяем реализаций: {realizations.Count}");
                foreach (var realization in realizations) {
                    if (Status == SyncStatus.NeedUpdate)
                        return;
                    try {
                        if (realization.status == null)
                            continue;
                        if (realization.status.id == "363" || realization.status.name == "ЯНДЕКС МАРКЕТ" ||
                         realization.status.id == "365" || realization.status.name == "ОЗОН" ||
                         realization.status.id == "371" || realization.status.name == "Wildberries") {
                            foreach (var good in realization.goods) {
                                var busPrice = _bus.Find(f => f.id == good.good.id)?.Price ?? 0;
                                if (busPrice == 0)
                                    continue;
                                var priceCorrected = Math.Round(0.75 * busPrice,2).ToString().Replace(",",".");
                                if (good.price != priceCorrected) {
                                    s = await RequestAsync("put", "realizationgoods", new Dictionary<string, string> {
                                { "id", good.id },
                                { "realization_id", realization.id },
                                { "good_id", good.good.id },
                                { "amount", good.amount },
                                { "price", priceCorrected },
                                { "sum", Math.Round(float.Parse(good.amount) * 0.75 * busPrice,2).ToString().Replace(",",".") }
                            });
                                    if (s.Contains("updated"))
                                        Log.Add($"CheckRealisationsAsync: № {realization.number} ({realization.status.name}) - " +
                                            $"{good.good.name} - цена в отгрузке скорректирована: {busPrice} р -> {priceCorrected} р");
                                }
                            }
                        }
                    } catch (Exception x) {
                        Log.Add("CheckRealisationsAsync: ошибка - " + x.Message);
                    }
                }
            }
            Log.Add("CheckRealisationsAsync: метод завершен");
        }
        //обновление описаний
        public static async Task DescriptionsEdit() {
            Form f4 = new FormEdit();
            f4.Owner = Form.ActiveForm;
            f4.ShowDialog();
            f4.Dispose();
        }
        public static async Task CheckPartnersDubles() {
            List<PartnerContactinfoClass> pc = new List<PartnerContactinfoClass>();
            try {
                for (int i = 1; i > 0; i++) {
                    if (Status == SyncStatus.NeedUpdate)
                        return;
                    string s = await RequestAsync("get", "partnercontactinfo", new Dictionary<string, string>
                        {
                            {"contact_info_type_id","1"},
                            {"limit", PAGE_LIMIT_BASE.ToString()},
                            {"page", i.ToString()}
                        });

                    if (s.Length > 3) {
                        pc.AddRange(JsonConvert.DeserializeObject<PartnerContactinfoClass[]>(s));
                        Log.Add(L + "CheckPartnersDubles " + pc.Count.ToString());
                    } else {
                        break;
                    }
                }
            } catch (Exception x) {
                Log.Add(L + "CheckPartnersDubles - ошибка при запросе информации о контрагентах из бизнес.ру!!" + x.Message + x.InnerException?.Message);
            }
            Log.Add(L + "CheckPartnersDubles - получено контрагентов " + pc.Count);
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
            Log.Add(L + "CheckPartnersDubles - найдены дубли:\n" + dub);
        }
        // массовая замена текста (не используется)
        public static async Task ReplaceTextAsync(string checkText, string newText) {
            for (int b = 0; b < _bus.Count; b++) {
                if (Status == SyncStatus.NeedUpdate)
                    return;
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
            var attr = JsonConvert.DeserializeObject<GoodsAttributes[]>(s);
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
        //замена атрибутов с размерами на поля в карточке (не используется)
        public static async Task AttributesChange(int i) {
            for (int b = 0; b < _bus.Count && i > 0; b++) {
                if (Status == SyncStatus.NeedUpdate)
                    return;
                try {
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
        //массовое изменение цен закупки на товары введенных на остатки (не используется)
        public static async Task ChangeRemainsPrices(int procent = 80) { //TODO переделать, чтобы метод получал список измененных карточек, а не перебирал все
            for (int i = 1; ; i++) {
                if (Status == SyncStatus.NeedUpdate)
                    return;
                //запрашиваю товары из документов "ввод на остатки"
                var s = await RequestAsync("get", "remaingoods", new Dictionary<string, string> {
                        {"limit", PAGE_LIMIT_BASE.ToString()},
                        {"page", i.ToString()},
                    });
                if (s.Length <= 4)
                    break;
                var remainGoods = JsonConvert.DeserializeObject<List<RemainGoods>>(s);
                foreach (var rg in remainGoods) {
                    var indBus = _bus.FindIndex(f => f.id == rg.good_id);
                    if (indBus > -1 && _bus[indBus].Amount > 0) {
                        var priceIn = rg.FloatPrice;
                        var priceOut = _bus[indBus].Price;
                        var procentCurrent = 100 * priceIn / priceOut;
                        if (Math.Abs(procentCurrent - procent) > 5) {
                            var newPrice = priceOut * procent * 0.01;
                            s = await RequestAsync("put", "remaingoods", new Dictionary<string, string> {
                                { "id", rg.id },
                                { "remain_id",rg.remain_id },
                                { "good_id", rg.good_id},
                                { "price", newPrice.ToString("#.##")},
                            });
                            if (!string.IsNullOrEmpty(s) && s.Contains("updated"))
                                Log.Add("business.ru: " + _bus[indBus].name + " - цена закупки изменена с " + priceIn + " на " + newPrice.ToString("#.##"));
                            await Task.Delay(50);
                        }
                    }
                }
            }
        }
        //массовое изменение цен закупки в оприходованиях
        public static async Task ChangePostingsPrices(int procent = 80) {//TODO переделать, чтобы метод получал список измененных карточек, а не перебирал все
            if (!await DB.GetParamBoolAsync("changePostingsPrices"))
                return;
            for (int i = 1; ; i++) {
                if (Status == SyncStatus.NeedUpdate)
                    return;
                //запрашиваю товары из документов "Оприходования"
                var s = await RequestAsync("get", "postinggoods", new Dictionary<string, string> {
                        {"limit", PAGE_LIMIT_BASE.ToString()},
                        {"updated[from]", DateTime.Now.AddHours(-10).ToString("dd.MM.yyyy")},
                        {"page", i.ToString()},
                    });
                if (s.Length <= 4)
                    break;
                var postingGoods = JsonConvert.DeserializeObject<List<PostingGoods>>(s);
                foreach (var pg in postingGoods) {
                    var indBus = _bus.FindIndex(f => f.id == pg.good_id);
                    if (indBus > -1 && _bus[indBus].Amount > 0 && _bus[indBus].Price > 0) {
                        var priceIn = pg.Price;
                        var priceOut = _bus[indBus].Price;
                        var procentCurrent = 100 * priceIn / priceOut;
                        if (Math.Abs(procentCurrent - procent) > 5) {
                            var newPrice = priceOut * procent * 0.01;
                            s = await RequestAsync("put", "postinggoods", new Dictionary<string, string> {
                                { "id", pg.id },
                                { "posting_id",pg.posting_id },
                                { "good_id", pg.good_id},
                                { "price", newPrice.ToString("#.##")},
                                { "sum", (newPrice*pg.Amount).ToString("#.##") }
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
        public static async Task<bool> MakeReserve(Class365Source source, string comment, Dictionary<string, int> goods, string dt = null) {
            if (dt == null)
                dt = DateTime.Now.ToString();
            Log.Add("MakeReserve - проверка резерва товаров для заказа с " + source.name + ": "
                + goods.Select(g => g.Key).Aggregate((a, b) => a + ", " + b));
            if (goods.Count <= 0)
                return false;
            //проверка резерва перед созданием
            var s = await RequestAsync("get", "customerorders", new Dictionary<string, string> {
                                { "request_source_id", source.id },              //источник заказа
                                { "comment", comment },                          //комментарий
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
                                { "status_id", OrderStatus("Маркетплейсы").id },                //статус заказа покупателя
                                { "comment", comment },                                         //комментарий
                                { "request_source_id", source.id },                             //источник заказа
                                { "date",  dt}
                            });
            if (s == null || !s.Contains("updated")) {
                Log.Add(L + "MakeReserve - ошибка создания заказа!");
                if (DB.GetParamBool("alertSound"))
                    new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
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
            if (s == null || !s.Contains("updated")) {
                Log.Add(L + "MakeReserve - ошибка создания резерва!");
                if (DB.GetParamBool("alertSound"))
                    new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
                return false;
            }
            Log.Add("MakeReserve - резерв для заказа создан: "+ comment);
            //привязка товаров к заказу
            foreach (var good in goods) {
                s = await RequestAsync("post", "customerordergoods", new Dictionary<string, string> {
                                { "customer_order_id", order.id },                              //id заказа 
                                { "good_id",good.Key },                                         //id товара
                                { "amount", good.Value.ToString() },                            //количество товара
                                { "price", _bus.Find(f=>f.id==good.Key).Price.ToString() },     //цена товара
                            });
                if (s == null || !s.Contains("updated")) {
                    Log.Add(L + "MakeReserve - ошибка добавления товара в заказ! - " + good.Key);
                    if (DB.GetParamBool("alertSound"))
                        new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
                    return false;
                }
                Log.Add("MakeReserve - " + good.Key + " товар добавлен в заказ (" + good.Value + ")");
                var b = _bus.FindIndex(f => f.id == good.Key);
                _bus[b].Reserve += good.Value;
                _bus[b].updated = SyncStartTime.ToString();
            }
            if (DB.GetParamBool("alertOrder"))
                new System.Media.SoundPlayer(@"..\data\neworder.wav").Play();
            return true;
        }
        public static async Task<bool> AddStickerCodeToReserve(Class365Source source, string comment, string commentToAdd) {
            //ищем резерв
            var s = await RequestAsync("get", "reservations", new Dictionary<string, string> {
                                { "request_source_id", source.id },              //источник заказа
                                { "comment", comment },                          //комментарий
                            });
            if (s != null && s.Length > 4) {
                var reservations = JsonConvert.DeserializeObject<List<reservations>>(s);
                //поиск реализации
                s = await RequestAsync("get", "realizations", new Dictionary<string, string> {
                    { "reservation_id", reservations[0].id}
                });
                List<Realization> realization=null;
                if (s != null && s.Length > 4) {
                    realization = JsonConvert.DeserializeObject<List<Realization>>(s);
                    //реализация найдена и проведена - снимаем проведение для изменения резерва
                    if (realization.Any() && realization[0].held) {
                        Log.Add("AddStickerCodeToReserve: реализации найдена "+ realization[0].id);
                        s=await RequestAsync("put", "realizations", new Dictionary<string, string> {
                            { "id", realization[0].id},
                            { "held", "0"}
                        });
                        if (s.Contains("updated"))
                            Log.Add("AddStickerCodeToReserve: проводка реализации отменена");
                        else
                            Log.Add("AddStickerCodeToReserve: ошибка при отмене проведения!");
                    }
                }
                //теперь меняем комментарий в резерве
                var result = await RequestAsync("put", "reservations", new Dictionary<string, string> {
                    { "id", reservations[0].id },
                    { "number", reservations[0].number },
                    { "comment", comment + commentToAdd }
                });
                //и проводим обратно реализацию
                if (realization !=null && realization.Any() && realization[0].held) { 
                        s=await RequestAsync("put", "realizations", new Dictionary<string, string> {
                            { "id", realization[0].id},
                            { "held", "1"}
                        });
                        if (s.Contains("updated"))
                            Log.Add("AddStickerCodeToReserve: реализация проведена");
                        else
                            Log.Add("AddStickerCodeToReserve: ошибка при проведении реализации!");
                }
                if (result != null && result.Contains("updated")) {
                    Log.Add($"AddStickerCodeToReserve: комментарий в резервировании обновлен ({comment + commentToAdd})");
                    return true;    
                } else
                    Log.Add($"AddStickerCodeToReserve: ошибка добавления стикера! {result}");
            }
            return false;
        }
    }
}

//сортировка
//string[] sortedKeys = form.AllKeys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
