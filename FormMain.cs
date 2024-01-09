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
using System.Timers;

namespace Selen {
    public partial class FormMain : Form {
        string _version = "1.170";

        //DB _db = new DB();

        VK _vk = new VK();
        Drom _drom = new Drom();
        Izap24 _izap24 = new Izap24();
        Kupiprodai _kupiprodai = new Kupiprodai();
        GdeRu _gde = new GdeRu();
        Satom _sat = new Satom();
        OzonApi _ozon = new OzonApi();
        static System.Timers.Timer _timer = new System.Timers.Timer();

        bool _saveCookiesBeforeClose;

        //для возврата из форм
        public List<string> lForm3 = new List<string>();
        public string nForm3 = "";
        public string BindedName = "";

        //писать лог
        bool _writeLog = true;

        //конструктор формы
        public FormMain() {
            InitializeComponent();
            Class365API.syncAllEvent += SyncAllAsync;
            _timer.Interval = 50000;
            _timer.Elapsed += timer_sync_Tick;
            _timer.Start();
        }

        private void timer_sync_Tick(object sender, ElapsedEventArgs e) {
            if (Class365API._isBusinessNeedRescan) 
                ChangeStatus(button_BaseGet,ButtonStates.ActiveWithProblem);
            else if (Class365API._isBusinessCanBeScan) 
                ChangeStatus(button_BaseGet,ButtonStates.Active);
            else
                ChangeStatus(button_BaseGet,ButtonStates.NoActive);
        }

