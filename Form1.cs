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
using VkNet;
using VkNet.Enums.Filters;
using VkNet.Model;
using Application = System.Windows.Forms.Application;
using System.Text.RegularExpressions;
using BlueSimilarity;
using Color = System.Drawing.Color;
using VkNet.Model.Attachments;
using Selen.Sites;
using Selen.Tools;
using VkNet.Model.RequestParams.Market;
using System.Diagnostics;

namespace Selen {
    public partial class Form1 : Form {
        string _version = "1.22.4";

        public List<RootGroupsObject> busGr = new List<RootGroupsObject>();
        public List<RootObject> bus = new List<RootObject>();
        public List<MarketAlbum> vkAlb = new List<MarketAlbum>();
        public List<Market> vkMark = new List<Market>();
        public List<AvitoObject> avito = new List<AvitoObject>();
        public List<DromObject> drom = new List<DromObject>();
        public List<AutoObject> auto = new List<AutoObject>();
        private List<RootObject> newTiuGoods = new List<RootObject>();
        public List<RootObject> lightSyncGoods = new List<RootObject>();

        VkApi _vk = new VkApi();
        Cdek _cdek = new Cdek();
        Drom _drom = new Drom();
        AvtoPro _avtoPro = new AvtoPro();
        Avito _avito = new Avito();

        public IWebDriver tiu;
        public IWebDriver av;
        public IWebDriver au;
        public IWebDriver kp;
        public IWebDriver gde;

        long marketId = 23570129;
        int pageLimitVk = 200;
        int pageLimitBase = 250;

        string tiuXmlUrl = "http://xn--80aejmkqfc6ab8a1b.xn--p1ai/yandex_market.xml?html_description=1&hash_tag=3f22d0f72761b35f77efeaffe7f4bcbe&yandex_cpa=0&group_ids=&exclude_fields=&sales_notes=&product_ids=";

        string dopDescVk = "Дополнительные фотографии по запросу\n" +
                           "Есть и другие запчасти на данный автомобиль!\n" +
                           "Вы можете установить данную деталь на нашем АвтоСервисе с доп.гарантией и со скидкой!\n" +
                           "Перед выездом обязательно уточняйте наличие запчастей по телефону - товар на складе!\n" +
                           "Время на проверку и установку - 2 недели!\n" +
                           "Отправляем наложенным платежом ТК СДЭК (только негабаритные посылки)\n" +
                           "Бесплатная доставка до транспортной компании!";

        string dopDescVk2 = "АДРЕС: г.Калуга, ул. Московская, д. 331\n" +
                            "ТЕЛ: 8-920-899-45-45, 8-910-602-76-26, Viber/WhatsApp 8-962-178-79-15\n" +
                            "Звонить строго с 9-00 до 19-00 (воскресенье-выходной)";

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

        string[] dopDescAuto = new[] {
            "Дополнительные фотографии по запросу",
            "Есть и другие запчасти на данный автомобиль!",
            "Вы можете установить данную деталь в нашем АвтоСервисе с доп.гарантией и со скидкой!",
            "Перед выездом обязательно уточняйте наличие запчастей по телефону",
            "за 30 минут перед выездом (можно бесплатно через Viber) - товар на складе!",
            "Звонить строго с 9:00 до 19:00 (воскресенье - выходной)",
            "Бесплатная доставка до транспортной компании!",
            "Отправляем наложенным платежом ТК СДЭК (только негабаритные посылки)"
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

        decimal nums_auto;

        //время запуска очередной синхронизации
        DateTime sync_start;
        DateTime scanTime;

        private Object thisLock = new Object();

        //конструктор формы
        public Form1() {
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
                dateTimePicker1.Value = DateTime.Parse(dSet.Tables["controls"].Rows[0]["lastScanTime"].ToString());
            } catch (Exception) {
                MessageBox.Show("ошибка чтения set.xml");
                Thread.Sleep(5000);
            }
        }
        //закрываем форму, сохраняем настройки
        private void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            dSet.WriteXml(fSet);
            ClearTempFiles();
            av?.Quit();
            tiu?.Quit();
            au?.Quit();
            kp?.Quit();
            gde?.Quit();
            _cdek?.Quit();
            _drom?.Quit();
            _avtoPro?.Quit();
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
                await Task.Delay(15000);
                button_drom_get.PerformClick();
                await Task.Delay(15000);
                button_cdek.PerformClick();
                await Task.Delay(15000);
                button_auto_get.PerformClick();
                await Task.Delay(15000);
                buttonKupiprodai.PerformClick();
                await Task.Delay(15000);
                button_GdeGet.PerformClick();
                await Task.Delay(15000);
                button_tiu_sync.PerformClick();
                await Task.Delay(15000);
                button_vk_sync.PerformClick();
                await Task.Delay(15000);
                button_avto_pro.PerformClick();
            }

            await GetBusGroupsAsync();
            await GetBusGoodsAsync2();

            await AddPartNumsAsync();//добавление артикулов из описания

            var tlog = bus.Count + "/" + bus.Count(c => c.tiu.Contains("http") && c.amount > 0);
            ToLog("получено интернет товаров с остатками " + tlog);
            ToLog("из них с ценами " + bus.Count(c => c.amount > 0 && c.tiu.Contains("http") && c.price > 0));
            label_bus.Text = tlog;

            await SaveBus();

            base_rescan_need = false;
            base_can_rescan = true;

            while (!IsButtonsActive()) {
                await Task.Delay(60000);
            }
            button_base_get.BackColor = System.Drawing.Color.GreenYellow;

