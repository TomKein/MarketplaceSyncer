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

namespace Selen {
    public partial class FormMain : Form {
        string _version = "1.61.2";

        DB _db = new DB();

        public List<RootGroupsObject> busGroups = new List<RootGroupsObject>();
        public List<RootObject> bus = new List<RootObject>();
        public List<RootObject> lightSyncGoods = new List<RootObject>();

        VK _vk = new VK();
        Cdek _cdek = new Cdek();
        Drom _drom = new Drom();
        Tiu _tiu = new Tiu();
        AvtoPro _avto = new AvtoPro();
        Avito _avito = new Avito();
        AutoRu _auto = new AutoRu();
        EuroAuto _euroAuto = new EuroAuto();
        Izap24 _izap24 = new Izap24();
        Kupiprodai _kupiprodai = new Kupiprodai();
        GdeRu _gde = new GdeRu();
        Youla _youla = new Youla();

        int _pageLimitBase = 250;
        bool _saveCookiesBeforeClose;

        //для возврата из форм
        public List<string> lForm3 = new List<string>();
        public string nForm3 = "";
        public string BindedName = "";
        //глобальный индекс для формы
        public int _i;
        //флаг - нужен рескан базы
        bool base_rescan_need = false;
        //флаг - можно запускать новый цикл синхронизации
        bool base_can_rescan = false;
        //время запуска очередной синхронизации
        DateTime sync_start;
        DateTime scanTime;
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
        async void AvitoRu_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            if (await _db.GetParamBoolAsync("avito.syncEnable")) {
                try {
                    while (base_rescan_need) await Task.Delay(30000);
                    await _avito.StartAsync(bus);
                    ChangeStatus(sender, ButtonStates.Active);
                } catch (Exception x) {
                    Log.Add("avito.ru: ошибка синхронизации! - " + x.Message);
                    if (x.Message.Contains("timed out") ||
                        x.Message.Contains("already closed") ||
                        x.Message.Contains("invalid session id") ||
                        x.Message.Contains("chrome not reachable")) {
                        Log.Add("avito.ru: ошибка браузера, перезапуск через 1 минуту...");
                        await Task.Delay(60000);
                        _avito.Quit();
                        AvitoRu_Click(sender, e);
                    } else ChangeStatus(sender, ButtonStates.ActiveWithProblem);

                }
            }
        }
        //VK.COM
        async void VkCom_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                Log.Add("вк начало выгрузки");
                while (base_rescan_need) await Task.Delay(20000);
                await _vk.VkSyncAsync(bus);
                Log.Add("вк выгрузка завершена");
                ChangeStatus(sender, ButtonStates.Active);
            } catch (Exception x) {
                Log.Add("ошибка синхронизации вк: " + x.Message);
                ChangeStatus(sender, ButtonStates.ActiveWithProblem);
            }
        }
        //TIU.RU
        async void TiuRu_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            for (int i = 0; i < 10; i++) {
                try {
                    while (base_rescan_need || bus.Count == 0) await Task.Delay(30000);
                    await _tiu.TiuSyncAsync(bus, ds);
                    break;
                } catch (Exception x) {
                    ChangeStatus(sender, ButtonStates.NonActiveWithProblem);
                    Log.Add("tiu.ru: ошибка синхронизации - " + x.Message);
                    if (x.Message.Contains("timed out") ||
                            x.Message.Contains("already closed") ||
                            x.Message.Contains("invalid session id") ||
                            x.Message.Contains("chrome not reachable")) {
                        _tiu.Quit();
                        await Task.Delay(60000);
                    }
                }
            }
            ChangeStatus(sender, ButtonStates.Active);
        }
        async Task TiuCheckAsync() {        //TODO вынести в класс tiu.cs
            int i = 0;
            for (int b = 0; b < bus.Count; b++) {
                if (bus[b].tiu.Contains("product2")) {
                    bus[b].tiu = "https://my.tiu.ru/cms/product" + bus[b].tiu.Replace("product2", "|").Split('|').Last();
                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>() {
                        {"id", bus[b].id },
                        {"name", bus[b].name },
                        {"209325",bus[b].tiu}
                    });
                    i++;
                    Log.Add("исправлена ссылка тиу " + b + ":\n" + bus[b].name + "\n" + bus[b].tiu + "\n");
                    if (i >= 10) break;
                }
            }
        }
        //DROM.RU
        async void DromRu_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                while (base_rescan_need) await Task.Delay(30000);
                await _drom.DromStartAsync(bus);
                label_Drom.Text = bus.Count(c => !string.IsNullOrEmpty(c.drom) && c.drom.Contains("http") && c.amount > 0).ToString();
                ChangeStatus(sender, ButtonStates.Active);
            } catch (Exception x) {
                Log.Add("drom.ru: ошибка синхронизации! \n" + x.Message + "\n" + x.InnerException.Message);
                if (x.Message.Contains("timed out") ||
                    x.Message.Contains("already closed") ||
                    x.Message.Contains("invalid session id") ||
                    x.Message.Contains("chrome not reachable")) {
                    _drom.Quit();
                    await Task.Delay(60000);
                    DromRu_Click(sender, e);
                }
                ChangeStatus(sender, ButtonStates.ActiveWithProblem);
            }
        }
        //AUTO.RU
        async void AutoRu_Click(object sender, EventArgs e) {
            if (await _db.GetParamBoolAsync("auto.syncEnable")) {
                ChangeStatus(sender, ButtonStates.NoActive);
                try {
                    Log.Add("auto.ru: начало выгрузки...");
                    while (base_rescan_need) await Task.Delay(30000);                    
                    await _auto.AutoRuStartAsync(bus);
                    Log.Add("auto.ru: выгрузка завершена!");
                    ChangeStatus(sender, ButtonStates.Active);
                } catch (Exception x) {
                    ChangeStatus(sender, ButtonStates.ActiveWithProblem);
                    Log.Add("auto.ru: ошибка выгрузки! - " + x.Message);
                    if (x.Message.Contains("timed out") ||
                        x.Message.Contains("already closed") ||
                        x.Message.Contains("invalid session id") ||
                        x.Message.Contains("chrome not reachable")) {
                        _auto?.Quit();
                        await Task.Delay(180000);
                        _auto = new AutoRu();
                        AutoRu_Click(sender, e);
                    }
                }
            }
        }
        //KUPIPRODAI.RU
        async void KupiprodaiRu_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            for (int i = 0; ; i++) {
                try {
                    while (base_rescan_need) await Task.Delay(30000);
                    await _kupiprodai.StartAsync(bus);
                    label_Kp.Text = bus.Count(c => !string.IsNullOrEmpty(c.kp) && c.kp.Contains("http") && c.amount > 0).ToString();
                    ChangeStatus(sender, ButtonStates.Active);
                    break;
                } catch (Exception x) {
                    Log.Add("kupiprodai.ru: ошибка синхронизации! - " + x.Message + " - " + x.InnerException.Message);
                    if (x.Message.Contains("timed out") ||
                        x.Message.Contains("already closed") ||
                        x.Message.Contains("invalid session id") ||
                        x.Message.Contains("chrome not reachable")) {
                        _kupiprodai.Quit();
                        await Task.Delay(60000);
                    }
                    if (i > 5) {
                        Log.Add("kupiprodai.ru: ошибка! превышено количество попыток!");
                        ChangeStatus(sender, ButtonStates.ActiveWithProblem);
                        break;
                    }
                }
            }
        }
        async void KupiprodaiRuAdd_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (base_rescan_need) await Task.Delay(30000);
            await _kupiprodai.AddAsync();
            label_Kp.Text = bus.Count(c => !string.IsNullOrEmpty(c.kp) && c.kp.Contains("http") && c.amount > 0).ToString();
            ChangeStatus(sender, ButtonStates.Active);
        }
        //GDE.RU
        async void GdeRu_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (base_rescan_need || bus.Count == 0) await Task.Delay(30000);
            if (await _gde.StartAsync(bus)) {
                label_Gde.Text = bus.Count(c => c.gde != null && c.gde.Contains("http")).ToString();
                ChangeStatus(sender, ButtonStates.Active);
            }else 
                ChangeStatus(sender, ButtonStates.ActiveWithProblem);
        }
        //AVTO.PRO
        async void AvtoPro_Click(object sender, EventArgs e) {
            if (await _db.GetParamBoolAsync("autoPro.syncEnable")) {
                ChangeStatus(sender, ButtonStates.NoActive);
                try {
                    Log.Add("avto.pro: начало выгрузки...");
                    while (base_rescan_need) await Task.Delay(30000);
                    await _avto.AvtoProStartAsync(bus);
                    Log.Add("avto.pro: выгрузка завершена!");

                    var lastScanTime = await _db.GetParamStrAsync("AvtoProLastScanTime");

                    //достаточно проверять один раз в недекю, и только ночью
                    if (DateTime.Parse(lastScanTime) < DateTime.Now.AddHours(-24)
                            && DateTime.Now.Hour < 3
                            && DateTime.Today.DayOfWeek == DayOfWeek.Sunday) {
                        Log.Add("avto.pro: парсинг сайта...");
                        await _avto.CheckAsync();
                        await _db.SetParamAsync("AvtoProLastScanTime", DateTime.Now.ToString());
                        Log.Add("avto.pro: парсинг завершен");
                    }
                    ChangeStatus(sender, ButtonStates.Active);
                } catch (Exception x) {
                    ChangeStatus(sender, ButtonStates.ActiveWithProblem);
                    Log.Add("avto.pro: ошибка выгрузки! - " + x.Message);//TODO изменить регистр сообщения 
                    if (x.Message.Contains("timed out") ||
                        x.Message.Contains("already closed") ||
                        x.Message.Contains("invalid session id") ||
                        x.Message.Contains("chrome not reachable")) {
                        _avto?.Quit();
                        await Task.Delay(180000);
                        _avto = new AvtoPro();
                        AvtoPro_Click(sender, e);
                    }
                }
            }
        }
        //CDEK.MARKET
        async void Cdek_Click(object sender, EventArgs e) {
            if (checkBox_CdekSyncActive.Checked) {
                ChangeStatus(sender, ButtonStates.NoActive);
                while (base_rescan_need) await Task.Delay(60000);
                try {
                    await _cdek.SyncCdekAsync(bus, (int)numericUpDown_CdekAddNewCount.Value);
                    label_Cdek.Text = bus.Count(c => c.cdek != null && c.cdek.Contains("http")).ToString();
                } catch (Exception x) {
                    Log.Add("cdek.market: ошибка синхронизации! - " + x.Message);
                    ChangeStatus(sender, ButtonStates.ActiveWithProblem);
                    return;
                }
                if (numericUpDown_СdekCheckUrls.Value > 0) {
                    try {
                        await _cdek.CheckUrlsAsync();
                        await _cdek.ParseSiteAsync();
                    } catch (Exception x) {
                        Log.Add("cdek.market: ошибка при проверке объявлений! - " + x.Message);
                    }
                }
            }
            ChangeStatus(sender, ButtonStates.Active);
        }
        //EUROAUTO.RU
        async void EuroAuto_Click(object sender, EventArgs e) {
            if (DateTime.Now.Hour > 7 && DateTime.Now.Hour % 4 == 0) {
                ChangeStatus(sender, ButtonStates.NoActive);
                while (base_rescan_need) await Task.Delay(60000);
                try {
                    await _euroAuto.SyncAsync(bus);
                    Log.Add("euroauto.ru: выгрузка ок!");
                    ChangeStatus(sender, ButtonStates.Active);
                } catch (Exception x) {
                    Log.Add("euroauto.ru: ошибка выгрузки! - " + x.Message + x.InnerException.Message);
                    ChangeStatus(sender, ButtonStates.ActiveWithProblem);
                }
            }
        }
        //IZAP24.RU
        async void Izap24_Click(object sender, EventArgs e) {
            if (DateTime.Now.Hour > 7 && DateTime.Now.Hour % 4 == 0) {
                ChangeStatus(sender, ButtonStates.NoActive);
                while (base_rescan_need ||
                    (ds == null || ds.Tables.Count == 0))
                    await Task.Delay(60000);
                try {
                    Log.Add("izap24.ru: начинаю выгрузку...");
                    await _izap24.SyncAsync(bus, ds);
                    Log.Add("izap24.ru: выгрузка ок!");
                    ChangeStatus(sender, ButtonStates.Active);
                } catch (Exception x) {
                    Log.Add("izap24.ru: ошибка выгрузки! - " + x.Message + x.InnerException.Message);
                    ChangeStatus(sender, ButtonStates.ActiveWithProblem);
                }
            }
        }
        //YOULA.RU
        async void Youla_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (base_rescan_need || bus.Count == 0) await Task.Delay(30000);
            if (await _youla.StartAsync(bus)) {
                label_Youla.Text = bus.Count(c => c.youla != null && c.youla.Contains("http")).ToString();
                ChangeStatus(sender, ButtonStates.Active);
            } else
                ChangeStatus(sender, ButtonStates.ActiveWithProblem);
        }

        //===========================================
        //=== основной цикл (частота 1 раз в мин) ===
        //===========================================
        private async void timer_sync_Tick(object sender, EventArgs e) {
            //если синхронизация включена и завершен предыдуший цикл
            if (checkBox_sync.Checked && base_can_rescan) {
                //галочка liteSync
                if (await _db.GetParamBoolAsync("useLiteSync") && 
                    bus.Count > 0 &&   //список товаров содержит товары
                   !base_rescan_need) { //и его не нужно перезапрашивать
                    await GoLiteSync();
                } else if (DateTime.Now.AddHours(-1) > dateTimePicker1.Value) { //со времени последней синхронизации прошло больше часа
                    button_BaseGet.PerformClick();
                }
            }
        }
        //оптимизация синхронизации - чтобы не запрашивать всю базу каждый час - запросим только изменения
        async Task GoLiteSync() {
            ChangeStatus(button_BaseGet, ButtonStates.NoActive);
            var stage = "";
            try {
                base_can_rescan = false;
                sync_start = DateTime.Now;
                Log.Add("lite sync started...");
                var lastTime = await _db.GetParamStrAsync("liteScanTime");
                //запросим новые/измененные карточки товаров
                string s = await Class365API.RequestAsync("get", "goods", new Dictionary<string, string>{
                            {"archive", "0"},
                            {"type", "1"},
                            //{"limit", pageLimitBase.ToString()},
                            //{"page", i.ToString()},
                            {"with_additional_fields", "1"},
                            {"with_remains", "1"},
                            {"with_prices", "1"},
                            {"type_price_ids[0]","75524" },
                            {"updated[from]", lastTime}
                    });
                s = s.Replace("\"209325\":", "\"tiu\":")
                    .Replace("\"209326\":", "\"avito\":")
                    .Replace("\"209334\":", "\"drom\":")
                    .Replace("\"209360\":", "\"vk\":")
                    .Replace("\"313971\":", "\"auto\":")
                    .Replace("\"402489\":", "\"youla\":")
                    .Replace("\"657256\":", "\"ks\":")
                    .Replace("\"833179\":", "\"kp\":")
                    .Replace("\"854872\":", "\"gde\":")
                    .Replace("\"1437133\":", "\"avtopro\":")
                    .Replace("\"854874\":", "\"cdek\":");
                lightSyncGoods.Clear();
                if (s.Length > 3) lightSyncGoods.AddRange(JsonConvert.DeserializeObject<RootObject[]>(s));

                stage = "текущие остатки...";
                var ids = JsonConvert.DeserializeObject<List<GoodIds>>(
                    await Class365API.RequestAsync("get", "storegoods", new Dictionary<string, string>{
                        {"updated[from]", lastTime}
                    }));

                stage = "текущие цены...";
                ids.AddRange(JsonConvert.DeserializeObject<List<GoodIds>>(
                    await Class365API.RequestAsync("get", "salepricelistgoods", new Dictionary<string, string>{
                        {"updated[from]", lastTime},
                        {"price_type_id","75524" } //интересуют только отпускные цены
                    })));

                stage = "запросим реализации...";
                ids.AddRange(JsonConvert.DeserializeObject<List<GoodIds>>(
                    await Class365API.RequestAsync("get", "realizationgoods", new Dictionary<string, string>{
                        {"updated[from]", lastTime}
                    })));

                //добавляем к запросу карточки, привязанные к тиу, но с нулевой ценой. решает глюк нулевой цены после поступления
                ids.AddRange(bus.Where(w => w.tiu.Contains("http") && w.price == 0).Select(_ => new GoodIds { good_id = _.id }));

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
                //если изменений слишком много - нужен полный рескан базы
                if (lightSyncGoods.Count > 200) {
                    Log.Add("business.ru: Новых/измененных карточек: " + lightSyncGoods.Count +
                            " -- будет произведен запрос полной базы товаров...");
                    base_rescan_need = true;
                    base_can_rescan = true;
                    ChangeStatus(button_BaseGet, ButtonStates.Active);
                    return;
                }
                Log.Add("business.ru: Новых/измененных карточек: " + lightSyncGoods.Count + " (выложенных на сайте " + lightSyncGoods.Count(c => c.tiu.Contains("http")) + ")");
                stage = "...";

                var ignoreUrlChanges = await _db.GetParamBoolAsync("ignoreUrlChanges");
                //переносим обновления в загруженную базу
                foreach (var lg in lightSyncGoods) {
                    var ind = bus.FindIndex(f => f.id == lg.id);
                    //индекс карточки найден
                    if (ind != -1) {
                        if (ignoreUrlChanges) {
                            if (bus[ind].amount != lg.amount ||             //если изменилось количество или
                                bus[ind].price != lg.price ||               //если изменилась цена или
                                bus[ind].archive != lg.archive ||           //статус архивный или
                                bus[ind].name != lg.name ||                 //изменилось наименование или
                                bus[ind].description != lg.description ||   //изменилось описание или
                                bus[ind].part != lg.part ||                 //изменился артикул
                                bus[ind].weight != lg.weight                //изменился вес
                                ) {
                                //переносим все данные
                                bus[ind] = lg;
                                //время обновления карточки сдвигаю на время начала цикла синхронизации
                                bus[ind].updated = sync_start.ToString();
                            } else {
                                //сохраняю в новых данных старую дату обновления
                                lg.updated = bus[ind].updated;
                                //переношу все новые данные
                                bus[ind] = lg;
                            }
                        }
                    } else { //если индекс карточки не найден - добавляем в коллекцию
                        lg.updated = sync_start.ToString();
                        bus.Add(lg);
                    }
                }

                //база теперь должна быть в актуальном состоянии
                //сохраняем время, на которое база актуальна (только liteScanTime! 
                //lastScanTime сохраним когда перенесем изменения в объявления!!)
                //когда реализуем перенос изменения сразу - можно будет оперировать только одной переменной                

                label_Bus.Text = bus.Count + "/" + bus.Count(c => c.tiu.Contains("http") && c.amount > 0);
                await _db.SetParamAsync("liteScanTime", sync_start.AddMinutes(-1).ToString());
                await _db.SetParamAsync("controlBus", bus.Count.ToString());


                ///вызываем событие в котором сообщаем о том, что в базе есть изменения... на будущее, когда будем переходить на событийную модель
                ///пока события не реализованы в проекте, поэтому пока мы лишь обновили уже загруженной базе только те карточки, 
                ///которые изменились с момента последнего запроса
                ///и дальше сдвинем контрольное время актуальности базы
                ///а дальше всё как обычно, только сайты больше не парсим,
                ///только вызываем методы обработки изменений и подъема упавших

                if (checkBox_sync.Checked && (DateTime.Now.Minute >= 55 || dateTimePicker1.Value.AddMinutes(70) < DateTime.Now)) {
                    button_Avito.PerformClick();
                    await AddPartNumsAsync();//добавление артикулов из описания
                    await CheckArhiveStatusAsync();//проверка архивного статуса
                    await Task.Delay(60000);
                    button_Drom.PerformClick();
                    await Task.Delay(60000);
                    button_Kupiprodai.PerformClick();
                    await Task.Delay(60000);
                    button_Gde.PerformClick();
                    await Task.Delay(60000);
                    button_AvtoPro.PerformClick();
                    await Task.Delay(60000);
                    button_AutoRu.PerformClick();
                    await Task.Delay(60000);
                    button_Satom.PerformClick();
                    await Task.Delay(60000);
                    button_EuroAuto.PerformClick();
                    await Task.Delay(60000);
                    button_Izap24.PerformClick();
                    await Task.Delay(60000);
                    button_Vk.PerformClick();
                    await Task.Delay(60000);
                    button_cdek.PerformClick();
                    await Task.Delay(60000);
                    button_Tiu.PerformClick();
                    await Task.Delay(60000);
                    button_Youla.PerformClick();
                    //нужно подождать конца обновлений объявлений
                    await WaitButtonsActiveAsync();
                    //проверка задвоенности наименований карточек товаров
                    await CheckDublesAsync();//проверка дублей
                    await CheckMultipleApostropheAsync();//проверка лишних аппострофов
                    if (await _db.GetParamBoolAsync("articlesClear")) await ArtCheckAsync();//чистка артикулов от лишних символов
                    await GroupsMoveAsync();//проверка групп
                    await TiuCheckAsync();//исправляем ссылки на тиу
                    await PhotoClearAsync();//очистка ненужных фото
                    dateTimePicker1.Value = sync_start;
                    await SaveBusAsync();
                }
                Log.Add("lite sync complete.");
                base_can_rescan = true;
            } catch (Exception ex) {
                Log.Add("ошибка lite sync: " + ex.Message + "\n"
                    + ex.Source + "\n"
                    + ex.InnerException + "\n"
                    + stage);
                base_can_rescan = true;
                base_rescan_need = true;
            }
            ChangeStatus(button_BaseGet, ButtonStates.Active);
        }
        //полный скан базы бизнес.ру
        async void BaseGet(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            Log.Add("business.ru: старт полного цикла синхронизации");
            base_rescan_need = true;
            base_can_rescan = false;
            sync_start = DateTime.Now;
            await GetBusGroupsAsync();
            await GetBusGoodsAsync2();
            var tlog = bus.Count + "/" + bus.Count(c => c.tiu.Contains("http") && c.amount > 0);
            Log.Add("business.ru: получено товаров с остатками из группы интернет магазин " + tlog + "\nиз них с ценами " + bus.Count(c => c.amount > 0 && c.tiu.Contains("http") && c.price > 0));
            label_Bus.Text = tlog;
            await AddPartNumsAsync();
            await SaveBusAsync();
            base_rescan_need = false;
            //запуск браузеров
            button_Avito.PerformClick();
            await Task.Delay(60000);
            button_Drom.PerformClick();
            await Task.Delay(60000);
            button_Tiu.PerformClick();
            await Task.Delay(60000);
            button_Kupiprodai.PerformClick();
            await Task.Delay(60000);
            button_AvtoPro.PerformClick();
            await Task.Delay(60000);
            button_Vk.PerformClick();
            await Task.Delay(60000);
            button_Gde.PerformClick();
            await Task.Delay(60000);
            button_AutoRu.PerformClick();
            await Task.Delay(60000);
            button_EuroAuto.PerformClick();
            await Task.Delay(60000);
            button_Izap24.PerformClick();
            await Task.Delay(60000);
            button_cdek.PerformClick();
            await Task.Delay(60000);
            button_Youla.PerformClick();
            await WaitButtonsActiveAsync();
            Log.Add("business.ru: полный цикл синхронизации завершен");
            dateTimePicker1.Value = sync_start;
            base_can_rescan = true;
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
                bus.Clear();
                try {
                    for (int i = 1; i > 0; i++) {
                        string s = await Class365API.RequestAsync("get", "goods", new Dictionary<string, string>{
                            {"archive", "0"},
                            {"type", "1"},
                            {"limit", _pageLimitBase.ToString()},
                            {"page", i.ToString()},
                            {"with_additional_fields", "1"},
                            {"with_remains", "1"},
                            {"with_prices", "1"},
                            {"type_price_ids[0]","75524" }
                        });
                        if (s.Contains("name")) {
                            s = s.Replace("\"209325\":", "\"tiu\":")
                                .Replace("\"209326\":", "\"avito\":")
                                .Replace("\"209334\":", "\"drom\":")
                                .Replace("\"209360\":", "\"vk\":")
                                .Replace("\"313971\":", "\"auto\":")
                                .Replace("\"402489\":", "\"youla\":")
                                .Replace("\"657256\":", "\"ks\":")
                                .Replace("\"833179\":", "\"kp\":")
                                .Replace("\"854872\":", "\"gde\":")
                                .Replace("\"1437133\":", "\"avtopro\":")
                                .Replace("\"854874\":", "\"cdek\":");
                            bus.AddRange(JsonConvert.DeserializeObject<List<RootObject>>(s));
                            label_Bus.Text = bus.Count.ToString();
                        } else break;
                        //await Task.Delay(2000);
                    }
                } catch (Exception x) {
                    Log.Add("business.ru: ошибка при запросе товаров из базы!!! - " + x.Message);
                    await Task.Delay(60000);
                }
                await _db.SetParamAsync("controlBus", bus.Count.ToString());
            } while (bus.Count == 0 || Math.Abs(lastScan - bus.Count) > 400);
            Log.Add("business.ru: получено товаров " + bus.Count);
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
                    else break;
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
                        s = s.Replace("\"209325\":", "\"tiu\":")
                            .Replace("\"209326\":", "\"avito\":")
                            .Replace("\"209334\":", "\"drom\":")
                            .Replace("\"209360\":", "\"vk\":")
                            .Replace("\"313971\":", "\"auto\":")
                            .Replace("\"402489\":", "\"youla\":")
                            .Replace("\"657256\":", "\"ks\":")
                            .Replace("\"833179\":", "\"kp\":")
                            .Replace("\"854872\":", "\"gde\":")
                            .Replace("\"1437133\":", "\"avtopro\":")
                            .Replace("\"854874\":", "\"cdek\":");
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
                        File.WriteAllText(@"..\bus.json", JsonConvert.SerializeObject(bus));
                        Log.Add("business.ru: bus.json - сохранение успешно");
                    }
                });
            } catch (Exception x) {
                Log.Add("business.ru: ошибка сохранения локальной базы данных bus.json - " + x.Message);
            }
        }
        //подгружаю товары из файла
        async Task LoadBusJSON() {
            var t = _db.GetParamDateTime("lastScanTime");
            if (File.Exists(@"..\bus.json") && File.GetLastWriteTime(@"..\bus.json") > t) {
                await GetBusGroupsAsync();
                Log.Add("business.ru: загружаю список товаров...");
                await Task.Factory.StartNew(() => {
                    var s = File.ReadAllText(@"..\bus.json");
                    bus = JsonConvert.DeserializeObject<List<RootObject>>(s);
                });
                Log.Add("business.ru: загружено " + bus.Count + " карточек товаров");
                label_Bus.Text = bus.Count + "/" + bus.Count(c => !string.IsNullOrEmpty(c.tiu) && c.tiu.Contains("http") && c.amount > 0);
                button_BaseGet.BackColor = System.Drawing.Color.GreenYellow;
            }
            base_can_rescan = true;
        }
        //загрузка формы
        private async void FormMain_Load(object sender, EventArgs e) {
            //подписываю обработчик на событие
            Log.LogUpdate += LogUpdate;
            //устанавливаю глубину логирования
            Log.Level = await _db.GetParamIntAsync("logSize");
            //меняю заголовок окна
            this.Text += _version;
            _writeLog = await _db.GetParamBoolAsync("writeLog");
            dateTimePicker1.Value = _db.GetParamDateTime("lastScanTime");
            _saveCookiesBeforeClose = await _db.GetParamBoolAsync("saveCookiesBeforeClose");
            await LoadBusJSON();
        }
        // поиск и исправление дубликатов названий
        private async Task CheckDublesAsync() {
            List<string> bus_dubl = new List<string>();
            List<string> bus_upper_names = new List<string>();
            do {
                Task t1 = Task.Factory.StartNew(() => {
                    bus_upper_names = bus.Where(a => !a.name.Contains("Без имени"))
                                         .Select(t => t.name.ToUpper())
                                         .ToList();
                    bus_dubl = bus_upper_names.Where(a => bus_upper_names.Count(f => a == f) > 1)
                                              .Distinct()
                                              .ToList();
                });
                await t1;
                foreach (var d in bus_dubl) {
                    int ind = bus.FindLastIndex(t => t.name.ToUpper() == d);
                    bus[ind].name += !string.IsNullOrEmpty(bus[ind].part) && !bus[ind].name.Contains(bus[ind].part) ? " " + bus[ind].part :
                                     bus[ind].name.EndsWith("`") ? "`" : " `";
                    Thread.Sleep(10);
                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>
                    {
                        { "id" , bus[ind].id},
                        { "name" , bus[ind].name}
                    });
                    Log.Add("база исправлен дубль названия " + bus[ind].name);
                    Thread.Sleep(1000);
                }
            } while (bus_dubl.Count() > 0);
            Thread.Sleep(10);
        }
        //редактирование описаний, добавленние синонимов
        private async void button_put_desc_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            //загрузим словарь
            List<string> eng = new List<string>();
            List<string> rus = new List<string>();
            List<string> file = new List<string>(System.IO.File.ReadAllLines("dict.txt", Encoding.UTF8));
            foreach (var s in file) {
                var ar = s.Split(',');
                eng.Add(ar[0]);
                rus.Add(ar[1]);
            }
            file.Clear();

            //пробегаемся по описаниям карточек базы
            for (_i = 0; _i < bus.Count; _i++) {
                //если есть привязка к тиу
                if (bus[_i].tiu.Contains("tiu.ru")) {
                    bool flag_need_form4 = false;
                    //старое название, нужно обрезать
                    if (bus[_i].description.Contains("Есть и другие")) {
                        bus[_i].description = bus[_i].description.Replace("Есть и другие", "|").Split('|')[0];
                        flag_need_form4 = true;
                    }
                    //для каждого слова из словаря проверим, содержится ли оно в описании
                    for (int d = 0; d < eng.Count; d++) {
                        //если содержит английское написание И не содержит такого же на русском ИЛИ содержит
                        if (bus[_i].description.Contains(eng[d]) && !bus[_i].description.Contains(rus[d])) {
                            bus[_i].description = bus[_i].description.Replace(eng[d], eng[d] + " / " + rus[d]);
                            flag_need_form4 = true;
                            break;
                        }
                    }
                    if (flag_need_form4) {
                        Form f4 = new Form4();
                        f4.Owner = this;
                        f4.ShowDialog();
                        if (f4.DialogResult == DialogResult.OK) {
                            int i = _i;
                            var s = await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>()
                            {
                                {"id", bus[i].id},
                                {"name", bus[i].name},
                                {"description", bus[i].description},
                            });
                            Log.Add("business.ru: " + bus[i].name + " - описание карточки обновлено - " + bus[i].description);
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
                            label_Bus.Text = bus.Count.ToString();
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
            for (int b = 0; b < bus.Count; b++) {
                try {
                    var desc = !String.IsNullOrEmpty(bus[b].description) ? bus[b].description.ToLowerInvariant() : "";

                    //проверим на нулевое значение
                    bus[b].part = bus[b].part ?? "";
                    bus[b].store_code = bus[b].store_code ?? "";
                    if (bus[b].part.Contains(" ")
                        || bus[b].part.Contains(".")
                        || (bus[b].part.Contains("/") && bus[b].tiu.Contains("http"))
                        || (bus[b].part.Contains(",") && bus[b].tiu.Contains("http"))
                        || bus[b].part.Contains("_")
                        || bus[b].store_code.Contains(" ")
                        || bus[b].store_code.Contains(".")
                        //|| bus[b].store_code.Contains("-")
                        || bus[b].store_code.Contains("_")
                        || bus[b].store_code.Contains("(")
                        || bus[b].name.StartsWith(" ")
                        || bus[b].name.EndsWith(" ")
                        || bus[b].name.Contains("  ")
                        || bus[b].name.Contains(";")
                        || bus[b].name.Contains(@"\")
                        || bus[b].name.Contains("!")
                        || bus[b].name.Contains("\t")
                        || bus[b].name.Contains("\u00a0")

                        //|| ((bus[b].name.Contains(" l ") || bus[b].name.Contains(" L ")) && desc.Contains("лев"))
                        //|| ((bus[b].name.Contains(" r ") || bus[b].name.Contains(" R ")) && desc.Contains("прав"))
                        //|| bus[b].name.Contains(" l/r ")
                        //|| bus[b].name.Contains(" L/R ")
                        //|| bus[b].name.Contains(@" l\r ")
                        //|| bus[b].name.Contains(@" L\R ")

                        //или артикул написан маленькими буквами
                        || bus[b].part.ToUpper() != bus[b].part

                        //или описание содержит названия месенджеров (запрещено на многих сайтах)
                        || desc.Contains("viber") || desc.Contains("вайбер")
                    ) {
                        //словарь аргументов
                        var args = new Dictionary<string, string>();

                        //удаляем упоминание в тексте мессенжеров
                        if (!String.IsNullOrEmpty(bus[b].description)) {
                            var newDesc = bus[b].description
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
                            if (newDesc != bus[b].description) {
                                args.Add("description", newDesc);
                                bus[b].description = newDesc;
                            }
                        }
                        var new_part = bus[b].part.Replace(" ", "")
                            .Replace(".", "")
                            .Replace("/", "")
                            .Split(',').First()
                            .Replace("_", "")
                            .ToUpper();

                        var new_store_code = bus[b].store_code.Replace(" ", "")
                            .Replace(".", "")
                            //.Replace("-", "")
                            .Replace("_", "");

                        var new_name = bus[b].name.Trim()
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
                        args.Add("id", bus[b].id);
                        args.Add("name", new_name);
                        args.Add("part", new_part);
                        args.Add("store_code", new_store_code);

                        await Class365API.RequestAsync("put", "goods", args);

                        Log.Add("исправлена карточка\n" +
                              bus[b].name + "\n" +
                              new_name + "\n" +
                              bus[b].part + "\t" + bus[b].store_code + "\n" +
                              new_part + "\t" + new_store_code);

                        bus[b].part = new_part;
                        bus[b].store_code = new_store_code;
                        bus[b].name = new_name;
                        await Task.Delay(1000);
                    }
                } catch (Exception x) {
                    Log.Add("business.ru: " + bus[b].name + " - ошибка при обработке артикулов! - " + x.Message);
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
        private async Task GroupsMoveAsync() {        //перемещение в группу Заказы всех карточек, непривязанных к группам
            for (int b = 0; b < bus.Count; b++) {
                if (bus[b].group_id == "1") {
                    bus[b].group_id = "209277";
                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>
                    {
                        {"id", bus[b].id},
                        {"name", bus[b].name},
                        {"group_id", bus[b].group_id}
                    });
                    Log.Add("business.ru: " + bus[b].name + " --> в группу Заказы");
                    await Task.Delay(1000);
                }
            }
        }

        async Task CheckArhiveStatusAsync() {
            try {
                foreach (var item in bus.Where(w => w.amount > 0 && w.archive)) {
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
            while (!(button_Tiu.Enabled &&
                button_EuroAuto.Enabled &&
                button_Drom.Enabled &&
                button_Vk.Enabled &&
                button_Kupiprodai.Enabled &&
                button_Avito.Enabled &&
                button_AutoRu.Enabled &&
                button_Gde.Enabled &&
                button_cdek.Enabled &&
                button_Youla.Enabled &&
                button_AvtoPro.Enabled)
            ) await Task.Delay(20000);
        }

        async Task CheckMultipleApostropheAsync() {
            try {
                foreach (var item in bus.Where(w => (w.name.Contains("''''") || w.name.Contains("' `")) && w.amount > 0)) {
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
                foreach (var item in bus.Where(w => (!string.IsNullOrEmpty(w.description) && w.description.Contains("№") && string.IsNullOrEmpty(w.part)))) {
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
                scanTime = dateTimePicker1.Value;
                _db.SetParamAsync("lastScanTime", scanTime.ToString());
                _db.SetParamAsync("liteScanTime", scanTime.ToString());
                RootObject.ScanTime = scanTime;
            } catch (Exception x) {
                Log.Add("ошибка изменения даты синхронизации! - " + x.Message + " - " + x.InnerException.Message);
            }
        }

        async void buttonSatom_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            //выгрузка xml через ссылку на товары тиу
            ChangeStatus(sender, ButtonStates.Active);
        }

        private async void button_PricesCheck_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            await ChangePostingsPrices();
            await ChangeRemainsPrices();
            ChangeStatus(sender, ButtonStates.Active);
        }

        //сохранить куки
        private void button_SaveCookie_Click(object sender, EventArgs e) {
            _tiu.SaveCookies();
            _kupiprodai.SaveCookies();
            _avto.SaveCookies();
            _drom.SaveCookies();
            _avito.SaveCookies();
            _auto.SaveCookies();
            _gde.SaveCookies();
            _youla.SaveCookies();
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
                try {//lock
                    if (logBox.InvokeRequired)
                        logBox.Invoke(new Action<string>((a) => logBox.Text += a), s + "\n");
                    else
                        logBox.Text += s + "\n";
                } catch (Exception x) {
                    Console.WriteLine(x.Message);
                    Console.ReadLine();
                }
        }
        //прокрутка лога
        private void richTextBox1_TextChanged(object sender, EventArgs e) {
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        }
        //обработка события на добавление записи
        public void LogUpdate() {
            ToLogBox(Log.LogLastAdded);
        }
        //обработчик изменений значений фильтра лога
        void textBox_LogFilter_TextChanged(object sender, EventArgs e) {
            FillLogAsync();
        }
        //загружаю лог асинхронно
        async void FillLogAsync() {
            try {
                DataTable table = await _db.GetLogAsync(textBox_LogFilter.Text, Log.Level);
                if (table.Rows.Count > 0)
                    logBox.Text = table.Select().Select(s => s[1] + ": " + s[3]).Aggregate((a, b) => b + "\n" + a);
                else logBox.Text = "по заданному фильтру ничего не найдено!";
            } catch (Exception x) {
                Log.Add(x.Message);
            }
        }
        //очистка фильтра
        void button_LogFilterClear_Click(object sender, EventArgs e) {
            textBox_LogFilter.Text = "";
        }
        //закрываем форму, сохраняем настройки
        async void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            this.Visible = false;
            ClearTempFiles();
            if(_saveCookiesBeforeClose) {
                _tiu?.SaveCookies();
                _gde?.SaveCookies();
                _cdek?.SaveCookies();
                _drom?.SaveCookies();
                _avto?.SaveCookies();
                _auto?.SaveCookies();
                _avito?.SaveCookies();
                _kupiprodai?.SaveCookies();
                _youla?.SaveCookies();
            }
            _tiu?.Quit();
            _gde?.Quit();
            _cdek?.Quit();
            _drom?.Quit();
            _avto?.Quit();
            _auto?.Quit();
            _avito?.Quit();
            _kupiprodai?.Quit();
            _youla?.Quit();
        }
        //удаление временных файлов
        void ClearTempFiles() {
            foreach (var file in Directory.EnumerateFiles(Application.StartupPath, "*.jpg")) {
                File.Delete(file);
            }
        }
        //загрузка окна настроек
        void button_SettingsFormOpen_Click(object sender, EventArgs e) {
            FormSettings fs = new FormSettings();
            fs.Owner = this;
            fs.ShowDialog();
            fs.Dispose();
        }
        //массовое изменение цен закупки на товары введенных на остатки
        async Task ChangeRemainsPrices(int procent = 80) {
            //цикл для пагинации запросов
            for (int i = 1; ; i++) {
                //запрашиваю товары из документов "ввод на остатки"
                var s = await Class365API.RequestAsync("get", "remaingoods", new Dictionary<string, string> {
                        {"limit", _pageLimitBase.ToString()},
                        {"page", i.ToString()},
                    });
                //если запрос товаров вернул пустой ответ - товары закончились - прерываю цикл
                if (s.Length <= 4) break;
                //десериализую json в список товаров
                var remainGoods = JsonConvert.DeserializeObject<List<RemainGoods>>(s);
                //перебираю товары из списка
                foreach (var rg in remainGoods) {
                    //индекс карточки товара
                    var indBus = bus.FindIndex(f => f.id == rg.good_id);
                    //если индекс и остаток положительный
                    if (indBus > -1 && bus[indBus].amount > 0) {
                        //цена ввода на остатки (цена закупки)
                        var priceIn = rg.FloatPrice;
                        //цена отдачи (розничная)
                        var priceOut = bus[indBus].price;
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
                                Log.Add("business.ru: " + bus[indBus].name + " - цена закупки изменена с " + priceIn + " на " + newPrice.ToString("#.##"));
                            await Task.Delay(50);
                        }
                    }
                }
            }
        }
        //массовое изменение цен закупки в оприходованиях
        async Task ChangePostingsPrices(int procent = 80) {
            //цикл для пагинации запросов
            for (int i = 1; ; i++) {
                //запрашиваю товары из документов "Оприходования"
                var s = await Class365API.RequestAsync("get", "postinggoods", new Dictionary<string, string> {
                        {"limit", _pageLimitBase.ToString()},
                        {"page", i.ToString()},
                    });
                //если запрос товаров вернул пустой ответ - товары закончились - прерываю цикл
                if (s.Length <= 4) break;
                //десериализую json в список товаров
                var postingGoods = JsonConvert.DeserializeObject<List<PostingGoods>>(s);
                //перебираю товары из списка
                foreach (var pg in postingGoods) {
                    //индекс карточки товара
                    var indBus = bus.FindIndex(f => f.id == pg.good_id);
                    //если индекс и остаток положительный
                    if (indBus > -1 && bus[indBus].amount > 0 && bus[indBus].price > 0) {
                        //цена ввода на остатки (цена закупки)
                        var priceIn = pg.FloatPrice;
                        //цена отдачи (розничная)
                        var priceOut = bus[indBus].price;
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
                                Log.Add("business.ru: " + bus[indBus].name + " - цена оприходования изменена с " + priceIn + " на " + newPrice.ToString("#.##"));
                            await Task.Delay(50);
                        }
                    }
                }
            }
        }
        //удаление фото из карточек
        async Task PhotoClearAsync() {
            var cnt = await _db.GetParamIntAsync("photosCheckCount");
            for (int b = 0, i=0; b < bus.Count && i < cnt; b++) {
                if (bus[b].amount <= 0 &&
                    bus[b].images.Count >0 ||
                  
                    bus[b].amount > 0 &&
                    bus[b].images.Count == 1 &&
                    GetTiuPhotosCount(b) > 1
                    ) {
                    try {
                        await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>(){
                                    {"id", bus[b].id},
                                    {"name", bus[b].name},
                                    {"images", "[]"}
                                });
                        Log.Add(bus[b].name + " ["+b+"] - удалены фото из карточки! (" + bus[b].images.Count + "), остаток - " + bus[b].amount + "]");
                        bus[b].images.Clear();
                        await Task.Delay(10);
                        i++;
                    } catch (Exception x) {
                        Log.Add("ошибка при удалении фото из карточки! - " + bus[b].name + " - " + x.Message);
                    }
                }
            }
        }
        //проверка количества фото в каталоге тиу
        private int GetTiuPhotosCount(int b) {
            try {
                var tiuId = bus[b].tiu.Split('/').Last();        //ищу id товара в каталоге tiu
                if (tiuId.Length > 0) {
                    //ищу запись в таблице offer с таким id - нахожу соответствующий ключ id к таблице picture
                    var idRow = ds.Tables["offer"].Select("id = '" + tiuId + "'");
                    if (idRow.Length == 0) Log.Add("ошибка! "+bus[b].name+" с id = " + tiuId + " - не найден в каталоге тиу!");
                    else {
                        var id = idRow[0]["offer_id"]; //беру поле offer_id из первой найденной строки
                        var image_rows = ds.Tables["picture"].Select("offer_id = '" + id.ToString() + "'"); //все строки со ссылками на фото
                        return image_rows.Count();
                    }
                }
            } catch (Exception x) {
                Log.Add(bus[b].name + " - ошибка поиска фотографий в каталоге тиу! - " + x.Message);
            }
            return 0;
        }
        //отчет остатки по уровню цен
        async void PriceLevelsRemainsReport(object sender, EventArgs e) {
            var priceLevelsStr = _db.GetParamStr("priceLevelsForRemainsReport");
            var priceLevels = JsonConvert.DeserializeObject<int[]>(priceLevelsStr);
            foreach (var price in priceLevels) {
                var x = bus.Count(w => w.tiu.Contains("http") && w.price >= price && w.amount > 0);
                Log.Add("позиций с положительным остатком и ценой " + price + "+ : " + x);
            }
        }
        //метод для тестов
        async void buttonTest_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                for (int b = 0, j = 10; b < bus.Count; b++) {
                    if(!string.IsNullOrEmpty(bus[b].youla) && 
                        bus[b].youla.Contains("youla.io")) {
                        await Class365API.RequestAsync("put","goods",new Dictionary<string, string> {
                            { "id", bus[b].id},
                            { "name", bus[b].name},
                            { "402489","" }
                        });
                        Log.Add("youla.ru: " + bus[b].name + " - удалена старая ссылка из карточки - "+bus[b].youla);
                        bus[b].youla = "";
                        await Task.Delay(2000);
                        j--;
                    }
                }
                
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
            } catch (Exception x) {
                Log.Add(x.Message);
            }
            ChangeStatus(sender, ButtonStates.Active);
        }
    }
}