        //========================
        //=== РАБОТА С САЙТАМИ ===
        //========================
        //AVITO.RU
        async void ButtonAvitoRu_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                while (Class365API._isBusinessNeedRescan)
                    await Task.Delay(30000);
                var av = new AvitoXml();
                await av.GenerateXML(Class365API._bus);
                ChangeStatus(sender, ButtonStates.Active);
            } catch (Exception x) {
                Log.Add("avito.ru: ошибка синхронизации! - " + x.Message);
                ChangeStatus(sender, ButtonStates.ActiveWithProblem);
            }
        }
        //VK.COM
        async void ButtonVkCom_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                Log.Add("вк начало выгрузки");
                while (Class365API._isBusinessNeedRescan)
                    await Task.Delay(20000);
                await _vk.VkSyncAsync(Class365API._bus);
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
                while (Class365API._isBusinessNeedRescan)
                    await Task.Delay(30000);
                await _drom.DromStartAsync(Class365API._bus);
                label_Drom.Text = Class365API._bus.Count(c => !string.IsNullOrEmpty(c.drom) && c.drom.Contains("http") && c.amount > 0).ToString();
                ChangeStatus(sender, ButtonStates.Active);
            } catch (Exception x) {
                Log.Add("drom.ru: ошибка синхронизации! \n" + x.Message + "\n" + x.InnerException?.Message);
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
            while (Class365API._isBusinessNeedRescan || !button_KupiprodaiAdd.Enabled)
                await Task.Delay(30000);
            if (await _kupiprodai.StartAsync(Class365API._bus))
                ChangeStatus(sender, ButtonStates.Active);
            else
                ChangeStatus(sender, ButtonStates.ActiveWithProblem);
            label_Kp.Text = Class365API._bus.Count(c => !string.IsNullOrEmpty(c.kp) && c.kp.Contains("http") && c.amount > 0).ToString();
        }
        async void ButtonKupiprodaiRuAdd_Click(object sender, EventArgs e) {
            if (!button_Kupiprodai.Enabled)
                return;
            ChangeStatus(sender, ButtonStates.NoActive);
            while (Class365API._isBusinessNeedRescan)
                await Task.Delay(30000);
            await _kupiprodai.AddAsync();
            label_Kp.Text = Class365API._bus.Count(c => !string.IsNullOrEmpty(c.kp) && c.kp.Contains("http") && c.amount > 0).ToString();
            ChangeStatus(sender, ButtonStates.Active);
        }
        //GDE.RU
        async void ButtonGdeRu_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (Class365API._isBusinessNeedRescan || Class365API._bus.Count == 0)
                await Task.Delay(30000);
            if (await _gde.StartAsync(Class365API._bus)) {
                label_Gde.Text = Class365API._bus.Count(c => c.gde != null && c.gde.Contains("http")).ToString();
                ChangeStatus(sender, ButtonStates.Active);
            } else
                ChangeStatus(sender, ButtonStates.ActiveWithProblem);
        }
        //IZAP24.RU
        async void ButtonIzap24_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (Class365API._isBusinessNeedRescan)
                await Task.Delay(60000);
            if (await _izap24.SyncAsync(Class365API._bus))
                ChangeStatus(sender, ButtonStates.Active);
            else
                ChangeStatus(sender, ButtonStates.ActiveWithProblem);
        }
        //YOULA.RU
        async void ButtonYoula_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (Class365API._isBusinessNeedRescan || Class365API._bus.Count == 0)
                await Task.Delay(30000);
            var youlaXml = new YoulaXml();
            await youlaXml.GenerateXML_avito(Class365API._bus);
            ChangeStatus(sender, ButtonStates.Active);
        }
        //SATOM.RU
        async void ButtonSatom_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (Class365API._isBusinessNeedRescan || Class365API._bus.Count == 0)
                await Task.Delay(30000);
            await _sat.SyncAsync(Class365API._bus);
            ChangeStatus(sender, ButtonStates.Active);
        }
        //YANDEX MARKET
        async void ButtonYandexMarket_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            if (Class365API.IsWorkTime()) {
                while (Class365API._isBusinessNeedRescan || Class365API._bus.Count == 0)
                    await Task.Delay(30000);
                var yandexMarket = new YandexMarket();
                await yandexMarket.GenerateXML(Class365API._bus);
            }
            ChangeStatus(sender, ButtonStates.Active);
        }
        //OZON
        async void button_ozon_ClickAsync(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (Class365API._isBusinessNeedRescan || Class365API._bus.Count == 0)
                await Task.Delay(30000);
            await _ozon.SyncAsync();
            ChangeStatus(sender, ButtonStates.Active);
        }
        async Task SyncAllAsync() {
            await Class365API.AddPartNumsAsync();//добавление артикулов из описания
            await Class365API.CheckArhiveStatusAsync();//проверка архивного статуса
            await Class365API.CheckBu();
            await Class365API.CheckUrls(); //удаление ссылок из черновиков
            button_Avito.Invoke(new Action(()=> button_Avito.PerformClick()));
            await Task.Delay(10000);
            button_YandexMarket.Invoke(new Action(() => button_YandexMarket.PerformClick()));
            await Task.Delay(10000);
            button_ozon.Invoke(new Action(() => button_ozon.PerformClick()));
            await Task.Delay(10000);
            button_Satom.Invoke(new Action(() => button_Satom.PerformClick()));
            await Task.Delay(10000);
            button_Vk.Invoke(new Action(() => button_Vk.PerformClick()));
            await Task.Delay(10000);
            button_Youla.Invoke(new Action(() => button_Youla.PerformClick()));
            await Task.Delay(10000);
            button_Gde.Invoke(new Action(() => button_Gde.PerformClick()));
            await Task.Delay(10000);
            button_Kupiprodai.Invoke(new Action(() => button_Kupiprodai.PerformClick()));
            await Task.Delay(10000);
            button_Drom.Invoke(new Action(() => button_Drom.PerformClick()));
            await Task.Delay(10000);
            //button_EuroAuto.PerformClick();
            //await Task.Delay(10000);
            button_Izap24.Invoke(new Action(() => button_Izap24.PerformClick()));
            await Task.Delay(10000);
            await WaitButtonsActiveAsync();
            button_PricesIncomeCorrection.Invoke(new Action(() => button_PricesIncomeCorrection.PerformClick()));
            //проверка задвоенности наименований карточек товаров
            await Class365API.CheckDublesAsync();   //проверка дублей
            await Class365API.CheckMultipleApostropheAsync();   //проверка лишних аппострофов
            await Class365API.ArtCheckAsync();//чистка артикулов от лишних символов
            await Class365API.GroupsMoveAsync();//проверка групп
            await Class365API.PhotoClearAsync();//очистка ненужных фото
            await Class365API.ArchivateAsync();//архивирование старых карточек
            await Class365API.CheckDescriptions();
            await Class365API.CheckRealisationsAsync(); //проверка реализаций, добавление расходов
            dateTimePicker1.Invoke(new Action(() => dateTimePicker1.Value = Class365API._syncStartTime));
        }
        //полный скан базы бизнес.ру
        async void BaseGet(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            await Class365API.BaseGetAsync();
            label_Bus.Text = Class365API._labelBusText;
            dateTimePicker1.Value = Class365API._syncStartTime;
            ChangeStatus(sender, ButtonStates.Active);
        }
        //загрузка формы
        private async void FormMain_Load(object sender, EventArgs e) {
            //подписываю обработчик на событие
            Log.LogUpdate += LogUpdate;
            //устанавливаю глубину логирования
            Log.Level = await DB.GetParamIntAsync("logSize");
            //меняю заголовок окна
            this.Text += _version;
            var currentVersion = await DB.GetParamStrAsync("version");
            if (!_version.Contains(currentVersion)) {
                Log.Add("доступна новая версия " + currentVersion);
            }
            _writeLog = await DB.GetParamBoolAsync("writeLog");
            dateTimePicker1.Value = DB.GetParamDateTime("lastScanTime");
            _saveCookiesBeforeClose = await DB.GetParamBoolAsync("saveCookiesBeforeClose");
            await CheckMultiRunAsync();
            await Class365API.LoadBusJSON();
            label_Bus.Text = Class365API._labelBusText;
            button_BaseGet.BackColor = Color.GreenYellow;
        }
        //проверка на параллельный запуск
        async Task CheckMultiRunAsync() {
            if (await DB.GetParamBoolAsync("checkMultiRun")) {
                while (true) {
                    try {
                        //запрашиваю последнюю запись из лога
                        DataTable table = await DB.GetLogAsync("", 1);
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
        //редактирование описаний, добавленние синонимов
        private async void ButtonDescriptionsEdit_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            await Class365API.DescriptionsEdit();
            ChangeStatus(sender, ButtonStates.Active);
        }
        //тест задвоения карточек контрагентов
        private async void ButtonTestPartnersClick(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            await Class365API.CheckPartnersDubles();
            ChangeStatus(sender, ButtonStates.Active);
        }
        //пока не активируются все кнопки ожидаем 20 сек
        async Task WaitButtonsActiveAsync() {
            while (!(
                button_Drom.Enabled &&
                button_Vk.Enabled &&
                button_Kupiprodai.Enabled &&
                button_Avito.Enabled &&
                button_Gde.Enabled &&
                button_ozon.Enabled &&
                button_Youla.Enabled
                )
            )
                await Task.Delay(5000);
        }
        private async void dateTimePicker1_ValueChanged(object sender, EventArgs e) {
            try {
                Class365API._scanTime = dateTimePicker1.Value;
                await DB.SetParamAsync("lastScanTime", Class365API._scanTime.ToString());
                await DB.SetParamAsync("liteScanTime", Class365API._scanTime.ToString());
                RootObject.ScanTime = Class365API._scanTime;
            } catch (Exception x) {
                Log.Add("ошибка изменения даты синхронизации! - " + x.Message + " - " + x.InnerException?.Message);
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
                        if (!but.Enabled || but.BackColor!= Color.GreenYellow) {
                            but.Enabled = true;
                            but.BackColor = Color.GreenYellow;
                        }
                        break;
                    case ButtonStates.NoActive:
                        if (but.Enabled || but.BackColor != Color.Yellow) {
                            but.Enabled = false;
                            but.BackColor = Color.Yellow;
                        }
                        break;
                    case ButtonStates.ActiveWithProblem:
                        if (!but.Enabled || but.BackColor != Color.Red) {
                            but.Enabled = true;
                            but.BackColor = Color.Red;
                        }
                        break;
                    case ButtonStates.NonActiveWithProblem:
                        if (but.Enabled || but.BackColor != Color.Red) {
                            but.Enabled = false;
                            but.BackColor = Color.Red;
                        }
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
                DataTable table = await DB.GetLogAsync(textBox_LogFilter.Text, Log.Level);
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
            if (Class365API._isBusinessCanBeScan)
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
            while (Class365API._isBusinessNeedRescan || Class365API._bus.Count == 0)
                await Task.Delay(5000);
            FormWeightsDimentions fw = new FormWeightsDimentions(Class365API._bus);
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
                        {"limit", Class365API._pageLimitBase.ToString()},
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
                    if (indBus > -1 && Class365API._bus[indBus].amount > 0) {
                        //цена ввода на остатки (цена закупки)
                        var priceIn = rg.FloatPrice;
                        //цена отдачи (розничная)
                        var priceOut = Class365API._bus[indBus].price;
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
        async Task ChangePostingsPrices(int procent = 80) {//TODO переделать, чтобы метод получал список измененных карточек, а не перебирал все
            //цикл для пагинации запросов
            for (int i = 1; ; i++) {
                //запрашиваю товары из документов "Оприходования"
                var s = await Class365API.RequestAsync("get", "postinggoods", new Dictionary<string, string> {
                        {"limit", Class365API._pageLimitBase.ToString()},
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
                    var indBus = Class365API._bus.FindIndex(f => f.id == pg.good_id);
                    //если индекс и остаток положительный
                    if (indBus > -1 && Class365API._bus[indBus].amount > 0 && Class365API._bus[indBus].price > 0) {
                        //цена ввода на остатки (цена закупки)
                        var priceIn = pg.FloatPrice;
                        //цена отдачи (розничная)
                        var priceOut = Class365API._bus[indBus].price;
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
                                Log.Add("business.ru: " + Class365API._bus[indBus].name + " - цена оприходования изменена с " + priceIn + " на " + newPrice.ToString("#.##"));
                            await Task.Delay(1000);
                        }
                    }
                }
            }
        }
        //отчет остатки по уровню цен
        async void PriceLevelsRemainsReport(object sender, EventArgs e) {
            var priceLevelsStr = DB.GetParamStr("priceLevelsForRemainsReport");
            var priceLevels = JsonConvert.DeserializeObject<int[]>(priceLevelsStr);
            foreach (var price in priceLevels) {
                var x = Class365API._bus.Count(w => w.images.Count > 0 && w.price >= price && w.amount > 0);
                Log.Add("позиций фото, положительным остатком и ценой " + price + "+ : " + x);
            }
        }
        //заполнить применимость
        private void button_application_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            FormApplication form = new FormApplication(Class365API._bus);
            form.Owner = this;
            form.ShowDialog();
            form.Dispose();
            ChangeStatus(sender, ButtonStates.Active);
        }
        //метод для тестов
        async void ButtonTest_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                //tests
 
            } catch (Exception x) {
                Log.Add(x.Message);
            }
            ChangeStatus(sender, ButtonStates.Active);
        }
    }
}