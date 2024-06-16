using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Application = System.Windows.Forms.Application;
using Color = System.Drawing.Color;
using Selen.Sites;
using Selen.Tools;
using Selen.Base;
using System.Timers;
using System.Globalization;
using Selen.Forms;

namespace Selen {
    public partial class FormMain : Form {
        string _version = "1.190";
        //todo move this fields to class365api class
        YandexMarket _yandexMarket = new YandexMarket();
        VK _vk;
        Drom _drom;
        Izap24 _izap24;
        OzonApi _ozon;
        Avito _avito;
        MegaMarket _mm;

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
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
            InitializeComponent();
            Class365API.syncAllEvent += SyncAllHandlerAsync;
            Class365API.updatePropertiesEvent += PropertiesUpdateHandler;
            _timer.Interval = 5000;
            _timer.Elapsed += timer_sync_Tick;
            _timer.Start();
        }

        private void timer_sync_Tick(object sender, ElapsedEventArgs e) {
            if (Class365API.IsBusinessNeedRescan) 
                ChangeStatus(button_BaseGet,ButtonStates.ActiveWithProblem);
            else if (Class365API.IsBusinessCanBeScan) 
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
                while (Class365API.IsBusinessNeedRescan)
                    await Task.Delay(30000);
                await _avito.GenerateXML(Class365API._bus);
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
                while (Class365API.IsBusinessNeedRescan)
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
                while (Class365API.IsBusinessNeedRescan)
                    await Task.Delay(30000);
                await _drom.DromStartAsync();
                label_Drom.Text = Class365API._bus.Count(c => !string.IsNullOrEmpty(c.drom) && c.drom.Contains("http") && c.Amount > 0).ToString();
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
        //IZAP24.RU
        async void ButtonIzap24_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (Class365API.IsBusinessNeedRescan)
                await Task.Delay(60000);
            if (await _izap24.SyncAsync(Class365API._bus))
                ChangeStatus(sender, ButtonStates.Active);
            else
                ChangeStatus(sender, ButtonStates.ActiveWithProblem);
        }
        //YANDEX MARKET
        async void ButtonYandexMarket_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (Class365API.IsBusinessNeedRescan || Class365API._bus.Count == 0)
                await Task.Delay(30000);
            await _yandexMarket.GenerateXML(Class365API._bus);
            ChangeStatus(sender, ButtonStates.Active);
        }
        //OZON
        async void button_ozon_ClickAsync(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (Class365API.IsBusinessNeedRescan || Class365API._bus.Count == 0)
                await Task.Delay(30000);
            await _ozon.SyncAsync();
            ChangeStatus(sender, ButtonStates.Active);
        }
        //MegaMarket
        async void button_MegaMarket_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            await _mm.GenerateXML();
            ChangeStatus(sender, ButtonStates.Active);
        }

        //todo move this method to class365api 
        async Task SyncAllHandlerAsync() {
            while (Class365API.IsBusinessNeedRescan || Class365API._bus.Count == 0)
                await Task.Delay(30000);
            await _ozon.MakeReserve();
            await _yandexMarket.MakeReserve();
            await _avito.MakeReserve();
            //TODO mm reserve!!
            //await _mm.MakeReserve(); 
            await _drom.MakeReserve();
            
            button_Drom.Invoke(new Action(() => button_Drom.PerformClick()));
            await Task.Delay(10000);
            button_ozon.Invoke(new Action(() => button_ozon.PerformClick()));
            await Task.Delay(10000);
            button_YandexMarket.Invoke(new Action(() => button_YandexMarket.PerformClick()));
            await Task.Delay(10000);
            button_Izap24.Invoke(new Action(() => button_Izap24.PerformClick()));
            await Task.Delay(10000);
            button_Vk.Invoke(new Action(() => button_Vk.PerformClick()));
            await Task.Delay(10000);
            button_Avito.Invoke(new Action(()=> button_Avito.PerformClick()));
            await Task.Delay(10000);
            button_MegaMarket.Invoke(new Action(() => button_MegaMarket.PerformClick()));
            await WaitButtonsActiveAsync();
            dateTimePicker1.Invoke(new Action(() => dateTimePicker1.Value = Class365API.SyncStartTime));
        }
        //полный скан базы бизнес.ру
        async void BaseGet(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            await Class365API.BaseGetAsync();
            dateTimePicker1.Value = Class365API.SyncStartTime;
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
            _writeLog = await DB.GetParamBoolAsync("writeLog");
            dateTimePicker1.Value = DB.GetParamDateTime("lastScanTime");
            _saveCookiesBeforeClose = await DB.GetParamBoolAsync("saveCookiesBeforeClose");
            await CheckMultiRunAsync();
            if (!_version.Contains(currentVersion)) {
                Log.Add("доступна новая версия " + currentVersion);
            }
            await Class365API.LoadBusJSON();
            button_BaseGet.BackColor = Color.GreenYellow;
            _drom = new Drom();
            _izap24 = new Izap24();
            _ozon = new OzonApi();
            _avito = new Avito();
            _vk = new VK();
            _mm = new MegaMarket();
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
                button_Avito.Enabled &&
                button_ozon.Enabled&&
                button_MegaMarket.Enabled
                )
            )
                await Task.Delay(5000);
        }
        private async void dateTimePicker1_ValueChanged(object sender, EventArgs e) {
            try {
                Class365API.ScanTime = dateTimePicker1.Value;
                await DB.SetParamAsync("lastScanTime", Class365API.ScanTime.ToString());
                await DB.SetParamAsync("liteScanTime", Class365API.ScanTime.ToString());
                GoodObject.ScanTime = Class365API.ScanTime;
            } catch (Exception x) {
                Log.Add("ошибка изменения даты синхронизации! - " + x.Message + " - " + x.InnerException?.Message);
            }
        }
        private async void Button_PricesCheck_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            await Class365API.ChangePostingsPrices();
            await Task.Delay(10);//ChangeRemainsPrices(); //not used
            ChangeStatus(sender, ButtonStates.Active);
        }
        //сохранить куки
        private void Button_SaveCookie_Click(object sender, EventArgs e) {
            _drom.SaveCookies();
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
        async Task PropertiesUpdateHandler() {
            label_Bus.Invoke(new Action(()=>label_Bus.Text = Class365API.LabelBusText));
        }
        //закрываем форму, сохраняем настройки
        async void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            if (Class365API.IsBusinessCanBeScan)
                Log.Add("синхронизация остановлена!");
            this.Visible = false;
            ClearTempFiles();
            if (_saveCookiesBeforeClose) {
                _drom?.SaveCookies();
            }
            _drom?.Quit();
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
            while (Class365API.IsBusinessNeedRescan || Class365API._bus.Count == 0)
                await Task.Delay(5000);
            FormWeightsDimentions fw = new FormWeightsDimentions(Class365API._bus);
            fw.Owner = this;
            fw.ShowDialog();
            fw.Dispose();
            ChangeStatus(sender, ButtonStates.Active);
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
        //отчет остатки по уровню цен
        async void PriceLevelsRemainsReport(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            await Class365API.PriceLevelsRemainsReport();
            ChangeStatus(sender, ButtonStates.Active);
        }
        //метод для тестов
        async void ButtonTest_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                //tests
                //Log.Add(Class365API._bus.Find(b => b.id == "2846864").New.ToString());

                Form f = new FormAvito();
                f.Owner = this;
                f.ShowDialog();
                f.Dispose();


                //CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
                //Log.Add($"ru-RU: {Class365API.ScanTime.ToString()}");
                //CultureInfo.CurrentCulture = new CultureInfo("en-US");
                //Log.Add($"en-US: {Class365API.ScanTime.ToString()}");
                //CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
                //Log.Add($"ru-RU: {Class365API.ScanTime.ToString()}");

            } catch (Exception x) {
                Log.Add(x.Message);
            }
            ChangeStatus(sender, ButtonStates.Active);
        }

    }
}