using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Color = System.Drawing.Color;
using Selen.Sites;
using Selen.Tools;
using Selen.Base;
using System.Globalization;
using Selen.Forms;

namespace Selen {
    public partial class FormMain : Form {
        SyncWorker _worker;
        string _headerText = "Синхронизация сайтов -";
        public FormMain() {
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
            InitializeComponent();
        }
        private async void FormMain_Load(object sender, EventArgs e) {
            var user = await DB.GetParamStrAsync("userName");
            _headerText = $"[{user}] {_headerText}";
            Text = $"{_headerText} - загрузка...";
            Log.LogUpdate += LogUpdate;
            Log.Level = await DB.GetParamIntAsync("logSize");
            await CheckMultiRunAsync();
            _worker = new SyncWorker();
            dateTimePicker1.Value = Class365API.LastScanTime;
            Class365API.updatePropertiesEvent += PropertiesUpdateHandler;
            SyncWorker.updatePropertiesEvent += PropertiesUpdateHandler;
        }
        //проверка на параллельный запуск
        async Task CheckMultiRunAsync() {
            if (await DB.GetParamBoolAsync("checkMultiRun")) {
                while (true) {
                    try {
                        //запрашиваю последнюю запись из лога
                        DataTable table = await DB.GetLogAsync("", limit: 1);
                        //если запись получена
                        if (table.Rows.Count > 0) {
                            DateTime time = (DateTime) table.Rows[0].ItemArray[1];
                            string text = table.Rows[0].ItemArray[3] as string;
                            Log.Add("Последняя запись в логе\n*****\n" + time + ": " + text + "\n*****\n", false);
                            //есть текст явно указывает, что приложение было остановлено или прошло больше 5 минут выход с true
                            if (text.Contains("синхронизация остановлена") || time.AddSeconds(15) < DateTime.Now)
                                return;
                            else {
                                Log.Add("защита от параллельных запусков! повторная попытка...", false);
                                await Task.Delay(15000);
                            }
                        }
                    } catch (Exception x) {
                        Log.Add("ошибка при контроле параллельных запусков - " + x.Message, false);
                    }
                }
            }
        }

        private void button_avito_Click(object sender, EventArgs e) {
            textBox_LogFilter.Text = "avito:";
        }
        private void button_drom_Click(object sender, EventArgs e) {
            textBox_LogFilter.Text = "drom:";
        }
        private void button_vk_Click(object sender, EventArgs e) {
            textBox_LogFilter.Text = "vk:";
        }
        private void button_Izap24_Click(object sender, EventArgs e) {
            textBox_LogFilter.Text = "izap24:";
        }
        private void button_ym_Click(object sender, EventArgs e) {
            textBox_LogFilter.Text = "yandex:";
        }
        private void button_ozon_Click(object sender, EventArgs e) {
            textBox_LogFilter.Text = "ozon:";
        }
        private void button_wb_Click(object sender, EventArgs e) {
            textBox_LogFilter.Text = "wb:";
        }
        private void button_mm_Click(object sender, EventArgs e) {
            textBox_LogFilter.Text = "megamarket:";
        }
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
        private void dateTimePicker1_Validated(object sender, EventArgs e) {
            Class365API.LastScanTime = dateTimePicker1.Value;
        }
        private async void Button_PricesCheck_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            Seller24ru s24 = new Seller24ru();
            await s24.FillPrices();
            ChangeStatus(sender, ButtonStates.Active);
        }
        //статус контрола
        void ChangeStatus(object sender, ButtonStates buttonState) {
            var but = sender as System.Windows.Forms.Button;
            if (but != null) {
                switch (buttonState) {
                    case ButtonStates.Active:
                        if (!but.Enabled || but.BackColor != Color.GreenYellow) {
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
            ToLogBox(Log.GetLogLastAdded);
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
        }
        //показать только ошибки
        private void button_showErrors_Click(object sender, EventArgs e) {
            textBox_LogFilter.Text = "ошибка";
            Class365API.LastErrorShowTime = DateTime.Now;
        }
        //показать только предупреждения
        private void button_showWarnings_Click(object sender, EventArgs e) {
            textBox_LogFilter.Text = "предупреждение";
            Class365API.LastWarningShowTime = DateTime.Now;
        }
        async Task PropertiesUpdateHandler() {
            dateTimePicker1.Invoke(new Action(() => dateTimePicker1.Value = Class365API.LastScanTime));
            label_Bus.Invoke(new Action(() => label_Bus.Text = Class365API.LabelBusText));
            if (Class365API.Status == SyncStatus.NeedUpdate)
                label_Bus.Invoke(new Action(() => Text = $"{_headerText}1.{Class365API.VERSION} " +
                $" - требуется полное обновление списка товаров!"));
            else if (Class365API.Status == SyncStatus.Waiting)
                label_Bus.Invoke(new Action(() => Text = $"{_headerText}1.{Class365API.VERSION} - ожидание"));
            else if (Class365API.Status == SyncStatus.ActiveLite)
                label_Bus.Invoke(new Action(() => Text = $"{_headerText}1.{Class365API.VERSION} " +
                $"- обновление..."));
            else if (Class365API.Status == SyncStatus.ActiveFull)
                label_Bus.Invoke(new Action(() => Text = $"{_headerText}1.{Class365API.VERSION} " +
                $"- полный запрос всех товаров и обновление..."));
            if (_worker._wb.IsSyncActive)
                ChangeStatus(button_wb, ButtonStates.NoActive);
            else
                ChangeStatus(button_wb, ButtonStates.Active);
            if (_worker._ozon.IsSyncActive)
                ChangeStatus(button_ozon, ButtonStates.NoActive);
            else
                ChangeStatus(button_ozon, ButtonStates.Active);
            if (_worker._ym.IsSyncActive)
                ChangeStatus(button_ym, ButtonStates.NoActive);
            else
                ChangeStatus(button_ym, ButtonStates.Active);
            if (_worker._avito.IsSyncActive)
                ChangeStatus(button_avito, ButtonStates.NoActive);
            else
                ChangeStatus(button_avito, ButtonStates.Active);
            if (_worker._drom.IsSyncActive)
                ChangeStatus(button_drom, ButtonStates.NoActive);
            else
                ChangeStatus(button_drom, ButtonStates.Active);
            if (_worker._vk.IsSyncActive)
                ChangeStatus(button_vk, ButtonStates.NoActive);
            else
                ChangeStatus(button_vk, ButtonStates.Active);
            if (_worker._mm.IsSyncActive)
                ChangeStatus(button_mm, ButtonStates.NoActive);
            else
                ChangeStatus(button_mm, ButtonStates.Active);
            if (_worker._izap24.IsSyncActive)
                ChangeStatus(button_Izap24, ButtonStates.NoActive);
            else
                ChangeStatus(button_Izap24, ButtonStates.Active);
        }
        //закрываем форму, сохраняем настройки
        async void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            Class365API.Status = SyncStatus.NeedUpdate;
            Log.Add("синхронизация остановлена!");

            this.Visible = false;
            _worker.Stop();

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
        //тестирование
        async void ButtonTest_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                Log.Add("test start");
                //...
                Log.Add("test end");
            } catch (Exception x) {
                Log.Add(x.Message);
            }
            ChangeStatus(sender, ButtonStates.Active);
        }
    }
}