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
using System.Diagnostics;

namespace Selen {
    public partial class FormMain : Form {
        int _version = 228;
        //todo move this fields to class365api class
        YandexMarket _yandexMarket;
        VK _vk;
        Drom _drom;
        Izap24 _izap24;
        OzonApi _ozon;
        Avito _avito;
        MegaMarket _mm;
        Wildberries _wb;

        bool _saveCookiesBeforeClose;

        //для возврата из форм
        public List<string> lForm3 = new List<string>();
        public string nForm3 = "";
        public string BindedName = "";

        //писать лог
        DateTime _logShowFromTime = DateTime.Now.AddYears(-1);

        string _headerText = "Синхронизация сайтов ";
        string _status;

        //конструктор формы
        public FormMain() {
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
            InitializeComponent();
        }

        //========================
        //=== РАБОТА С САЙТАМИ ===
        //========================
        //AVITO.RU
        async void ButtonAvitoRu_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                await _avito.GenerateXML();
                ChangeStatus(sender, ButtonStates.Active);
            } catch (Exception x) {
                Log.Add("avito.ru: ошибка синхронизации! - " + x.Message);
                ChangeStatus(sender, ButtonStates.ActiveWithProblem);
            }
        }
        //Avito категории
        private void button_AvitoCategories_Click(object sender, EventArgs e) {
            try {
                Form f = new FormAvito();
                f.Owner = this;
                f.ShowDialog();
                f.Dispose();
            } catch (Exception x) {
                Log.Add(x.Message);
            }
        }
        //VK.COM
        async void ButtonVkCom_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                Log.Add("вк начало выгрузки");
                await _vk.SyncAsync();
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
            await _izap24.SyncAsync();
            ChangeStatus(sender, ButtonStates.Active);
        }
        //YANDEX MARKET
        async void ButtonYandexMarket_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            await _yandexMarket.GenerateXML();
            ChangeStatus(sender, ButtonStates.Active);
        }
        //OZON
        async void button_ozon_ClickAsync(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            await _ozon.SyncAsync();
            ChangeStatus(sender, ButtonStates.Active);
        }
        //MegaMarket
        async void button_MegaMarket_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            await _mm.GenerateXML();
            ChangeStatus(sender, ButtonStates.Active);
        }
        //Wildberries
        async void button_Wildberries_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            await _wb.SyncAsync();
            ChangeStatus(sender, ButtonStates.Active);
        }

        //todo move this method to class365api 
        async Task SyncAllHandlerAsync() {
            while (Class365API.Status == SyncStatus.NeedUpdate)
                await Task.Delay(30000);
            await _ozon.MakeReserve();
            await _wb.MakeReserve();
            await _yandexMarket.MakeReserve();
            await _avito.MakeReserve();
            //await _mm.MakeReserve();  //TODO mm reserve?
            await _drom.MakeReserve();

            button_wildberries.Invoke(new Action(() => button_wildberries.PerformClick()));
            await Task.Delay(2000);
            button_Drom.Invoke(new Action(() => button_Drom.PerformClick()));
            await Task.Delay(2000);
            button_Avito.Invoke(new Action(()=> button_Avito.PerformClick()));
            await Task.Delay(2000);
            button_YandexMarket.Invoke(new Action(() => button_YandexMarket.PerformClick()));
            await Task.Delay(2000);
            button_MegaMarket.Invoke(new Action(() => button_MegaMarket.PerformClick()));
            await Task.Delay(2000);
            button_ozon.Invoke(new Action(() => button_ozon.PerformClick()));
            await Task.Delay(10000);
            button_Izap24.Invoke(new Action(() => button_Izap24.PerformClick()));
            await Task.Delay(10000);
            button_Vk.Invoke(new Action(() => button_Vk.PerformClick()));
            await WaitButtonsActiveAsync();
            Class365API.LastScanTime = Class365API.SyncStartTime;
        }
        //загрузка формы
        private async void FormMain_Load(object sender, EventArgs e) {
            //меняю заголовок окна
            var user = await DB.GetParamStrAsync("userName");
            _headerText = "[" + user + "] " + _headerText;
            Text = $"{_headerText} 1.{_version} - загрузка...";
            var actualVersion = await DB.GetParamIntAsync("version");
            if (_version < actualVersion) {
                var url = await DB.GetParamStrAsync("newVersionUrl");
                var res = MessageBox.Show($"Необходимо обновление программы! Нажмите ОК для скачивания!",
                                          $"Доступна новая версия 1.{actualVersion}", MessageBoxButtons.OKCancel);
                if(res == DialogResult.OK) {
                    var ps = new ProcessStartInfo(url) {
                        //UseShellExecute = false,
                        UseShellExecute = true,
                        Verb = "open"
                    };
                    Process.Start(ps);
                    await Task.Delay(10000);
                }
                Close();
                return;
            }
            await Task.Delay(200);
            //подписываю обработчик на событие
            Log.LogUpdate += LogUpdate;
            //устанавливаю глубину логирования
            Log.Level = await DB.GetParamIntAsync("logSize");
            if (_version > actualVersion)
                Text += " (тестовая версия)";
            dateTimePicker1.Value = Class365API.LastScanTime;
            _saveCookiesBeforeClose = await DB.GetParamBoolAsync("saveCookiesBeforeClose");
            await CheckMultiRunAsync();
            _drom = new Drom();
            _izap24 = new Izap24();
            _ozon = new OzonApi();
            _avito = new Avito();
            _vk = new VK();
            _mm = new MegaMarket();
            _wb = new Wildberries();
            _yandexMarket = new YandexMarket();
            Class365API.syncAllEvent += SyncAllHandlerAsync;
            Class365API.updatePropertiesEvent += PropertiesUpdateHandler;
            Class365API.StartSync();
        }
        //проверка на параллельный запуск
        async Task CheckMultiRunAsync() {
            if (await DB.GetParamBoolAsync("checkMultiRun")) {
                while (true) {
                    try {
                        //запрашиваю последнюю запись из лога
                        DataTable table = await DB.GetLogAsync("",limit:1);
                        //если запись получена
                        if (table.Rows.Count > 0) {
                            DateTime time = (DateTime) table.Rows[0].ItemArray[1];
                            string text = table.Rows[0].ItemArray[3] as string;
                            Log.Add("Последняя запись в логе\n*****\n" + time + ": " + text + "\n*****\n\n", false);
                            //есть текст явно указывает, что приложение было остановлено или прошло больше 5 минут выход с true
                            if (text.Contains("синхронизация остановлена") || time.AddMinutes(2) < DateTime.Now)
                                return;
                            else {
                                Log.Add("защита от параллельных запусков! повторная попытка...", false);
                                await Task.Delay(10000);
                            }
                        } 
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
                button_MegaMarket.Enabled &&
                button_wildberries.Enabled
                )
            )
            await Task.Delay(5000);
        }
        private void dateTimePicker1_Validated(object sender, EventArgs e) {
            Class365API.LastScanTime = dateTimePicker1.Value;
        }
        private async void Button_PricesCheck_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            Seller24ru s24 = new Seller24ru();
            await s24.FillPrices();
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
                DataTable table = await DB.GetLogAsync(textBox_LogFilter.Text, limit: Log.Level);
                if (table.Rows.Count > 0)
                    logBox.Text = table.Select().Select(s => s[1] + ": " + s[3]).Aggregate((a, b) => b + "\n" + a) + "\n";
                else
                    logBox.Text = "по заданному фильтру ничего не найдено!\n";
            } catch (Exception x) {
                Log.Add(x.Message);
            }
        }
        //показать весь лог (сброс фильтра)
        void Button_LogFilterClear_Click(object sender, EventArgs e) {
            textBox_LogFilter.Text = "";
            _logShowFromTime = DateTime.Now.AddYears(-1);
        }
        //показать только ошибки
        private void button_showErrors_Click(object sender, EventArgs e) {
            textBox_LogFilter.Text = "ошибка";
            _logShowFromTime = Class365API.LastErrorShowTime;
            Class365API.LastErrorShowTime = DateTime.Now;
        }
        //показать только предупреждения
        private void button_showWarnings_Click(object sender, EventArgs e) {
            textBox_LogFilter.Text = "предупреждение";
            _logShowFromTime = Class365API.LastWarningShowTime;
            Class365API.LastWarningShowTime = DateTime.Now;
        }
        async Task PropertiesUpdateHandler() {
            dateTimePicker1.Invoke(new Action(() => dateTimePicker1.Value = Class365API.LastScanTime));
            label_Bus.Invoke(new Action(()=>label_Bus.Text = Class365API.LabelBusText));
            if (Class365API.Status == SyncStatus.NeedUpdate)
                label_Bus.Invoke(new Action(() => Text = _headerText + "1."+_version 
                + " - требуется полное обновление базы товаров"));
            else if (Class365API.Status == SyncStatus.Waiting)
                label_Bus.Invoke(new Action(() => Text = _headerText + "1." + _version 
                + " - ожидание..."));
            else if (Class365API.Status == SyncStatus.ActiveLite)
                label_Bus.Invoke(new Action(() => Text = _headerText + "1." + _version 
                + " - синхронизация..."));
            else if (Class365API.Status == SyncStatus.ActiveFull)
                label_Bus.Invoke(new Action(() => Text = _headerText + "1." + _version 
                + " - запрос полной базы товаров и синхронизация..."));
        }
        //закрываем форму, сохраняем настройки
        async void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            Class365API.Status = SyncStatus.NeedUpdate;
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
            //обновляю глубину логирования после закрытия настроек
            Log.Level = DB.GetParamInt("logSize");
        }
        //окно веса, размеры
        private async void Button_WeightsDimensions_ClickAsync(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (Class365API.Status == SyncStatus.NeedUpdate)
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
                Log.Add("test start");
                
                
                await _wb.GetCharcsAsync(7985);
                
                
                Log.Add("test end");
            } catch (Exception x) {
                Log.Add(x.Message);
            }
            ChangeStatus(sender, ButtonStates.Active);
        }
    }
}