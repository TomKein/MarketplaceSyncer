using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Application = System.Windows.Forms.Application;
using Color = System.Drawing.Color;
using Selen.Sites;
using Selen.Tools;
using Selen.Base;
using DocumentFormat.OpenXml.Drawing.Diagrams;

namespace Selen {
    public partial class FormMain : Form {
        string _version = "1.156";

        DB _db = new DB();

        public List<RootGroupsObject> busGroups = new List<RootGroupsObject>();
        public static List<RootObject> _bus = new List<RootObject>();
        public List<RootObject> lightSyncGoods = new List<RootObject>();

        VK _vk = new VK();
        Drom _drom = new Drom();
        Izap24 _izap24 = new Izap24();
        Kupiprodai _kupiprodai = new Kupiprodai();
        GdeRu _gde = new GdeRu();
        Satom _sat = new Satom();
        OzonApi _ozon = new OzonApi();

        string _busFileName = @"..\bus.json";
        int _pageLimitBase = 250;
        bool _saveCookiesBeforeClose;

        //для возврата из форм
        public List<string> lForm3 = new List<string>();
        public string nForm3 = "";
        public string BindedName = "";
        //флаг - нужен рескан базы
        bool _isBusinessNeedRescan = false;
        //флаг - можно запускать новый цикл синхронизации
        bool _isBusinessCanBeScan = false;
        //время запуска очередной синхронизации
        DateTime _syncStartTime;
        DateTime _scanTime;
        //писать лог
        bool _writeLog = true;
        //конструктор формы
        public FormMain() {
            InitializeComponent();
        }
        //========================
        //=== РАБОТА С САЙТАМИ ===
        //========================
        //AVITO.RU
        async void ButtonAvitoRu_Click(object sender, EventArgs e) {
            if (await _db.GetParamBoolAsync("avito.syncEnable")) {
                ChangeStatus(sender, ButtonStates.NoActive);
                try {
                    while (_isBusinessNeedRescan)
                        await Task.Delay(30000);
                    var av = new AvitoXml();
                    await av.GenerateXML(_bus);
                    ChangeStatus(sender, ButtonStates.Active);
                } catch (Exception x) {
                    Log.Add("avito.ru: ошибка синхронизации! - " + x.Message);
                }
            }
        }
        //VK.COM
        async void ButtonVkCom_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                Log.Add("вк начало выгрузки");
                while (_isBusinessNeedRescan)
                    await Task.Delay(20000);
                await _vk.VkSyncAsync(_bus);
                label_Vk.Text = _vk.MarketCount + "/" + _vk.UrlsCount;
                Log.Add("вк выгрузка завершена");
                ChangeStatus(sender, ButtonStates.Active);
            } catch (Exception x) {
                Log.Add("ошибка синхронизации вк: " + x.Message);
                ChangeStatus(sender, ButtonStates.ActiveWithProblem);
            }
        }
        //DROM.RU
        async void ButtonDromRu_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                while (_isBusinessNeedRescan)
                    await Task.Delay(30000);
                await _drom.DromStartAsync(_bus);
                label_Drom.Text = _bus.Count(c => !string.IsNullOrEmpty(c.drom) && c.drom.Contains("http") && c.amount > 0).ToString();
                ChangeStatus(sender, ButtonStates.Active);
            } catch (Exception x) {
                Log.Add("drom.ru: ошибка синхронизации! \n" + x.Message + "\n" + x.InnerException.Message);
                if (x.Message.Contains("timed out") ||
                    x.Message.Contains("already closed") ||
                    x.Message.Contains("invalid session id") ||
                    x.Message.Contains("chrome not reachable")) {
                    _drom.Quit();
                    await Task.Delay(60000);
                    ButtonDromRu_Click(sender, e);
                }
                ChangeStatus(sender, ButtonStates.ActiveWithProblem);
            }
        }
        //KUPIPRODAI.RU
        async void ButtonKupiprodaiRu_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (_isBusinessNeedRescan || !button_KupiprodaiAdd.Enabled)
                await Task.Delay(30000);
            if (await _kupiprodai.StartAsync(_bus))
                ChangeStatus(sender, ButtonStates.Active);
            else
                ChangeStatus(sender, ButtonStates.ActiveWithProblem);
            label_Kp.Text = _bus.Count(c => !string.IsNullOrEmpty(c.kp) && c.kp.Contains("http") && c.amount > 0).ToString();
        }
        async void ButtonKupiprodaiRuAdd_Click(object sender, EventArgs e) {
            if (!button_Kupiprodai.Enabled)
                return;
            ChangeStatus(sender, ButtonStates.NoActive);
            while (_isBusinessNeedRescan)
                await Task.Delay(30000);
            await _kupiprodai.AddAsync();
            label_Kp.Text = _bus.Count(c => !string.IsNullOrEmpty(c.kp) && c.kp.Contains("http") && c.amount > 0).ToString();
            ChangeStatus(sender, ButtonStates.Active);
        }
        //GDE.RU
        async void ButtonGdeRu_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (_isBusinessNeedRescan || _bus.Count == 0)
                await Task.Delay(30000);
            if (await _gde.StartAsync(_bus)) {
                label_Gde.Text = _bus.Count(c => c.gde != null && c.gde.Contains("http")).ToString();
                ChangeStatus(sender, ButtonStates.Active);
            } else
                ChangeStatus(sender, ButtonStates.ActiveWithProblem);
        }
        //IZAP24.RU
        async void ButtonIzap24_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (_isBusinessNeedRescan)
                await Task.Delay(60000);
            if (await _izap24.SyncAsync(_bus))
                ChangeStatus(sender, ButtonStates.Active);
            else
                ChangeStatus(sender, ButtonStates.ActiveWithProblem);
        }
        //YOULA.RU
        async void ButtonYoula_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (_isBusinessNeedRescan || _bus.Count == 0)
                await Task.Delay(30000);
            var youlaXml = new YoulaXml();
            await youlaXml.GenerateXML_avito(_bus);
            ChangeStatus(sender, ButtonStates.Active);
        }
        //SATOM.RU
        async void ButtonSatom_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (_isBusinessNeedRescan || _bus.Count == 0)
                await Task.Delay(30000);
            await _sat.SyncAsync(_bus);
            ChangeStatus(sender, ButtonStates.Active);
        }
        //YANDEX MARKET
        async void ButtonYandexMarket_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            if (IsWorkTime()) {
                while (_isBusinessNeedRescan || _bus.Count == 0)
                    await Task.Delay(30000);
                var yandexMarket = new YandexMarket();
                await yandexMarket.GenerateXML(_bus);
            }
            ChangeStatus(sender, ButtonStates.Active);
        }
        //OZON
        async void button_ozon_ClickAsync(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (_isBusinessNeedRescan || _bus.Count == 0)
                await Task.Delay(30000);
            await _ozon.SyncAsync();
            ChangeStatus(sender, ButtonStates.Active);
        }

        //===========================================
        //=== основной цикл (частота 1 раз в мин) ===
        //===========================================
        private async void timer_sync_Tick(object sender, EventArgs e) {
            if (!_isBusinessCanBeScan)
                return;
            if (DateTime.Now.Hour < await _db.GetParamIntAsync("syncStartHour"))
                return;
            if (DateTime.Now.Hour >= await _db.GetParamIntAsync("syncStopHour"))
                return;
            if (await _db.GetParamBoolAsync("useLiteSync"))
                await GoLiteSync();
            if (DateTime.Now.AddDays(-6) > dateTimePicker1.Value || _isBusinessNeedRescan)
                button_BaseGet.PerformClick();
        }
        //оптимизированная синхронизации - запрос только последних изменений
        async Task GoLiteSync() {
            if (_bus.Count <= 0 || _isBusinessNeedRescan)
                return;
            ChangeStatus(button_BaseGet, ButtonStates.NoActive);
            var checkSyncStatus = checkBox_SyncSites.Checked;
            var stage = "";
            try {
                _isBusinessCanBeScan = false;
                _syncStartTime = DateTime.Now;
                Log.Add("business.ru: запрос изменений...");
                var liteScanTimeShift = await _db.GetParamIntAsync("liteScanTimeShift");
                var lastLiteScanTime = await _db.GetParamStrAsync("liteScanTime");
                var lastScanTime = await _db.GetParamDateTimeAsync("lastScanTime");
                var lastWriteBusFile = File.GetLastWriteTime(_busFileName);

                var isBusFileOld = lastWriteBusFile.AddMinutes(5) < lastScanTime;
                //если файл базы устарел - выключаю обновление сайтов
                if (isBusFileOld) {
                    checkBox_SyncSites.Checked = false;
                    lastLiteScanTime = lastWriteBusFile.ToString();
                }
                //запросим новые/измененные карточки товаров
                string s = await Class365API.RequestAsync("get", "goods", new Dictionary<string, string>{
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
                    await Class365API.RequestAsync("get", "storegoods", new Dictionary<string, string>{
                        {"updated[from]", lastLiteScanTime}
                    }));

                stage = "текущие цены...";
                ids.AddRange(JsonConvert.DeserializeObject<List<GoodIds>>(
                    await Class365API.RequestAsync("get", "salepricelistgoods", new Dictionary<string, string>{
                        {"updated[from]", lastLiteScanTime},
                        {"price_type_id","75524" } //интересуют только отпускные цены
                    })));

                stage = "запросим реализации...";
                ids.AddRange(JsonConvert.DeserializeObject<List<GoodIds>>(
                    await Class365API.RequestAsync("get", "realizationgoods", new Dictionary<string, string>{
                        {"updated[from]", lastLiteScanTime}
                    })));

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
                    lastScanTime.Day != DateTime.Now.Day) {
                    Log.Add("business.ru: будет произведен запрос полной базы товаров...");
                    _isBusinessNeedRescan = true;
                    _isBusinessCanBeScan = true;
                    ChangeStatus(button_BaseGet, ButtonStates.Active);
                    return;
                }
                Log.Add("business.ru: изменены карточки: " + lightSyncGoods.Count + " (с фото, ценой и остатками: " +
                    lightSyncGoods.Count(c => c.amount > 0 && c.price > 0 && c.images.Count > 0) + ")");
                foreach (var item in lightSyncGoods) {
                    Log.Add("business.ru: " + item.name + " (цена " + item.price + ", кол. " + item.amount + ")");
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

                label_Bus.Text = _bus.Count + "/" + _bus.Count(c => c.amount > 0 && c.price > 0 && c.images.Count > 0);
                if (!isBusFileOld) {
                    await _db.SetParamAsync("liteScanTime", _syncStartTime.AddMinutes(-liteScanTimeShift).ToString());
                    await _db.SetParamAsync("controlBus", _bus.Count.ToString());
                }

                ///вызываем событие в котором сообщаем о том, что в базе есть изменения... на будущее, когда будем переходить на событийную модель
                ///пока события не реализованы в проекте, поэтому пока мы лишь обновили уже загруженной базе только те карточки, 
                ///которые изменились с момента последнего запроса
                ///и дальше сдвинем контрольное время актуальности базы
                ///а дальше всё как обычно, только сайты больше не парсим,
                ///только вызываем методы обработки изменений и подъема упавших

                if (DateTime.Now.Minute == 55 || lastScanTime.AddHours(1) < DateTime.Now ||
                                            lastWriteBusFile.AddHours(1) < DateTime.Now || isBusFileOld) {
                    await SaveBusAsync();
                    if (checkBox_SyncSites.Checked)
                        await SyncAllAsync();
                    if (isBusFileOld) {
                        dateTimePicker1.Value = lastScanTime;
                        if (checkSyncStatus)
                            checkBox_SyncSites.Checked = true;
                    } else {
                        dateTimePicker1.Value = _syncStartTime;
                    }
                }
                Log.Add("business.ru: цикл синхронизации завершен");
                _isBusinessCanBeScan = true;
            } catch (Exception ex) {
                Log.Add("business.ru: ошибка синхронизации: " + ex.Message + "\n"
                    + ex.Source + "\n"
                    + ex.InnerException + "\n"
                    + stage);
                _isBusinessCanBeScan = true;
                _isBusinessNeedRescan = true;
            }
            ChangeStatus(button_BaseGet, ButtonStates.Active);
        }
        //цепочка обработок
        private async Task SyncAllAsync() {
            await AddPartNumsAsync();//добавление артикулов из описания
            await CheckArhiveStatusAsync();//проверка архивного статуса
            await CheckBu();
            await CheckUrls(); //удаление ссылок из черновиков
            button_Avito.PerformClick();
            await Task.Delay(10000);
            button_YandexMarket.PerformClick();
            await Task.Delay(10000);
            button_ozon.PerformClick();
            await Task.Delay(10000);
            button_Satom.PerformClick();
            await Task.Delay(10000);
            button_Vk.PerformClick();
            await Task.Delay(10000);
            button_Youla.PerformClick();
            await Task.Delay(10000);
            button_Gde.PerformClick();
            await Task.Delay(10000);
            button_Kupiprodai.PerformClick();
            await Task.Delay(10000);
            button_Drom.PerformClick();
            await Task.Delay(10000);
            //button_EuroAuto.PerformClick();
            //await Task.Delay(60000);
            button_Izap24.PerformClick();
            await Task.Delay(10000);
            //нужно подождать конца обновлений объявлений
            await WaitButtonsActiveAsync();
            button_PricesCorrection.PerformClick(); //коррекция цен в оприходованиях
                                                    //проверка задвоенности наименований карточек товаров
            await CheckDublesAsync();//проверка дублей
            await CheckMultipleApostropheAsync();//проверка лишних аппострофов
            if (await _db.GetParamBoolAsync("articlesClear"))
                await ArtCheckAsync();//чистка артикулов от лишних символов
            await GroupsMoveAsync();//проверка групп
            await PhotoClearAsync();//очистка ненужных фото
            await ArchivateAsync();//архивирование старых карточек
            await CheckDescriptions();
        }
        //проверка ссылок в черновиках
        async Task CheckUrls() {
            for (int b = 0; b < _bus.Count; b++) {
                if (_bus[b].GroupName().Contains("РАЗБОРКА") &&
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
                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
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

        //проверка переносов в описаниях
        private async Task CheckDescriptions() {
            for (int i = _bus.Count - 1; i > -1; i--) {
                if (_bus[i].name.Contains("test")) { await Task.Delay(10); }
                if (_bus[i].description?.Contains("\n") ?? false) {
                    _bus[i].description = _bus[i].description.Replace("\n", "<br />");
                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>() {
                                {"id", _bus[i].id},
                                {"name", _bus[i].name},
                                {"description", _bus[i].description}
                            });
                    Log.Add("CheckDescriptions: " + _bus[i].name + " - описание обновлено\n" + _bus[i].description);
                }
            }
        }
        private async Task CheckBu() => await Task.Factory.StartNew(() => {
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

        //полный скан базы бизнес.ру
        async void BaseGet(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            Log.Add("business.ru: старт полного цикла синхронизации");
            _isBusinessNeedRescan = true;
            _isBusinessCanBeScan = false;
            _syncStartTime = DateTime.Now;
            await GetBusGroupsAsync();
            await GetBusGoodsAsync2();
            var tlog = _bus.Count + "/" + _bus.Count(c => c.images.Count > 0 && c.amount > 0 && c.price > 0);
            Log.Add("business.ru: получено товаров с остатками из группы интернет магазин " + tlog);
            label_Bus.Text = tlog;
            await SaveBusAsync();
            _isBusinessNeedRescan = false;
            if (checkBox_SyncSites.Checked)
                await SyncAllAsync();
            Log.Add("business.ru: полный цикл синхронизации завершен");
            dateTimePicker1.Value = _syncStartTime;
            _isBusinessCanBeScan = true;
            ChangeStatus(sender, ButtonStates.Active);
        }
        //запрашиваю группы товаров
        async Task GetBusGroupsAsync() {
            Log.Add("business.ru: получаю список групп...");
            do {
                busGroups.Clear();
                try {
                    var tmp = await Class365API.RequestAsync("get", "groupsofgoods", new Dictionary<string, string> {
                        //{"parent_id", "205352"} // БУ запчасти
                    });
                    var tmp2 = JsonConvert.DeserializeObject<List<RootGroupsObject>>(tmp);
                    busGroups.AddRange(tmp2);
                } catch (Exception x) {
                    Log.Add("business.ru: ошибка запроса групп товаров из базы!!! - " + x.Message + " - " + x.InnerException.Message);
                    await Task.Delay(60000);
                }
            } while (busGroups.Count < 30);
            Log.Add("business.ru: получено " + busGroups.Count + " групп товаров");
            RootObject.Groups = busGroups;
        }
        //получаю карточки товаров
        async Task GetBusGoodsAsync2() {
            int lastScan;
            do {
                lastScan = await _db.GetParamIntAsync("controlBus");
                _bus.Clear();
                try {
                    for (int i = 1; i > 0; i++) {
                        string s = await Class365API.RequestAsync("get", "goods", new Dictionary<string, string>{
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
                            label_Bus.Text = _bus.Count.ToString();
                        } else
                            break;
                    }
                } catch (Exception x) {
                    Log.Add("business.ru: ошибка при запросе товаров из базы!!! - " + x.Message);
                    await Task.Delay(60000);
                }
                await _db.SetParamAsync("controlBus", _bus.Count.ToString());
            } while (_bus.Count == 0 || Math.Abs(lastScan - _bus.Count) > 400);
            Log.Add("business.ru: получено товаров " + _bus.Count);
        }

        async Task<List<RootObject>> GetBusGoodsAsync(List<string> ids) {
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
                d.Add("with_additional_fields", "1");
                d.Add("with_remains", "1");
                d.Add("with_prices", "1");
                d.Add("type_price_ids[0]", "75524");
                int err = 0;
                do {
                    string s = "";
                    try {
                        s = await Class365API.RequestAsync("get", "goods", d);
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
                        Log.Add("business.ru: ошибка запроса товаров!!! - " + d + " - " + x.Message + " - " + s);
                        err++;
                        await Task.Delay(60000);
                    }
                } while (err < 10);
            }
            return lro;
        }
        //сохраняю товары в файл
        async Task SaveBusAsync() {
            try {
                await Task.Factory.StartNew(() => {
                    if (_db.GetParamBool("saveDataBaseLocal")) {
                        File.WriteAllText(_busFileName, JsonConvert.SerializeObject(_bus));
                        Log.Add("business.ru: bus.json - сохранение успешно");
                        File.SetLastWriteTime(_busFileName, _syncStartTime);
                    }
                });
            } catch (Exception x) {
                Log.Add("business.ru: ошибка сохранения локальной базы данных bus.json - " + x.Message);
            }
        }
        //подгружаю товары из файла
        async Task LoadBusJSON() {
            if (File.Exists(_busFileName)) {
                await GetBusGroupsAsync();
                Log.Add("business.ru: загружаю список товаров...");
                await Task.Factory.StartNew(() => {
                    var s = File.ReadAllText(_busFileName);
                    _bus = JsonConvert.DeserializeObject<List<RootObject>>(s);
                });
                Log.Add("business.ru: загружено " + _bus.Count + " карточек товаров");
                label_Bus.Text = _bus.Count + "/" + _bus.Count(c => c.images.Count > 0 && c.price > 0 && c.amount > 0);
                button_BaseGet.BackColor = Color.GreenYellow;
            }
            _isBusinessCanBeScan = true;
        }
        //загрузка формы
        private async void FormMain_Load(object sender, EventArgs e) {
            //подписываю обработчик на событие
            Log.LogUpdate += LogUpdate;
            //устанавливаю глубину логирования
            Log.Level = await _db.GetParamIntAsync("logSize");
            //меняю заголовок окна
            this.Text += _version;
            var currentVersion = await _db.GetParamStrAsync("version");
            if (!_version.Contains(currentVersion)) {
                Log.Add("доступна новая версия " + currentVersion);
            }
            _writeLog = await _db.GetParamBoolAsync("writeLog");
            dateTimePicker1.Value = _db.GetParamDateTime("lastScanTime");
            _saveCookiesBeforeClose = await _db.GetParamBoolAsync("saveCookiesBeforeClose");
            await CheckMultiRunAsync();
            await LoadBusJSON();
        }
        //проверка на параллельный запуск
        async Task CheckMultiRunAsync() {
            if (await _db.GetParamBoolAsync("checkMultiRun")) {
                while (true) {
                    try {
                        //запрашиваю последнюю запись из лога
                        DataTable table = await _db.GetLogAsync("", 1);
                        //если запись получена
                        if (table.Rows.Count > 0) {
                            DateTime time = (DateTime) table.Rows[0].ItemArray[1];
                            string text = table.Rows[0].ItemArray[3] as string;
                            Log.Add("Последняя запись в логе\n*****\n" + time + ": " + text + "\n*****\n\n", false);
                            //есть текст явно указывает, что приложение было остановлено или прошло больше 5 минут выход с true
                            if (text.Contains("синхронизация остановлена") || time.AddMinutes(5) < DateTime.Now)
                                return;
                            else
                                Log.Add("защита от параллельных запусков! повторная попытка через 1 минуту...", false);
                        } else
                            Log.Add("ошибка чтения лога - записи не найдены! повторная попытка через 1 минуту...", false);
                        await Task.Delay(61 * 1000);
                    } catch (Exception x) {
                        Log.Add("ошибка при контроле параллельных запусков - " + x.Message, false);
                    }
                }
            }
        }
        // поиск и исправление дубликатов названий
        private async Task CheckDublesAsync() {
            try {
                var count = await _db.GetParamIntAsync("checkDublesCount");
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
                        await Class365API.RequestAsync("put", "goods", new Dictionary<string, string> {
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
                            await Class365API.RequestAsync("put", "goods", new Dictionary<string, string> {
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

        //редактирование описаний, добавленние синонимов
        private async void ButtonPutDesc_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            //загрузим словарь
            List<string> eng = new List<string>();
            List<string> rus = new List<string>();
            List<string> file = new List<string>(System.IO.File.ReadAllLines(@"..\dict.txt", Encoding.UTF8));
            foreach (var s in file) {
                var ar = s.Split(',');
                eng.Add(ar[0]);
                rus.Add(ar[1]);
            }
            file.Clear();
            //количество изменений за один раз
            var n = await _db.GetParamIntAsync("descriptionEditCount");
            //пробегаемся по описаниям карточек базы
            //for (int i = bus.Count-1; i > -1 && n > 0; i--) {
            for (int i = _bus.Count - 1; i > -1 && n > 0; i--) {
                //если в карточке есть фото и остатки
                if (_bus[i].images.Count > 0 /*&& bus[i].amount > 0*/) {
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
                        Form f4 = new FormEdit(i);
                        f4.Owner = this;
                        f4.ShowDialog();
                        if (f4.DialogResult == DialogResult.OK) {
                            var s = await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>() {
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
            }
            eng.Clear();
            rus.Clear();
            ChangeStatus(sender, ButtonStates.Active);
        }
        //тест задвоения карточек контрагентов
        private async void ButtonTestPartnersClick(object sender, EventArgs e) {
            List<PartnerContactinfoClass> pc = new List<PartnerContactinfoClass>();
            do {
                //    //lastScan = Convert.ToInt32(dSet.Tables["controls"].Rows[0]["controlBus"]);
                pc.Clear();
                try {
                    for (int i = 1; i > 0; i++) {
                        string s = await Class365API.RequestAsync("get", "partnercontactinfo", new Dictionary<string, string>
                        {
                            {"contact_info_type_id","1"},
                            {"limit", _pageLimitBase.ToString()},
                            {"page", i.ToString()}
                        });

                        if (s.Length > 3) {
                            pc.AddRange(JsonConvert.DeserializeObject<PartnerContactinfoClass[]>(s));
                            Thread.Sleep(500);
                            label_Bus.Text = _bus.Count.ToString();
                        } else {
                            break;
                        }
                    }
                } catch {
                    Log.Add("ОШИБКА ПРИ ЗАПРОСЕ КОНТАКТНОЙ ИНФОРМАЦИИ ИЗ БАЗЫ!!!");
                    Thread.Sleep(2000);
                }
                //обновляем полученное количество товаров
                //    dSet.Tables["controls"].Rows[0]["controlBus"] = bus.Count.ToString();
            } while (pc.Count == 0);
            Log.Add("получено контактов " + pc.Count);
            var dub = pc.Where(w => pc.Count(c => c.contact_info
                                        .Contains(w.contact_info
                                            .Replace("-", "")
                                            .Replace(" ", "")
                                            .Replace("(", "")
                                            .Replace(")", "")
                                            .Replace("+", "")
                                            .TrimStart('7')
                                            .TrimStart('8'))
                                    ) > 1);
            foreach (var d in dub) {
                Log.Add("НАЙДЕНЫ ДУБЛИ: " + d.contact_info);
            }
        }
        //проверка артикулов
        private async Task ArtCheckAsync() {
            for (int b = 0; b < _bus.Count; b++) {
                try {
                    var desc = !String.IsNullOrEmpty(_bus[b].description) ? _bus[b].description.ToLowerInvariant() : "";

                    //проверим на нулевое значение
                    _bus[b].part = _bus[b].part ?? "";
                    _bus[b].store_code = _bus[b].store_code ?? "";
                    if (_bus[b].part.Contains(" ")
                        || _bus[b].part.Contains(".")
                        || _bus[b].part.Contains("/") && _bus[b].images.Count > 0
                        || _bus[b].part.Contains(",") && _bus[b].images.Count > 0
                        || _bus[b].part.Contains("_")
                        || _bus[b].store_code.Contains(" ")
                        || _bus[b].store_code.Contains(".")
                        || _bus[b].store_code.Contains("_")
                        || _bus[b].store_code.Contains("(")
                        || _bus[b].name.StartsWith(" ")
                        || _bus[b].name.EndsWith(" ")
                        || _bus[b].name.Contains("  ")
                        || _bus[b].name.Contains(";")
                        || _bus[b].name.Contains(@"\")
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
                            .Replace("/", "")
                            .Split(',').First()
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

                        if (desc.Contains("лев")) {
                            new_name = new_name.Replace(" L ", " l ");
                            new_name = new_name.Contains(" l ") ? new_name.Replace(" l ", " " + GetNameForm(desc, "лев") + " ") : new_name;
                        }
                        if (desc.Contains("прав")) {
                            new_name = new_name.Replace(" R ", " r ");
                            new_name = new_name.Contains(" r ") ? new_name.Replace(" r ", " " + GetNameForm(desc, "прав") + " ") : new_name;
                        }
                        args.Add("id", _bus[b].id);
                        args.Add("name", new_name);
                        args.Add("part", new_part);
                        args.Add("store_code", new_store_code);

                        await Class365API.RequestAsync("put", "goods", args);

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
        //найти словоформу
        string GetNameForm(string desc, string side) {
            return desc.Replace("\n", " ").Replace("\r", " ")
                        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .First(f => f.StartsWith(side))
                        .Split('<').First().ToString()
                        .Trim('!');
        }
        //изменение групп
        private async Task GroupsMoveAsync() {  //TODO добавиить перемещение по группам карточек с ценами из группы черновики
            for (int b = 0; b < _bus.Count; b++) {
                //перемещение в группу Заказы всех карточек, непривязанных к группам
                if (_bus[b].group_id == "1") {
                    _bus[b].group_id = "209277";
                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>
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
        //контроль архивного статуса на карточках с положительным остатком
        async Task CheckArhiveStatusAsync() {
            try {
                foreach (var item in _bus.Where(w => w.amount > 0 && w.archive)) {
                    Log.Add("business.ru: ошибка! карточка с положительным остатком в архиве! - " + item.name);
                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
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
        //пока не активируются все кнопки ожидаем 20 сек
        async Task WaitButtonsActiveAsync() {
            while (!(
                button_Drom.Enabled &&
                button_Vk.Enabled &&
                button_Kupiprodai.Enabled &&
                button_Avito.Enabled &&
                button_Gde.Enabled &&
                button_Youla.Enabled
                )
            )
                await Task.Delay(5000);
        }

        async Task CheckMultipleApostropheAsync() {
            try {
                foreach (var item in _bus.Where(w => (w.name.Contains("''''") || w.name.Contains("' `")) && w.amount > 0)) {
                    Log.Add("business.ru: обнаружено название с множеством апострофов - " + item.name);
                    var s = item.name;
                    while (s.EndsWith("'") || s.EndsWith("`"))
                        s = s.TrimEnd('\'').TrimEnd('`').TrimEnd(' ');
                    item.name = s;
                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
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

        async Task AddPartNumsAsync() {
            try {
                var i = 0;
                foreach (var item in _bus.Where(w => (!string.IsNullOrEmpty(w.description) && w.description.Contains("№") && string.IsNullOrEmpty(w.part)))) {
                    Log.Add("business.ru: обнаружен пустой артикул - " + item.name);
                    //ищем номера в описании
                    string num = item.description
                            .Split('№')[1]
                            .Replace("&nbsp;", " ")
                            .Replace("&thinsp;", " ")
                            .Trim(' ')
                            .Replace("<", " ")
                            .Replace("</p>", " ")
                            .Replace("\n", " ")
                            .Split(' ')[0]
                            .Split(',')[0];
                    if (num.Length > 0) {
                        item.part = num;
                        await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                                {"id", item.id},
                                {"name", item.name},
                                {"part", num}
                        });
                        Log.Add("business.ru: добавлен артикул - " + num);
                        Thread.Sleep(1000);
                    }
                    i++;
                    //if (i > 10) break;
                }
            } catch (Exception x) {
                Log.Add("business.ru: ошибка при добавлении артикула - " + x.Message);
            }

        }

        private void dateTimePicker1_ValueChanged(object sender, EventArgs e) {
            try {
                _scanTime = dateTimePicker1.Value;
                _db.SetParamAsync("lastScanTime", _scanTime.ToString());
                _db.SetParamAsync("liteScanTime", _scanTime.ToString());
                RootObject.ScanTime = _scanTime;
            } catch (Exception x) {
                Log.Add("ошибка изменения даты синхронизации! - " + x.Message + " - " + x.InnerException.Message);
            }
        }


        private async void Button_PricesCheck_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            await ChangePostingsPrices();
            await Task.Delay(1000);//ChangeRemainsPrices();
            ChangeStatus(sender, ButtonStates.Active);
        }

        //сохранить куки
        private void Button_SaveCookie_Click(object sender, EventArgs e) {
            _kupiprodai.SaveCookies();
            _drom.SaveCookies();
            _gde.SaveCookies();
        }
        //статус контрола
        void ChangeStatus(object sender, ButtonStates buttonState) {
            var but = sender as System.Windows.Forms.Button;
            if (but != null) {
                switch (buttonState) {
                    case ButtonStates.Active:
                        but.Enabled = true;
                        but.BackColor = Color.GreenYellow;
                        break;
                    case ButtonStates.NoActive:
                        but.Enabled = false;
                        but.BackColor = Color.Yellow;
                        break;
                    case ButtonStates.ActiveWithProblem:
                        but.Enabled = true;
                        but.BackColor = Color.Red;
                        break;
                    case ButtonStates.NonActiveWithProblem:
                        but.Enabled = false;
                        but.BackColor = Color.Red;
                        break;
                    default:
                        break;
                }
            }
        }
        //============
        //=== ЛОГИ ===
        //============
        public void ToLogBox(string s) {
            if (textBox_LogFilter.Text.Length == 0 || s.Contains(textBox_LogFilter.Text))
                try {
                    logBox.Invoke(new Action<string>((a) => {
                        var t = logBox.Lines.Length > Log.Level ? logBox.Text.Substring(logBox.Text.Length / 10)
                                                                : logBox.Text;
                        logBox.Text = t + a;
                    }), s + "\n");
                } catch (Exception x) {
                    Console.WriteLine(x.Message);
                    Console.ReadLine();
                }
        }
        //прокрутка лога
        private void RichTextBox1_TextChanged(object sender, EventArgs e) {
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        }
        //обработка события на добавление записи
        public void LogUpdate() {
            ToLogBox(Log.LogLastAdded);
        }
        //обработчик изменений значений фильтра лога
        void TextBox_LogFilter_TextChanged(object sender, EventArgs e) {
            FillLogAsync();
        }
        //загружаю лог асинхронно
        async void FillLogAsync() {
            try {
                DataTable table = await _db.GetLogAsync(textBox_LogFilter.Text, Log.Level);
                if (table.Rows.Count > 0)
                    logBox.Text = table.Select().Select(s => s[1] + ": " + s[3]).Aggregate((a, b) => b + "\n" + a) + "\n";
                else
                    logBox.Text = "по заданному фильтру ничего не найдено!\n";
            } catch (Exception x) {
                Log.Add(x.Message);
            }
        }
        //очистка фильтра
        void Button_LogFilterClear_Click(object sender, EventArgs e) {
            textBox_LogFilter.Text = "";
        }
        //закрываем форму, сохраняем настройки
        async void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            if (_isBusinessCanBeScan)
                Log.Add("синхронизация остановлена!");
            this.Visible = false;
            ClearTempFiles();
            if (_saveCookiesBeforeClose) {
                _gde?.SaveCookies();
                _drom?.SaveCookies();
                _kupiprodai?.SaveCookies();
            }
            _gde?.Quit();
            _drom?.Quit();
            _kupiprodai?.Quit();
        }
        //удаление временных файлов
        void ClearTempFiles() {
            foreach (var file in Directory.EnumerateFiles(Application.StartupPath, "*.jpg")) {
                File.Delete(file);
            }
        }
        //загрузка окна настроек
        void Button_SettingsFormOpen_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            FormSettings fs = new FormSettings();
            fs.Owner = this;
            fs.ShowDialog();
            fs.Dispose();
            ChangeStatus(sender, ButtonStates.Active);
        }
        //окно веса, размеры
        private async void Button_WeightsDimensions_ClickAsync(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (_isBusinessNeedRescan || _bus.Count == 0)
                await Task.Delay(5000);
            FormWeightsDimentions fw = new FormWeightsDimentions(_bus);
            fw.Owner = this;
            fw.ShowDialog();
            fw.Dispose();
            ChangeStatus(sender, ButtonStates.Active);
        }
        //массовое изменение цен закупки на товары введенных на остатки
        async Task ChangeRemainsPrices(int procent = 80) { //TODO переделать, чтобы метод получал список измененных карточек, а не перебирал все
            //цикл для пагинации запросов
            for (int i = 1; ; i++) {
                //запрашиваю товары из документов "ввод на остатки"
                var s = await Class365API.RequestAsync("get", "remaingoods", new Dictionary<string, string> {
                        {"limit", _pageLimitBase.ToString()},
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
                    var indBus = _bus.FindIndex(f => f.id == rg.good_id);
                    //если индекс и остаток положительный
                    if (indBus > -1 && _bus[indBus].amount > 0) {
                        //цена ввода на остатки (цена закупки)
                        var priceIn = rg.FloatPrice;
                        //цена отдачи (розничная)
                        var priceOut = _bus[indBus].price;
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
                                Log.Add("business.ru: " + _bus[indBus].name + " - цена закупки изменена с " + priceIn + " на " + newPrice.ToString("#.##"));
                            await Task.Delay(50);
                        }
                    }
                }
            }
        }
        //массовое изменение цен закупки в оприходованиях
        async Task ChangePostingsPrices(int procent = 80) {//TODO переделать, чтобы метод получал список измененных карточек, а не перебирал все
            //цикл для пагинации запросов
            for (int i = 1; ; i++) {
                //запрашиваю товары из документов "Оприходования"
                var s = await Class365API.RequestAsync("get", "postinggoods", new Dictionary<string, string> {
                        {"limit", _pageLimitBase.ToString()},
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
                    if (indBus > -1 && _bus[indBus].amount > 0 && _bus[indBus].price > 0) {
                        //цена ввода на остатки (цена закупки)
                        var priceIn = pg.FloatPrice;
                        //цена отдачи (розничная)
                        var priceOut = _bus[indBus].price;
                        //процент цены закупки от цены отдачи
                        var procentCurrent = 100 * priceIn / priceOut;
                        //если процент различается более чем на 5%
                        if (Math.Abs(procentCurrent - procent) > 5) {
                            //новая цена закупки
                            var newPrice = priceOut * procent * 0.01;
                            //- меняем цену закупки
                            s = await Class365API.RequestAsync("put", "postinggoods", new Dictionary<string, string> {
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
        //удаление фото из старых карточек
        async Task PhotoClearAsync() {
            var cnt = await _db.GetParamIntAsync("photosCheckCount");
            var days = await _db.GetParamIntAsync("photosCheckDays");
            if (cnt == 0)
                return;
            //список карточек с фото, но без остатка, с ценой и с поступлениями на карточку, отсортированный с самых старых
            var buschk = _bus.Where(b => b.images.Count > 0 &&
                                        b.amount <= 0 &&
                                        b.price > 0 &&
                                        b.remains.Count > 0 &&
                                        DateTime.Now.AddDays(-days) > DateTime.Parse(b.updated))
                .OrderBy(o => DateTime.Parse(o.updated))
                .ToList();
            Log.Add("PhotoClearAsync: карточек с фото и ценой без остатка, обновленных более месяца назад: " + buschk.Count);
            var lastDate = DateTime.Now.AddYears(-2).ToString();
            for (int b = 0; cnt > 0 && b < buschk.Count; b++) {
                try {
                    //количество реализаций товара
                    var s = await Class365API.RequestAsync("get", "realizationgoods", new Dictionary<string, string>(){
                                    {"good_id", buschk[b].id},
                                    {"updated[from]", lastDate}
                                });


                    var realizations = JsonConvert.DeserializeObject<List<realizationgoods>>(s)
                                                  .OrderBy(o => DateTime.Parse(o.updated));
                    DateTime controlDate;
                    if (realizations.Any())
                        controlDate = DateTime.Parse(realizations.Last().updated).AddDays(days + 10 * realizations.Count());
                    else
                        controlDate = DateTime.Parse(buschk[b].updated).AddDays(days);

                    Log.Add("PhotoClearAsync: " + buschk[b].id + " - " + buschk[b].name + ", updated: " + buschk[b].updated + ", реализаций: " + realizations.Count() + ", контрольная дата: " + controlDate);
                    if (DateTime.Now < controlDate) {
                        Log.Add("PhotoClearAsync: пропуск - дата не подошла (Now < controlDate)");
                        continue;
                    }

                    //удаляю фото
                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>(){
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

        //отчет остатки по уровню цен
        async void PriceLevelsRemainsReport(object sender, EventArgs e) {
            var priceLevelsStr = _db.GetParamStr("priceLevelsForRemainsReport");
            var priceLevels = JsonConvert.DeserializeObject<int[]>(priceLevelsStr);
            foreach (var price in priceLevels) {
                var x = _bus.Count(w => w.images.Count > 0 && w.price >= price && w.amount > 0);
                Log.Add("позиций фото, положительным остатком и ценой " + price + "+ : " + x);
            }
        }
        //архивирование старых карточек
        async Task ArchivateAsync() {
            var cnt = await _db.GetParamIntAsync("archivateCount");
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
                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>(){
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
        //сейчас рабочее время?
        private bool IsWorkTime() {
            var dt = DateTime.Now;
            if (dt.DayOfWeek == DayOfWeek.Sunday)
                return false;
            if (dt.Hour >= 19 || dt.Hour < 9)
                return false;
            if (dt.DayOfWeek == DayOfWeek.Saturday && dt.Hour >= 15)
                return false;
            return true;
        }
        //массовая замена текста
        async Task ReplaceTextAsync(string checkText, string newText) {
            for (int b = 0; b < _bus.Count; b++) {
                if (_bus[b].images.Count == 0)
                    continue;
                if (!string.IsNullOrEmpty(_bus[b].name) && _bus[b].name.Contains(checkText)) {
                    var newName = _bus[b].name.Replace(checkText, newText);
                    var result = MessageBox.Show(_bus[b].name + "\n\n↓↓↓\n\n" + newName, "Изменить название?", MessageBoxButtons.YesNo);
                    if (result == DialogResult.Yes) {
                        _bus[b].name = newName;
                        var s = await Class365API.RequestAsync("put", "goods", new Dictionary<string, string> {
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
                        var s = await Class365API.RequestAsync("put", "goods", new Dictionary<string, string> {
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
        //замена атрибутов с размерами на поля в карточке
        async Task AttributesChange(int i) {
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
                            var s = await Class365API.RequestAsync("put", "goods", new Dictionary<string, string> {
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
        //удаление атрибута в карточке
        private async Task DeleteAttribute(int b, string atrId) {
            var s = await Class365API.RequestAsync("get", "goodsattributes", new Dictionary<string, string> {
                                {"good_id",_bus[b].id},
                                {"attribute_id",atrId }
                            });
            var attr = JsonConvert.DeserializeObject<Goodsattributes[]>(s);
            //удаляю привязку по id
            if (attr.Any()) {
                s = await Class365API.RequestAsync("delete", "goodsattributes", new Dictionary<string, string> {
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

        //метод для тестов
        async void ButtonTest_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {

                await _ozon.GetCategoriesAsync();
                //await GetNewGoodsForOzon();

                {

                    //await ReplaceTextAsync("Фиат Пунто 1 1.2", "Фиат Пунто 176, 1.2, 1996г. 3-х дверка");
                    //_drom.CheckOffersAsync();
                    //CheckDescriptions();
                    //var ids = bus.Where(w => w.IsTimeUpDated()).Select(s => s.id).ToList();
                    //var str = JsonConvert.SerializeObject(ids);

                    //await _db.SetParamAsync("avito.upFromHour",str);





                    //var weight = 1.20;
                    //string s = weight.ToString();
                    //weight = 1.0;
                    //s = weight.ToString();
                    //weight = 11.0;
                    //s = weight.ToString();
                    //weight = 11.1;
                    //s = weight.ToString();


                    //var str = await Class365API.RequestAsync("get", "attributesforgoods",new Dictionary<string, string>());


                    //var img = new List<Image>() {
                    //                    new Image() {
                    //                       name = "1.jpg",
                    //                        url = "https://static.baza.drom.ru/drom/1513339810647_default" }
                    //                };
                    //var images = JsonConvert.SerializeObject(img);
                    //var s = await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>(){
                    //                    {"id", "554634"},
                    //                    {"name", "Крыло правое Chevrolet Lanos"},
                    //                    {"images", images}
                    //                });




                    //Log.Add("TEST: "+s);

                    //await CheckDublesAsync();

                    //var youlaXml = new YoulaXml();
                    //await youlaXml.GenerateXML_avito(bus);
                    //await ArchivateAsync();

                    //var av = new AvitoXml();
                    //await av.GenerateXML(bus);

                    //_avito.StartAsync(bus);

                    //if (_avito._dr!=null)_avito._dr.ScreenShot();
                    //if (_drom._dr != null) _drom._dr.ScreenShot();
                    //if (_kupiprodai._dr != null) _kupiprodai._dr.ScreenShot();
                    //if (_tiu._dr != null) _tiu._dr.ScreenShot();
                    //if (_youla._dr != null) _youla._dr.ScreenShot();
                    //if (_gde._dr != null) _gde._dr.ScreenShot();
                    //if (_cdek._dr != null) _cdek._dr.ScreenShot();
                    //if (_avto._dr != null) _avto._dr.ScreenShot();
                    //if (_auto._dr != null) _auto._dr.ScreenShot();


                    //var s = await Class365API.RequestAsync("get", "remaingoods", new Dictionary<string, string> {       { "help", "1" },   });
                    //s = await Class365API.RequestAsync("get", "remains", new Dictionary<string, string> { { "help", "1" },});


                    //не выложено на авито
                    //Log.Add(
                    //    bus.Count(w => w.amount > 0 &&
                    //              w.price >= 500 &&
                    //              w.tiu.Contains("http") &&
                    //             !w.avito.Contains("http"))
                    //       .ToString()
                    //    );



                    //Log.Add(DateTime.Now.ToString().Replace(".", ""));
                    //await SftpUploadAsync();
                    //var json = JsonConvert.SerializeObject(bus[0]);

                    //var x = _db.SetGood(int.Parse(bus[0].id), DateTime.Now.ToString(), json);
                    //var y = _db.SetGood(int.Parse(bus[1].id), DateTime.Now.ToString(), JsonConvert.SerializeObject(bus[1]));

                    //var j = _db.GetGood("id", bus[0].id.ToString());
                    //var b = JsonConvert.DeserializeObject<RootObject>(j);

                    //var j2 = _db.GetGood("drom", "47176874");
                    //var b2 = JsonConvert.DeserializeObject<RootObject>(j2);


                    //var res = db.SetParam("test", "123");
                    //var res2 = db.SetParam("test", "777");
                    //var str = db.GetParamStr("test");
                    //var i = db.GetParamInt("test");
                    //res = db.SetParam("test", DateTime.Now.ToString());
                    //var dt = db.GetParamDateTime("test");

                    //пробую записать в лог из вторичного потока (в 10 потоков)
                    //for (int j = 0; j < 10; j++) {
                    //    Task.Factory.StartNew(() => {
                    //        for (int i = 0; i < 100; i++) {
                    //            db.Log.Add("site", j+" thread = " + i.ToString());
                    //        }
                    //    });
                    //}
                }
            } catch (Exception x) {
                Log.Add(x.Message);
            }
            ChangeStatus(sender, ButtonStates.Active);
        }

    }
}