            //если авто синхронизация включена
            if (checkBox_sync.Checked) {
                button_tiu_sync.PerformClick();
            }
        }

        public async Task GetBusGroupsAsync() {
            int lastScan;
            do {
                lastScan = Convert.ToInt32(dSet.Tables["controls"].Rows[0]["controlBusGr"]);
                busGr.Clear();
                try {
                    var tmp = await Class365API.RequestAsync("get", "groupsofgoods", new Dictionary<string, string>{
                            {"parent_id", "205352"} // интернет магазин
                        });
                    var tmp2 = JsonConvert.DeserializeObject<List<RootGroupsObject>>(tmp);
                    busGr.AddRange(tmp2);

                    dSet.Tables["controls"].Rows[0]["controlBusGr"] = busGr.Count.ToString();
                } catch (Exception x) {
                    ToLog("ОШИБКА ЗАПРОСА ГРУПП ТОВАРОВ ИЗ БАЗЫ!!!\n" + x.Message + "\n" + x.InnerException.Message);
                    await Task.Delay(60000);
                }
            } while (busGr.Count == 0 || lastScan != busGr.Count);
            ToLog("получено групп товаров из базы " + busGr.Count);
            RootObject.Groups = busGr;
            dSet.WriteXml(fSet);
        }

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
                    ToLog("ОШИБКА ПРИ ЗАПРОСЕ КАРТОЧЕК ТОВАРОВ ИЗ БАЗЫ!!!\n" + x.Message);
                    await Task.Delay(10000);
                }
                dSet.Tables["controls"].Rows[0]["controlBus"] = bus.Count.ToString();
            } while (bus.Count == 0 || Math.Abs(lastScan - bus.Count) > 400);
            ToLog("получено карточек товаров " + bus.Count);
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
                        ToLog("ОШИБКА ЗАПРОСА КАРТОЧЕК ИЗ БАЗЫ!\n" + d + "\n" + x.Message + "\n" + s);
                        err++;
                        await Task.Delay(10000);
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
                    ToLog("Ошибка сохранения bus.json");
                }
                ToLog("bus.json - сохранение успешно");
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
            if (checkBox_avito_use.Checked) {
                try {
                    while (base_rescan_need) await Task.Delay(30000);
                    ToLog("avito.ru: начало выгрузки...");
                    if (sync_start.Hour >= 6 && sync_start.Hour < 20) _avito.CountToUp = (int)numericUpDown_AvitoToUpCount.Value;
                    else _avito.CountToUp = 0;
                    ToLog("avito: поднимаю " + _avito.CountToUp + " объявлений");
                    _avito.AddCount = (int)numericUpDown_AvitoAddCount.Value;
                    await _avito.AvitoStartAsync(bus);
                    ToLog("avito.ru: выгрузка завершена");
                } catch (Exception x) {
                    ToLog("avito.ru: ОШИБКА ВЫГРУЗКИ! \n" + x.Message);
                    ChangeStatus(sender, ButtonStates.ActiveWithProblem);
                }
            }
            ChangeStatus(sender, ButtonStates.Active);
        }

        private async void button_avito_add_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            if (checkBox_avito_use.Checked) {
                try {
                    while (base_rescan_need) await Task.Delay(30000);
                    ToLog("avito.ru: добавляю объявления...");
                    _avito.AddCount = (int) numericUpDown_AvitoAddCount.Value;
                    await _avito.AddAsync();
                    ToLog("avito.ru: добавление завершено.");
                } catch (Exception x) {
                    ToLog("avito.ru: ОШИБКА ДОБАВЛЕНИЯ ОБЪЯВЛЕНИЙ! \n" + x.Message);
                    ChangeStatus(sender, ButtonStates.ActiveWithProblem);
                }
            }
            ChangeStatus(sender, ButtonStates.Active);
        }

        private void numericUpDown_AvitoAdd_ValueChanged(object sender, EventArgs e) {
            try {
                _avito.AddCount = (int) numericUpDown_AvitoAddCount.Value;
            } catch (Exception x) {
                ToLog("avito.ru: ошибка установки количества добавляемых объявлений");
            }
        }

        private void numericUpDown_AvitoToUpCount_ValueChanged(object sender, EventArgs e) {
            try {
                _avito.CountToUp = (int)numericUpDown_AvitoToUpCount.Value;
            } catch (Exception x) {
                ToLog("avito.ru: ошибка установки количества поднимаемых объявлений");
            }
        }

        //=== вк ===

        private async void VkSyncAsync(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                while (base_rescan_need) {
                    ToLog("вк ожидает загрузку базы... ");
                    await Task.Delay(60000);
                }
                if (await IsVKAuthorizatedAsync()) {
                    try {
                        await Task.Factory.StartNew(() => {
                            //2. проверяем соответствие групп вк с базой
                            bool f; //флаг было добавление группы в цикле - нужно пересканировать
                            do {
                                vkAlb.Clear();
                                try {
                                    vkAlb.AddRange(_vk.Markets.GetAlbums(-marketId));
                                } catch (Exception ex) {
                                    ToLog("вк ошибка загрузки альбомов/n" + ex.Message);
                                    Thread.Sleep(60000);
                                    break;
                                }
                                f = false;
                                for (int u = 0; u < busGr.Count; u++) {
                                    if (busGr[u].name != "Корневая группа") //Корневая группа не нужна в вк
                                    {
                                        //ищем группы из базы в группах вк
                                        int ind = vkAlb.FindIndex(t => t.Title == busGr[u].name);
                                        //если индекс -1 значит нет такой в списке
                                        if (ind == -1) {
                                            //добавляем группу товаров вк
                                            try {
                                                _vk.Markets.AddAlbum(-marketId, busGr[u].name);
                                                Thread.Sleep(1000);
                                                f = true;
                                            } catch (Exception ex) {
                                                ToLog("вк ошибка добавления группы!\n" + ex.Message);
                                            }
                                        }
                                    }
                                }
                            } while (f);
                        });
                    }
                    catch(Exception x) {
                        ToLog("ВК ошибка получения альбомов\n"+x.Message+x.InnerException.Message);
                    }
                    ToLog("получено альбомов вк " + vkAlb.Count);

                    //3. получим товары из вк
                    int lastScan;
                    if (vkAlb.Count > 0) {
                        try {
                            await Task.Factory.StartNew(() => {
                                do {
                                    lastScan = Convert.ToInt32(dSet.Tables["controls"].Rows[0]["controlVK"]);
                                    vkMark.Clear();
                                    for (int v = 0; ; v++) {
                                        int num = vkMark.Count;
                                        try {
                                            vkMark.AddRange(_vk.Markets.Get(-marketId, count: pageLimitVk, offset: v * pageLimitVk));
                                            Thread.Sleep(500);
                                            if (num == vkMark.Count) break;
                                        } catch (Exception ex) {
                                            ToLog("вк ошибка запроса товаров\n" + ex.Message);
                                            Thread.Sleep(10000);
                                            v--;
                                        }
                                    }
                                    dSet.Tables["controls"].Rows[0]["controlVK"] = vkMark.Count.ToString();
                                } while (vkMark.Count < 0 /*|| Math.Abs(lastScan - vkMark.Count) >= 200 */
                                                          /*|| bus.Count(c => c.tiu.Contains("tiu.ru") && c.amount > 0) - vkMark.Count >= pageLimitVk*/);
                            });
                            label_vk.Text = vkMark.Count.ToString();
                        }
                        catch(Exception x) {
                            ToLog("ВК ошибка получения объявлений товаров\n");
                        }
                        ToLog("Получено товаров вк " + vkMark.Count);

                        //4. удаляем объявления, которых нет в базе товаров, или они обновлены
                        for (int i = 0; i < vkMark.Count; i++) {
                            if (base_rescan_need) break;
                            //для каждого товара поищем индекс в базе карточек товаров
                            int iBus = bus.FindIndex(t => t.vk.EndsWith(vkMark[i].Id.ToString()));
                            //если не найден - значит к херам это объявление

                            //ToLog(bus[iBus].name + "\nbus = "+bus[iBus].updated+"\n"+vkMark[i].Date+"\n-------------");
                            var vkDate = vkMark[i].Date;
                            //var busDate = bus[iBus].updated;
                            if (iBus == -1 ||
                                bus[iBus].price != vkMark[i].Price.Amount / 100 ||
                                bus[iBus].amount <= 0 ||
                                bus[iBus].name != vkMark[i].Title ||
                                DateTime.Parse(bus[iBus].updated).AddMinutes(-223).CompareTo(vkDate) > 0
                                //|| vkMark.Count(c => c.Title == vkMark[i].Title) > 1
                                //|| vkMark[i].Description.Contains("70-70-97")
                                ) {
                                //удаляем из вк
                                var t = Task.Factory.StartNew(() => {
                                    _vk.Markets.Delete(-marketId, (long)vkMark[i].Id);
                                    Thread.Sleep(330);
                                });
                                try {
                                    await t;
                                    if (false && iBus > -1) {//удаление ссылок отключено, т.к. создает лишние обновления сайтов
                                        bus[iBus].vk = "";
                                        bus[iBus].updated = DateTime.Now.ToString();
                                        var s = await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                                        {"id", bus[iBus].id},
                                        {"name", bus[iBus].name},
                                        {"209360", bus[iBus].vk}
                                    });
                                        ToLog("ВК: удалена ссылка из базы\n" + bus[iBus].name);
                                    }
                                    vkMark.Remove(vkMark[i]);
                                    ToLog("удалено из вк:\n" + vkMark[i].Title);
                                    label_vk.Text = vkMark.Count.ToString();
                                    i--;
                                } catch {
                                    ToLog("вк не удалось удалить\n" + bus[iBus].name);
                                }

                                //bus[iBus].updated = DateTime.Now.ToString();
                            }
                        }

                        //5. выкладываем товары вк из тех, которые имеют фото, есть ссылка тиу, цена и кол-во
                        int newVk = 0;
                        for (int i = 0; i < bus.Count && newVk < vkAddUpDown.Value; i++) {
                            if (base_rescan_need) break;
                            int num;
                            //если у карточки есть фото и привязка к тиу, значит это карточка товара интернет-магазина
                            if (bus[i].images.Count > 0 && bus[i].tiu.Contains("tiu.ru")
                                //если есть цена и количество на остатках
                                && bus[i].price > 0 && bus[i].amount > 0 && bus[i].images.Count > 0) {
                                //ищем индекс соответсвующего элемента в коллекции вк
                                int indVk = vkMark.FindIndex(t => bus[i].vk.Contains(t.Id.ToString()));

                                //если индекс не найден значит надо выложить
                                if (indVk < 0) {
                                    //ToLog("выкладываем в вк новый товар!\n" + bus[i].name + " фото: " + bus[i].images.Count + " цена: " + bus[i].price);
                                    //проверяем количество фото в карточке товара, ограничиваем до 5 - ограничение вк!
                                    int numPhotos = bus[i].images.Count > 5 ? 5 : bus[i].images.Count;
                                    //сперва опрелелим индекс группы из базы
                                    int busGrInd = busGr.FindIndex(t => t.id == bus[i].group_id);
                                    //если индекс -1, то для вк тоже указываем -1, что значит привязывать к альбому не будем
                                    int vkAlbInd = busGrInd < 0 ? -1 : vkAlb.FindIndex(t => t.Title == busGr[busGrInd].name);
                                    //переменные для хранения индексов фото, загруженных на сервер вк
                                    long mainPhoto = 0;
                                    List<long> dopPhotos = new List<long>();
                                    //отправляем каждую фотку на сервер вк
                                    System.Net.WebClient cl = new System.Net.WebClient();
                                    cl.Encoding = Encoding.UTF8;
                                    num = 0;
                                    for (int u = 0; u < numPhotos; u++) {
                                        bool flag; // флаг для проверки успешности
                                        do {
                                            try {
                                                byte[] bts = cl.DownloadData(bus[i].images[u].url);
                                                File.WriteAllBytes("vk_" + u + ".jpg", bts);
                                                flag = true;
                                            } catch (Exception ex) {
                                                ToLog("\nошибка загрузки фото " + num + "\n" + bus[i].name + "\n" + ex.Message);
                                                flag = false;
                                                num++;
                                                Task tw = Task.Factory.StartNew(() => { Thread.Sleep(30000); });
                                                await tw;
                                            }
                                        } while (!flag && num <= 3);
                                        if (num == 3) break;
                                        try {
                                            //запросим адрес сервера для загрузки фото
                                            var uplSever = _vk.Photo.GetMarketUploadServer(marketId, true, 0, 0);
                                            //аплодим файл асинхронно
                                            var respImg = Encoding.ASCII.GetString(
                                                    await cl.UploadFileTaskAsync(uplSever.UploadUrl, Application.StartupPath + "\\" + "vk_" + u + ".jpg"));

                                            //сохраняем фото в маркете - вываливается с ошибкой
                                            //var photo = vk.Photo.SaveMarketPhoto(marketId, respImg);

                                            var photo = _vk.Photo.SaveMarketPhoto(marketId, respImg);

                                            //файл больше не нужен
                                            //System.IO.File.Delete("file_vk.jpg");
                                            //проверим, главное ли фото, или дополнительное
                                            if (u == 0) //first photo
                                            {
                                                mainPhoto = photo.FirstOrDefault().Id.Value;
                                            } else {
                                                dopPhotos.Add(photo.FirstOrDefault().Id.Value);
                                            }
                                        } catch (Exception ex) {
                                            ToLog("vk: " + ex.ToString());
                                        }
                                    }
                                    if (num == 3) break;
                                    //меняем доп описание
                                    string desc = bus[i].description.Replace("&nbsp;", " ")
                                        .Replace("&quot;", "")
                                        .Replace("</p>", "\n")
                                        .Replace("<p>", "")
                                        .Replace("<br>", "\n")
                                        .Replace("<br />", "\n")
                                        .Replace("<strong>", "")
                                        .Replace("</strong>", "")
                                        .Replace(" &gt", "")
                                        .Replace("Есть", "|")
                                        .Split('|')[0];

                                    //если группа не автохимия и не колеса, добавляем описсание есть и другие запчасти на данный автомобиль...
                                    if (bus[i].IsGroupValid()) {
                                        desc += dopDescVk;
                                    }
                                    desc += dopDescVk2;

                                    //выкладываем товар вк!
                                    try {
                                        //создаем товар
                                        long itemId = _vk.Markets.Add(new MarketProductParams {
                                            OwnerId = -marketId,
                                            Name = bus[i].name,
                                            Description = desc,
                                            CategoryId = 404,
                                            Price = bus[i].price,
                                            MainPhotoId = mainPhoto,
                                            PhotoIds = dopPhotos,
                                            Deleted = false
                                        });
                                        //привяжем к альбому если есть индекс
                                        if (vkAlbInd > -1) {
                                            List<long> sList = new List<long>();
                                            sList.Add((long)vkAlb[vkAlbInd].Id);
                                            _vk.Markets.AddToAlbum(-marketId, itemId, sList);
                                        }

                                        bus[i].vk = "https://vk.com/market-23570129?w=product-23570129_" + itemId;
                                        bus[i].updated = DateTime.Now.AddMinutes(-224).ToString();
                                        var s = await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>
                                        {
                                        {"id", bus[i].id},
                                        {"name", bus[i].name},
                                        {"209360", bus[i].vk}
                                    });
                                        ToLog("вк добавлено успешно!! " + newVk + "\n" + bus[i].name);
                                        label_vk.Text = (vkMark.Count + ++newVk).ToString();
                                    } catch (Exception ex) {
                                        ToLog("вк ошибка добавления товара\n" + bus[i].name + "\n" + ex.Message);
                                    }
                                }
                            }
                        }
                    }
                }
            } catch (Exception x) {
                ToLog("вк: " + x.Message);
            }
            ChangeStatus(sender, ButtonStates.Active);
        }
        //проверяем авторизацию вк
        private async Task<bool> IsVKAuthorizatedAsync() {
            // https://vk.com/market-23570129?w=product-23570129_552662
            // 23570129 - id группы
            // 552662 - id товара
            var t = Task.Factory.StartNew(() => {
                _vk.Authorize(new ApiAuthParams {
                    ApplicationId = 5820993,
                    Login = "rogachev.aleksey@gmail.com",
                    Password = "$drum122",
                    Settings = Settings.All,
                    AccessToken = "ba600fc83b526e417526749b992277da60ac125353644a2dd32f4eceb1527e130a75ce872ec706555a6dc"
                    //AccessToken = "UZjSkUeX3OAZ51PtDSu9"
                });
            });
            try {
                await t;
                ToLog("авторизация вк успешно!");
                return true;
            } catch (Exception ex) {
                ToLog("вк ошибка авторизации\n" + ex.Message);
                return false;
            }
        }

        //=========================//
        // выгрузка - загрузка tiu //
        //=========================//
        private async void TiuSyncAsync(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (base_rescan_need) {await Task.Delay(10000);}
            await TiuUploadAsync();
            await TiuHide();
            await AddPhotosToBaseAsync();
            await AddSupplyAsync();
            await TiuSyncAsync2();
            ChangeStatus(sender, ButtonStates.Active); 
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
                    ToLog("TIU - ОШИБКА ЗАПРОСА XML!");
                }
                if (loadSuccess /*&& sync_start.Hour % 4 == 0 && sync_start.Minute > 55*/) {//выгружать 1 раз в конце каждого 4го часа будет достаточно
                    ToLog("тиу.ру подготовка выгрузки...");
                    var fexp = "ex3.xls";
                    var ftemp = "tmpl.xls";
                    do {
                        try {
                            File.Copy(Application.StartupPath + "\\" + "tmpl.xls", Application.StartupPath + "\\" + "ex3.xls", true);
                            break;
                        } catch (Exception ex) {
                            ToLog("ошибка доступа к файлу выгрузки xls!\n" + ex.Message);
                            await Task.Delay(30000);
                        }
                    } while (true);

                    Workbook wb = Workbook.Load(fexp);
                    Worksheet ws = wb.Worksheets[0];

                    //проверяем количество полученных товарных позиций
                    tiuCount = ds.Tables["offer"].Rows.Count;
                    label_tiu.Text = tiuCount.ToString();
                    ToLog("тиу получено объявлений из кабинета " + tiuCount.ToString());

                    while (base_rescan_need || bus.Count == 0) {
                        ToLog("тиу ожидает загрузку базы... ");
                        await Task.Delay(60000);
                    }

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

                                string a = bus[i].amount > 0
                                    ? "+"
                                    //: bus[i].pod_zakaz != ""
                                    //    ? bus[i].pod_zakaz
                                    : "-";
                                if (!String.IsNullOrEmpty(bus[i].part)) ws.Cells[iRow, 0] = new Cell(bus[i].part);
                                ws.Cells[iRow, 1] = new Cell(bus[i].name);
                                ws.Cells[iRow, 3] = new Cell(s);
                                ws.Cells[iRow, 5] = new Cell(bus[i].price > 0 ? bus[i].price : 100);
                                ws.Cells[iRow, 6] = new Cell("RUB");
                                ws.Cells[iRow, 7] = new Cell(m);
                                ws.Cells[iRow, 12] = new Cell(a);
                                ws.Cells[iRow, 13] = new Cell(bus[i].amount > 0 ? (int)bus[i].amount : 0);
                                ws.Cells[iRow, 20] = new Cell(idTiu);

                                if (iRow % 10 == 0) {
                                    label_tiu.Text = iRow.ToString();
                                }
                            } else {
                                if (bus[i].amount > 0)
                                    ToLog("TIU ID ИЗ ССЫЛКИ В КАРТОЧКЕ НЕ НАЙДЕН В ВЫГРУЗКЕ XML\n" + bus[i].name + "\n" + idTiu);
                            }
                        }
                    }
                    label_tiu.Text = iRow.ToString();

                    //проверим, есть ли в базе карточки без ссылки на тиу
                    var goods = bus.Where(w => w.images.Count > 0 && w.amount > 0 && !w.tiu.Contains("tiu.ru"))
                        .Select(s => s.name)
                        .ToList();
                    foreach (var good in goods) {
                        ToLog("НЕТ ССЫЛКИ В КАРТОЧКЕ НА САЙТ\n" + good);
                    }

                    //111 переделать в xls
                    //сохраняем изменения
                    wb.Save(Application.StartupPath + "\\" + fexp);

                    System.Net.WebClient ftp = new System.Net.WebClient();

                    //ftp.Credentials = new NetworkCredential("rogachevaleksey", "$drumbotanik");//https://rogachevaleksey.000webhostapp.com/ex3.xls
                    //ftp.Credentials = new NetworkCredential("u148353358", "$drumbotanik");
                    //http://basepoint.hol.es/ex3.xls

                    ftp.Credentials = new NetworkCredential("forVano", "$drum122");
                    //https://nutramir.ru/vano/ex3.xls

                    for (int f = 1; f <= 5; f++) {
                        //await ftp.UploadFileTaskAsync("ftp://files.000webhost.com:21/public_html/" + fexp, "STOR", fexp);
                        //await ftp.UploadFileTaskAsync("ftp://93.188.160.137:21/" + fexp, "STOR", fexp);
                        Task tt = Task.Factory.StartNew(() => {
                            ftp.UploadFile("ftp://31.31.196.233:21/" + fexp, "STOR", fexp);
                            //  ftp.UploadFile("ftp://93.188.160.137:21/" + fexp, "STOR", fexp);
                        });
                        try {
                            await tt;
                            ToLog("тиу: ex3.xls успешно отправлен на сервер!");
                            break;
                        } catch (Exception ex) {
                            ToLog("тиу ошибка выгрузки на FTP, попытка " + f + "\n" + ex.Message);
                            await Task.Delay(30000);
                        }
                    }
                }
            } catch (Exception x) {
                ToLog("Tiu ошибка выгрузки:\n" + x.Message);
            }
        }
        private async Task TiuSyncAsync2() {
            for (int b = 0; b < bus.Count; b++) {
                if (bus[b].tiu != null && bus[b].tiu.Contains("http") &&
                    bus[b].IsTimeUpDated() && bus[b].price > 0) {
                    try {
                        await Task.Factory.StartNew(() => { TiuOfferUpdate(b); });

                    } catch (Exception x) {
                        ToLog("tiu.ru ошибка!/n" + x.Message);
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
                                ToLog("НЕТ ИЗОБРАЖЕНИЙ... ПОДГРУЖЕНЫ ИЗ ТИУ!\n" + bus[b].name + "\n" + s);
                                Thread.Sleep(3000);
                            }
                        } catch {
                            ToLog("Ошибка загрузки фотографий с тиу в базу\n" + bus[b].name);
                        }
                    }
                }
            }
        }
        public async Task WaitTiuAsync() {
            while (tiuCount == 0) {
                ToLog("ожидаем загрузку tiu...");
                var task = Task.Factory.StartNew(() => { Thread.Sleep(10000); });
                await task;
            }
        }
        //скрываем на тиу объявления, которых нет в наличии
        private async Task TiuHide() {
            if (tiu == null) {
                await Task.Factory.StartNew(() => {
                    tiu = new ChromeDriver();
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
                } catch { }
                tiu.Navigate().Refresh();
                Thread.Sleep(3000);
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
                //a.Click().Perform();
                c.Click();
                Thread.Sleep(2000);
            } catch /*(Exception x)*/ {
                //ToLog("Ошибка тиу.ру!\n" + x.Message);
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
                label_drom.Text = bus.Count(c => !string.IsNullOrEmpty(c.drom) && c.drom.Contains("http")).ToString();
                ChangeStatus(sender, ButtonStates.Active);
            } catch (Exception x) {
                ToLog("DROM.RU ОШИБКА СИНХРОНИЗАЦИИ! \n" + x.Message + "\n" + x.InnerException.Message);
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

                    ToLog("база исправлен дубль названия " + bus[ind].name);
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
                            ToLog("база обновлена карточка\n" + bus[i].name + "\n" + bus[i].description);
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
                    ToLog("ОШИБКА ПРИ ЗАПРОСЕ КОНТАКТНОЙ ИНФОРМАЦИИ ИЗ БАЗЫ!!!");
                    Thread.Sleep(2000);
                }
                //обновляем полученное количество товаров
                //    dSet.Tables["controls"].Rows[0]["controlBus"] = bus.Count.ToString();
            } while (pc.Count == 0);
            ToLog("получено контактов " + pc.Count);
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
                ToLog("НАЙДЕНЫ ДУБЛИ: " + d.contact_info);
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

                        ToLog("исправлена карточка\n" +
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
                    ToLog("Ошибка при обработке артикулов\n" + bus[b].name + "\n" + x.Message);
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
                    ToLog("карточка перемещена в группу заказы " + b + " " + bus[b].name);
                    await Task.Delay(1000);
                }
            }
        }

        //===================//
        // сканируем auto.ru //
        //===================//
        //т.к. нет возможности отпарсить полностью авто.ру (выдается только последние 1000 объявлений)
        //будем опираться на записи в базе
        //1. удалим объявления, которых нет на остатках
        //2. редактируем те, что изменились
        //3. парсим последние несколько листов, привязываем непривязанные
        //4. парсим неактивные и поднимаем те,что есть на остатках с положительной ценой
        //5. выкладываем новые
        private async void button_auto_get_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            ToLog("парсим авто.ру...");
            //откроем браузер
            if (au == null) {
                au = new ChromeDriver();
                au.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
                au.Navigate().GoToUrl("https://auto.ru/404");
                LoadCookies(au, "auto.json");
            }
            try {
                await AutoAuthAsync();

                while (base_rescan_need || !button_base_get.Enabled) {
                    ToLog("авто ожидает загрузку базы... ");
                    await Task.Delay(60000);
                }
                //выводим статистику
                var busCnt = bus.Count(c => c.auto.Length > 4);
                label_auto.Text = busCnt.ToString();
                ToLog("авто.ру: ссылок в базе " + busCnt);
                //закрытие всплывающей рекламы
                var closeButtons = au.FindElements(By.XPath("//div[@aria-hidden='false']//div[@class='Modal__closer']"));
                //если есть окошко с крестиком
                if (closeButtons.Count > 0) {
                    Actions a = new Actions(au);
                    a.MoveToElement(closeButtons.First()).Click().Build().Perform();
                }

                //нажмем кнопку обновить
                try {
                    au.Navigate().Refresh();
                    au.FindElements(By.XPath("//span[@class='Button__text' and text()='Обновить']/../.."))
                    .First().Click();
                } catch { }
                for (int b = 0; b < bus.Count; b++) {
                    //1. удаляем те, которых нет на остатках
                    if (bus[b].auto.Contains("http") && (bus[b].amount <= 0 || bus[b].price < 100)) {
                        var id = bus[b].auto.Replace("offerId=", "|").Split('|')[1].Split('&')[0];
                        bool flag;
                        //повторяем удаление объявления пока не будет успешно
                        do {
                            flag = false;
                            try {
                                await Task.Factory.StartNew(() => {
                                    au.Navigate().GoToUrl("https://auto.ru/parts/lk?rgid=6&id=" + id);
                                    try {
                                        au.SwitchTo().Alert().Accept();
                                    } catch { }
                                    var frame = au.FindElements(By.CssSelector("div[class*='OffersItem']"));
                                    //если элементы надены для такого id - значит объявление на сайте, удаляем первое в списке
                                    if (frame.Count > 0) {
                                        flag = true;
                                        //div.user-offers__item-cell.user-offers__item-cell_name_offer > div
                                        Actions fc = new Actions(au);
                                        fc.MoveToElement(frame[0]);
                                        fc.Build();
                                        fc.Perform();
                                        Thread.Sleep(2000);
                                        foreach (var bu in au.FindElements(By.TagName("button"))) {
                                            if (bu.GetAttribute("title").Contains("Удалить объявление")) {
                                                Actions a = new Actions(au);
                                                a.MoveToElement(bu);
                                                a.Build();
                                                a.Perform();
                                                Thread.Sleep(1000);
                                                a.Click();
                                                a.Perform();
                                                Thread.Sleep(1000);
                                                //проверим окно подтверждения
                                                au.SwitchTo().Alert().Accept();
                                                Thread.Sleep(30000);
                                                au.Navigate().GoToUrl("https://auto.ru/parts/lk?rgid=6");
                                                Thread.Sleep(5000);
                                                break;
                                            }
                                        }
                                    }
                                });
                            } catch {
                                ToLog("auto: ошибка удаления \n" + bus[b].name + "\n" + bus[b].auto);
                            }
                        } while (flag);
                        //теперь можно удалить ссылку из карточки в базе
                        await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>
                        {
                        {"id", bus[b].id},
                        {"name", bus[b].name},
                        {"313971", ""}
                    });
                        bus[b].auto = "";
                        ToLog("авто удалено объявление - нет на остатках\n" + b + " " + bus[b].name);
                        label_auto.Text = bus.Count(c => c.auto.Length > 4).ToString();
                    }

                    //2. редактируем те, что изменились
                    if (bus[b].auto.Contains("http")
                        && bus[b].IsTimeUpDated()
                    // && bus[b].price >= 1000
                    ) {
                        try {
                            await Task.Factory.StartNew(() => {
                                //переходим на страницу объявления
                                au.Navigate().GoToUrl(bus[b].auto);
                                try {
                                    au.SwitchTo().Alert().Accept();
                                } catch { }
                                //редактируем цену
                                var we = au.FindElement(By.CssSelector("label[class*='PriceFormControl'] input"));
                                WriteToIWebElement(au, we, bus[b].price.ToString());

                                we = au.FindElement(By.CssSelector("div[class*='FormField_name_oem'] input"));
                                WriteToIWebElement(au, we, bus[b].part);
                                //описание
                                we = au.FindElement(By.CssSelector("div[class='TextArea__box'] textarea"));
                                WriteToIWebElement(au, we, sl: GetAutoDesc(b));
                                //прокрутим страницу к кнопке публикации
                                Actions a = new Actions(au);
                                a.MoveToElement(au.FindElement(By.CssSelector("div.FormField.FormField_name_submit > button"))).Build().Perform();
                                //добавим адрес точки продаж
                                //we = au.FindElement(By.CssSelector("div[class*='name_stores'] button"));
                                //we.Click();
                                //Thread.Sleep(1000);
                                //a.SendKeys(OpenQA.Selenium.Keys.ArrowDown).Build().Perform();
                                //Thread.Sleep(1000);
                                //a.SendKeys(OpenQA.Selenium.Keys.Enter).Build().Perform();
                                Thread.Sleep(3000);
                                au.FindElement(By.CssSelector("button[name='submit']")).Click();
                                Thread.Sleep(5000);
                                //если всплывает алерт - жмем ок, т.к. оно появляется не всегда, то в блоке try
                                try {
                                    au.SwitchTo().Alert().Accept();
                                } catch { }
                            });
                        } catch (Exception x) {
                            ToLog("Ошибка обновления auto.ru\n" + bus[b].name + "\n" + x.Message);
                        }
                    }
                    //catch
                    //{
                    //    //wasErrors = true;  //авто ру не будем пока учитывать
                    //    ToLog("АВТО ОШИБКА ПРИ ВНЕСЕНИИ ИЗМЕНЕНИЙ! " + bus[b].name);
                    //}            
                }

                //3.парсинг и привязка
                //var pages = (int)numericUpDown_auto.Value;
                var pages = 0;

                auto.Clear();
                for (int i = pages; i > 0; i--) {
                    var tb = Task.Factory.StartNew(() => {
                        au.Navigate().GoToUrl("https://auto.ru/parts/lk?page=" + i + "&pageSize=50&rgid=6");
                        //парсим элементы
                        foreach (var li in au.FindElements(By.CssSelector("li.UserOffers__list-item"))) {
                            var el_name = li.FindElement(By.ClassName("UserOffersItem__title"));
                            var name = el_name.Text;
                            var id = el_name.FindElement(By.CssSelector("a[class*=UserOffersItem]"))
                                .GetAttribute("href")
                                .Replace("/offer/", "|")
                                .Split('|')[1]
                                .TrimEnd('/');
                            var price = Convert.ToInt32(li.FindElement(By.CssSelector("span[class*=UserOffersItem__price]"))
                                .Text
                                .TrimEnd('Р')
                                .Replace(" ", "")
                                .Replace("\u2009", ""));
                            var image = li.FindElement(
                                    By.CssSelector("img[class*=Item__image]"))
                                .GetAttribute("src")
                                .Replace("/small", "/big");
                            var status = true;
                            //li.FindElement(By.CssSelector("div.user-offers__offer-cell.user-offers__offer-cell_name_status > span")).Text;

                            auto.Add(new AutoObject {
                                name = name,
                                price = price,
                                image = image,
                                status = status,
                                id = id
                            });
                        }
                    });
                    try {
                        await tb;
                    } catch (Exception ex) {
                        ToLog("авто.ру ошибка парсинга сайта, страница " + i + "\n" + ex.Message);
                    }
                    if (base_rescan_need) { break; }
                }

                //привязка

                //объект для сравнение строк методом расстояний Джаро-Винклера
                JaroWinkler jw = new JaroWinkler();
                //пары значений индекс строки - значение расстояния сравнения
                List<StrComp> sc = new List<StrComp>();

                for (int a = 0; a < auto.Count; a++) {
                    //проверим, есть ли в базе карточка с таким id в ссылке
                    var iBus = bus.FindIndex(b => b.auto.Contains(auto[a].id));
                    //если индекс не найден - объявление еще не привязано
                    if (iBus == -1) {
                        //выберем коллекцию индексов не привязанных карточек кандидатов
                        var b_unb = bus.Where(b => b.tiu.Contains("tiu.ru"))
                            .Where(b => b.auto.Contains("опубликовано"))
                            .Select(b => bus.IndexOf(b))
                            .ToList();
                        //посчитаем расстояния для всех найденных индексов базы
                        sc.Clear();
                        foreach (var b in b_unb) {
                            sc.Add(new StrComp() {
                                ind = b,
                                sim = jw.GetSimilarity(auto[a].name, bus[b].name)
                            });
                        }
                        sc.Sort((x1, x2) => x2.sim.CompareTo(x1.sim));

                        if (!checkBox_auto_chbox.Checked) {
                            //обнуляем возвращаемое имя перед вызовом формы
                            BindedName = "";
                            //вызываем новое окно, в конструктор передаем имя объявления авито и маасив кандидатов
                            Form2 f2 = new Form2("auto", a, sc.Select(t => t.ind).ToList());
                            f2.Owner = this;
                            f2.ShowDialog();
                            f2.Dispose();
                        } else {
                            BindedName = bus[sc[0].ind].name;
                        }
                        if (BindedName != "") {
                            iBus = bus.FindIndex(t => t.name == BindedName);
                        }
                    }
                    if (iBus > -1 && !String.IsNullOrEmpty(BindedName)) {
                        bus[iBus].auto = "https://auto.ru/parts/user-offer/?offerId=" + auto[a].id + "&rgid=6";

                        await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>
                            {
                                {"id", bus[iBus].id},
                                {"name", bus[iBus].name},
                                {"313971", bus[iBus].auto}
                            });
                        BindedName = "";
                        ToLog("авто привязано объявление " + a + " " + bus[iBus].name);
                    }
                }
                //5.удаляем неоплаченные по 10 шт за проход 
                //    au.Navigate().GoToUrl("https://auto.ru/parts/lk?page=1&rgid=6&status=unpaid");
                //    //проверим, попали на страницу неоплаченных
                //    if (au.FindElement(By.CssSelector("span[class*=UserOffers__tab_type_unpaid]"))
                //        .GetAttribute("class").Contains("UserOffers__tab_current")) {
                //        //счетчик-ограничение количества удаляемых
                //        for (int i = 0; i < 50; i++) {
                //            au.Navigate().GoToUrl("https://auto.ru/parts/lk?page=1&rgid=6&status=unpaid");
                //            var el = au.FindElements(By.CssSelector("li.UserOffers__list-item"));
                //            if (el.Count > 0) {
                //                var el_name = el[0].FindElement(By.ClassName("UserOffersItem__title"));
                //                var id = el_name.FindElement(By.CssSelector("a[class*=UserOffersItem]"))
                //                    .GetAttribute("href")
                //                    .Replace("offer/", "|")
                //                    .Split('|')
                //                    .Last()
                //                    .TrimEnd('/');
                //                var b = bus.FindIndex(t => t.auto.Contains(id));
                //                int offSetX;
                //                if (b != -1 && bus[b].amount > 0 && bus[b].price >= 1000) offSetX = 0;
                //                else {
                //                    offSetX = 28;
                //                    if (b > -1) {
                //                        bus[b].auto = "";
                //                        await api.RequestAsync("put", "goods", new Dictionary<string, string>{
                //                                {"id", bus[b].id},
                //                                {"name", bus[b].name},
                //                                {"313971", bus[b].auto}
                //                            });
                //                        ToLog("auto.ru удалена ссылка из базы " + (i+1) + "\n" + bus[b].name);
                //                        label_auto.Text = bus.Count(c => c.auto.Length > 4).ToString();
                //                        await Task.Delay(1000);
                //                    }
                //                }
                //                //смещение задает то, в какую кнопку будем жать, продлить или удалить
                //                var frame = el[0].FindElement(By.CssSelector("button[class*=action_type_edit]"));
                //                Actions fc = new Actions(au);
                //                fc.MoveToElement(frame);
                //                fc.Build();
                //                fc.Perform();
                //                await Task.Delay(2000);
                //                fc.MoveByOffset(offSetX, 0);
                //                fc.Click();
                //                fc.Perform();
                //                await Task.Delay(2000);
                //                if (offSetX > 0) au.SwitchTo().Alert().Accept();
                //                else au.FindElement(By.CssSelector("button[name='submit']")).Click();
                //                await Task.Delay(5000);

                //            }
                //            else break;
                //        }
                //    }
                //6.подъем неактивных            
                au.Navigate().GoToUrl("https://auto.ru/parts/lk?page=1&rgid=6&status=inactive");
                //проверим, попали на страницу неактивных
                var el = au.FindElements(By.CssSelector("span[class*=UserOffers__tab_type_inactive]"));
                if (el.Count > 0 && el.First().GetAttribute("class").Contains("UserOffers__tab_current")) {
                    for (int i = 0; i < 50; i++) {
                        au.Navigate().GoToUrl("https://auto.ru/parts/lk?page=1&rgid=6&status=inactive");
                        el = au.FindElements(By.CssSelector("li.UserOffers__list-item"));
                        if (el.Count > 0) {
                            var el_name = el[0].FindElement(By.ClassName("UserOffersItem__title"));
                            var id = el_name.FindElement(By.CssSelector("a[class*=UserOffersItem]"))
                                            .GetAttribute("href")
                                            .Replace("offer/", "|")
                                            .Split('|')[1]
                                            .TrimEnd('/');
                            var b = bus.FindIndex(t => t.auto.Contains(id));
                            //смещение задает то, в какую кнопку будем жать, продлить или удалить
                            int offSetX;
                            if (b != -1 && bus[b].amount > 0 && bus[b].price >= 1000) offSetX = 0;
                            else {
                                offSetX = 28;
                                if (b > -1) {
                                    bus[b].auto = "";
                                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                                         {"id", bus[b].id},
                                         {"name", bus[b].name},
                                         {"313971", bus[b].auto}
                                     });
                                    ToLog("auto.ru удалена ссылка из базы\n" + bus[b].name);
                                    label_auto.Text = bus.Count(c => c.auto.Length > 4).ToString();
                                    await Task.Delay(2000);
                                }
                            }
                            var frame = el[0].FindElement(By.CssSelector("button[class*=action_type_play]"));
                            Actions fc = new Actions(au);
                            fc.MoveToElement(frame);
                            fc.Build();
                            fc.Perform();
                            await Task.Delay(1000);
                            fc.MoveByOffset(offSetX, 0);
                            fc.Click();
                            fc.Perform();
                            await Task.Delay(2000);
                            if (offSetX > 0) au.SwitchTo().Alert().Accept();
                            await Task.Delay(2000);
                        } else break;
                    }
                }
            } catch (Exception x) {
                ToLog("auto.ru:ошибка загрузки страницы!/n" + x.Message);
            }

            if (checkBox_auto_chbox.Checked) {
                button_auto_add.PerformClick();
            }
            ChangeStatus(sender, ButtonStates.Active);
        }

        private async Task AutoAuthAsync() {
            //авторизация
            await Task.Factory.StartNew(() => {
                try {
                    au.Navigate().GoToUrl("https://auto.ru/parts/lk?rgid=6");
                    Thread.Sleep(1000);
                    au.Navigate().GoToUrl("https://auth.auto.ru/login/?r=https%3A%2F%2Fauto.ru%2Fparts%2Flk%3Frgid%3D6");
                    try {
                        au.SwitchTo().Alert().Accept();
                    } catch { }
                    WriteToIWebElement(au, au.FindElement(By.CssSelector("input[name=login]")), "9106027626@mail.ru"); Thread.Sleep(2000);
                    au.FindElement(By.CssSelector("button[class*=blue]")).Click(); Thread.Sleep(2000);
                    //отправлено письмо с паролем на вход

                    var p = new Pop3();
                    var pas = p.GetPass();
                    WriteToIWebElement(au, au.FindElement(By.CssSelector("div[class*='FormCodeInput'] input")), pas);
                    Thread.Sleep(1000);
                    var el = au.FindElement(By.CssSelector("i[class*='WhatsNew__close']"));
                    Actions a = new Actions(au);
                    a.MoveToElement(el).Perform();
                    Thread.Sleep(1000);
                    a.Click().Perform();
                } catch { }
            });
        }

        public void ToSelect(string s) {
            try {
                //var we = au.FindElement(By.CssSelector("div.user-offer__name > div > div > div > span > input"));
                var we = au.FindElement(By.CssSelector("div[class='FormParser'] input"));
                WriteToIWebElement(au, we, s);
                we = au.FindElement(By.CssSelector("button[class*='FormParser__submit']"));
                we.Click();
                Thread.Sleep(500);
            } catch {
                ToLog("авто.ру ошибка при выборе группы!");
            }
        }

        public async Task SelectElement(string s) {
            Task t8 = Task.Factory.StartNew(() => { Thread.Sleep(2000); }); await t8;
            foreach (var we in au.FindElements(By.CssSelector("body > div > div > div > div"))) {
                if (we.Text == s) {
                    we.Click();
                    Task t9 = Task.Factory.StartNew(() => { Thread.Sleep(2000); }); await t9;
                }
            }
        }

        private async void AutoNewAddAsync(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                var newCount = 0;
                for (int b = bus.Count - 1; b > -1; b--) {
                    if (newCount >= numericUpDown_auto.Value) break;
                    if (
                        bus[b].tiu.Contains("http") &&
                        !bus[b].auto.Contains("http") &&
                        bus[b].amount > 0 &&
                        bus[b].price >= 100 &&
                        bus[b].images.Count > 0
                        //!bus[b].auto.Contains("опубликовано") &&
                        //auto.FindIndex(_ => _.name == bus[b].name) == -1) 
                        ) {
                        try {
                            //сперва определим группу из базы
                            var g = bus[b].GroupName();
                            if (g == "Автохимия") continue;

                            //определяем авто
                            var m = await SelectAutoAsync(b);

                            if (m == null) continue;


                            au.Navigate().GoToUrl("https://auto.ru/parts/user-offer?rgid=6");
                            //===================
                            //выбор категории!
                            //===================

                            //сохраним имя в нижнем регистре
                            var n = bus[b].name.ToLower();
                            //выбор категории
                            try {
                                switch (g) {
                                    case "Стартеры": ToSelect("стартер" + m); break;

                                    case "Автостекло":
                                        if (n.Contains("заднее") && !n.Contains(" l ") && !n.Contains(" r ")
                                            && !n.Contains("лев") && !n.Contains("прав")
                                            && !n.Contains("l/r") && !n.Contains(@"l\r")
                                            && !n.Contains("r/l") && !n.Contains(@"r\l")) { ToSelect("стекло заднее" + m); break; }
                                        if (n.Contains("лобов")) { ToSelect("стекло лобовое" + m); break; }
                                        ToSelect("стекло боковое" + m);
                                        if (n.Contains("задн")) {
                                            au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div")).Click();
                                            await SelectElement("Сзади");
                                        }
                                        if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                            au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                            await SelectElement("Правая");
                                        }
                                        break;

                                    case "Генераторы":
                                        if (n.Contains("щетки")) { ToSelect("щетки генератора" + m); break; }
                                        ToSelect("генератор и комплектующие" + m); break;

                                    case "Рулевые рейки, рулевое управление":
                                        if (n.Contains("насос")) { ToSelect("насос гидроусилителя" + m); break; }
                                        if (n.Contains("шкив")) { ToSelect("шкив насоса гидроусилителя" + m); break; }
                                        if (n.Contains("бачок")) { ToSelect("бачок гидроусилителя руля" + m); break; }
                                        if (n.Contains("рейка")) { ToSelect("рулевая рейка" + m); break; }
                                        if (n.Contains("вал")) { ToSelect("вал рулевой" + m); break; }
                                        if (n.Contains("кардан")) { ToSelect("кардан рулевой" + m); break; }
                                        if (n.Contains("руль")) { ToSelect("Рулевое колесо" + m); break; }
                                        if (n.Contains("колонка")) { ToSelect("Колонки рулевые в сборе" + m); break; }
                                        if (n.Contains("шланг")) { ToSelect("Шланг гидроусилителя" + m); break; }
                                        if (n.Contains("радиатор")) { ToSelect("Радиатор гидроусилителя" + m); break; }
                                        if (n.Contains("трубк")) { ToSelect("трубка гидроусилителя" + m); break; }
                                        if (n.Contains("тяг")) { ToSelect("Рулевая тяга" + m); break; }
                                        ToSelect("Рулевое управление" + m); break;

                                    case "Трансмиссия и привод":
                                        if (n.Contains("гидротрансформатор")) { ToSelect("Гидротрансформатор АКПП" + m); break; }
                                        if (n.Contains("привод")) {
                                            ToSelect("Приводной вал" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("кулиса")) { ToSelect("Кулиса КПП" + m); break; }
                                        if (n.Contains("ыжимной")) { ToSelect("Подшипник выжимной" + m); break; }
                                        if (n.Contains("подшипн")) {
                                            ToSelect("Подшипник ступицы" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("ступиц")) {
                                            ToSelect("Ступица колеса" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("мкпп ")) { ToSelect("МКПП" + m); break; }
                                        if (n.Contains("акпп ")) { ToSelect("АКПП" + m); break; }
                                        if (n.Contains("шрус")) { ToSelect("ШРУС" + m); break; }
                                        if (n.Contains("кардан")) { ToSelect("Карданный вал" + m); break; }
                                        if (n.Contains("корзина")) { ToSelect("Корзина сцепления" + m); break; }
                                        if (n.Contains("главный")) { ToSelect("Цилиндр сцепления главный" + m); break; }
                                        if (n.Contains("рабочий")) { ToSelect("Цилиндр сцепления рабочий" + m); break; }
                                        if (n.Contains("редуктор")) { ToSelect("Редуктор" + m); break; }
                                        if (n.Contains("сцеплени")) { ToSelect("Комплект сцепления" + m); break; }
                                        if (n.Contains("фильтракпп")) { ToSelect("Фильтр АКПП" + m); break; }
                                        if (n.Contains("раздаточн")) { ToSelect("Раздаточная коробка" + m); break; }
                                        if (n.Contains("кулис")) { ToSelect("Кулиса КПП" + m); break; }
                                        if (n.Contains("кольц")) { ToSelect("Кольцо упорное КПП" + m); break; }
                                        ToSelect("Трансмиссия" + m); break;

                                    case "Зеркала":
                                        if (n.Contains("салон")) { ToSelect("Салонное зеркало заднего вида" + m); break; }
                                        if (n.Contains("элемент")) {
                                            ToSelect("Зеркальный элемент" + m);
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("зеркало")) {
                                            ToSelect("Боковые зеркала заднего вида" + m);
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        ToSelect("Зеркала" + m); break;

                                    case "Автохимия":
                                        if (n.Contains("паста") && n.Contains("рук")) { ToSelect("Очиститель для рук"); break; }
                                        ToSelect("Автохимия"); break;

                                    case "Аудио-видеотехника":
                                        if (n.Contains("магнитола")) { ToSelect("Магнитола" + m); break; }
                                        if (n.Contains("динамик")) { ToSelect("Динамики" + m); break; }
                                        ToSelect("Аудиотехника" + m); break;

                                    case "Датчики":
                                        if (n.Contains("abs")) { ToSelect("Датчик ABS" + m); break; }
                                        if (n.Contains("давления")) { ToSelect("Датчик абсолютного давления" + m); break; }
                                        if (n.Contains("масл")) { ToSelect("Датчик давления масла" + m); break; }
                                        if (n.Contains("кислород") || n.Contains("ямбда")) { ToSelect("Датчик кислорода" + m); break; }
                                        if (n.Contains("детонац")) { ToSelect("Датчик детонации" + m); break; }
                                        if (n.Contains("коленвала")) { ToSelect("Датчик положения коленвала" + m); break; }
                                        if (n.Contains("распредвала")) { ToSelect("Датчик положения распредвала" + m); break; }
                                        if (n.Contains("температуры")) { ToSelect("Датчик температуры охл. жидкости" + m); break; }
                                        if (n.Contains("расх")) { ToSelect("Датчик массового расхода воздуха" + m); break; }
                                        if (n.Contains("топлив")) { ToSelect("Датчик уровня топлива" + m); break; }
                                        if (n.Contains("парков")) { ToSelect("Парковочная система" + m); break; }
                                        if (n.Contains("подуш") && n.Contains("bag")) { ToSelect("Датчик подушки безопасности" + m); break; }
                                        ToSelect("Датчик износа тормозных колодок" + m); break;

                                    case "Детали двигателей":
                                    case "Двигатели":
                                        if (n.Contains("двигатель")) { ToSelect("Двигатель в сборе" + m); break; }
                                        if (n.Contains("цилиндров")) { ToSelect("Блок цилиндров" + m); break; }
                                        if (n.Contains("маховик")) { ToSelect("Маховик" + m); break; }
                                        if (n.Contains("промежуточный")) { ToSelect("Вал промежуточный" + m); break; }
                                        if (n.Contains("лапан") && n.Contains("впуск")) { ToSelect("Клапан впускной" + m); break; }
                                        if (n.Contains("клапан ")) { ToSelect("Клапан выпускной" + m); break; }
                                        if (n.Contains("кожух")) { ToSelect("Крышка ремня ГРМ" + m); break; }
                                        if (n.Contains("прокладкагбц")) { ToSelect("Прокладка головки блока цилиндров" + m); break; }
                                        if (n.Contains("лапанн") && n.Contains("крышк")) { ToSelect("Крышка головки блока цилиндров" + m); break; }
                                        if (n.Contains("гбц") && bus[b].price > 500) { ToSelect("Головка блока цилиндров" + m); break; }
                                        if (n.Contains("ольца")) { ToSelect("Кольца поршневые" + m); break; }
                                        if (n.Contains("грм") && n.Contains("крыш")) { ToSelect("Крышка ремня ГРМ" + m); break; }
                                        if (n.Contains("грм") && n.Contains("защит")) { ToSelect("Крышка ремня ГРМ" + m); break; }
                                        if (n.Contains("оддон") && n.Contains("двигател")) { ToSelect("Поддон двигателя" + m); break; }
                                        if (n.Contains("маслоотражатель")) { ToSelect("Маслоотражатель турбины" + m); break; }
                                        if (n.Contains("крышка") && n.Contains("двигател")) { ToSelect("Крышка двигателя декоративная" + m); break; }
                                        if (n.Contains("шатун") || n.Contains("оршен")) { ToSelect("Коленвал, поршень, шатун" + m); break; }
                                        if (n.Contains("шкив") && n.Contains("колен")) { ToSelect("Шкив коленвала" + m); break; }
                                        if (n.Contains("распредвал")) { ToSelect("Распредвал и клапаны" + m); break; }
                                        if (n.Contains("ролик")) { ToSelect("Натяжитель приводного ремня" + m); break; }
                                        if (n.Contains("шкив")) { ToSelect("Шкив коленвала" + m); break; }
                                        if (n.Contains("шестерн") && n.Contains("баланс")) { ToSelect("Шестерня вала балансирного" + m); break; }
                                        if (n.Contains("шестерн")) { ToSelect("Шестерня коленвала" + m); break; }
                                        if (n.Contains("гидрокомпенсат")) { ToSelect("Гидрокомпенсатор" + m); break; }
                                        if (n.Contains("ланг") && n.Contains("вентиляц")) { ToSelect("Патрубок вентиляции картерных газов" + m); break; }
                                        if (n.Contains("ружин") && n.Contains("клапан")) { ToSelect("Пружина клапана" + m); break; }
                                        if (n.Contains("поддон") && n.Contains("проклад")) { ToSelect("Прокладка поддона двигателя" + m); break; }
                                        if (n.Contains("пыльник") && n.Contains("двигат")) { ToSelect("Пыльник двигателя" + m); break; }
                                        if (n.Contains("поддон")) { ToSelect("Поддон двигателя" + m); break; }
                                        if (n.Contains("вал") && n.Contains("насос")) { ToSelect("Вал промежуточный" + m); break; }
                                        if (n.Contains("ванос") || (n.Contains("егуляторфаз"))) { ToSelect("Муфта изменения фаз ГРМ" + m); break; }
                                        if (n.Contains("крыш") && n.Contains("коленв")) { ToSelect("Крышка коленвала" + m); break; }
                                        if (n.Contains("кладыш") && n.Contains("корен")) { ToSelect("Вкладыши коленвала" + m); break; }
                                        if (n.Contains("коленвал")) { ToSelect("Коленвал" + m); break; }
                                        if (n.Contains("идронатяжитель") || n.Contains("прокладк")) { ToSelect("Натяжитель ремня ГРМ" + m); break; }
                                        if (n.Contains("роклад") && n.Contains("пер")) { ToSelect("Прокладка крышки блока цилиндров" + m); break; }
                                        if (n.Contains("роклад") && n.Contains("крыш")) { ToSelect("Прокладка клапанной крышки" + m); break; }
                                        if (n.Contains("компл") && n.Contains("прокл")) { ToSelect("Комплект прокладок двигателя" + m); break; }
                                        if (n.Contains("маслопри")) { ToSelect("Маслоприемник" + m); break; }
                                        ToSelect("Двигатель и система зажигания" + m); break;

                                    case "Кронштейны, опоры":
                                        if (n.Contains("гур") || n.Contains("гидроус")) { ToSelect("Кронштейн гидроусилителя руля" + m); break; }
                                        if (n.Contains("генератор")) { ToSelect("Крепление генератора" + m); break; }
                                        if (n.Contains("двигател") || n.Contains("двс")) { ToSelect("Опора двигателя" + m); break; }
                                        if (n.Contains("компресс") || n.Contains("кондицион")) { ToSelect("Крепление компрессора кондиционера" + m); break; }
                                        if (n.Contains("маслян")) { ToSelect("Крепление масляного фильтра" + m); break; }
                                        if (n.Contains("аккумулят")) { ToSelect("Крепление аккумулятора" + m); break; }
                                        if (n.Contains("кпп")) { ToSelect("Кронштейн КПП" + m); break; }
                                        if (n.Contains("бампер")) {
                                            ToSelect("Кронштейн бампера" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("консол")) { ToSelect("Консоль" + m); break; }
                                        if (n.Contains("амортиз") && bus[b].price > 200) { ToSelect("Опора амортизатора" + m); break; }
                                        if (n.Contains("редукт")) { ToSelect("Опора редуктора" + m); break; }
                                        if (n.Contains("балк")) { ToSelect("Кронштейн подрамника" + m); break; }
                                        if (n.Contains("бак")) { ToSelect("Кронштейн топливного бака" + m); break; }
                                        if (n.Contains("безопас")) { ToSelect("Крепление ремня безопасности" + m); break; }
                                        if (n.Contains("опор")) { ToSelect("Опора двигателя" + m); break; }
                                        ToSelect("Кузовные запчасти" + m); break;

                                    case "Пластик кузова":
                                    case "Ручки и замки кузова":
                                    case "Петли":
                                    case "Кузовные запчасти":
                                        if (n.Contains("бампер ")) {
                                            ToSelect("Бампер" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            break;
                                        }
                                        if (n.Contains("бампер") && n.Contains("решет") && bus[b].price >= 500) {
                                            ToSelect("Решётка бампера" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("бампер") && n.Contains("усилител")) {
                                            ToSelect("Усилитель бампера" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            break;
                                        }
                                        if (n.Contains("бампер") && (n.Contains("сорбер") || n.Contains("аполнит"))) {
                                            ToSelect("Абсорбер бампера" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            break;
                                        }
                                        if (n.Contains("бампер") && (n.Contains("олдинг") || n.Contains("кладка") || n.Contains("губа"))) { ToSelect("Молдинг бампера" + m); break; }
                                        if (n.Contains("бампер") && n.Contains("аправляющ")) {
                                            ToSelect("Направляющая бампера" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            break;
                                        }
                                        if (n.Contains("подрамник")) {
                                            ToSelect("подрамник" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            break;
                                        }

                                        if (n.Contains("двер") && (n.Contains("олдинг") || n.Contains("акладка"))) {
                                            ToSelect("Молдинг двери" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("крыл") && (n.Contains("олдинг") || n.Contains("хром"))) {
                                            ToSelect("Молдинг крыла" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("петл") && (n.Contains("двер") || n.Contains("перед"))) {
                                            ToSelect("Петля двери" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("петл") && n.Contains("капот")) {
                                            ToSelect("Петля капота" + m);
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Правая");//верный селектор
                                            }
                                            break;
                                        }
                                        if (n.Contains("лобов") && n.Contains("олдинг")) { ToSelect("Молдинг лобового стекла" + m); break; }
                                        if (n.Contains("петл") && n.Contains("багаж")) { ToSelect("Петля багажника" + m); break; }
                                        if (n.Contains("крыш") && n.Contains("олдинг")) { ToSelect("Молдинг крыши" + m); break; }
                                        if (n.Contains("дверь ") && bus[b].price >= 1000) {
                                            ToSelect("Дверь боковая" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("подкрыл") || n.Contains("локер")) {
                                            ToSelect("Подкрылок" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("ручка") && n.Contains("внутр")) { ToSelect("Ручка двери внутренняя" + m); break; }
                                        if (n.Contains("ручка") && (n.Contains("наружн") || n.Contains("внешн") || n.Contains("двери"))) {
                                            ToSelect("Ручка двери наружная" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("ручка") && n.Contains("багаж")) { ToSelect("Ручка багажника" + m); break; }
                                        if ((n.Contains("решетка") || n.Contains("решётка")) && n.Contains("радиат")) { ToSelect("Решётка радиатора" + m); break; }
                                        if (n.Contains("ручка") && n.Contains("капот")) { ToSelect("Ручка открывания капота" + m); break; }
                                        if (n.Contains("порог ")) {
                                            ToSelect("Порог кузовной" + m);
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("бачок") && n.Contains("омыв")) { ToSelect("Бачок стеклоомывателя" + m); break; }
                                        if (n.Contains("жабо") || n.Contains("дождевик") || n.Contains("водяной") || n.Contains("водосток")) {
                                            ToSelect("Панель под лобовое стекло" + m); break;
                                        }
                                        if (n.Contains("замок") && n.Contains("двер") || n.Contains("замка") && (n.Contains("част") || n.Contains("клапан"))) {
                                            ToSelect("Замок двери" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("трапеция")) { ToSelect("Трапеция стеклоочистителя" + m); break; }
                                        if (n.Contains("дворник")) {
                                            ToSelect("Поводок стеклоочистителя" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if ((n.Contains("замок") || n.Contains("замк")) && n.Contains("багаж")) { ToSelect("Замок багажника в сборе" + m); break; }
                                        if (n.Contains("амортиз") && n.Contains("багаж")) { ToSelect("Амортизатор двери багажника" + m); break; }
                                        if (n.Contains("амортиз") && n.Contains("капот")) { ToSelect("Амортизатор капота" + m); break; }
                                        if (n.Contains("замок") && n.Contains("капот")) { ToSelect("Замок капота" + m); break; }
                                        if (n.Contains("брызговик")) {
                                            ToSelect("Брызговик" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("крыло ")) {
                                            ToSelect("Крыло" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if ((n.Contains("ержатель") || n.Contains("упор")) && n.Contains("капот")) { ToSelect("Упор капота" + m); break; }
                                        if (n.Contains("ащита") && (n.Contains("двс") || n.Contains("гравий"))) { ToSelect("Защита картера двигателя" + m); break; }
                                        if (n.Contains("рышка") && n.Contains("багажник")) { ToSelect("Крышка багажника" + m); break; }
                                        if (n.Contains("лючок") && n.Contains("бак")) { ToSelect("Лючок топливного бака" + m); break; }
                                        if (n.Contains("крыша")) { ToSelect("Панель крыши" + m); break; }
                                        if (n.Contains("капот ")) { ToSelect("Капот" + m); break; }
                                        if (n.Contains("стеклопод")) {
                                            ToSelect("Стеклоподъемный механизм" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            if (!n.Contains("элек")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[4]/div/div/button")).Click();
                                                await SelectElement("Механический");
                                            }
                                            break;
                                        }
                                        if ((n.Contains("уплотнител") || n.Contains("резинк")) && n.Contains("двер")) {
                                            ToSelect("Уплотнитель двери" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("акладка") && n.Contains("порог")) {
                                            ToSelect("Накладка порога" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[1]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("акладка") && n.Contains("крыл")) {
                                            ToSelect("Накладка крыла" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("акладка") && n.Contains("багажн")) { ToSelect("Накладка багажника" + m); break; }
                                        if (n.Contains("четверть") || n.Contains("лонжер")) {
                                            ToSelect("Четверть кузова" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("гайка") && n.Contains("колес")) { ToSelect("Гайка колесная" + m); break; }
                                        if (n.Contains("кнопк") && n.Contains("багаж")) { ToSelect("Замок багажника в сборе" + m); break; }
                                        if ((n.Contains("балка") && n.Contains("радиат")
                                            || n.Contains("панел") && n.Contains("передн")
                                            || n.Contains("телевизор")) && bus[b].price >= 1000) { ToSelect("Рамка радиатора" + m); break; }
                                        if (n.Contains("боковина")) {
                                            ToSelect("Боковина" + m);
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if ((n.Contains("резинка") || n.Contains("уплотнитель")) && n.Contains("задн") && n.Contains("стекл")) {
                                            ToSelect("Уплотнитель заднего стекла" + m); break;
                                        }
                                        if ((n.Contains("резинка") || n.Contains("уплотнитель")) && n.Contains("лобов") && n.Contains("стекл")) {
                                            ToSelect("Уплотнитель лобового стекла" + m); break;
                                        }
                                        if ((n.Contains("резинка") || n.Contains("уплотнитель")) && n.Contains("боков") && n.Contains("стекл")) {
                                            ToSelect("Уплотнитель бокового стекла" + m); break;
                                        }
                                        if (n.Contains("личинк") && n.Contains("замк")) {
                                            ToSelect("Личинка замка" + m); break;
                                        }
                                        if (n.Contains("задня") && n.Contains("част") && n.Contains("кузов")) {
                                            ToSelect("Задняя часть автомобиля" + m); break;
                                        }
                                        if (n.Contains("крюк") && n.Contains("буксир")) {
                                            ToSelect("Петля буксировочная" + m); break;
                                        }
                                        if (n.Contains("эмблем")) { ToSelect("Эмблема" + m); break; }
                                        if ((n.Contains("резинка") || n.Contains("уплотнитель")) && n.Contains("багажн")) {
                                            ToSelect("Уплотнитель крышки багажника" + m); break;
                                        }
                                        if ((n.Contains("защита") || n.Contains("экран")) && n.Contains("коллект")) {
                                            ToSelect("Тепловые экраны" + m); break;
                                        }
                                        if (n.Contains("молдинг") || n.Contains("накладка") || n.Contains("хром")) {
                                            ToSelect("молдинги" + m); break;
                                        }
                                        if (n.Contains("ограничител") && n.Contains("двер")) {
                                            ToSelect("Ограничитель двери" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("арк") && n.Contains("обшив")) {
                                            ToSelect("Арка колеса" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("пыльник") && n.Contains("двигат")) { ToSelect("Пыльник двигателя" + m); break; }
                                        if (n.Contains("пыльник") && n.Contains("рулев")) { ToSelect("Пыльник рулевой рейки" + m); break; }
                                        if (n.Contains("пыльник") && n.Contains("шрус")) { ToSelect("Пыльник ШРУСа" + m); break; }
                                        if (n.Contains("крышка") && (n.Contains("фонар") || n.Contains("фар"))) {
                                            ToSelect("Крышка фары" + m); break;
                                        }
                                        if (n.Contains("крышка") && (n.Contains("предохр") || n.Contains("фар"))) {
                                            ToSelect("Крышка блока предохранителей" + m); break;
                                        }
                                        if (n.Contains("крышка") && (n.Contains("заливн") || n.Contains("масл"))) {
                                            ToSelect("Крышка маслозаливной горловины" + m); break;
                                        }
                                        if (n.Contains("ронштейн") && n.Contains("замка")) { ToSelect("Кронштейн замка двери" + m); break; }
                                        if (n.Contains("корпус") && n.Contains("блока")) { ToSelect("Корпус блока предохранителей" + m); break; }
                                        if (n.Contains("люк ")) { ToSelect("люк" + m); break; }
                                        if (n.Contains("треуголь") && n.Contains("зерка")) { ToSelect("Треугольники зеркал" + m); break; }
                                        if (n.Contains("треуголь") && n.Contains("двер")) {
                                            ToSelect("Накладка двери" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("щит") && n.Contains("опорн")) {
                                            ToSelect("Щит опорный" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if ((n.Contains("щит") || n.Contains("защит")) && (n.Contains("диск") || n.Contains("торм"))) {
                                            ToSelect("Щиток тормозного диска" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("ролик") && n.Contains("двер")) { ToSelect("Ролик сдвижной двери" + m); break; }
                                        if (n.Contains("торсион")) { ToSelect("Торсион крышки багажника" + m); break; }
                                        if ((n.Contains("обшивка") || n.Contains("изоляция")) && n.Contains("капот")) {
                                            ToSelect("Обшивка капота" + m); break;
                                        }
                                        if (n.Contains("спойлер")) { ToSelect("Спойлер" + m); break; }
                                        if (n.Contains("уплотнит") && n.Contains("капот")) { ToSelect("Уплотнитель капота" + m); break; }
                                        if (n.Contains("рейлинг")) { ToSelect("Рейлинги" + m); break; }
                                        ToSelect("Кузовные запчасти" + m);
                                        break;

                                    case "Подвеска и тормозная система":
                                        if (n.Contains("амортизатор ") || n.Contains("амортизаторы")) {
                                            ToSelect("Амортизатор в сборе" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            au.FindElement(By.XPath("//div[2]/fieldset[1]/div[1]/div/div/button")).Click();
                                            await SelectElement("Газо-масляный");
                                            break;
                                        }
                                        if (n.Contains("суппорт ")) {
                                            ToSelect("Суппорт тормозной" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("кулак ")) { ToSelect("Кулак поворотный" + m); break; }
                                        if (n.Contains("блок") && n.Contains("abs")) { ToSelect("Блок ABS гидравлический" + m); break; }
                                        if (n.Contains("балка ") || n.Contains("подрамник")) {
                                            ToSelect("Подрамник" + m); break;
                                        }
                                        if (n.Contains("диск") && (n.Contains("тормоз") || n.Contains("вент"))) {
                                            ToSelect("Диск тормозной" + m); break;
                                        }
                                        if (n.Contains("барабан") && (n.Contains("тормоз") || n.Contains("задн"))) {
                                            ToSelect("Барабан тормозной" + m); break;
                                        }
                                        if (n.Contains("пружина ") || n.Contains("пружины ")) {
                                            ToSelect("Пружина подвески" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("рычаг")) {
                                            ToSelect("Рычаг подвески" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("стабилизатор")) {
                                            ToSelect("Стабилизатор" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            break;
                                        }
                                        if (n.Contains("стойка ") || n.Contains("стойки")) {
                                            ToSelect("Стойка амортизатора" + m);
                                            au.FindElement(By.XPath("//div[2]/fieldset[1]/div[1]/div/div/button")).Click();
                                            await SelectElement("Газо-масляный");
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[4]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("вакуумн")) { ToSelect("Вакуумный усилитель тормозов" + m); break; }
                                        if (n.Contains("главн") && n.Contains("тормоз") && n.Contains("цилин")) {
                                            ToSelect("Главный тормозной цилиндр" + m); break;
                                        }
                                        if (n.Contains("тяга ")) {
                                            ToSelect("Тяга подвески" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            break;
                                        }
                                        if (n.Contains("бачок") && n.Contains("тормоз") && n.Contains("цилин")) {
                                            ToSelect("Бачок главного тормозного цилиндра" + m); break;
                                        }
                                        if (n.Contains("ступица")) {
                                            ToSelect("Ступица колеса" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("шланг") && n.Contains("тормоз")) {
                                            ToSelect("Шланг тормозной" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("рем") && n.Contains("компл") && n.Contains("колод")) {
                                            ToSelect("Комплект монтажный колодок" + m); break;
                                        }
                                        if (n.Contains("рем") && n.Contains("компл") && n.Contains("ручн")) {
                                            ToSelect("Ремкомплект стояночного тормоза" + m); break;
                                        }
                                        if (n.Contains("скоб") && n.Contains("суппорт")) {
                                            ToSelect("Скоба суппорта" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("отбойник") && n.Contains("аморт")) {
                                            ToSelect("Отбойник амортизатора" + m); break;
                                        }
                                        if (n.Contains("колодк") && n.Contains("тормозн")) {
                                            ToSelect("Тормозные колодки" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("бараб")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[1]/div/div/button")).Click();
                                                await SelectElement("Барабанные");
                                            }
                                            break;
                                        }
                                        if ((n.Contains("регулят") || n.Contains("распределитель")) && n.Contains("тормоз")) {
                                            ToSelect("Тормозная система" + m); break;
                                        }
                                        ToSelect("Подвеска" + m);
                                        break;

                                    case "Топливная, выхлопная система":
                                        if (n.Contains("абсорбер ") || n.Contains("паров")) { ToSelect("Абсорбер" + m); break; }
                                        if (n.Contains("бак ")) { ToSelect("Бак топливный" + m); break; }
                                        if (n.Contains("коллектор ") && n.Contains("впуск")) { ToSelect("Коллектор впускной" + m); break; }
                                        if (n.Contains("коллектор ") && n.Contains("выпуск")) { ToSelect("Коллектор выпускной" + m); break; }
                                        if (n.Contains("заслонк") && n.Contains("дроссел")) { ToSelect("Заслонка дроссельная" + m); break; }
                                        if (n.Contains("насос") && n.Contains("топлив")) { ToSelect("Топливный насос" + m); break; }
                                        if (n.Contains("бачок") && n.Contains("вакуум")) { ToSelect("Бачок вакуумный" + m); break; }
                                        if (n.Contains("воздуховод") || n.Contains("воздухозаборник") || n.Contains("гофра ")
                                            || n.Contains("патрубок ") && n.Contains("возд")) { ToSelect("Воздухозаборник двигателя" + m); break; }
                                        if (n.Contains("глушитель")) { ToSelect("глушитель" + m); break; }
                                        if (n.Contains("горловина") && n.Contains("бака")) { ToSelect("Горловина топливного бака" + m); break; }
                                        if (n.Contains("гофр") && n.Contains("площад") && n.Contains("ремон")) { ToSelect("Гофра глушителя" + m); break; }
                                        if (n.Contains("катализатор")) { ToSelect("Катализатор" + m); break; }
                                        if (n.Contains("клапан") && n.Contains("вакуумн")) { ToSelect("Клапан абсорбера" + m); break; }
                                        if (n.Contains("клапан") && n.Contains("бака")) { ToSelect("Клапан вентиляционный бака" + m); break; }
                                        if (n.Contains("клапан") && (n.Contains("егр") || n.Contains("egr"))) { ToSelect("Клапан системы EGR" + m); break; }
                                        if (n.Contains("клапан") && n.Contains("электр")) { ToSelect("Клапан пневматический" + m); break; }
                                        if (n.Contains("клапан") && n.Contains("холост")) { ToSelect("Регулятор холостого хода" + m); break; }
                                        if (n.Contains("корпус") && n.Contains("фильт")) { ToSelect("Корпус воздушного фильтра" + m); break; }
                                        if ((n.Contains("патрубок") || n.Contains("трубка")) && n.Contains("топл") || n.Contains("шланг")) {
                                            ToSelect("Шланг топливный" + m); break;
                                        }
                                        if ((n.Contains("патрубок") || n.Contains("трубка")) && (n.Contains("абсорб") || n.Contains("вакуум"))) {
                                            ToSelect("Трубка адсорбера" + m); break;
                                        }
                                        if ((n.Contains("патрубок") || n.Contains("трубка")) && n.Contains("картер")) {
                                            ToSelect("Патрубок вентиляции картерных газов" + m); break;
                                        }
                                        if (n.Contains("резонатор") && (n.Contains("воздуш") || n.Contains("филь"))) {
                                            ToSelect("Резонатор воздушного фильтра" + m); break;
                                        }
                                        if (n.Contains("резонатор ")) { ToSelect("Резонатор" + m); break; }
                                        if (n.Contains("форсунк") || n.Contains("моновпрыск")) { ToSelect("Топливная форсунка" + m); break; }
                                        if (n.Contains("сапун")) { ToSelect("Сапун" + m); break; }
                                        if (n.Contains("крышка") && n.Contains("бака")) { ToSelect("Крышка топливного бака" + m); break; }
                                        if (n.Contains("насос") && n.Contains("вакуум")) { ToSelect("Вакуумный насос" + m); break; }
                                        if (n.Contains("топливн")) { ToSelect("Топливная система" + m); break; }
                                        if (n.Contains("приемная") && n.Contains("труба")) { ToSelect("Приемная труба" + m); break; }
                                        if (n.Contains("регулятор") && n.Contains("топл")) { ToSelect("Регулятор давления топлива" + m); break; }
                                        if (n.Contains("ресивер")) { ToSelect("Ресивер пневмосистемы" + m); break; }
                                        if (n.Contains("труб") && n.Contains("турб")) { ToSelect("Трубка турбокомпрессора" + m); break; }
                                        if (n.Contains("труб") && (n.Contains("тнвд") || n.Contains("выс"))) {
                                            ToSelect("Трубка топливного насоса" + m); break;
                                        }
                                        if (n.Contains("турбина ")) { ToSelect("Турбокомпрессор" + m); break; }
                                        ToSelect("Топливная система" + m);
                                        break;

                                    case "Электрика, зажигание":
                                        if (n.Contains("актуатор") && n.Contains("замк")) { ToSelect("Привод замка двери" + m); break; }
                                        if (n.Contains("антенн")) { ToSelect("Антенна" + m); break; }
                                        if (n.Contains("блок") && n.Contains("airbag")) { ToSelect("Блок управления подушки безопасности" + m); break; }
                                        if (n.Contains("кноп") && n.Contains("рул")) { ToSelect("Блок кнопок руля" + m); break; }
                                        if (n.Contains("переключат") && n.Contains("подрул")) { ToSelect("Подрулевой переключатель" + m); break; }
                                        if (n.Contains("блок") && (n.Contains("комфорт") || n.Contains("климат"))) { ToSelect("Блок комфорта" + m); break; }
                                        if (n.Contains("блок") && n.Contains("предохранител")) { ToSelect("Блок предохранителей аккумулятора" + m); break; }
                                        if (n.Contains("корпус") && n.Contains("предохранител")) { ToSelect("Корпус блока предохранителей" + m); break; }
                                        if (n.Contains("блок") && (n.Contains("двс") || n.Contains("двигател"))) { ToSelect("Блок управления ДВС" + m); break; }
                                        if (n.Contains("блок") && n.Contains("акпп")) { ToSelect("Блок управления АКПП" + m); break; }
                                        if (n.Contains("катушк") && n.Contains("зажиг")) { ToSelect("Катушка зажигания" + m); break; }
                                        if (n.Contains("блок") && n.Contains("srs")) { ToSelect("Блок SRS" + m); break; }
                                        if (n.Contains("управления") && n.Contains("свет")) { ToSelect("Блок управления светом" + m); break; }
                                        if (n.Contains("управления") && n.Contains("стеклопод")) { ToSelect("Блок переключателей подъемников" + m); break; }
                                        if ((n.Contains("кноп") || n.Contains("выключател")) && n.Contains("стеклопод")) {
                                            ToSelect("Переключатель стеклоподъемника" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("мотор") && n.Contains("стеклопод")) {
                                            ToSelect("Мотор стеклоподъемника" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("выключатель") && n.Contains("стоп")) { ToSelect("Концевик педали тормоза" + m); break; }
                                        if (n.Contains("кнопк") && n.Contains("аварий")) { ToSelect("Кнопка аварийной сигнализации" + m); break; }
                                        if (n.Contains("кнопк")) { ToSelect("Клипса многофункциональная" + m); break; }
                                        if (n.Contains("трамбл") || n.Contains("аспределитель")) { ToSelect("Трамблер" + m); break; }
                                        if (n.Contains("замок") && n.Contains("зажиг")) { ToSelect("Замок зажигания" + m); break; }
                                        if (n.Contains("панель") && n.Contains("прибор")) { ToSelect("Приборная панель" + m); break; }
                                        if ((n.Contains("подушка") || n.Contains("шторк")) && n.Contains("безоп")) {
                                            ToSelect("Подушка безопасности" + m); break;
                                        }
                                        if (n.Contains("клаксон") || n.Contains("зуммер")) { ToSelect("Сигнал звуковой в сборе" + m); break; }
                                        if (n.Contains("жгут") || n.Contains("проводк") || n.Contains("кабел")) {
                                            ToSelect("Жгут проводки" + m); break;
                                        }
                                        if (n.Contains("корректор")) { ToSelect("Корректор фар" + m); break; }
                                        if (n.Contains("мотор")) { ToSelect("Мотор стеклоочистителя" + m); break; }
                                        if (n.Contains("реле")) { ToSelect("Блок реле" + m); break; }
                                        if (n.Contains("провод") && n.Contains("акб")) { ToSelect("Провода аккумулятора" + m); break; }
                                        if ((n.Contains("насос") || n.Contains("привод") || n.Contains("компрессор")) && n.Contains("зам")) {
                                            ToSelect("Компрессор центрального замка" + m); break;
                                        }
                                        if (n.Contains("резистор") || n.Contains("еостат")) { ToSelect("Резистор отопителя" + m); break; }
                                        if (n.Contains("патрон")) { ToSelect("Патрон лампы" + m); break; }
                                        if (n.Contains("коммутатор")) { ToSelect("Коммутатор системы зажигания" + m); break; }
                                        if (n.Contains("сирена")) { ToSelect("Сирена сигнализации" + m); break; }
                                        if (n.Contains("шлейф") && n.Contains("рул")) { ToSelect("Шлейф подрулевой" + m); break; }
                                        if (n.Contains("свеч") && n.Contains("накал")) { ToSelect("Свеча накала" + m); break; }
                                        if (n.Contains("блок") && (n.Contains("управления") || n.Contains("электронный"))) {
                                            ToSelect("Блок управления ДВС" + m); break;
                                        }
                                        if (n.Contains("парктрон")) { ToSelect("Парковочная система" + m); break; }
                                        if (n.Contains("разъем") || n.Contains("фишка")) { ToSelect("Разъем" + m); break; }
                                        if (n.Contains("круиз") || n.Contains("курсов")) { ToSelect("Блок круиз-контроля" + m); break; }
                                        if (n.Contains("насос") && n.Contains("омыв")) { ToSelect("Насос стеклоомывателя" + m); break; }
                                        if (n.Contains("клемма")) { ToSelect("Клемма аккумулятора" + m); break; }
                                        if (n.Contains("дворн")) { ToSelect("Мотор стеклоочистителя" + m); break; }
                                        if (n.Contains("форсун") && n.Contains("омыв")) { ToSelect("Форсунка стеклоомывателя" + m); break; }
                                        ToSelect("Электрика и свет" + m);
                                        break;

                                    case "Салон":
                                        if (n.Contains("педал") && n.Contains("сцеплен")) { ToSelect("Педаль сцепления" + m); break; }
                                        if (n.Contains("педал") && n.Contains("тормоз")) { ToSelect("Педаль тормоза" + m); break; }
                                        if ((n.Contains("педал") && n.Contains("узел")) || n.Contains("педали")) {
                                            ToSelect("Педальный узел" + m); break;
                                        }
                                        if (n.Contains("педал")) { ToSelect("Педаль акселератора" + m); break; }
                                        if (n.Contains("бардач")) { ToSelect("Бардачок" + m); break; }
                                        if (n.Contains("блок") && n.Contains("печк")) { ToSelect("Блок управления отопителем" + m); break; }
                                        if ((n.Contains("обшивк") || n.Contains("наклад")) && n.Contains("багажн")) {
                                            ToSelect("Обшивка багажника" + m); break;
                                        }
                                        if (n.Contains("полка")) { ToSelect("Полка багажника" + m); break; }
                                        if ((n.Contains("обшивк") || n.Contains("накл")) && n.Contains("двер")) {
                                            ToSelect("Обшивка двери" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("кожух") && n.Contains("рулев")) { ToSelect("Кожух рулевой колонки" + m); break; }
                                        if (n.Contains("козыр") && n.Contains("солнц")) { ToSelect("Солнцезащитные козырьки" + m); break; }
                                        if ((n.Contains("решет") || n.Contains("дефлек")) && n.Contains("возд") && bus[b].price >= 800) {
                                            ToSelect("Дефлектор торпедо" + m); break;
                                        }
                                        if (n.Contains("пепел")) { ToSelect("Пепельница" + m); break; }
                                        if (n.Contains("воздух")) { ToSelect("Воздуховод отопителя" + m); break; }
                                        if (n.Contains("карман")) { ToSelect("Карман двери" + m); break; }
                                        if (n.Contains("ков")) { ToSelect("Коврик багажника" + m); break; }
                                        if (n.Contains("стойк")) { ToSelect("Обшивка салона" + m); break; }
                                        if (n.Contains("крыш") && n.Contains("предохр")) { ToSelect("Корпус блока предохранителей" + m); break; }
                                        if (n.Contains("порог")) { ToSelect("Накладка порога" + m); break; }
                                        if (n.Contains("ручк") && n.Contains("двер")) { ToSelect("Ручка двери внутренняя" + m); break; }
                                        if ((n.Contains("кожух") || n.Contains("обшив") || n.Contains("чехол") || n.Contains("крыш")) && n.Contains("рем")) {
                                            ToSelect("Кожух ремня безопасности" + m); break;
                                        }
                                        if ((n.Contains("регуля") || n.Contains("салаз")) && n.Contains("ремн")) {
                                            ToSelect("Направляющая ремня безопасности" + m); break;
                                        }
                                        if (n.Contains("натяж") && n.Contains("ремн")) { ToSelect("Преднатяжитель ремня безопасности" + m); break; }
                                        if ((n.Contains("обшив") || n.Contains("накл")) && (n.Contains("потол") || n.Contains("крыш"))) {
                                            ToSelect("Обшивка потолка" + m); break;
                                        }
                                        if ((n.Contains("ремень") || n.Contains("ремн")) && (n.Contains("безоп") || n.Contains("передн"))) {
                                            ToSelect("Ремень безопасности" + m); break;
                                        }
                                        if (n.Contains("ручк") && n.Contains("потол")) { ToSelect("Ручка потолочная" + m); break; }
                                        if (n.Contains("ручк")) { ToSelect("Ручка двери внутренняя" + m); break; }
                                        if ((n.Contains("накл") || n.Contains("стакан")) && n.Contains("торпедо")) {
                                            ToSelect("Накладка торпедо" + m); break;
                                        }
                                        if (n.Contains("торпедо") && bus[b].price > 700) { ToSelect("Основа торпедо" + m); break; }
                                        if (n.Contains("магнитол")) { ToSelect("Рамка автомагнитолы" + m); break; }
                                        if (n.Contains("ручник ")) { ToSelect("Рычаг стояночного тормоза" + m); break; }
                                        if (n.Contains("сидень") && n.Contains("наклад") ||
                                            (n.Contains("чехол") && n.Contains("салаз"))) { ToSelect("Крышка крепления сиденья" + m); break; }
                                        if (n.Contains("сидень")) { ToSelect("Сиденья" + m); break; }
                                        if (n.Contains("часы")) { ToSelect("часы" + m); break; }
                                        if (n.Contains("кулис")) { ToSelect("Кулиса КПП" + m); break; }
                                        if (n.Contains("подголовн")) {
                                            ToSelect("Подголовник" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            break;
                                        }
                                        if (n.Contains("подлокот")) { ToSelect("Подлокотник" + m); break; }
                                        if (n.Contains("ковр")) { ToSelect("Коврики салонные" + m); break; }
                                        if (n.Contains("стакан")) { ToSelect("Подстаканник" + m); break; }
                                        if (n.Contains("куриват")) { ToSelect("Прикуриватель" + m); break; }
                                        if (n.Contains("консол") || n.Contains("кожух") || ((n.Contains("наклад") || n.Contains("панел")) && n.Contains("ручник"))) {
                                            ToSelect("Консоль" + m); break;
                                        }
                                        ToSelect("Накладки салонные" + m);
                                        break;

                                    case "Световые приборы транспорта":
                                        if (n.Contains("катафот")) {
                                            ToSelect("Катафоты" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("плафон") || n.Contains("светильник")) { ToSelect("Светильник салона" + m); break; }
                                        if (n.Contains("поворотник") || n.Contains("повторитель")) {
                                            ToSelect("Указатель поворота" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("туман")) {
                                            ToSelect("Фара противотуманная" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("фара") && bus[b].price > 600) {
                                            ToSelect("Фары в сборе" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            break;
                                        }
                                        if (n.Contains("фара")) { ToSelect("Фары и комплектующие" + m); break; }
                                        if ((n.Contains("фонарь") || n.Contains("подсвет")) && n.Contains("номер")) {
                                            ToSelect("Фонарь освещения номерного знака" + m); break;
                                        }
                                        if (n.Contains("стоп")) { ToSelect("Стоп-сигнал" + m); break; }
                                        if (n.Contains("фары") || n.Contains("стекло")) {
                                            ToSelect("Стекло фары" + m);
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("плата")) {
                                            ToSelect("Плата фонаря" + m);
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("фонарь")) {
                                            ToSelect("Фонарь задний" + m);
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        if (n.Contains("лампа") && n.Contains("ксенон")) { ToSelect("Лампа ксеноновая" + m); break; }
                                        if (n.Contains("патрон")) { ToSelect("Патрон лампы" + m); break; }
                                        ToSelect("Электрика и свет" + m);
                                        break;

                                    case "Система отопления и кондиционирования":
                                        if (n.Contains("воздуховод")) { ToSelect("Воздуховод отопителя" + m); break; }
                                        if (n.Contains("компрессор")) { ToSelect("Компрессор кондиционера" + m); break; }
                                        if ((n.Contains("корп") || n.Contains("крышк")) && n.Contains("фильтр")) { ToSelect("Корпус воздушного фильтра" + m); break; }
                                        if (n.Contains("корп") && (n.Contains("печк") || n.Contains("отопит"))) { ToSelect("Корпус отопителя" + m); break; }
                                        if (n.Contains("корп") && (n.Contains("мотор") || n.Contains("вентил"))) { ToSelect("Корпус моторчика печки" + m); break; }
                                        if (n.Contains("мотор") && n.Contains("заслон")) { ToSelect("Привод заслонок отопителя" + m); break; }
                                        if ((n.Contains("трубк") || n.Contains("шланг")) && n.Contains("кондиц")) { ToSelect("Трубка кондиционера" + m); break; }
                                        if ((n.Contains("мотор") || n.Contains("вентил")) && (n.Contains("печк") || n.Contains("отопит"))) {
                                            ToSelect("Мотор отопителя" + m); break;
                                        }
                                        if (n.Contains("радиатор") && (n.Contains("отопит") || n.Contains("печк"))) { ToSelect("Радиатор отопителя" + m); break; }
                                        if ((n.Contains("испарит") || n.Contains("осушит")) && n.Contains("кондиц")) { ToSelect("Испаритель кондиционера" + m); break; }
                                        if (n.Contains("радиат") && n.Contains("кондиц")) { ToSelect("Радиатор кондиционера" + m); break; }
                                        if (n.Contains("вентил") && n.Contains("кондиц")) { ToSelect("Мотор охлаждения кондиционера" + m); break; }
                                        if (n.Contains("труб")) { ToSelect("Патрубок системы охлаждения" + m); break; }

                                        ToSelect("Кондиционер и отопитель" + m);
                                        break;

                                    case "Система охлаждения двигателя":
                                        if (n.Contains("бачок") && n.Contains("расшир")) { ToSelect("Расширительный бачок" + m); break; }
                                        if (n.Contains("вентилятор ")) { ToSelect("Вентилятор охлаждения радиатора" + m); break; }
                                        if (n.Contains("вискомуфта")) { ToSelect("Вискомуфта" + m); break; }
                                        if (n.Contains("шкив") && n.Contains("помпы")) { ToSelect("Шкив помпы" + m); break; }
                                        if (n.Contains("помп") || (n.Contains("насос") && (n.Contains("вод") || n.Contains("охлажд")))) {
                                            ToSelect("Помпа водяная" + m); break;
                                        }
                                        if (n.Contains("корпус") && n.Contains("термост")) { ToSelect("Корпус термостата" + m); break; }
                                        if (n.Contains("насос") && n.Contains("масл")) { ToSelect("Насос масляный" + m); break; }
                                        if (n.Contains("труб") && n.Contains("масл")) { ToSelect("Трубка масляная" + m); break; }
                                        if (n.Contains("труб") || n.Contains("шланг")) { ToSelect("Патрубок системы охлаждения" + m); break; }
                                        if ((n.Contains("радиат") || n.Contains("охладит")) && n.Contains("масл")) {
                                            ToSelect("Радиатор масляный" + m); break;
                                        }
                                        if ((n.Contains("радиат") || n.Contains("теплооб")) && (n.Contains("охлажд") ||
                                            n.Contains("двс")) || n.Contains("нтеркулер")) { ToSelect("Радиатор двигателя" + m); break; }
                                        if (n.Contains("крыльчат")) { ToSelect("Крыльчатка вентилятора охлаждения" + m); break; }
                                        if (n.Contains("мотор") && n.Contains("охлаж")) { ToSelect("Мотор вентилятора охлаждения" + m); break; }
                                        if (n.Contains("тройн") && n.Contains("охлаж")) { ToSelect("Система охлаждения" + m); break; }
                                        if (n.Contains("фланец")) { ToSelect("Фланец системы охлаждения" + m); break; }
                                        if (n.Contains("диффуз")) { ToSelect("Диффузор" + m); break; }
                                        if (n.Contains("щуп") && n.Contains("масл")) { ToSelect("Щуп масляный" + m); break; }
                                        ToSelect("Система охлаждения" + m);
                                        break;

                                    case "Тросы автомобильные":
                                        if (n.Contains("газ")) { ToSelect("Тросик акселератора" + m); break; }
                                        if (n.Contains("кпп")) { ToSelect("Трос КПП" + m); break; }
                                        if (n.Contains("капота")) { ToSelect("Тросик замка капота" + m); break; }
                                        if (n.Contains("ручн")) { ToSelect("Трос стояночного тормоза" + m); break; }
                                        if (n.Contains("багаж")) { ToSelect("Трос открывания багажника" + m); break; }
                                        if (n.Contains("сцепл")) { ToSelect("Тросик сцепления" + m); break; }
                                        if (n.Contains("печк")) { ToSelect("Трос отопителя" + m); break; }
                                        if (n.Contains("спидом")) { ToSelect("Тросик спидометра" + m); break; }
                                        if (n.Contains("замка")) {
                                            ToSelect("Тяга замка" + m);
                                            if (n.Contains("задн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Сзади");
                                            }
                                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button")).Click();
                                                await SelectElement("Правая");
                                            }
                                            break;
                                        }
                                        ToSelect("Трос подсоса" + m);
                                        break;

                                    case "Шины, диски, колеса":
                                        if (n.Contains("ступ")) { ToSelect("Крышка ступицы" + m); break; }
                                        if (n.Contains("окатка")) { ToSelect("Колесо запасное" + m); break; }
                                        if (n.Contains("колпак") && bus[b].price > 400) {
                                            ToSelect("Колпаки" + m);
                                            if (n.Contains(" r1")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[1]/div/div/button")).Click();
                                                var size = n.Split(' ')
                                                            .Where(w => w.StartsWith("r"))
                                                            .ToList()[0]
                                                            .TrimStart('r');
                                                await SelectElement(size);
                                            }
                                            break;
                                        }
                                        if (n.Contains("колпак")) { ToSelect("Комплектующие колёс" + m); break; }


                                        if (n.Contains("диск")) {
                                            ToSelect("Колёсные диски" + m);
                                            if (n.Contains(" r1")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[1]/div/div/button")).Click();
                                                var size = n.Split(' ')
                                                            .Where(w => w.StartsWith("r"))
                                                            .ToList()[0]
                                                            .TrimStart('r');
                                                await SelectElement(size);
                                            }
                                            if (bus[b].description.ToLower().Contains("штамп")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Штампованный");
                                            } else if (bus[b].description.ToLower().Contains("литые")
                                                  || bus[b].description.ToLower().Contains("литой")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Литой");
                                            } else if (bus[b].description.ToLower().Contains("кован")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[2]/div/div/button")).Click();
                                                await SelectElement("Кованый");
                                            }
                                            break;
                                        }
                                        if (n.Contains("диск")) {
                                            ToSelect("Колёса в сборе" + m);
                                            if (n.Contains(" r1")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[1]/div/div/button"))
                                                    .Click();
                                                var size = n.Split(' ')
                                                    .Where(w => w.StartsWith("r"))
                                                    .ToList()[0]
                                                    .TrimStart('r');
                                                await SelectElement(size);
                                            }
                                            if (bus[b].description.ToLower().Contains("летн")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button"))
                                                    .Click();
                                                await SelectElement("Летние");
                                            } else if (bus[b].description.ToLower().Contains("всесезон")) {
                                                au.FindElement(By.XPath("//div[2]/fieldset[1]/div[3]/div/div/button"))
                                                    .Click();
                                                await SelectElement("Всесезонные");
                                            }

                                            break;
                                        }
                                        ToSelect("Колёса" + m);
                                        break;

                                    default:
                                        if (n.Contains("домкрат")) { ToSelect("Домкрат" + m); break; }
                                        ToSelect(n + m);
                                        break;
                                }
                            } catch { }

                            //редактируем цену
                            var we = au.FindElement(By.CssSelector("label[class*='PriceFormControl'] input"));
                            WriteToIWebElement(au, we, bus[b].price.ToString());

                            //подставляем в качестве номера id карточки из базы, что позволит легко найти объявление для привязки
                            string num = bus[b].id;

                            we = au.FindElement(By.CssSelector("div[class*='FormField_name_oem'] input"));
                            WriteToIWebElement(au, we, num);
                            //описание
                            we = au.FindElement(By.CssSelector("div[class='TextArea__box'] textarea"));
                            WriteToIWebElement(au, we, sl: GetAutoDesc(b));
                            //прокрутим страницу к кнопке публикации
                            Actions a = new Actions(au);
                            a.MoveToElement(au.FindElement(By.CssSelector("div.FormField.FormField_name_submit > button"))).Build().Perform();
                            //добавим адрес точки продаж
                            we = au.FindElement(By.CssSelector("div[class*='name_stores'] button"));
                            we.Click();
                            await Task.Delay(200);

                            a.SendKeys(OpenQA.Selenium.Keys.Enter).Build().Perform();
                            await Task.Delay(200);
                            //загрузим фотки из базы
                            WebClient cl = new WebClient();
                            cl.Encoding = Encoding.UTF8;
                            //var sb = new StringBuilder();
                            for (int u = 0; u < bus[b].images.Count; u++) {
                                if (u >= 5) break;
                                bool flagSucc = true; // флаг для проверки успешности
                                for (int i = 0; i < 3; i++) {
                                    try {
                                        byte[] bts = await cl.DownloadDataTaskAsync(bus[b].images[u].url);
                                        File.WriteAllBytes("file.jpg", bts);
                                        flagSucc = true;
                                        break;
                                    } catch (Exception ex) {
                                        ToLog("авто.ру: " + ex.ToString() + "\nошибка загрузки фото " + i + "\n" + bus[b].name + "\n" + bus[b].images[u].url);
                                        flagSucc = false;
                                    }
                                }
                                //отправим фото на сайт
                                if (flagSucc) {
                                    try {
                                        var el = au.FindElement(By.CssSelector("input[type=file]"));
                                        el.SendKeys(Application.StartupPath + "\\" + "file.jpg");
                                        while (true) {
                                            Thread.Sleep(2000);
                                            var x = au.FindElements(By.XPath("//li/*[contains(@class,'IconSvg_close')]"));
                                            if (x.Count > u + 1)
                                                x.Last().Click();
                                            else break;
                                            Thread.Sleep(2000);
                                        }
                                    } catch { }
                                }
                            }
                            await Task.Delay(1000);
                            //б/у
                            var d = bus[b].description.ToLower();
                            if (d.Contains("нова") || d.Contains("новы") || d.Contains("ново")) {
                                foreach (IWebElement but in au.FindElements(By.TagName("button"))
                                                             .Where(w => w.GetAttribute("type") == "button")
                                                             .Where(w => w.Text == "Новый товар")) {
                                    but.Click();
                                    break;
                                }
                            }
                            await Task.Delay(10000);
                            //жмем кнопку сохранить и ищем объявление с нашим номером
                            try {
                                do {
                                    var but = au.FindElements(By.XPath("//button[@type='submit']"));
                                    if (but.Count == 0) break;
                                    but.First().Click();
                                    await Task.Delay(10000);
                                } while (true);
                                do {
                                    au.Navigate().GoToUrl("https://auto.ru/parts/lk/?text=" + num);
                                    var webElements = au.FindElements(By.CssSelector("a.UserOffersItem__link"));
                                    if (webElements.Count == 1) {
                                        var id = webElements[0].GetAttribute("href")
                                            .Replace("offer/", "|")
                                            .Split('|')
                                            .Last()
                                            .Split('/')
                                            .First();
                                        if (id.Length > 4) {
                                            bus[b].auto = "https://auto.ru/parts/user-offer/?offerId=" + id;
                                            await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                                            {"id", bus[b].id},
                                            {"name", bus[b].name},
                                            {"313971", bus[b].auto}
                                        });
                                            ToLog("авто добавлено новое объявление " + (newCount + 1) + "\n" + b + " " + bus[b].name);
                                            newCount++;
                                            break;
                                        }
                                    }
                                    await Task.Delay(5000);
                                } while (true);
                            } catch (Exception x) {
                                ToLog("auto.ru ошибка при привязке объявления в базу\n" + bus[b].name + "\n" + x.Message);
                            }
                            label_auto.Text = bus.Count(c => c.auto.Length > 4).ToString();
                        } catch (Exception ex) {
                            ToLog("авто.ру ошибка при добавлении объявления!\n" + ex.Message);
                            break;
                        }
                    }
                }
            } catch (Exception x) {
                ToLog(x.Message + "\n\n" + x.InnerException.Message);
            }
            ChangeStatus(sender, ButtonStates.Active);
        }

        private void numericUpDown_auto_ValueChanged(object sender, EventArgs e) {
            nums_auto = numericUpDown_auto.Value;
        }
        //указываем для какого автомобиля
        public async Task<string> SelectAutoAsync(int b) {
            var desc = bus[b].name.ToLowerInvariant() + " " + bus[b].description.ToLowerInvariant();
            var auto = File.ReadAllLines(Application.StartupPath + "\\auto.txt");
            var dict = new Dictionary<string, int>();
            for (int i = 0; i < auto.Length; i++) {
                dict.Add(auto[i], 0);
                foreach (var word in auto[i].Split(';')) {
                    if (desc.Contains(word))
                        dict[auto[i]]++;
                }
            }//== dict.Values.Max()
            var best = dict.OrderByDescending(o => o.Value).Where(w => w.Value >= 3).Select(s => s.Key).ToList();
            if (best.Count > 0) {
                var s = best[0].Split(';');
                return " для " + s[0] + " " + s[1] + " " + s[2];
            }
            File.AppendAllText(Application.StartupPath + "\\auto.txt", "\n" + desc.Replace("есть и другие", "|").Split('|').First());
            return null;
        }



        //готовим описание для auto.ru
        private List<string> GetAutoDesc(int b) {
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
                s.AddRange(dopDescAuto);
            }
            //контролируем длину описания
            var newSLength = 0;
            for (int u = 0; u < s.Count; u++) {
                if (s[u].Length + newSLength > 980) {
                    s.Remove(s[u]);
                    u = u - 1;
                } else {
                    newSLength = newSLength + s[u].Length;
                }
            }
            return s;
        }



        //переносим с тиу новые товары в базу - создаем карточки, вводим остатки и цены
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
                                ToLog("Исправлен дубль названия в создаваемой карточке \n" + tiuName);
                            }
                        } while (bus_dubles.Length > 0);
                        //добавим в список товаров, на которые нужно сделать поступление и цены
                        string group_id;
                        try {
                            group_id = busGr.Find(f => f.name.Contains(category_Text.Trim())).id;
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
                        ToLog(x.Message);
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
                    ToLog("СОЗДАНА КАРТОЧКА В БАЗЕ\n" + good.name);
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
                    ToLog("СОЗДАНЫ ОСТАТКИ ПО СКЛАДУ №9999");
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
                        ToLog("ПРИВЯЗАНА КАРТОЧКА К ОТСТАКАМ\n" + good.name);
                    }

                    //создаём новый прайс лист
                    var spl = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                        await Class365API.RequestAsync("post", "salepricelists", new Dictionary<string, string>()
                        {
                            {"organization_id", "75519"},//радчено и.г.
                            {"responsible_employee_id", "76221"},//рогачев
                        }));
                    ToLog("СОЗДАН НОВЫЙ ПРАЙС ЛИСТ " + spl.id);
                    Thread.Sleep(1000);
                    //привяжем к нему тип цены
                    var splpt = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                        await Class365API.RequestAsync("post", "salepricelistspricetypes", new Dictionary<string, string>()
                    {
                        {"price_list_id", spl.id},
                        {"price_type_id", "75524"}//розничная
                    }));
                    ToLog("ПРИВЯЗАН ТИП ЦЕН К ПРАЙСУ 75524 (розничная)");
                    Thread.Sleep(1000);

                    //для каждого товара сделаем привязку у прайс листу
                    foreach (var good in newTiuGoods) {
                        var splg = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                            await Class365API.RequestAsync("post", "salepricelistgoods", new Dictionary<string, string>()
                            {
                                {"price_list_id", spl.id},
                                {"good_id", good.id},
                            }));
                        ToLog("ПРИВЯЗАНА КАРТОЧКА К ПРАЙС-ЛИСТУ \n" + good.name);

                        Thread.Sleep(1000);
                        //и назначение цены
                        var splgp = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                            await Class365API.RequestAsync("post", "salepricelistgoodprices", new Dictionary<string, string>()
                            {
                                {"price_list_good_id", splg.id},
                                {"price_type_id", "75524"},
                                {"price", good.price.ToString()}
                            }));
                        ToLog("НАЗНАЧЕНА ЦЕНА К ПРИВЯЗКЕ ПРАЙС ЛИСТА " + good.price);
                        Thread.Sleep(1000);
                    }
                    ToLog("ПОСТУПЛЕНИЕ И ЦЕНЫ УСПЕШНО СОЗДАНЫ В БАЗЕ НА " + newTiuGoods.Count + " ТОВАРОВ!");
                }
            } catch (Exception ex) {
                ToLog("AddSupplyAsync: " + ex.Message);
            }
            newTiuGoods.Clear();
        }


        private async void button_getHelp_Click(object sender, EventArgs e) {
            await Task.Delay(100);
            var lastTime = dSet.Tables["controls"].Rows[0]["liteScanTime"].ToString();
            var s = await Class365API.RequestAsync("get", "currentprices", new Dictionary<string, string>{
                {"updated[from]", lastTime},
                {"price_type_id","75524" } //интересуют только отпускные цены
            });
            Thread.Sleep(100);


            //ToLog(c.ToString());


            //var s = await api.RequestAsync("get", "tasks", new Dictionary<string, string>
            //{
            //    //{"good_id[0]","162695"},
            //    //{ "good_id[1]","162704" }
            //    //{"help", "1"}
            //    //{"amount[from]","0.1" } //worked!!
            //    //{"updated[from]", lastTime}
            //});

            Thread.Sleep(1000);
        }

        //оптимизация синхронизации - чтобы не запрашивать всю базу каждый час - запросим только новые данные
        private async Task GoLiteSync() {
            ChangeStatus(button_base_get, ButtonStates.NoActive);
            var stage = "";
            try {
                if (base_can_rescan) {
                    base_can_rescan = false;

                    sync_start = DateTime.Now;

                    ToLog("lite sync started...");
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

                    ToLog("Новых/измененных карточек: " + lightSyncGoods.Count + " (выложенных на сайте " + lightSyncGoods.Count(c => c.tiu.Contains("http")) + ")");
                    stage = "...";

                    //переносим обновления в загруженную базу
                    foreach (var lg in lightSyncGoods) {
                        var ind = bus.FindIndex(f => f.id == lg.id);
                        if (ind != -1) {
                            bus[ind] = lg;
                            bus[ind].updated = sync_start.ToString();
                        } else {
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
                        await Task.Delay(15000);
                        button_avito_get.PerformClick();
                        await Task.Delay(15000);
                        button_drom_get.PerformClick();
                        await Task.Delay(15000);
                        button_auto_get.PerformClick();
                        await Task.Delay(15000);
                        buttonSatom.PerformClick();
                        await Task.Delay(15000);
                        buttonKupiprodai.PerformClick();
                        await Task.Delay(15000);
                        button_GdeGet.PerformClick();
                        await Task.Delay(15000);
                        button_cdek.PerformClick();
                        await Task.Delay(15000);
                        button_tiu_sync.PerformClick();
                        await Task.Delay(15000);
                        button_vk_sync.PerformClick();
                        await Task.Delay(15000);
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
                    ToLog("lite sync complete.");
                    base_can_rescan = true;
                }
            } catch (Exception ex) {
                ToLog("ошибка lite sync: " + ex.Message + "\n"
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
                button_auto_get.Enabled &&
                button_GdeGet.Enabled &&
                button_cdek.Enabled &&
                button_avto_pro.Enabled;
        }

        private async Task CheckMultipleApostropheAsync() {
            try {
                foreach (var item in bus.Where(w => (w.name.Contains("''''") || w.name.Contains("' `")) && w.amount > 0)) {
                    ToLog("ОБНАРУЖЕНО НАЗВАНИЕ С МНОЖЕСТВОМ АПОСТРОФОВ\n" + item.name);
                    var s = item.name;
                    while (s.EndsWith("'") || s.EndsWith("`"))
                        s = s.TrimEnd('\'').TrimEnd('`').TrimEnd(' ');
                    item.name = s;
                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                                {"id", item.id},
                                {"name", item.name}
                    });
                    ToLog("ИСПРАВЛЕНО ИМЯ С МНОЖЕСТВОМ АПОСТРОФОВ \n" + item.name);
                    Thread.Sleep(1000);
                }
            } catch (Exception x) {
                ToLog("Ошибка при переименовании множественных апострофов\n" + x.Message);
            }
        }

        private async Task AddPartNumsAsync() {
            try {
                var i = 0;
                foreach (var item in bus.Where(w => (!string.IsNullOrEmpty(w.description) && w.description.Contains("№") && string.IsNullOrEmpty(w.part)))) {
                    ToLog("ОБНАРУЖЕН ПУСТОЙ АРТИКУЛ\n" + item.name);
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
                        ToLog("ДОБАВЛЕН АРТИКУЛ\n" + num);
                        Thread.Sleep(1000);
                    }
                    i++;
                    //if (i > 10) break;
                }
            } catch (Exception x) {
                ToLog("ОШИБКА ПРИ ДОБАВЛЕНИИ АРТИКУЛА\n" + x.Message);
            }

        }

        private void dateTimePicker1_ValueChanged(object sender, EventArgs e) {
            try {
                scanTime = dateTimePicker1.Value;
                dSet.Tables["controls"].Rows[0]["lastScanTime"] = scanTime;
                dSet.Tables["controls"].Rows[0]["liteScanTime"] = scanTime;
                RootObject.ScanTime = scanTime;
                dSet.WriteXml(fSet);
            } catch (Exception x){
                ToLog("Ошибка изменения даты синхронизации\n"+x.Message+"\n"+x.InnerException.Message);
            }
        }

        async void button_autoCheck_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            var lastScan = Convert.ToInt32(dSet.Tables["controls"].Rows[0]["controlAuto"]);
            lastScan = lastScan + 1 >= bus.Count ? 0 : lastScan;//опрокидываем счетчик

            for (int b = lastScan; b < bus.Count; b++) {
                if (base_rescan_need) { break; }
                if (bus[b].auto.Contains("http")
                    && bus[b].amount > 0) {
                    //колчество элементов на странице
                    System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> but = null;

                    var ch = Task.Factory.StartNew(() => {
                        au.Navigate().GoToUrl(bus[b].auto);
                        Thread.Sleep(1000);
                        but = au.FindElements(By.CssSelector("div.FormField.FormField_name_submit > button"));
                    });
                    try {
                        await ch;
                        //если нет такого
                        if (but.Count == 0) {
                            bus[b].auto = " ";

                            await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>
                            {
                                {"id", bus[b].id},
                                {"name", bus[b].name},
                                {"313971", bus[b].auto},
                            });
                            ToLog("AUTO.RU УДАЛЕНА БИТАЯ ССЫЛКА ИЗ БАЗЫ!!!!!!!!\n" + bus[b].name);
                        } else {
                            label_auto.Text = b.ToString() + " (" + b * 100 / bus.Count + "%)";
                            dSet.Tables["controls"].Rows[0]["controlAuto"] = b + 1;
                            try {
                                dSet.WriteXml(fSet);
                            } catch (Exception x) {
                                ToLog("ошибка записи файла настроек!\n" + x.Message);
                            }

                            //редактируем цену
                            var we = au.FindElement(By.CssSelector("label[class*='PriceFormControl'] input"));
                            WriteToIWebElement(au, we, bus[b].price.ToString());

                            //описание
                            //we = au.FindElement(By.CssSelector("div[class='TextArea__box'] textarea"));
                            //WriteToIWebElement(au, we, sl: GetAutoDesc(b));

                            //добавим адрес точки продаж
                            Actions a = new Actions(au);
                            a.MoveToElement(but[0]).Build().Perform();
                            await Task.Delay(200);

                            var el = au.FindElement(By.CssSelector("div[class*='name_stores'] button"));
                            el.Click();
                            await Task.Delay(2000);
                            if (!el.Text.Contains("Московская")) {
                                a.SendKeys(OpenQA.Selenium.Keys.Enter).Build().Perform();
                                await Task.Delay(500);
                                //если адреса нет - выходим, чтобы дать юзеру дозаполнить и сохранить
                                a.MoveToElement(au.FindElement(By.CssSelector("div[class*='Form__section_name_category']"))).Perform();
                                break;
                            }
                            //сохраняем
                            but[0].Submit();
                            await Task.Delay(10000);
                            try {
                                au.SwitchTo().Alert().Dismiss();
                            } catch { }
                            //but[0].Click();
                        }
                    } catch (Exception ex) {
                        ToLog("button_autoCheck_Click: " + ex.Message);
                        break;
                    }
                }

            }
            ChangeStatus(sender, ButtonStates.Active);
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
                        ToLog("база - удалены лишние фото из карточки!\n" + bus[b].name);
                        await Task.Delay(1000);
                        break;
                    } catch (Exception x) {
                        ToLog("ошибка при удалении фото из базы!\n" + bus[b].name + "\n" + x.Message);
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
                    ToLog("исправлена ссылка тиу " + b + ":\n" + bus[b].name + "\n" + bus[b].tiu + "\n");
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
                await KupiProdaiAutorize();
                while (base_rescan_need) {
                    ToLog("купипродай ожидает загрузку базы... ");
                    await Task.Delay(60000);
                }
                labelKP.Text = bus.Count(c => c.kp != null && c.kp.Contains("http")).ToString();
                //проверяем изменеия
                await KupiProdaiEditAsync();
                //выкладываем объявления
                await KupiProdaiAddAsync();
                //продливаем объявления
                await KupiProdaiUpAsync();
                //удаляем ненужные
                await KupiProdaiDelAsync();
            } catch (Exception x) {
                ToLog("Купипродай Ошибка при обработке\n" + x.Message);
            }
            ChangeStatus(sender, ButtonStates.Active);
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
                ToLog("купипродай: ошибка при поднятии!\n" + x.Message);
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
                ToLog("купипродай: ошибка при поднятии!\n" + x.Message);
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

                                ToLog("купипродай выложено и привязано объявление " + b + "\n" + bus[b].name);
                                numericUpDownKupiprodaiAdd.Value--;

                                //labelKP.Text = bus.Count(c => c.kp.Contains("http")).ToString();
                                labelKP.Text = bus.Count(c => c.kp != null && c.kp.Contains("http")).ToString();
                            }
                            Thread.Sleep(200);
                        } else {
                            throw new Exception("не найдено объявление для привязки");
                        }
                    } catch (Exception e) {
                        ToLog("купипродай ошибка добавления!\n" + bus[b].name + "\n" + e.Message);
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
            var group = busGr.Find(f => f.id == bus[b].group_id).name;
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

        private async Task KupiProdaiAutorize() {
            if (kp == null) {
                kp = new ChromeDriver();
                kp.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            }
            Task t = Task.Factory.StartNew(() => {
                kp.Navigate().GoToUrl("https://kupiprodai.ru/login");
                if (!kp.FindElement(By.CssSelector("#user_link")).Text.Contains("9106027626@mail.ru")) {
                    var email = kp.FindElement(By.CssSelector("input[name='login']"));
                    WriteToIWebElement(kp, email, "9106027626@mail.ru");
                    Thread.Sleep(2000);

                    var password = kp.FindElement(By.CssSelector("input[name='pass']"));
                    WriteToIWebElement(kp, password, "rad00239000");
                    Thread.Sleep(2000);

                    var but = kp.FindElement(By.CssSelector("ul input[type='submit']"));
                    but.Click();
                    Thread.Sleep(2000);
                }
            });
            try {
                await t;
            } catch (Exception x) {
                //ToLog(x.Message);
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
                            ToLog("купипродай ошибка удаления!\n" + bus[b].name + "\n" + x.Message);
                        }
                        //убиваем ссылку из базы в любом случае, потому что удалим физически, проверив отстатки после парсинга и подъема неактивных
                        bus[b].kp = " ";
                        await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                            {"id", bus[b].id},
                            {"name", bus[b].name},
                            {"833179", bus[b].kp}
                        });
                        ToLog("купипродай - удалена ссылка из карточки\n" + bus[b].name);
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
                            ToLog("KupiProdaiEditAsync: " + bus[b].name + "\n" + x.Message);
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
                ToLog("купипродай: ошибка добавления названия объявления\n" + bus[b].name + "\n" + x.Message);
            }
        }

        private void SetKPPrice(int b) {
            try {
                var pr = kp.FindElement(By.CssSelector("input[name*='price']"));
                WriteToIWebElement(kp, pr, bus[b].price.ToString());
            } catch (Exception x) {
                ToLog("купипродай: ошибка добавления цены объявления\n" + bus[b].name + "\n" + x.Message);
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
                            ToLog("SetKPImages: " + ex.ToString() + "\nошибка загрузки фото " + bus[b].images[u].url);
                            flag = false;
                        }
                        Thread.Sleep(100);
                    } while (!flag && tryNum < 3);

                    if (flag) kp.FindElement(By.CssSelector("input[type='file']"))
                                .SendKeys(Application.StartupPath + "\\" + "kp_" + u + ".jpg ");
                }
            } catch (Exception e) {
                ToLog("SetKPImages: " + e.Message);
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
                ToLog("KPPressOkButton: " + x.Message);
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
                            ToLog("!!!!!!!!!!!!!!!!!!!!!! KP БИТАЯ ССЫЛКА В БАЗЕ!!!!!!!!!!!!!!!!\n" + "в базе:" + bus[b].name + "\nна kp:" + nameParse + "\n!!!!!!!!!!!!!!!!!!!!!!!!!!");
                        } else {
                            labelKP.Text = b.ToString() + " (" + b * 100 / bus.Count + "%)";
                            ToLog("kp... " + b);
                            dSet.Tables["controls"].Rows[0]["controlKP"] = b + 1;
                            try {
                                dSet.WriteXml(fSet);
                            } catch (Exception x) {
                                ToLog("ошибка записи файла настроек!\n" + x.Message);
                            }
                        }
                    } catch (Exception ex) {
                        ToLog("button_autoCheck_Click: " + ex.Message);
                        break;
                    }
                }
            }
            ChangeStatus(sender, ButtonStates.Active);
        }

        //========//
        // gde.ru //
        //========//
        public async void button_GdeGet_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            try {
                await GdeAutorize();
                while (base_rescan_need) {
                    ToLog("Gde.ru ожидает загрузку базы... ");
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
                ToLog("Gde.ru Ошибка при обработке\n" + x.Message);
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
                ToLog("gde.ru ошибка удаления!\n" + x.Message);
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
                gde = new ChromeDriver();
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
                //ToLog(x.Message);
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
                            ToLog("gde.ru ошибка удаления!\n" + bus[b].name + "\n" + x.Message);
                        }
                        //убиваем ссылку из базы в любом случае, потому что удалим физически, проверив отстатки после парсинга и подъема неактивных
                        bus[b].gde = " ";
                        await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                            {"id", bus[b].id},
                            {"name", bus[b].name},
                            {"854872", bus[b].gde}
                        });
                        ToLog("gde.ru - удалена ссылка из карточки\n" + bus[b].name);
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
                            ToLog("GdeEditAsync: " + bus[b].name + "\n" + x.Message);
                            if (x.Message.Contains("Unexpected error")) {
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
                ToLog("gde.ru: ошибка добавления адреса в объявление\n" + x.Message);
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
                ToLog("gde.ru: ошибка добавления названия объявления\n" + bus[b].name + "\n" + x.Message);
            }
        }


        private void SetGdePrice(int b) {
            try {
                var priceInput = gde.FindElement(By.CssSelector("#AInfoForm_price"));
                WriteToIWebElement(gde, priceInput, bus[b].price.ToString());
            } catch (Exception x) {
                ToLog("купипродай: ошибка добавления цены объявления\n" + bus[b].name + "\n" + x.Message);
            }
        }

        private void SetGdePhone() {
            try {
                var phInput = gde.FindElement(By.CssSelector("#AInfoForm_phone"));
                WriteToIWebElement(gde, phInput, "9621787915");
            } catch (Exception x) {
                ToLog("купипродай: ошибка добавления номера телефона\n" + x.Message);
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
                ToLog("GdePressOkButton: " + x.Message);
            }
        }

        private void GdeCheckFreeOfCharge() {
            try {
                gde.FindElement(By.CssSelector("label[for='radio-0']")).Click();
            } catch (Exception x) {
                ToLog("GdeCheckFreeOfCharge: " + x.Message);
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
                            ToLog("Gde.ru выложено и привязано объявление " + b + "\n" + bus[b].name);
                            labelGde.Text = bus.Count(c => c.gde != null && c.gde.Contains("http")).ToString();
                        }
                    } catch (Exception e) {
                        ToLog("GDE.RU ОШИБКА ДОБАВЛЕНИЯ!\n" + bus[b].name + "\n");
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
                            ToLog("SetGdeImages: " + ex.ToString() + "\nошибка загрузки фото " + bus[b].images[u].url);
                            flag = false;
                        }
                        Thread.Sleep(1000);
                    } while (!flag && tryNum < 3);

                    if (flag) gde.FindElement(By.CssSelector("input[name=file]"))
                                .SendKeys(Application.StartupPath + "\\" + "gde_" + u + ".jpg ");
                }
            } catch (Exception e) {
                ToLog("SetGdeImages: " + e.Message);
            }
            Thread.Sleep(5000);
        }

        //
        //
        // CDEK
        //
        private async void button_cdek_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            while (base_rescan_need || !button_base_get.Enabled) {
                ToLog("CDEK ожидает загрузку базы... ");
                await Task.Delay(60000);
            }
            if (checkBoxCdekSyncActive.Checked) {
                try {
                    await _cdek.SyncCdekAsync(bus, (int)numericUpDown_CdekAddNewCount.Value);
                } catch (Exception x) {
                    ToLog("CDEK ошибка синхронизации:\n" + x.Message);
                }
                label_cdek.Text = bus.Count(c => c.cdek != null && c.cdek.Contains("http")).ToString();
                ToLog("CDEK выгрузка ок!");
            }
            if (numericUpDown_СdekCheckUrls.Value > 0) {
                try {
                    var num = int.Parse(dSet.Tables["controls"].Rows[0]["controlCdek"].ToString());
                    num = await _cdek.CheckUrlsAsync(num, (int)numericUpDown_СdekCheckUrls.Value);
                    dSet.Tables["controls"].Rows[0]["controlCdek"] = num;
                    dSet.WriteXml(fSet);
                    await _cdek.ParseSiteAsync();
                } catch (Exception x) {
                    ToLog("CDEK ошибка при проверке объявлений!\n"+x.Message);
                }
            }
            ChangeStatus(sender, ButtonStates.Active);
        }

        private void button_SaveCookie_Click(object sender, EventArgs e) {
            SaveCookies(tiu, "tiu.json");
            SaveCookies(au, "auto.json");
            _avtoPro.SaveCookies();
            _drom.SaveCookies();
            _avito.SaveCookies();
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
                ToLog("ошибка сохранения куки " + name + "\n" + x.Message);
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
                ToLog("Ошибка загузки куки " + name + "\n" + x.Message);
            }

        }

        // запись в элемент
        public void WriteToIWebElement(IWebDriver d, IWebElement we, string s = null, List<string> sl = null) {
            Actions a = new Actions(d);
            if (sl != null) {
                a.MoveToElement(we)
                 //.ContextClick()
                 .Click()
                 .Perform();
                System.Threading.Thread.Sleep(2000);
                a.KeyDown(OpenQA.Selenium.Keys.Control)
                 .SendKeys("a")
                 .KeyUp(OpenQA.Selenium.Keys.Control)
                 .Perform();
                System.Threading.Thread.Sleep(200);
                a.SendKeys(OpenQA.Selenium.Keys.Backspace)
                 .Perform();
                System.Threading.Thread.Sleep(200);
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


        //запись лога
        public void ToLog(string s) {
            lock (thisLock) {
                for (int i = 0; i < 3; i++) {
                    try {
                        var dt = DateTime.Now;
                        s = dt + " " + s + "\n";
                        logBox.Text += s;
                        if (logBox.Lines.Count() > numericUpDown_LOG.Value) {
                            logBox.Lines = logBox.Lines.Skip(10).ToArray();
                        }
                        if (checkBox_WriteLog.Checked) {
                            var date = dt.Year + "." + dt.Month + "." + dt.Day;
                            File.AppendAllText("log_" + date + ".txt", s);
                            break;
                        }
                        break;
                    } catch { }
                }
            }

        }
        //прокрутка лога
        private void richTextBox1_TextChanged(object sender, EventArgs e) {
            logBox.SelectionStart = logBox.Text.Length - 1;
            logBox.ScrollToCaret();
        }

        private async void Form1_Load(object sender, EventArgs e) {
            this.Text += _version;
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
                dSet.ReadXml(fSet);
            } catch (Exception x) {
                ToLog(x.Message);
            }
        }

        //=========================//
        //тестим селениум
        private async void SeleniumTest(object sender, EventArgs e) {
            try {
                for (int b = 0; b < bus.Count; b++) {
                    if (bus[b].name.Contains("\u00a0")) ToLog(bus[b].name);
                }
                await Task.Delay(1000);
                //string res =  await  Class365API.RequestAsync("get", "goods", new Dictionary<string, string>{
                //            {"archive", "0"},
                //            {"type", "1"},
                //            {"with_additional_fields", "1"},
                //            {"with_remains", "1"},
                //            {"with_prices", "1"},
                //            {"type_price_ids[0]","75524" }
                //        });
                //if (res.Contains("name")) {
                //    res = res.Replace("\"209325\":", "\"tiu\":")
                //        .Replace("\"209326\":", "\"avito\":")
                //        .Replace("\"209334\":", "\"drom\":")
                //        .Replace("\"209360\":", "\"vk\":")
                //        .Replace("\"313971\":", "\"auto\":")
                //        .Replace("\"402489\":", "\"youla\":")
                //        .Replace("\"657256\":", "\"ks\":")
                //        .Replace("\"833179\":", "\"kp\":")
                //        .Replace("\"854872\":", "\"gde\":")
                //        .Replace("\"1437133\":", "\"avtopro\":")
                //        .Replace("\"854874\":", "\"cdek\":");
                //    bus.AddRange(JsonConvert.DeserializeObject<List<RootObject>>(res));
                //}

                //var s = JsonConvert.SerializeObject(bus);
                //File.WriteAllText("bus.json", s);
                //File.WriteAllText("bus-utf8.json", s, Encoding.UTF8);

                //var busNew = JsonConvert.DeserializeObject<List<RootObject>>(File.ReadAllText("bus.json"));
                //Thread.Sleep(1000);

                //await _cdek.CheckUrlsAsync();

                //var tst = new Selenium();
                //tst.Navigate("https://cdek.market/v/");
                //if (tst.GetElementsCount("#mainrightnavbar") == 0) {
                //    tst.WriteToSelector("#username", "rad.i.g@list.ru");
                //    tst.WriteToSelector("#password", "rad701242");
                //    tst.ButtonClick("input[type='submit']");
                //}          
                //tst.Navigate("https://cdek.market/v/?dispatch=products.add");
                //var inp_but = tst.FindElements("//span/ul/li/input").First();
                //inp_but.Click();
                //while (tst.GetElementsCount("//li[contains(@class,'results__option--load-more')]")>0) {
                //    inp_but.SendKeys(OpenQA.Selenium.Keys.ArrowDown);
                //}

                //var str = new StringBuilder();
                //foreach(var el in tst.FindElements("//li[@class='select2-results__option']")) {
                //    str.Append(el.FindElements(By.XPath(".//div[@class='select2__category-name']")).First().Text);
                //    str.Append(" / ");
                //    str.Append(el.FindElements(By.XPath(".//div[@class='select2__category-parents']")).First().Text);
                //    str.Append("\n");
                //}
                //File.WriteAllText("cdek_category.txt", str.ToString());



                //tst.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
                //tst.Navigate().GoToUrl("https://my.tiu.ru");
                //LoadCookies(tst, "tiu.json");

                //var s = await Class365API.RequestAsync("get", "salepricelistgoods", new Dictionary<string, string>{
                //        {"updated[from]", DateTime.Now.AddHours(-24).ToString()},

                //        {"price_type_id","75524" } //интересуют только отпускные цены
                //    });

            } catch (Exception x) {
                Debug.WriteLine(x.Message);
            }

        }

        //=== выгрузка avto.pro ===
        private async void button_avto_pro_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            if (checkBox_avto_pro_use.Checked) {
                try {
                    ToLog("avto.pro: начало выгрузки...");
                    while (base_rescan_need) await Task.Delay(30000);
                    await _avtoPro.AvtoProStartAsync(
                        bus,
                        (int) numericUpDown_avto_pro_add.Value,
                        (int) numericUpDown_avto_pro_check.Value);
                    ToLog("avto.pro: выгрузка завершена успешно!");
                } catch (Exception x) {
                    ToLog("AVTO.PRO: ОШИБКА ВЫГРУЗКИ! \n" + x.Message);
                    ChangeStatus(sender, ButtonStates.ActiveWithProblem);
                }
            }
            ChangeStatus(sender, ButtonStates.Active);
        }

    }
}