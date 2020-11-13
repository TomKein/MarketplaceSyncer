using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExcelLibrary.SpreadSheet;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using Application = System.Windows.Forms.Application;
using System.Text.RegularExpressions;
using BlueSimilarity;
using Color = System.Drawing.Color;
using Selen.Sites;
using Selen.Tools;
using Selen.Base;

namespace Selen {
    public partial class FormMain : Form {
        string _version = "1.42.2";
        
        DB _db = new DB();

        public List<RootGroupsObject> busGroups = new List<RootGroupsObject>();
        public List<RootObject> bus = new List<RootObject>();
        private List<RootObject> newTiuGoods = new List<RootObject>();
        public List<RootObject> lightSyncGoods = new List<RootObject>();

        VK _vk = new VK();
        Cdek _cdek = new Cdek();
        Drom _drom = new Drom();
        AvtoPro _avtoPro = new AvtoPro();
        Avito _avito = new Avito();
        AutoRu _autoRu = new AutoRu();

        public IWebDriver tiu;
        public IWebDriver kp;
        public IWebDriver gde;

        int pageLimitBase = 250;

        string tiuXmlUrl = "http://xn--80aejmkqfc6ab8a1b.xn--p1ai/yandex_market.xml?html_description=1&hash_tag=3f22d0f72761b35f77efeaffe7f4bcbe&yandex_cpa=0&group_ids=&exclude_fields=&sales_notes=&product_ids=";

        string dopDescTiu = "Дополнительные фотографии по запросу<br />" +
                            "Есть и другие запчасти на данный автомобиль!<br />" +
                            "Вы можете установить данную деталь на нашем АвтоСервисе с доп.гарантией и со скидкой!<br />" +
                            "Перед выездом обязательно уточняйте наличие запчастей по телефону - товар на складе!<br />" +
                            "Отправляем наложенным платежом ТК СДЭК (только негабаритные посылки)<br />" +
                            "Бесплатная доставка до транспортной компании!";

        string[] dopDescTiu2 = {
            "Дополнительные фотографии по запросу",
            "Есть и другие запчасти на данный автомобиль!",
            "Вы можете установить данную деталь на нашем АвтоСервисе с доп.гарантией и со скидкой!",
            "Перед выездом обязательно уточняйте наличие запчастей по телефону - товар на складе!",
            "Отправляем наложенным платежом ТК СДЭК (только негабаритные посылки)",
            "Бесплатная доставка до транспортной компании!"
        };

        string[] dopDescKP = {
            "Дополнительные фотографии по запросу",
            "Есть и другие запчасти на данный автомобиль!",
            "Вы можете установить данную деталь в нашем АвтоСервисе с доп.гарантией и со скидкой!",
            "Перед выездом уточняйте наличие по телефону - товар на складе!",
            "Звонить строго с 9:00 до 19:00 (воскресенье - выходной)",
            "Бесплатная доставка до транспортной компании!",
            "Отправляем наложенным платежом ТК СДЭК (только негабаритные посылки)",
            "Осуществляем бесплатную доставку своим транспортом до следующих городов: Малоярославец, Обнинск, Тула (посёлок Иншинский)"
        };

        //настройки и контрольные значения
        string fSet = "set.xml";
        //имена файлов для выгрузки тиу
        string _fexp = "ex3.xls";
        string _ftemp = "tmpl.xls";

        //для возврата из форм
        public List<string> lForm3 = new List<string>();

        public string nForm3 = "";

        public string BindedName = "";

        //глобальный индекс для формы
        public int _i;

        //флаг - нужен рескан базы
        bool base_rescan_need = true;

        //флаг - можно запускать новый цикл синхронизации
        bool base_can_rescan = true;

        //флаг - были ошибки при обработке изменений - возможно не все объявления обновлены
        bool wasErrors = false;

        int tiuCount = 0;

        //время запуска очередной синхронизации
        DateTime sync_start;
        DateTime scanTime;

        private Object thisLock = new Object();

        //конструктор формы
        public FormMain() {
            InitializeComponent();
        }

        //===================================
        //основной цикл (частота 1 раз в мин)
        //===================================
        private async void timer_sync_Tick(object sender, EventArgs e) {
            if (base_can_rescan)//тру, если завершен предыдуший цикл
            {
                if (checkBox_sync.Checked)//синхронизация включена
                {
                    if (checkBox_liteSync.Checked && bus.Count > 0 && !base_rescan_need)//включен также ключ лайтсинк и база не нуждается в полной перезагрузке
                    {
                        await GoLiteSync();
                    } else if (base_rescan_need ||
                               DateTime.Now.Minute > 55 &&
                               (DateTime.Now.AddMinutes(-90) >
                                   DateTime.Parse(dSet.Tables["controls"].Rows[0]["lastScanTime"].ToString()))) { //со времени последней синхронизации прошло больше 30 мин
                        button_base_get.PerformClick();
                    }
                }
            }
        }

        //загрузка файла настроек
        private void dsOptions_Initialized(object sender, EventArgs e) {
            try {
                dSet.ReadXml(fSet);
                dateTimePicker1.Value = _db.GetParamDateTime("lastScanTime");
            } catch (Exception) {
                MessageBox.Show("ошибка чтения set.xml");
                Thread.Sleep(5000);
            }
        }
        //закрываем форму, сохраняем настройки
        private void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            this.Visible = false;
            dSet.WriteXml(fSet);
            ClearTempFiles();
            tiu?.Quit();
            kp?.Quit();
            gde?.Quit();
            _avito?.Quit();
            _cdek?.Quit();
            _drom?.Quit();
            _avtoPro?.Quit();
            _autoRu?.Quit();
        }

        void ClearTempFiles() {
            foreach (var file in Directory.EnumerateFiles(Application.StartupPath, "*.jpg")) {
                File.Delete(file);
            }
        }

        //=== полный скан базы бизнес.ру ==//
        private async void BaseGet(object sender, EventArgs e) {
            button_base_get.BackColor = Color.Yellow;
            button_base_get.Enabled = false;
            base_rescan_need = true;
            base_can_rescan = false;
            //сбрасываем флаг ошибки редактирования
            wasErrors = false;
            sync_start = DateTime.Now;
            if (checkBox_sync.Checked) {
                button_avito_get.PerformClick();
                await Task.Delay(30000);
                button_drom_get.PerformClick();
                await Task.Delay(30000);
                button_cdek.PerformClick();
                await Task.Delay(30000);
                button_AutoRuStart.PerformClick();
                await Task.Delay(30000);
                buttonKupiprodai.PerformClick();
                await Task.Delay(30000);
                button_GdeGet.PerformClick();
                await Task.Delay(30000);
                button_tiu_sync.PerformClick();
                await Task.Delay(30000);
                button_avto_pro.PerformClick();
                await Task.Delay(30000);
                button_vk_sync.PerformClick();
            }
            await GetBusGroupsAsync();
            await GetBusGoodsAsync2();

            await AddPartNumsAsync();//добавление артикулов из описания

            var tlog = bus.Count + "/" + bus.Count(c => c.tiu.Contains("http") && c.amount > 0);
            Log.Add("business.ru: получено товаров с остатками из группы интернет магазин " + tlog);
            Log.Add("из них с ценами " + bus.Count(c => c.amount > 0 && c.tiu.Contains("http") && c.price > 0));
            label_bus.Text = tlog;

            await SaveBus();

            base_rescan_need = false;
            base_can_rescan = true;

            while (!IsButtonsActive()) {
                await Task.Delay(60000);
            }
            button_base_get.BackColor = Color.GreenYellow;

            //если авто синхронизация включена
            if (checkBox_sync.Checked) {
                button_tiu_sync.PerformClick();
            }
        }
        //запрашиваю группы товаров
        public async Task GetBusGroupsAsync() {
            do {
                busGroups.Clear();
                try {
                    var tmp = await Class365API.RequestAsync("get", "groupsofgoods", new Dictionary<string, string>{
                        //{"parent_id", "205352"} // интернет магазиy БУ запчасти
                    });
                    var tmp2 = JsonConvert.DeserializeObject<List<RootGroupsObject>>(tmp);
                    busGroups.AddRange(tmp2);
                } catch (Exception x) {
                    Log.Add("business.ru: ошибка запроса групп товаров из базы!!! - " + x.Message + " - " + x.InnerException.Message);
                    await Task.Delay(60000);
                }
            } while (busGroups.Count < 20);
            Log.Add("business.ru: получено "+ busGroups.Count +" групп товаров");
            RootObject.Groups = busGroups;
        }
        //получаю карточки товаров
        public async Task GetBusGoodsAsync2() {
            int lastScan;
            do {
                lastScan = Convert.ToInt32(dSet.Tables["controls"].Rows[0]["controlBus"]);
                bus.Clear();
                try {
                    for (int i = 1; i > 0; i++) {
                        string s = await Class365API.RequestAsync("get", "goods", new Dictionary<string, string>{
                            {"archive", "0"},
                            {"type", "1"},
                            {"limit", pageLimitBase.ToString()},
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
                            label_bus.Text = bus.Count.ToString();
                        } else break;
                        await Task.Delay(2000);
                    }
                } catch (Exception x) {
                    Log.Add("business.ru: ошибка при запросе карточек товаров из базы!!! - " + x.Message);
                    await Task.Delay(60000);
                }
                dSet.Tables["controls"].Rows[0]["controlBus"] = bus.Count.ToString();
            } while (bus.Count == 0 || Math.Abs(lastScan - bus.Count) > 400);
            Log.Add("business.ru: получено карточек товаров " + bus.Count);
            dSet.WriteXml(fSet);
        }

        public async Task<List<RootObject>> GetBusGoodsAsync(List<string> ids) {
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

        async Task SaveBus() {
            if (checkBox_BusSave.Checked) {
                try {
                    await Task.Factory.StartNew(() => {
                        File.WriteAllText("bus.json", JsonConvert.SerializeObject(bus));
                    });
                } catch (Exception x) {
                    Log.Add("Ошибка сохранения bus.json");
                }
                Log.Add("bus.json - сохранение успешно");
            }
        }

        async Task LoadBus() {
            if (File.Exists("bus.json")) {
                await GetBusGroupsAsync();
                await Task.Factory.StartNew(() => {
                    var s = File.ReadAllText("bus.json");
                    bus = JsonConvert.DeserializeObject<List<RootObject>>(s);
                });
                label_bus.Text = bus.Count + "/" + bus.Count(c => !string.IsNullOrEmpty(c.tiu) && c.tiu.Contains("http") && c.amount > 0);
                button_base_get.BackColor = System.Drawing.Color.GreenYellow;
                base_rescan_need = false;
                dSet.Tables["controls"].Rows[0]["liteScanTime"] = dateTimePicker1.Value.AddMinutes(-1);
                dSet.WriteXml(fSet);
                await GoLiteSync();
            }
        }
        //=== авито ===
        public async void AvitoGetAsync(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            if (_db.GetParamBool("avito.syncEnable")) {
                try {
                    while (base_rescan_need) await Task.Delay(30000);
                    await _avito.AvitoStartAsync(bus);
                } catch (Exception x) {
                    Log.Add("avito.ru: ошибка выгрузки! - " + x.Message);
                    if (x.Message.Contains("timed out") ||
                        x.Message.Contains("already closed") ||
                        x.Message.Contains("invalid session id") ||
                        x.Message.Contains("chrome not reachable")) {
                        Log.Add("avito.ru: ошибка браузера, перезапуск через 5 минут...");
                        await Task.Delay(120000);
                        _avito.Quit();
                        AvitoGetAsync(sender, e);
                    }
                    ChangeStatus(sender, ButtonStates.ActiveWithProblem);
                }
            }
            ChangeStatus(sender, ButtonStates.Active);
        }
        //==========
        //    ВК
        //==========
        private async void VkSyncAsync(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                Log.Add("вк начало выгрузки");
                while (base_rescan_need) await Task.Delay(20000);
                _vk.AddCount = (int) numericUpDown_vkAdd.Value;
                await _vk.VkSyncAsync(bus);
                Log.Add("вк выгрузка завершена");
                ChangeStatus(sender, ButtonStates.Active);
            } catch (Exception x) {
                Log.Add("ошибка синхронизации вк: " + x.Message);
                ChangeStatus(sender, ButtonStates.ActiveWithProblem);
            }
        }
        private void numericUpDown_vkAdd_ValueChanged(object sender, EventArgs e) {
            try {
                _vk.AddCount = (int)numericUpDown_vkAdd.Value;
            } catch (Exception x) {
                Log.Add("ВК: ошибка установки количества добавляемых объявлений");
            }
        }
        //==============
        //    TIU.RU
        //==============
        private async void TiuSyncAsync(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                while (base_rescan_need) {await Task.Delay(10000);}
                await TiuUploadAsync();
                await TiuHide();
                await AddPhotosToBaseAsync();
                await AddSupplyAsync();
                await TiuSyncAsync2();
                ChangeStatus(sender, ButtonStates.Active); 
            } catch (Exception x) {
                Log.Add("tiu.ru: ошибка синхронизации - " + x.Message);
                ChangeStatus(sender, ButtonStates.ActiveWithProblem); 
            }
        }

        async Task TiuUploadAsync() {
            try {
                //загрузим xml с тиу сразу в dataSet на фоне
                bool loadSuccess = true;
                newTiuGoods.Clear();
                try {
                    await Task.Factory.StartNew(() => {
                        tiuCount = 0;
                        ds.Clear();
                        ds.ReadXml(tiuXmlUrl);
                    });
                } catch {
                    loadSuccess = false;
                    Log.Add("tiu.ru: ошибка запроса XML!");
                }
                if (loadSuccess /*&& sync_start.Hour % 4 == 0 && sync_start.Minute > 55*/) {

                    Workbook wb = Workbook.Load(_fexp);
                    Worksheet ws = wb.Worksheets[0];

                    //проверяем количество полученных товарных позиций
                    tiuCount = ds.Tables["offer"].Rows.Count;
                    label_tiu.Text = tiuCount.ToString();
                    Log.Add("tiu.ru: получено объявлений " + tiuCount.ToString());

                    while (base_rescan_need || bus.Count == 0) {
                        Log.Add("tiu.ru: ожидаю загрузку базы... ");
                        await Task.Delay(60000);
                    }

                    Log.Add("tiu.ru: готовлю файл выгрузки...");
                    do {
                        try {
                            File.Copy(Application.StartupPath + "\\" + _ftemp, Application.StartupPath + "\\" + _fexp, true);
                            break;
                        } catch (Exception x) {
                            Log.Add("tiu.ru: ошибка доступа к файлу! - " + x.Message);
                            await Task.Delay(30000);
                        }
                    } while (true);

                    //текущий индекс строки для записи в таблицу
                    int iRow = 0;

                    //для каждого товара из базы товаров
                    for (int i = 0; i < bus.Count; i++) {
                        //есть привязка к тиу и есть цена, значит выгружаем на тиу
                        if (bus[i].tiu.Contains("tiu.ru")) {
                            //получаем id товара тиу из ссылки
                            string idTiu = bus[i].tiu.Replace("/edit/", "|").Split('|')[1];

                            //ищем строку с таким товаром
                            var tRow = ds.Tables["offer"].Select("id = '" + idTiu + "'");

                            //пишем строку в экспорт если
                            if (tRow.Length > 0
                                 || (bus[i].amount > 0 && bus[i].price > 0)
                            ) {
                                iRow++;
                                //запишем в экспорт новую строку
                                string s = bus[i].description.Replace("&nbsp;", " ")
                                    .Replace("&quot;", "")
                                    .Replace(" &gt", "")
                                    .Replace("Есть и другие", "|")
                                    .Split('|')[0];
                                if (bus[i].IsGroupValid()) {
                                    s += dopDescTiu;
                                }
                                string m = bus[i].measure_id == "11"
                                    ? "пара"
                                    : bus[i].measure_id == "13"
                                        ? "комплект"
                                        : bus[i].measure_id == "9"
                                            ? "упаковка"
                                            : "шт.";
                                string a = bus[i].amount > 0 ? "+" : "-";
                                if (!string.IsNullOrEmpty(bus[i].part)) ws.Cells[iRow, 0] = new Cell(bus[i].part);
                                ws.Cells[iRow, 1] = new Cell(bus[i].name);
                                ws.Cells[iRow, 3] = new Cell(s);
                                ws.Cells[iRow, 5] = new Cell(bus[i].price > 0 ? bus[i].price : 100);
                                ws.Cells[iRow, 6] = new Cell("RUB");
                                ws.Cells[iRow, 7] = new Cell(m);
                                ws.Cells[iRow, 12] = new Cell(a);
                                ws.Cells[iRow, 13] = new Cell(bus[i].amount > 0 ? (int)bus[i].amount : 0);
                                ws.Cells[iRow, 20] = new Cell(idTiu);
                            } else {
                                if (bus[i].amount > 0)
                                    Log.Add("tiu.ru: ошибка! - tiu_ID = " + idTiu + " из ссылки карточки не найден в выгрузке XML - " + bus[i].name);
                            }
                        }
                    }
                    label_tiu.Text = iRow.ToString();

                    CheckTiuUrlsInBus();
                    //сохраняем изменения
                    wb.Save(Application.StartupPath + "\\" + _fexp);
                    SftpClient.Upload(Application.StartupPath + "\\" + _fexp);
                }
            } catch (Exception x) {
                Log.Add("tiu.ru: ошибка выгрузки - " + x.Message);
            }
        }
        //проверяю, есть ли в базе карточки без ссылки на тиу
        private void CheckTiuUrlsInBus() {
            var goods = bus.Where(w => w.images.Count > 0 && w.amount > 0 && !w.tiu.Contains("tiu.ru"))
                .Select(s => s.name)
                .ToList();
            foreach (var good in goods) {
                Log.Add("tiu.ru: ошибка! - карточка есть на остатках, с фотографиями, но нет ссылки на объявление! - " + good);
            }
        }
        //редактируем объявления
        private async Task TiuSyncAsync2() {
            for (int b = 0; b < bus.Count; b++) {
                if (bus[b].tiu != null && bus[b].tiu.Contains("http") &&
                    bus[b].IsTimeUpDated() && bus[b].price > 0) {
                    try {
                        await Task.Factory.StartNew(() => {
                            TiuOfferUpdate(b);
                        });
                    } catch (Exception x) {
                        if (x.Message.Contains("timed out") ||
                            x.Message.Contains("already closed") ||
                            x.Message.Contains("invalid session id") ||
                            x.Message.Contains("chrome not reachable")) {
                            tiu.Quit();
                            tiu = null;
                            Log.Add("tiu.ru: ошибка браузера, перезапуск - " + x.Message);
                        } else
                            Log.Add("tiu.ru: ошибка! - " + x.Message);
                    }
                }
            }
        }
        //готовим описание для tiu.ru
        private List<string> GetTiuDesc(int b) {
            var s = Regex.Replace(bus[b].description
                        .Replace("Есть и другие", "|")
                        .Split('|')[0]
                        .Replace("\n", "|")
                        .Replace("<br />", "|")
                        .Replace("<br>", "|")
                        .Replace("</p>", "|")
                        .Replace("&nbsp;", " ")
                        .Replace("&quot;", "")
                        .Replace("&gt;", "")
                        .Replace(" &gt", ""),
                    "<[^>]+>", string.Empty)
                .Split(new[] { "|" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ta => ta.Trim())
                .Where(tb => tb.Length > 1)
                .ToList();

            if (bus[b].IsGroupValid()) {
                s.AddRange(dopDescTiu2);
            }
            //контролируем длину описания
            var newSLength = 0;
            for (int u = 0; u < s.Count; u++) {
                if (s[u].Length + newSLength > 2990) {
                    s.Remove(s[u]);
                    u = u - 1;
                } else {
                    newSLength = newSLength + s[u].Length;
                }
            }
            return s;
        }

        private void TiuCheckPopup() {
            var el = tiu.FindElements(By.XPath("//div[contains(@class,'sliding-panel__close-btn')]"));
            if (el.Count > 0)
                el.First().Click();
        }

        private void TiuOfferUpdate(int b) {
            tiu.Navigate().GoToUrl(bus[b].tiu);
            TiuCheckPopup();
            WriteToIWebElement(tiu, tiu.FindElement(By.CssSelector("input[data-qaid='product_name_input']")), bus[b].name);
            WriteToIWebElement(tiu, tiu.FindElement(By.XPath("//*[@id='cke_1_contents']/iframe")), sl: GetTiuDesc(b));

            var status = tiu.FindElement(By.CssSelector(".b-product-edit__partition .b-product-edit__partition div.js-toggle"));
            var curStatus = status.FindElement(By.CssSelector("span span")).Text;

            if (bus[b].amount > 0) {
                var displayStatus = tiu.FindElement(By.XPath("//div[@data-qaid='visibility_block']/div[1]//input"));//радио батн опубликован
                displayStatus.Click();
                if (curStatus != "В наличии") {
                    status.Click();
                    Thread.Sleep(3000);
                    var newStatus = tiu.FindElements(By.CssSelector("div[data-qaid='presence_stock_block'] .b-drop-down__list-item"))
                                       .Where(w => w.Text == "В наличии").First();
                    newStatus.Click();
                }
            }
            if (bus[b].amount <= 0) {
                var displayStatus = tiu.FindElement(By.XPath("//div[@data-qaid='visibility_block']/div[3]//input"));//радио батн скрытый
                displayStatus.Click();
                if (curStatus != "Нет в наличии") {
                    status.Click();
                    Thread.Sleep(3000);
                    var newStatus = tiu.FindElements(By.CssSelector("div[data-qaid='presence_stock_block'] .b-drop-down__list-item"))
                                       .Where(w => w.Text == "Нет в наличии").First();
                    newStatus.Click();
                }
            }
            Thread.Sleep(1000);
            WriteToIWebElement(tiu, tiu.FindElement(By.CssSelector("input[data-qaid='product_price_input']")), bus[b].price.ToString());
            //если нулевое количество 0 не пишем, просто удаляем что там указано
            var cnt = Math.Round(bus[b].amount, 0) > 0 ? Math.Round(bus[b].amount, 0).ToString() : OpenQA.Selenium.Keys.Backspace;
            WriteToIWebElement(tiu, tiu.FindElement(By.CssSelector("input[data-qaid='stock_input']")), cnt);
            var but = tiu.FindElement(By.XPath("//button[@data-qaid='save_return_to_list']"));
            but.Click();
            Thread.Sleep(21000);
        }
        //добавляем фото с тиу в карточки товаров
        private async Task AddPhotosToBaseAsync() {
            if (checkBox_busPhotosUpload.Checked) {
                for (int b = 0; b < bus.Count; b++) {
                    if (bus[b].tiu.Contains("http") && bus[b].images.Count == 0 && bus[b].amount > 0) {
                        var imgUrls = new List<string>();
                        var t = Task.Factory.StartNew(() => {
                            tiu.Navigate().GoToUrl(bus[b].tiu);
                            Thread.Sleep(2000);
                            imgUrls.AddRange(tiu.FindElements(By.CssSelector(".b-uploader-extend__image-holder-img"))
                                               .Select(s => s.GetAttribute("src")
                                               .Split('?')
                                               .First()
                                               .Replace("w100_h100", "w1280_h1280")));
                        });
                        try {
                            await t;
                            if (imgUrls.Count > 0) {
                                for (int i = 0; i < imgUrls.Count; i++) {
                                    bus[b].images.Add(new Image() {
                                        name = imgUrls[i].Split('/').Last(),
                                        url = imgUrls[i]
                                    });
                                }

                                var s = await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>(){
                                        {"id", bus[b].id},
                                        {"name", bus[b].name},
                                        {"images", JsonConvert.SerializeObject(bus[b].images.ToArray())}
                                });
                                Log.Add("НЕТ ИЗОБРАЖЕНИЙ... ПОДГРУЖЕНЫ ИЗ ТИУ!\n" + bus[b].name + "\n" + s);
                                Thread.Sleep(3000);
                            }
                        } catch {
                            Log.Add("Ошибка загрузки фотографий с тиу в базу\n" + bus[b].name);
                        }
                    }
                }
            }
        }
        public async Task WaitTiuAsync() {
            while (tiuCount == 0) {
                Log.Add("ожидаем загрузку tiu...");
                await Task.Factory.StartNew(() => { Thread.Sleep(10000); });
            }
        }
        //скрываем на тиу объявления, которых нет в наличии
        private async Task TiuHide() {
            if (tiu == null) {
                await Task.Factory.StartNew(() => {
                    ChromeDriverService chromeservice = ChromeDriverService.CreateDefaultService();
                    chromeservice.HideCommandPromptWindow = true;
                    tiu = new ChromeDriver(chromeservice);
                    tiu.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
                    Thread.Sleep(2000);
                    tiu.Navigate().GoToUrl("https://my.tiu.ru/cms");
                    LoadCookies(tiu, "tiu.json");
                });
            }
            Task ts = Task.Factory.StartNew(() => {
                tiu.Navigate().GoToUrl("https://my.tiu.ru/cms/product?status=0&presence=not_avail");
                Thread.Sleep(3000);
                try {
                    tiu.FindElement(By.Id("phone_email")).SendKeys("9106027626@mail.ru");
                    Thread.Sleep(3000);
                    tiu.FindElement(By.XPath("//button[@id='phoneEmailConfirmButton']")).Click();
                    Thread.Sleep(3000);
                    tiu.FindElement(By.Id("enterPassword")).SendKeys("RAD00239000");
                    Thread.Sleep(3000);
                    tiu.FindElement(By.Id("enterPasswordConfirmButton")).Click();
                    Thread.Sleep(3000);
                    while (tiu.Url.Contains("sign-in")) {
                        Log.Add("tiu.ru: ошибка авторизации! ожидаю вход в кабинет...");
                        Thread.Sleep(60000);
                    }
                    Log.Add("tiu.ru: продолжаю работу...");
                } catch { }
                SaveCookies(tiu, "tiu.json");
                tiu.Navigate().Refresh();
                Thread.Sleep(3000);
                //закрываю окно объявления
                var bat = tiu.FindElements(By.XPath("//button/span[contains(text(),'Хорошо')]/.."));
                if (bat.Count > 0) bat.First().Click();
            });
            try {
                await ts;
                //галочка выделить все элементы
                tiu.FindElement(By.CssSelector("input[data-qaid='select_all_chbx']")).Click();
                Thread.Sleep(2000);
                //жмем кнопку выбора действия
                tiu.FindElement(By.CssSelector("span[data-qaid='selector_button']")).Click();
                Thread.Sleep(2000);
                //жмем изменить видимость
                var c = tiu.FindElements(By.XPath("//div[text()='Изменить видимость']")).First();
                Actions a = new Actions(tiu);
                a.MoveToElement(c).Perform();
                Thread.Sleep(1000);
                a.Click().Perform();
                Thread.Sleep(1000);
                //жмем скрытые
                c = tiu.FindElements(By.XPath("//div[text()='Скрытые']")).First();
                a.MoveToElement(c).Perform();
                Thread.Sleep(1000);
                c.Click();
                Thread.Sleep(2000);
            } catch (Exception x) {
                if (x.Message.Contains("timed out") ||
                x.Message.Contains("already closed") ||
                x.Message.Contains("invalid session id") ||
                x.Message.Contains("chrome not reachable")) {
                    tiu.Quit();
                    tiu = null;
                }
                //Log.Add("tiu.ru: ошибка - " + x.Message);
            }
        }

        //===================================
        // обработка дром
        //===================================
        private async void DromGetAsync(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                while (base_rescan_need) { await Task.Delay(20000); }
                await _drom.DromStartAsync(
                    bus,
                    (int)numericUpDown_dromAddCount.Value,
                    (int)numericUpDown_DromCheckPageCount.Value);
                label_drom.Text = bus.Count(c => !string.IsNullOrEmpty(c.drom) && c.drom.Contains("http") && c.amount>0).ToString();
                ChangeStatus(sender, ButtonStates.Active);
            } catch (Exception x) {
                Log.Add("drom.ru: ошибка синхронизации! \n" + x.Message + "\n" + x.InnerException.Message);
                if (x.Message.Contains("timed out") ||
                    x.Message.Contains("already closed") ||
                    x.Message.Contains("invalid session id") ||
                    x.Message.Contains("chrome not reachable")) {
                    _drom.Quit();
                    await Task.Delay(30000);
                    DromGetAsync(sender,e);
                }
                ChangeStatus(sender, ButtonStates.ActiveWithProblem);
            }
        }

        async void ButtonDromAddNewClick(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            await _drom.AddAsync((int)numericUpDown_dromAddCount.Value);
            ChangeStatus(sender, ButtonStates.Active);
        }

        //========================================
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

                            //await  GoScriptAsync(hostPhp[hostInd]
                            //                    + "set_desc.php?id=" + bus[tmp_ind].id
                            //                    + "&name=" + bus[tmp_ind].name
                            //                    + "&description=" + bus[tmp_ind].description);
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
                            {"limit", pageLimitBase.ToString()},
                            {"page", i.ToString()}
                        });

                        if (s.Length > 3) {
                            pc.AddRange(JsonConvert.DeserializeObject<PartnerContactinfoClass[]>(s));
                            Thread.Sleep(500);
                            label_bus.Text = bus.Count.ToString();
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

        //использование памяти
        private void timer2_Tick(object sender, EventArgs e) {
            label_Mem_Usage.Text = Convert.ToString(GC.GetTotalMemory(true) / 1024) + " kB  wasErrors:" + wasErrors;
            button_base_get.Enabled = true;
        }

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
                    Log.Add("business.ru: " + bus[b].name + " - ошибка при обработке артикулов! - "+ x.Message);
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
                    Log.Add("business.ru: "+ bus[b].name + " --> в группу Заказы");
                    await Task.Delay(1000);
                }
            }
        }

        //=== AUTO.RU ===
        private async void button_AutoRuStart_Click(object sender, EventArgs e) {
            if (checkBox_AutoRuSyncEnable.Checked) {
                ChangeStatus(sender, ButtonStates.NoActive);
                try {
                    Log.Add("auto.ru: начало выгрузки...");
                    while (base_rescan_need) await Task.Delay(30000);
                    _autoRu.AddCount = (int)numericUpDown_AutoRuAddCount.Value;
                    await _autoRu.AutoRuStartAsync(bus);
                    Log.Add("auto.ru: выгрузка завершена!");
                    ChangeStatus(sender, ButtonStates.Active);
                } catch (Exception x) {
                    ChangeStatus(sender, ButtonStates.ActiveWithProblem);
                    Log.Add("auto.ru: ошибка выгрузки! - " + x.Message);
                    if (x.Message.Contains("timed out") ||
                        x.Message.Contains("already closed") ||
                        x.Message.Contains("invalid session id") ||
                        x.Message.Contains("chrome not reachable")) {
                        _autoRu?.Quit();
                        await Task.Delay(180000);
                        _autoRu = new AutoRu();
                        button_AutoRuStart_Click(sender, e);
                    }
                }
            }
        }
        //метод обработчик изменений количества добавляемых объявлений
        private void numericUpDown_auto_ValueChanged(object sender, EventArgs e) {
            _autoRu.AddCount = (int)numericUpDown_AutoRuAddCount.Value;
        }

        //поступление товаров с тиу - создаю карточки, остатки и цены
        public async Task AddSupplyAsync() {
            try {
                //проверяем, для каких товаров нужно сделать новые карточки и поступление
                tiuCount = ds.Tables.Count > 0 ? ds.Tables["offer"].Rows.Count : 0;

                for (int ti = 0; ti < tiuCount; ti++) {
                    string tiuName = ds.Tables["offer"].Rows[ti]["name"].ToString();
                    string tiuId = ds.Tables["offer"].Rows[ti]["id"].ToString();
                    int indBus = bus.FindIndex(f => !String.IsNullOrEmpty(f.tiu) && f.tiu.Contains(tiuId));
                    if (indBus == -1) {
                        string desc = Regex.Replace(ds.Tables["offer"].Rows[ti]["description"].ToString()
                                    .Replace("Есть и другие", "|").Split('|')[0]
                                    .Replace("&nbsp;", " ")
                                    .Replace("&", " ")
                                    .Replace("<br />", "|")
                                    .Replace("</p>", "|"),
                                "<[^>]+>", string.Empty)
                            .Replace("|", "<br />")
                            .Replace("\n", "")
                            .Replace("Есть и другие", "|").Split('|')[0];

                        string categoryId = ds.Tables["offer"].Rows[ti]["categoryId"].ToString();
                        var rows = ds.Tables["category"].Select("id = '" + categoryId + "'");
                        string category_Text = rows[0]["category_Text"].ToString();

                        var offer_id = ds.Tables["offer"].Rows[ti]["offer_id"];

                        //проверим, нет ли повторений наименования
                        string[] bus_dubles;
                        do {
                            bus_dubles = bus.Select(tn => tn.name.ToUpper()).Where(f => f == tiuName.ToUpper()).ToArray();
                            if (bus_dubles.Length > 0) {
                                tiuName += tiuName.EndsWith("`") ? "`" : " `";
                                Log.Add("Исправлен дубль названия в создаваемой карточке \n" + tiuName);
                            }
                        } while (bus_dubles.Length > 0);
                        //добавим в список товаров, на которые нужно сделать поступление и цены
                        string group_id;
                        try {
                            group_id = busGroups.Find(f => f.name.Contains(category_Text.Trim())).id;
                        } catch {
                            group_id = "169326";
                        }

                        newTiuGoods.Add(new RootObject() {
                            name = tiuName,
                            tiu = "https://my.tiu.ru/cms/product/edit/" + tiuId,
                            description = desc,
                            group_id = group_id
                        });
                    }
                }
                foreach (var good in newTiuGoods) {
                    //подгружаем цену, количество и ед имз. для каждого товара
                    try {
                        tiu.Navigate().GoToUrl(good.tiu);
                        good.price = int.Parse(tiu.FindElement(By.CssSelector("input[data-qaid='product_price_input']")).GetAttribute("value"));
                        good.amount = int.Parse(tiu.FindElement(By.CssSelector("input[data-qaid='stock_input']")).GetAttribute("value"));
                        if (good.amount == 0) good.amount = 1;
                    } catch (Exception x) {
                        Log.Add(x.Message);
                    }
                    var measureTiu = tiu.FindElement(By.CssSelector("div[data-qaid='unit_dd'] span")).Text;
                    good.measure_id = measureTiu == "пара"
                        ? "11"
                        : measureTiu == "комплект"
                            ? "13"
                            : measureTiu == "упаковка"
                                ? "9"
                                : "1";
                    //создаем карточку товара
                    good.id = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                        await Class365API.RequestAsync("post", "goods", new Dictionary<string, string>()
                        {
                            {"name", good.name},
                            {"group_id", good.group_id},
                            {"description", good.description},
                            {"209325", good.tiu}
                        })).id;
                    //если единицы измерения не шт, то необходимо поменять привязку ед. изм. в карточке
                    if (good.measure_id != "1") {
                        //находим привязку единиц измерения
                        var gm = JsonConvert.DeserializeObject<GoodsMeasures[]>(
                            await Class365API.RequestAsync("get", "goodsmeasures", new Dictionary<string, string>()
                            {
                                {"good_id", good.id}
                            }));
                        //меняем значение кода единицы изм
                        var s = await Class365API.RequestAsync("put", "goodsmeasures", new Dictionary<string, string>()
                        {
                            {"id", gm[0].id},
                            {"measure_id", good.measure_id}
                        });
                    }
                    Log.Add("СОЗДАНА КАРТОЧКА В БАЗЕ\n" + good.name);
                    Thread.Sleep(1000);
                }
                //если есть что оприходовать
                if (newTiuGoods.Count > 0) {
                    //сделаем новое поступление
                    var remain = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                        await Class365API.RequestAsync("post", "remains", new Dictionary<string, string>()
                    {
                        {"organization_id", "75519"},//радченко
                        {"store_id", "1204484"},//склад
                        {"number", "9999"},//номер документа
                        {"employee_id", "76221"},//-рогачев 76197-радченко
                        {"currency_id", "1"},
                        {"date", DateTime.Now.ToShortDateString()},
                        {"comment", "Создано автоматически, цена закупки 50% от реализации"}
                    }));
                    Log.Add("СОЗДАНЫ ОСТАТКИ ПО СКЛАДУ №9999");
                    Thread.Sleep(1000);
                    //делаем привязку к поступлению каждого товара
                    foreach (var good in newTiuGoods) {
                        var res = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                            await Class365API.RequestAsync("post", "remaingoods", new Dictionary<string, string>()
                        {
                            {"remain_id", remain.id},
                            {"good_id", good.id},
                            {"amount", good.amount.ToString()},
                            {"measure_id", good.measure_id},
                            {"price", Convert.ToString(good.price/2)},
                            {"sum", Convert.ToString(good.amount*good.price/2)}
                        }));
                        Thread.Sleep(1000);
                        Log.Add("ПРИВЯЗАНА КАРТОЧКА К ОТСТАКАМ\n" + good.name);
                    }

                    //создаём новый прайс лист
                    var spl = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                        await Class365API.RequestAsync("post", "salepricelists", new Dictionary<string, string>()
                        {
                            {"organization_id", "75519"},//радчено и.г.
                            {"responsible_employee_id", "76221"},//рогачев
                        }));
                    Log.Add("СОЗДАН НОВЫЙ ПРАЙС ЛИСТ " + spl.id);
                    Thread.Sleep(1000);
                    //привяжем к нему тип цены
                    var splpt = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                        await Class365API.RequestAsync("post", "salepricelistspricetypes", new Dictionary<string, string>()
                    {
                        {"price_list_id", spl.id},
                        {"price_type_id", "75524"}//розничная
                    }));
                    Log.Add("ПРИВЯЗАН ТИП ЦЕН К ПРАЙСУ 75524 (розничная)");
                    Thread.Sleep(1000);

                    //для каждого товара сделаем привязку у прайс листу
                    foreach (var good in newTiuGoods) {
                        var splg = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                            await Class365API.RequestAsync("post", "salepricelistgoods", new Dictionary<string, string>()
                            {
                                {"price_list_id", spl.id},
                                {"good_id", good.id},
                            }));
                        Log.Add("ПРИВЯЗАНА КАРТОЧКА К ПРАЙС-ЛИСТУ \n" + good.name);

                        Thread.Sleep(1000);
                        //и назначение цены
                        var splgp = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                            await Class365API.RequestAsync("post", "salepricelistgoodprices", new Dictionary<string, string>()
                            {
                                {"price_list_good_id", splg.id},
                                {"price_type_id", "75524"},
                                {"price", good.price.ToString()}
                            }));
                        Log.Add("НАЗНАЧЕНА ЦЕНА К ПРИВЯЗКЕ ПРАЙС ЛИСТА " + good.price);
                        Thread.Sleep(1000);
                    }
                    Log.Add("ПОСТУПЛЕНИЕ И ЦЕНЫ УСПЕШНО СОЗДАНЫ В БАЗЕ НА " + newTiuGoods.Count + " ТОВАРОВ!");
                }
            } catch (Exception ex) {
                Log.Add("AddSupplyAsync: " + ex.Message);
            }
            newTiuGoods.Clear();
        }

        //оптимизация синхронизации - чтобы не запрашивать всю базу каждый час - запросим только изменения
        private async Task GoLiteSync() {
            ChangeStatus(button_base_get, ButtonStates.NoActive);
            var stage = "";
            try {
                if (base_can_rescan) {
                    base_can_rescan = false;

                    sync_start = DateTime.Now;

                    Log.Add("lite sync started...");
                    stage = "запрашиваем время последней синхронизации...";
                    var lastTime = dSet.Tables["controls"].Rows[0]["liteScanTime"].ToString();
                    stage = "запрашиваем карточки из базы...";
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
                            { "updated[from]", lastTime}
                    });
                    stage = "меняем номера групп на названия...";
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
                    stage = "добавляем к списку...";
                    lightSyncGoods.Clear();
                    if (s.Length > 3) {
                        lightSyncGoods.AddRange(JsonConvert.DeserializeObject<RootObject[]>(s));
                    }

                    stage = "текущие остатки...";
                    var ids = JsonConvert.DeserializeObject<List<GoodIds>>(await Class365API.RequestAsync("get", "storegoods", new Dictionary<string, string>{
                        {"updated[from]", lastTime}
                    }));

                    stage = "текущие цены...";
                    ids.AddRange(JsonConvert.DeserializeObject<List<GoodIds>>(await Class365API.RequestAsync("get", "salepricelistgoods", new Dictionary<string, string>{
                        {"updated[from]", lastTime},
                        {"price_type_id","75524" } //интересуют только отпускные цены
                    })));

                    stage = "запросим реализации...";
                    ids.AddRange(JsonConvert.DeserializeObject<List<GoodIds>>(await Class365API.RequestAsync("get", "realizationgoods", new Dictionary<string, string>{
                        {"updated[from]", lastTime}
                    })));

                    //добавляем к запросу карточки, привязанные к тиу, но с нулевой ценой. решает глюк нулевой цены после поступления
                    ids.AddRange(bus.Where(w => w.tiu.Contains("http") && w.price == 0).Select(_ => new GoodIds { good_id=_.id }));

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

                    Log.Add("Новых/измененных карточек: " + lightSyncGoods.Count + " (выложенных на сайте " + lightSyncGoods.Count(c => c.tiu.Contains("http")) + ")");
                    stage = "...";

                    //переносим обновления в загруженную базу
                    foreach (var lg in lightSyncGoods) {
                        var ind = bus.FindIndex(f => f.id == lg.id);
                        //индекс карточки найден
                        if (ind != -1) {
                            //если чекбокс "игнорировать ссылки" активен
                            if (checkBox_IgnoreUrls.Checked) {
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

                    label_bus.Text = bus.Count + "/" + bus.Count(c => c.tiu.Contains("http") && c.amount > 0);
                    dSet.Tables["controls"].Rows[0]["controlBus"] = bus.Count.ToString();
                    dSet.Tables["controls"].Rows[0]["liteScanTime"] = sync_start.AddMinutes(-1);
                    dSet.WriteXml(fSet);


                    ///вызываем событие
                    ///в котором сообщаем о том, что в базе есть изменения... на будущее
                    ///если будем использовать событийную модель



                    ///но пока события не реализованы в проекте
                    ///поэтому пока мы лишь обновили уже загруженной базе только те карточки, 
                    ///которые изменились с момента последнего запроса
                    ///и дальше сдвинем контрольное время актуальности базы
                    ///а дальше всё как обычно, только сайты больше не парсим,
                    ///только вызываем методы обработки изменений и подъема упавших

                    if (checkBox_sync.Checked && (DateTime.Now.Minute >= 55 || dateTimePicker1.Value.AddMinutes(70) < DateTime.Now)) {
                        await AddPartNumsAsync();//добавление артикулов из описания
                        await Task.Delay(30000);
                        button_avito_get.PerformClick();
                        await Task.Delay(60000);
                        button_drom_get.PerformClick();
                        await Task.Delay(60000);
                        button_AutoRuStart.PerformClick();
                        await Task.Delay(60000);
                        buttonSatom.PerformClick();
                        await Task.Delay(60000);
                        buttonKupiprodai.PerformClick();
                        await Task.Delay(60000);
                        button_GdeGet.PerformClick();
                        await Task.Delay(60000);
                        button_cdek.PerformClick();
                        await Task.Delay(60000);
                        button_tiu_sync.PerformClick();
                        await Task.Delay(60000);
                        button_vk_sync.PerformClick();
                        await Task.Delay(60000);
                        button_avto_pro.PerformClick();
                        //нужно подождать конца обновлений объявлений
                        while (!IsButtonsActive()) {
                            await Task.Delay(60000);
                        }
                        //проверка задвоенности наименований карточек товаров
                        await CheckDublesAsync();//проверка дублей
                        await CheckMultipleApostropheAsync();//проверка лишних аппострофов
                        if (checkBox_art_clear.Checked) await ArtCheckAsync();//чистка артикулов от лишних символов
                        if (checkBox_photo_clear.Checked) await PhotoClearAsync();//очистка ненужных фото
                        await GroupsMoveAsync();//проверка групп
                        await TiuCheckAsync();//исправляем ссылки на тиу
                        if (!wasErrors) {
                            dSet.Tables["controls"].Rows[0]["lastScanTime"] = sync_start;
                            RootObject.ScanTime = sync_start;
                            dateTimePicker1.Value = sync_start;
                            dSet.WriteXml(fSet);
                        }
                        //сбрасываем флаг ошибки для следующего раза
                        wasErrors = false;
                        await SaveBus();
                    }
                    Log.Add("lite sync complete.");
                    base_can_rescan = true;
                }
            } catch (Exception ex) {
                Log.Add("ошибка lite sync: " + ex.Message + "\n"
                    + ex.Source + "\n"
                    + ex.InnerException + "\n"
                    + stage);
                base_can_rescan = true;
                base_rescan_need = true;
            }
            ChangeStatus(button_base_get, ButtonStates.Active);
        }

        bool IsButtonsActive() {
            return
                button_tiu_sync.Enabled &&
                button_drom_get.Enabled &&
                button_vk_sync.Enabled &&
                buttonKupiprodai.Enabled &&
                button_avito_get.Enabled &&
                button_AutoRuStart.Enabled &&
                button_GdeGet.Enabled &&
                button_cdek.Enabled &&
                button_avto_pro.Enabled;
        }

        private async Task CheckMultipleApostropheAsync() {
            try {
                foreach (var item in bus.Where(w => (w.name.Contains("''''") || w.name.Contains("' `")) && w.amount > 0)) {
                    Log.Add("ОБНАРУЖЕНО НАЗВАНИЕ С МНОЖЕСТВОМ АПОСТРОФОВ\n" + item.name);
                    var s = item.name;
                    while (s.EndsWith("'") || s.EndsWith("`"))
                        s = s.TrimEnd('\'').TrimEnd('`').TrimEnd(' ');
                    item.name = s;
                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                                {"id", item.id},
                                {"name", item.name}
                    });
                    Log.Add("ИСПРАВЛЕНО ИМЯ С МНОЖЕСТВОМ АПОСТРОФОВ \n" + item.name);
                    Thread.Sleep(1000);
                }
            } catch (Exception x) {
                Log.Add("Ошибка при переименовании множественных апострофов\n" + x.Message);
            }
        }

        private async Task AddPartNumsAsync() {
            try {
                var i = 0;
                foreach (var item in bus.Where(w => (!string.IsNullOrEmpty(w.description) && w.description.Contains("№") && string.IsNullOrEmpty(w.part)))) {
                    Log.Add("ОБНАРУЖЕН ПУСТОЙ АРТИКУЛ\n" + item.name);
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
                        Log.Add("ДОБАВЛЕН АРТИКУЛ\n" + num);
                        Thread.Sleep(1000);
                    }
                    i++;
                    //if (i > 10) break;
                }
            } catch (Exception x) {
                Log.Add("ОШИБКА ПРИ ДОБАВЛЕНИИ АРТИКУЛА\n" + x.Message);
            }

        }

        private void dateTimePicker1_ValueChanged(object sender, EventArgs e) {
            try {
                scanTime = dateTimePicker1.Value;
                _db.SetParam("lastScanTime", scanTime.ToString());
                dSet.Tables["controls"].Rows[0]["liteScanTime"] = scanTime;
                RootObject.ScanTime = scanTime;
                dSet.WriteXml(fSet);
            } catch (Exception x){
                Log.Add("Ошибка изменения даты синхронизации\n"+x.Message+"\n"+x.InnerException.Message);
            }
        }

        async void buttonSatom_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            // TODO сатом реализовать выгрузку от 1000р
            // вероятно через формирование файла
            ChangeStatus(sender, ButtonStates.Active);
        }

        private async Task PhotoClearAsync() {
            for (int b = 0; b < bus.Count; b++) {
                if (bus[b].amount <= 0 && bus[b].images.Count > 0 ||     //если нет на остатках
                    bus[b].gde.Contains("http") &&                       //или если уже везде выложили - можно из базы подчистить
                    bus[b].drom.Contains("http") &&
                    (bus[b].avito.Contains("http") || bus[b].price < 2000) &&
                    bus[b].auto.Contains("http") &&
                    bus[b].cdek.Contains("http") &&
                    bus[b].kp.Contains("http") &&
                    bus[b].vk.Contains("http") &&
                    bus[b].tiu.Contains("http") &&
                    bus[b].images.Count > 10) {
                    try {
                        var name = bus[b].images[0].name;
                        bus[b].images.Clear();
                        if (bus[b].amount > 0)
                            bus[b].images.Add(new Image {
                                name = name,
                                url = "https://images.ru.prom.st/" + name
                            });
                        var im = JsonConvert.SerializeObject(bus[b].images.ToArray());
                        var s = await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>()
                                {
                                    {"id", bus[b].id},
                                    {"name", bus[b].name},
                                    {"images", im}
                                });
                        //bus[b].images = null;
                        Log.Add("база - удалены лишние фото из карточки!\n" + bus[b].name);
                        await Task.Delay(1000);
                        break;
                    } catch (Exception x) {
                        Log.Add("ошибка при удалении фото из базы!\n" + bus[b].name + "\n" + x.Message);
                    }
                }
            }
        }

        async Task TiuCheckAsync() {
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

        /// 
        /// купи-продай
        /// 
        async void buttonKupiprodai_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                await KupiProdaiAuth();
                while (base_rescan_need) {
                    Log.Add("купипродай ожидает загрузку базы... ");
                    await Task.Delay(60000);
                }
                labelKP.Text = bus.Count(c => c.kp != null && c.kp.Contains("http")).ToString();
                await KupiProdaiEditAsync();
                await KupiProdaiAddAsync();
                await KupiProdaiUpAsync();
                await KupiProdaiDelAsync();
                ChangeStatus(sender, ButtonStates.Active);
            } catch (Exception x) {
                Log.Add("Купипродай Ошибка при обработке\n" + x.Message);
                ChangeStatus(sender, ButtonStates.ActiveWithProblem);
            }
        }

        private async Task KupiProdaiDelAsync() {
            var t = Task.Factory.StartNew(() => {
                kp.Navigate().GoToUrl("https://vip.kupiprodai.ru/noactive/");
                Thread.Sleep(3000);
                var el = kp.FindElements(By.CssSelector("input[name='chek[]']"));
                //удалим те, что нет на остатаках
                for (int i = 0; i < el.Count; i++) {
                    var id = el[i].GetAttribute("value");
                    var b = bus.FindIndex(f => f.kp != null && f.kp.Contains(id));
                    if (b == -1 || bus[b].amount <= 0) {
                        el[i].Click();
                        Thread.Sleep(1000);
                    }
                }
                var ch = kp.FindElements(By.CssSelector("input[class='delete']"));
                if (ch.Count > 0) ch[0].Click();
                Thread.Sleep(3000);
            });
            try {
                await t;
            } catch (Exception x) {
                Log.Add("купипродай: ошибка при поднятии!\n" + x.Message);
            }
        }

        private async Task KupiProdaiUpAsync() {
            var t = Task.Factory.StartNew(() => {
                kp.Navigate().GoToUrl("https://vip.kupiprodai.ru/noactive/");
                Thread.Sleep(3000);
                var el = kp.FindElements(By.CssSelector("input[name='chek[]']"));
                //сперва продлим те, что упали
                for (int i = 0; i < el.Count; i++) {
                    var id = el[i].GetAttribute("value");
                    var b = bus.FindIndex(f => f.kp != null && f.kp.Contains(id));
                    if (b > -1 && bus[b].amount > 0) {
                        el[i].Click();
                        Thread.Sleep(1000);
                    }
                }
                var ch = kp.FindElements(By.CssSelector("input[class='activate']"));
                if (ch.Count > 0) ch[0].Click();
                Thread.Sleep(3000);
            });
            try {
                await t;
            } catch (Exception x) {
                Log.Add("купипродай: ошибка при поднятии!\n" + x.Message);
            }
        }

        async Task KupiProdaiAddAsync() {
            var saveNum = (int)numericUpDownKupiprodaiAdd.Value;
            for (int b = bus.Count - 1; b > -1; b--) {
                if (numericUpDownKupiprodaiAdd.Value <= 0 || !buttonKupiprodai.Enabled) break;
                if ((bus[b].kp == null || !bus[b].kp.Contains("http")) &&
                    bus[b].tiu.Contains("http") &&
                    bus[b].amount > 0 &&
                    bus[b].price >= 0 &&
                    bus[b].images.Count > 0) {
                    var t = Task.Factory.StartNew(() => {
                        kp.Navigate().GoToUrl("https://vip.kupiprodai.ru/add/");
                        SetKPTitle(b);
                        SetKPCategory(b);
                        SetKPDesc(b);
                        SetKPPrice(b);
                        SetKPImages(b);
                        KPPressOkButton();
                    });
                    try {
                        await t;
                        //сохраняем ссылку
                        var name = bus[b].name;
                        while (name.EndsWith("'") || name.EndsWith("`")) {
                            name = name.TrimEnd('\'').TrimEnd('`').TrimEnd(' ');
                        }
                        var url2 = kp.FindElements(By.XPath("//a[contains(text(),'" + name + "')]/../a[contains(@href,'delmsg')]"));
                        if (url2.Count > 0) {

                            var url = url2.First().GetAttribute("href").Replace("delmsg", "editmsg").TrimEnd('/');
                            //kp.FindElements(By.CssSelector(".grd_first .grd_first_info .price .i_blue"))[0]
                            //      .GetAttribute("href").TrimEnd('/');
                            if (url.Contains("editmsg")) {
                                bus[b].kp = url;
                                await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>
                                {
                                {"id", bus[b].id},
                                {"name", bus[b].name},
                                {"833179", bus[b].kp}
                            });

                                Log.Add("купипродай выложено и привязано объявление " + b + "\n" + bus[b].name);
                                numericUpDownKupiprodaiAdd.Value--;

                                //labelKP.Text = bus.Count(c => c.kp.Contains("http")).ToString();
                                labelKP.Text = bus.Count(c => c.kp != null && c.kp.Contains("http")).ToString();
                            }
                            Thread.Sleep(200);
                        } else {
                            throw new Exception("не найдено объявление для привязки");
                        }
                    } catch (Exception e) {
                        Log.Add("купипродай ошибка добавления!\n" + bus[b].name + "\n" + e.Message);
                    }
                }
            }
            //numericUpDownKupiprodaiAdd.Value = saveNum;
        }

        private void SetKPCategory(int b) {
            var name = bus[b].name.ToLowerInvariant();
            var desc = bus[b].description.ToLowerInvariant();
            //рубрика - попробуем сразу ткнуть в нужную опцию (на сайте авито прокатывает)
            kp.FindElement(By.CssSelector("option[value='7_6']")).Click();
            Thread.Sleep(500);
            //подгруппы
            var group = busGroups.Find(f => f.id == bus[b].group_id).name;
            switch (group) {
                case "Автохимия":
                    kp.FindElement(By.CssSelector("option[value='622']")).Click();
                    break;
                case "Аудио-видеотехника":
                    kp.FindElement(By.CssSelector("option[value='623']")).Click();
                    break;
                case "Шины, диски, колеса":
                    kp.FindElement(By.CssSelector("option[value='629']")).Click();
                    break;
                default:
                    kp.FindElement(By.CssSelector("option[value='619']")).Click();
                    Thread.Sleep(500);
                    kp.FindElement(By.CssSelector("option[value='632']")).Click();
                    break;
            }
            Thread.Sleep(500);
        }

        private async Task KupiProdaiAuth() {
            Task t = Task.Factory.StartNew(() => {
                if (kp == null) {
                    ChromeDriverService chromeservice = ChromeDriverService.CreateDefaultService();
                    chromeservice.HideCommandPromptWindow = true;
                    kp = new ChromeDriver(chromeservice);
                    kp.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
                    Thread.Sleep(2000);
                    kp.Navigate().GoToUrl("https://vip.kupiprodai.ru/");
                    LoadCookies(kp, "kupiprodai.json");
                }
                kp.Navigate().GoToUrl("https://kupiprodai.ru/login");
                if (kp.FindElements(By.XPath("//span[@id='nickname']")).Count == 0) {
                    var email = kp.FindElement(By.XPath("//input[@name='login']"));
                    WriteToIWebElement(kp, email, "9106027626@mail.ru");
                    Thread.Sleep(2000);
                    var password = kp.FindElement(By.XPath("//input[@name='pass']"));
                    WriteToIWebElement(kp, password, "rad00239000");
                    Thread.Sleep(2000);
                    var but = kp.FindElement(By.XPath("//input[@value='Войти']"));
                    but.Click();
                    //если в кабинет не попали - ждем ручной вход
                    while(kp.FindElements(By.XPath("//span[@id='nickname']")).Count == 0) Thread.Sleep(10000);
                    SaveCookies(kp, "kupiprodai.json");
                }
            });
            try {
                await t;
            } catch (Exception x) {
                kp = null;
                Thread.Sleep(300000);
                await KupiProdaiAuth();
            }
        }

        async Task KupiProdaiEditAsync() {
            //перебираем те карточки, у которых есть привязка в базе
            for (int b = 0; b < bus.Count; b++) {
                if (bus[b].kp != null && bus[b].kp.Contains("http")) {
                    //удаляем те, которых нет на остатках
                    if (bus[b].amount <= 0) {
                        var t = Task.Factory.StartNew(() => {
                            var url = bus[b].kp.Replace("editmsg", "delmsg");
                            kp.Navigate().GoToUrl(url);
                            Thread.Sleep(5000);
                        });
                        try {
                            await t;
                        } catch (Exception x) {
                            Log.Add("купипродай ошибка удаления!\n" + bus[b].name + "\n" + x.Message);
                        }
                        //убиваем ссылку из базы в любом случае, потому что удалим физически, проверив отстатки после парсинга и подъема неактивных
                        bus[b].kp = " ";
                        await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                            {"id", bus[b].id},
                            {"name", bus[b].name},
                            {"833179", bus[b].kp}
                        });
                        Log.Add("купипродай - удалена ссылка из карточки\n" + bus[b].name);
                        await Task.Delay(5000);
                    } else if (bus[b].kp != null && bus[b].price > 0 && bus[b].IsTimeUpDated()) {// редактируем
                        var t = Task.Factory.StartNew(() => {
                            kp.Navigate().GoToUrl(bus[b].kp);
                            SetKPTitle(b);
                            SetKPPrice(b);
                            SetKPDesc(b);
                            KPPressOkButton();
                        });
                        try {
                            await t;
                        } catch (Exception x) {
                            Log.Add("KupiProdaiEditAsync: " + bus[b].name + "\n" + x.Message);
                        }
                    }
                }
            }
        }

        private void SetKPTitle(int b) {
            try {
                var name = bus[b].name;
                while (name.Length > 80) {
                    name = name.Remove(name.LastIndexOf(' '));
                }
                var title = kp.FindElement(By.CssSelector("input[name*='title']"));
                WriteToIWebElement(kp, title, name);
            } catch (Exception x) {
                Log.Add("купипродай: ошибка добавления названия объявления\n" + bus[b].name + "\n" + x.Message);
            }
        }

        private void SetKPPrice(int b) {
            try {
                var pr = kp.FindElement(By.CssSelector("input[name*='price']"));
                WriteToIWebElement(kp, pr, bus[b].price.ToString());
            } catch (Exception x) {
                Log.Add("купипродай: ошибка добавления цены объявления\n" + bus[b].name + "\n" + x.Message);
            }
        }

        private void SetKPDesc(int b) {
            //готовим описание
            var s = Regex.Replace(bus[b].description
                        .Replace("Есть и другие", "|")
                        .Split('|')[0]
                        .Replace("\n", "|")
                        .Replace("<br />", "|")
                        .Replace("<br>", "|")
                        .Replace("</p>", "|")
                        .Replace("&nbsp;", " ")
                        .Replace("&quot;", "")
                        .Replace("&gt;", "")
                        .Replace(" &gt", ""),
                    "<[^>]+>", string.Empty)
                .Split(new[] { "|" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ta => ta.Trim())
                .Where(tb => tb.Length > 1)
                .ToList();
            if (bus[b].IsGroupValid()) {
                s.AddRange(dopDescKP);
            }
            //контролируем длину описания
            var newSLength = 0;
            for (int u = 0; u < s.Count; u++) {
                if (s[u].Length + newSLength > 2900) {
                    s.Remove(s[u]);
                    u = u - 1;
                } else {
                    newSLength = newSLength + s[u].Length;
                }
            }
            //заполняем
            WriteToIWebElement(kp, kp.FindElement(By.CssSelector(".form_content_long2 textarea")), sl: s);
        }

        void SetKPImages(int b) {
            try {
                WebClient cl = new WebClient();
                cl.Encoding = Encoding.UTF8;
                var num = bus[b].images.Count > 10 ? 10 : bus[b].images.Count;
                for (int u = 0; u < num; u++) {
                    bool flag; // флаг для проверки успешности
                    int tryNum = 0;
                    do {
                        tryNum++;
                        try {
                            byte[] bts = cl.DownloadData(bus[b].images[u].url);
                            File.WriteAllBytes("kp_" + u + ".jpg", bts);
                            flag = true;
                        } catch (Exception ex) {
                            Log.Add("SetKPImages: " + ex.ToString() + "\nошибка загрузки фото " + bus[b].images[u].url);
                            flag = false;
                        }
                        Thread.Sleep(100);
                    } while (!flag && tryNum < 3);

                    if (flag) kp.FindElement(By.CssSelector("input[type='file']"))
                                .SendKeys(Application.StartupPath + "\\" + "kp_" + u + ".jpg ");
                }
            } catch (Exception e) {
                Log.Add("SetKPImages: " + e.Message);
            }
            Thread.Sleep(500);
        }

        private void KPPressOkButton() {
            try {
                do {
                    var captInput = kp.FindElements(By.CssSelector("input[name='captcha']"));
                    if (captInput.Count > 0) {
                        captInput[0].Click();
                        while (true) {
                            if (captInput[0].GetAttribute("value").Length == 6) break;
                            Thread.Sleep(1000);
                        }
                    }
                    kp.FindElement(By.CssSelector("input[type='submit']")).Click();
                } while (kp.FindElements(By.CssSelector("input[name='captcha']")).Count > 0);
            } catch (Exception x) {
                Log.Add("KPPressOkButton: " + x.Message);
            }
        }


        private async void buttonKupiprodaiAdd_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            await KupiProdaiAddAsync();
            ChangeStatus(sender, ButtonStates.Active);
        }

        private async void button_kp_check_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            var lastScan = Convert.ToInt32(dSet.Tables["controls"].Rows[0]["controlKP"]);
            lastScan = lastScan + 1 >= bus.Count ? 0 : lastScan;//опрокидываем счетчик
            for (int b = lastScan; b < bus.Count; b++) {
                if (base_rescan_need) { break; }
                if (bus[b].kp == null) continue;
                if (bus[b].kp.Contains("http")) {
                    var nameParse = "";
                    var ch = Task.Factory.StartNew(() => {
                        kp.Navigate().GoToUrl(bus[b].kp);
                        Thread.Sleep(1000);
                        nameParse = kp.FindElement(By.CssSelector("input[name='bbs_title']")).GetAttribute("value");
                    });
                    try {
                        await ch;
                        //если нет такого
                        if (bus[b].name != nameParse) {

                            //await api.RequestAsync("put", "goods", new Dictionary<string, string>
                            //{
                            //    {"id", bus[b].id},
                            //    {"name", bus[b].name},
                            //    {"313971", bus[b].auto},
                            //});
                            Log.Add("!!!!!!!!!!!!!!!!!!!!!! KP БИТАЯ ССЫЛКА В БАЗЕ!!!!!!!!!!!!!!!!\n" + "в базе:" + bus[b].name + "\nна kp:" + nameParse + "\n!!!!!!!!!!!!!!!!!!!!!!!!!!");
                        } else {
                            labelKP.Text = b.ToString() + " (" + b * 100 / bus.Count + "%)";
                            Log.Add("kp... " + b);
                            dSet.Tables["controls"].Rows[0]["controlKP"] = b + 1;
                            try {
                                dSet.WriteXml(fSet);
                            } catch (Exception x) {
                                Log.Add("ошибка записи файла настроек!\n" + x.Message);
                            }
                        }
                    } catch (Exception ex) {
                        Log.Add("button_autoCheck_Click: " + ex.Message);
                        break;
                    }
                }
            }
            ChangeStatus(sender, ButtonStates.Active);
        }

        //TODO вынести в отдельный класс
        //========//
        // gde.ru //
        //========//
        public async void button_GdeGet_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                await GdeAutorize();
                while (base_rescan_need) {
                    Log.Add("Gde.ru ожидает загрузку базы... ");
                    await Task.Delay(60000);
                }
                labelGde.Text = bus.Count(c => c.gde != null && c.gde.Contains("http")).ToString();
                //проверяем изменения
                await GdeEditAsync();
                //выкладываем объявления
                await GdeAddAsync();
                //продливаем объявления
                await Task.Delay(10);// GdeUpAsync(); //пока не нужно
                //удаляем ненужные
                await GdeDelAsync();
            } catch (Exception x) {
                Log.Add("Gde.ru Ошибка при обработке\n" + x.Message);
            }
            ChangeStatus(sender, ButtonStates.Active);
        }

        async Task GdeDelAsync() {
            var t = Task.Factory.StartNew(() => {
                foreach (var b in bus.Where(w => w.tiu.Contains("http") &&
                                               w.gde.Contains("http") &&
                                               w.amount <= 0)) {
                    gde.Navigate().GoToUrl(b.gde.Replace("/update", "/delete"));
                    Thread.Sleep(3000);
                }
            });
            try {
                await t;
            } catch (Exception x) {
                Log.Add("gde.ru ошибка удаления!\n" + x.Message);
                if (x.Message.Contains("Unexpected error") || x.Message.Contains("timed out")) {
                    gde.Quit();
                    gde = null;
                }
            }
        }

        public async void button_GdeAdd_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            await GdeAddAsync();
            ChangeStatus(sender, ButtonStates.Active);
        }


        private async Task GdeAutorize() {
            if (gde == null) {
                ChromeDriverService chromeservice = ChromeDriverService.CreateDefaultService();
                chromeservice.HideCommandPromptWindow = true;
                gde = new ChromeDriver(chromeservice);
                gde.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            }
            Task t = Task.Factory.StartNew(() => {
                gde.Navigate().GoToUrl("https://kaluga.gde.ru/user/login");
                if (!gde.FindElement(By.CssSelector("#LoginForm_email")).Text.Contains("9106027626@mail.ru")) {
                    var email = gde.FindElement(By.CssSelector("#LoginForm_email"));
                    WriteToIWebElement(gde, email, "9106027626@mail.ru");
                    Thread.Sleep(2000);

                    var password = gde.FindElement(By.CssSelector("#LoginForm_password"));
                    WriteToIWebElement(gde, password, "rad00239000");
                    Thread.Sleep(2000);

                    var but = gde.FindElement(By.CssSelector("#login-form input[type='submit']"));
                    but.Click();
                    Thread.Sleep(2000);
                }
            });
            try {
                await t;
            } catch (Exception x) {
                //Log.Add(x.Message);
            }
        }


        private async Task GdeEditAsync() {
            //перебираем те карточки, у которых есть привязка в базе
            for (int b = 0; b < bus.Count; b++) {
                if (bus[b].gde != null && bus[b].gde.Contains("http")) {
                    //удаляем те, которых нет на остатках
                    if (bus[b].amount <= 0) {
                        var t = Task.Factory.StartNew(() => {

                            //https://kaluga.gde.ru/cabinet/item/update?id=36460379
                            //https://kaluga.gde.ru/cabinet/ads/complete/id/36460379

                            var url = bus[b].gde.Replace("item/update", "item/delete");
                            gde.Navigate().GoToUrl(url);
                            Thread.Sleep(3000);
                        });
                        try {
                            await t;
                        } catch (Exception x) {
                            Log.Add("gde.ru ошибка удаления!\n" + bus[b].name + "\n" + x.Message);
                        }
                        //убиваем ссылку из базы в любом случае, потому что удалим физически, проверив отстатки после парсинга и подъема неактивных
                        bus[b].gde = " ";
                        await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                            {"id", bus[b].id},
                            {"name", bus[b].name},
                            {"854872", bus[b].gde}
                        });
                        Log.Add("gde.ru - удалена ссылка из карточки\n" + bus[b].name);
                        await Task.Delay(3000);
                    } else if (bus[b].gde != null && bus[b].price > 0 && bus[b].IsTimeUpDated()) {// редактируем
                        var t = Task.Factory.StartNew(() => {
                            gde.Navigate().GoToUrl(bus[b].gde);
                            SetGdeTitle(b);
                            SetGdePrice(b);
                            SetGdeDesc(b);
                            //SetGdeAddr();
                            GdePressOkButton();
                        });
                        try {
                            await t;
                            //var newUrl = kp.Url.Replace("/edited", "") + "/edit";
                            //if (newUrl != bus[b].kp) {
                            //    bus[b].kp = newUrl;
                            //    await api.RequestAsync("put", "goods", new Dictionary<string, string>
                            //    {
                            //        {"id", bus[b].id},
                            //        {"name", bus[b].name},
                            //        {"657256", bus[b].kp}
                            //    });
                            //}
                        } catch (Exception x) {
                            Log.Add("gde.ru: ошибка! - " + bus[b].name + " - " + x.Message);
                            if (x.Message.Contains("Unexpected error")||
                                x.Message.Contains("timed out") ||
                                x.Message.Contains("already closed") ||
                                x.Message.Contains("invalid session id") ||
                                x.Message.Contains("chrome not reachable")) {
                                Log.Add("gde.ru: перезапуск модуля...");
                                gde.Quit();
                                gde = null;
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void SetGdeAddr() {
            try {
                var addr = gde.FindElement(By.CssSelector("#AInfoForm_address"));
                WriteToIWebElement(gde, addr, "ул. Московская, д. 331");
            } catch (Exception x) {
                Log.Add("gde.ru: ошибка добавления адреса в объявление\n" + x.Message);
            }
        }

        private void SetGdeTitle(int b) {
            try {
                var name = bus[b].name;
                while (name.Length > 80) {
                    name = name.Remove(name.LastIndexOf(' '));
                }
                var title = gde.FindElement(By.CssSelector("#AInfoForm_title"));
                WriteToIWebElement(gde, title, name);
            } catch (Exception x) {
                Log.Add("gde.ru: ошибка добавления названия объявления\n" + bus[b].name + "\n" + x.Message);
            }
        }


        private void SetGdePrice(int b) {
            try {
                var priceInput = gde.FindElement(By.CssSelector("#AInfoForm_price"));
                WriteToIWebElement(gde, priceInput, bus[b].price.ToString());
            } catch (Exception x) {
                Log.Add("купипродай: ошибка добавления цены объявления\n" + bus[b].name + "\n" + x.Message);
            }
        }

        private void SetGdePhone() {
            try {
                var phInput = gde.FindElement(By.CssSelector("#AInfoForm_phone"));
                WriteToIWebElement(gde, phInput, "9621787915");
            } catch (Exception x) {
                Log.Add("купипродай: ошибка добавления номера телефона\n" + x.Message);
            }
        }
        private void SetGdeDesc(int b) {
            //готовим описание
            var s = Regex.Replace(bus[b].description
                        .Replace("Есть и другие", "|")
                        .Split('|')[0]
                        .Replace("\n", "|")
                        .Replace("<br />", "|")
                        .Replace("<br>", "|")
                        .Replace("</p>", "|")
                        .Replace("&nbsp;", " ")
                        .Replace("&quot;", "")
                        .Replace("&gt;", "")
                        .Replace(" &gt", ""),
                    "<[^>]+>", string.Empty)
                .Split(new[] { "|" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ta => ta.Trim())
                .Where(tb => tb.Length > 1)
                .ToList();
            if (bus[b].IsGroupValid()) {
                s.AddRange(dopDescKP);
            }
            //контролируем длину описания
            var newSLength = 0;
            for (int u = 0; u < s.Count; u++) {
                if (s[u].Length + newSLength > 2900) {
                    s.Remove(s[u]);
                    u = u - 1;
                } else {
                    newSLength = newSLength + s[u].Length;
                }
            }
            //заполняем
            WriteToIWebElement(gde, gde.FindElement(By.CssSelector("#AInfoForm_content")), sl: s);
        }

        private void GdePressOkButton() {
            try {
                Thread.Sleep(15000);
                gde.FindElement(By.CssSelector("#post-item")).Click();
                Thread.Sleep(3000);
            } catch (Exception x) {
                Log.Add("GdePressOkButton: " + x.Message);
            }
        }

        private void GdeCheckFreeOfCharge() {
            try {
                gde.FindElement(By.CssSelector("label[for='radio-0']")).Click();
            } catch (Exception x) {
                Log.Add("GdeCheckFreeOfCharge: " + x.Message);
            }
        }

        bool IsNonActive(int b) {
            gde.Navigate().GoToUrl("https://kaluga.gde.ru/cabinet/ads/index/status/notactive");
            foreach (var name in gde.FindElements(By.CssSelector(".title a")).Select(s => s.Text)) {
                if (name == bus[b].name)
                    return true;
            }
            return false;

        }

        public async Task GdeAddAsync() {
            var saveNum = (int)numericUpDownGde.Value;
            for (int b = bus.Count - 1; b > -1; b--) {
                if (numericUpDownGde.Value <= 0 /*|| !button_GdeGet.Enabled*/) break;
                if ((bus[b].gde == null || !bus[b].gde.Contains("http")) &&
                    bus[b].tiu.Contains("http") &&
                    bus[b].amount > 0 &&
                    bus[b].price >= 0 &&
                    bus[b].images.Count > 0) {

                    numericUpDownGde.Value--;
                    var t = Task.Factory.StartNew(() => {
                        if (!IsNonActive(b)) {
                            gde.Navigate().GoToUrl("https://kaluga.gde.ru/post");
                            Thread.Sleep(2000);
                            SetGdeTitle(b);
                            SetGdePhone();
                            SetGdeCategory(b);
                            SetGdeDesc(b);
                            SetGdePrice(b);
                            SetGdeAddr();
                            SetGdeImages(b);
                            GdeCheckFreeOfCharge();
                            GdePressOkButton();
                        }
                    });
                    try {
                        await t;
                        //сохраняем ссылку
                        var url = @"https://kaluga.gde.ru/cabinet/item/update?id=" + gde.Url.Split('/').Last();
                        if (gde.Url.Contains("postLast")) {
                            bus[b].gde = url;
                            await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>
                            {
                                {"id", bus[b].id},
                                {"name", bus[b].name},
                                {"854872", bus[b].gde}
                            });
                            Log.Add("Gde.ru выложено и привязано объявление " + b + "\n" + bus[b].name);
                            labelGde.Text = bus.Count(c => c.gde != null && c.gde.Contains("http")).ToString();
                        }
                    } catch (Exception e) {
                        Log.Add("GDE.RU ОШИБКА ДОБАВЛЕНИЯ!\n" + bus[b].name + "\n");
                    }
                }
            }
            numericUpDownGde.Value = saveNum;
        }

        private void SetGdeCategory(int b) {
            var name = bus[b].name.ToLowerInvariant();
            var desc = bus[b].description.ToLowerInvariant();
            Thread.Sleep(500);
            //выбираем Автомобили и мотоциклы
            gde.FindElement(By.XPath("//li[text()='Автомобили и мотоциклы']")).Click();
            Thread.Sleep(2000);
            //подгруппы
            switch (bus[b].GroupName()) {
                case "Шины, диски, колеса":
                    gde.FindElement(By.XPath("//li[contains(text(),'Шины и диски')]")).Click();
                    break;
                default:
                    gde.FindElement(By.XPath("//li[contains(text(),'Запчасти и аксессуары')]")).Click();//запчасти и аксессуары
                    break;
            }
            //далее подгруппы
            Thread.Sleep(2000);
            switch (bus[b].GroupName()) {
                case "Аудио-видеотехника":
                    gde.FindElement(By.XPath("//li[contains(text(),'Тюнинг и автозвук')]")).Click();
                    break;
                case "Автохимия":
                    gde.FindElement(By.XPath("//li[contains(text(),'Средства для ухода за авто')]")).Click();
                    break;
                case "Шины, диски, колеса":
                    if (name.Contains("диск"))
                        gde.FindElement(By.XPath("//li[text()='Диски']")).Click();
                    else if (name.Contains("шины") || name.Contains("шина"))
                        gde.FindElement(By.XPath("//li[text()='Шины']")).Click();
                    else
                        gde.FindElement(By.XPath("//li[text()='Колеса']")).Click();
                    Thread.Sleep(1000);
                    //gde.FindElement(By.XPath("//span[contains(text(),'Выберите подкатегорию')]/..")).Click();
                    Thread.Sleep(1000);
                    if (name.Contains("лит"))
                        gde.FindElement(By.XPath("//li[text()='Литые']")).Click();
                    else if (name.Contains("кован"))
                        gde.FindElement(By.XPath("//li[text()='Кованные']")).Click();
                    else
                        try {
                            gde.FindElement(By.XPath("//li[text()='Штампованные']")).Click();
                        } catch {
                            gde.FindElement(By.XPath("//li[text()='Всесезонные']")).Click();
                        }
                    //размер диска
                    //gde.FindElement(By.XPath("//body")).SendKeys(OpenQA.Selenium.Keys.PageDown);
                    var size = bus[b].GetDiskSize();
                    gde.FindElement(By.XPath("//span[contains(text(),'Выберите вариант')]/..")).Click();
                    Thread.Sleep(1000);
                    gde.FindElement(By.XPath("//div[contains(@class,'selectmenu-open')]//li[text()='" + size + "']")).Click();
                    Thread.Sleep(500);

                    //ширина обода
                    gde.FindElement(By.XPath("//span[contains(text(),'Выберите вариант')]/..")).Click();
                    Thread.Sleep(1000);
                    gde.FindElement(By.XPath("//div[contains(@class,'selectmenu-open')]//li[text()='10']")).Click();
                    Thread.Sleep(500);

                    //количество отверстий
                    gde.FindElement(By.XPath("//span[contains(text(),'Выберите вариант')]/..")).Click();
                    Thread.Sleep(1000);
                    gde.FindElement(By.XPath("//div[contains(@class,'selectmenu-open')]//li[text()='4']")).Click();
                    Thread.Sleep(500);

                    //диам отверстий
                    gde.FindElement(By.XPath("//span[contains(text(),'Выберите вариант')]/..")).Click();
                    Thread.Sleep(1000);
                    gde.FindElement(By.XPath("//div[contains(@class,'selectmenu-open')]//li[text()='100']")).Click();
                    Thread.Sleep(500);

                    //вылет
                    WriteToIWebElement(gde, gde.FindElement(By.XPath("//label[text()='Вылет (ЕТ)']/..//input")), "10");
                    Thread.Sleep(500);

                    //модель
                    gde.FindElement(By.XPath("//span[contains(text(),'Выберите вариант')]/..")).Click();
                    Thread.Sleep(1000);
                    var stamp = name.Contains("лит") || name.Contains("кован") ? "Прочие" : "Штампованные";
                    try {
                        gde.FindElement(By.XPath("//div[contains(@class,'menu-open')]//li[text()='" + stamp + "']")).Click();
                    } catch {
                        gde.FindElement(By.XPath("//div[contains(@class,'menu-open')]//li[text()='155']")).Click();
                        Thread.Sleep(500);
                        gde.FindElement(By.XPath("//span[contains(text(),'Выберите вариант')]/..")).Click();
                        Thread.Sleep(500);
                        gde.FindElement(By.XPath("//div[contains(@class,'menu-open')]//li[text()='50']")).Click();
                    }
                    break;
                default:
                    gde.FindElement(By.XPath("//li[text()='Запчасти']")).Click();
                    break;
            }
            Thread.Sleep(6000);
        }

        void SetGdeImages(int b) {
            try {
                WebClient cl = new WebClient();
                cl.Encoding = Encoding.UTF8;
                var num = bus[b].images.Count > 12 ? 12 : bus[b].images.Count;

                for (int u = 0; u < num; u++) {
                    bool flag; // флаг для проверки успешности
                    int tryNum = 0;
                    do {
                        tryNum++;
                        try {
                            byte[] bts = cl.DownloadData(bus[b].images[u].url);
                            File.WriteAllBytes("gde_" + u + ".jpg", bts);
                            flag = true;
                        } catch (Exception ex) {
                            Log.Add("SetGdeImages: " + ex.ToString() + "\nошибка загрузки фото " + bus[b].images[u].url);
                            flag = false;
                        }
                        Thread.Sleep(1000);
                    } while (!flag && tryNum < 3);

                    if (flag) gde.FindElement(By.CssSelector("input[name=file]"))
                                .SendKeys(Application.StartupPath + "\\" + "gde_" + u + ".jpg ");
                }
            } catch (Exception e) {
                Log.Add("SetGdeImages: " + e.Message);
            }
            Thread.Sleep(5000);
        }

        //=== выгрузка avto.pro === 
        private async void button_avto_pro_Click(object sender, EventArgs e) {
            if (checkBox_AvtoProSyncEnable.Checked) {
                ChangeStatus(sender, ButtonStates.NoActive);
                try {
                    Log.Add("avto.pro: начало выгрузки...");
                    while (base_rescan_need) await Task.Delay(30000);
                    _avtoPro.AddCount = (int)numericUpDown_AvtoProAddCount.Value;
                    await _avtoPro.AvtoProStartAsync(bus);
                    Log.Add("avto.pro: выгрузка завершена!");

                    var lastScanTime = dSet.Tables["controls"].Rows[0]["AvtoProLastScanTime"].ToString();
                    if(DateTime.Parse(lastScanTime) < DateTime.Now.AddHours(-24) && DateTime.Now.Hour < 5) { //достаточно проверять один раз в сутки, и только ночью
                        Log.Add("avto.pro: парсинг сайта...");
                        await _avtoPro.CheckAsync();
                        dSet.Tables["controls"].Rows[0]["AvtoProLastScanTime"] = DateTime.Now;
                        dSet.WriteXml(fSet);
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
                        _avtoPro?.Quit();
                        await Task.Delay(180000);
                        _avtoPro = new AvtoPro();
                        button_avto_pro_Click(sender, e);
                    }
                }
            }
        }
        private void numericUpDown_avto_pro_add_ValueChanged(object sender, EventArgs e) {
            try {
                _avtoPro.AddCount = (int)numericUpDown_AvtoProAddCount.Value;
            } catch (Exception x) {
                Log.Add("avto.pro: ошибка установки количества добавляемых объявлений");
            }
        }        
        
        //=== синхронизация CDEK ===
        private async void button_cdek_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (base_rescan_need || !button_base_get.Enabled) {
                Log.Add("CDEK ожидает загрузку базы... ");
                await Task.Delay(60000);
            }
            if (checkBoxCdekSyncActive.Checked) {
                try {
                    await _cdek.SyncCdekAsync(bus, (int)numericUpDown_CdekAddNewCount.Value);
                } catch (Exception x) {
                    Log.Add("CDEK ошибка синхронизации:\n" + x.Message);
                }
                label_cdek.Text = bus.Count(c => c.cdek != null && c.cdek.Contains("http")).ToString();
                Log.Add("CDEK выгрузка ок!");
            }
            if (numericUpDown_СdekCheckUrls.Value > 0) {
                try {
                    var num = int.Parse(dSet.Tables["controls"].Rows[0]["controlCdek"].ToString());
                    num = await _cdek.CheckUrlsAsync(num, (int)numericUpDown_СdekCheckUrls.Value);
                    dSet.Tables["controls"].Rows[0]["controlCdek"] = num;
                    dSet.WriteXml(fSet);
                    await _cdek.ParseSiteAsync();
                } catch (Exception x) {
                    Log.Add("CDEK ошибка при проверке объявлений!\n"+x.Message);
                }
            }
            ChangeStatus(sender, ButtonStates.Active);
        }
        //загрузка куки
        private void button_SaveCookie_Click(object sender, EventArgs e) {
            SaveCookies(tiu, "tiu.json");
            _avtoPro.SaveCookies();
            _drom.SaveCookies();
            _avito.SaveCookies();
            _autoRu.SaveCookies();
            //SaveCookie(kp);
            //SaveCookie(gde);
            //SaveCookie(cd);
        }
        private void SaveCookies(IWebDriver dr, string name) {
            try {
                var ck = dr.Manage().Cookies.AllCookies;
                var json = JsonConvert.SerializeObject(ck);
                File.WriteAllText(name, json);
            } catch (Exception x) {
                Log.Add("ошибка сохранения куки " + name + "\n" + x.Message);
            }
        }
        private void LoadCookies(IWebDriver dr, string name) {
            try {
                var json = File.ReadAllText(name);
                var cookies = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(json);
                OpenQA.Selenium.Cookie ck;
                foreach (var c in cookies) {
                    DateTime? dt = null;
                    if (!string.IsNullOrEmpty(c["Expiry"])) {
                        dt = DateTime.Parse(c["Expiry"]);
                    }
                    ck = new OpenQA.Selenium.Cookie(c["Name"], c["Value"], c["Domain"], c["Path"], dt);
                    dr.Manage().Cookies.AddCookie(ck);
                }
            } catch (Exception x) {
                Log.Add("Ошибка загузки куки " + name + "\n" + x.Message);
            }
        }

        // запись в элемент
        public void WriteToIWebElement(IWebDriver d, IWebElement we, string s = null, List<string> sl = null) {
            Actions a = new Actions(d);
            if (sl != null) {
                a.MoveToElement(we)
                 .Click()
                 .Perform();
                Thread.Sleep(2000);
                a.KeyDown(OpenQA.Selenium.Keys.Control)
                 .SendKeys("a")
                 .KeyUp(OpenQA.Selenium.Keys.Control)
                 .Perform();
                Thread.Sleep(200);
                a.SendKeys(OpenQA.Selenium.Keys.Backspace)
                 .Perform();
                Thread.Sleep(200);
                foreach (var sub in sl) {
                    if (sub.Length > 0) {
                        a.SendKeys(sub);
                    }
                    a.SendKeys(OpenQA.Selenium.Keys.Enter).Build().Perform();
                    a = new Actions(d);
                }
                //a.Perform();
            }
            if (!string.IsNullOrEmpty(s)) {
                we.SendKeys(" ");
                we.SendKeys(OpenQA.Selenium.Keys.Control + "a");
                we.SendKeys(OpenQA.Selenium.Keys.Backspace);
                we.SendKeys(s);
            }
            Thread.Sleep(1000);
        }

        private async void Form1_Load(object sender, EventArgs e) {
            //подписываю обработчик на событие
            Log.LogUpdate += LogUpdate;
            //устанавливаю глубину логирования
            Log.Level = (int)numericUpDown_LOG.Value;
            //меняю заголовок окна
            this.Text += _version;
            //подгружаю базу
            await LoadBus();
        }

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
                    default:
                        break;
                }
            }
        }

        private void button_ReadSetXmlClick(object sender, EventArgs e) {
            try {
                dSet.Clear();
                dSet.ReadXml(fSet);
            } catch (Exception x) {
                Log.Add(x.Message);
            }
        }

        //Вывод лога на форму
        public void ToLog(string s) {
            try {
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
            logBox.SelectionStart = logBox.Text.Length - 1;
            logBox.ScrollToCaret();
        }
        //обработка события на добавление записи
        public void LogUpdate() {
            ToLog(Log.LogLastAdded);
        }
        //настройки
        private void button_SettingsFormOpen_Click(object sender, EventArgs e) {
            FormSettings fs = new FormSettings();
            fs.Owner = this;
            fs.ShowDialog();
            fs.Dispose();
        }
        //=========================//
        //метод для тестирования
        private async void ButtonTest(object sender, EventArgs e) {
            try {
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
        }

    }
}