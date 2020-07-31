using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using Selen.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Selen.Sites {
    class Avito {
        Selenium _dr;
        readonly string _url = "209326";
        List<RootObject> _bus = null;
        int _priceLevel = 2000;
        Random rnd = new Random();
        public int CountToUp { get; set; }
        public int AddCount { get; set; }

        readonly string[] _addDesc = new[]
        {
            "Дополнительные фотографии по запросу",
            "Есть и другие запчасти на данный автомобиль!",
            "Вы можете установить данную деталь в нашем АвтоСервисе с доп.гарантией и со скидкой!",
            "Перед выездом обязательно уточняйте наличие запчастей по телефону!",
            "Звоните нам за 30 минут перед выездом (можно бесплатно через Viber) - товар на складе!",
            "Время на проверку и установку - 2 недели, не понравится - вернём деньги!"
        };

        readonly string[] _addDesc2 = new[]
        {
            "Звонить строго с 9-00 до 19 - 00 (воскресенье - выходной)",
            "В нашем торговом зале вы можете оплатить товар наличными или картой. Принимаем к оплате карты VISA, MASTERCARD, MAESTRO. Оплата производится моментально. При покупке Вам будет выдан на руки товарный, кассовый чеки.",
            "От организаций принимаем оплату по безналичному расчету!",
            "Отправляем в регионы транспортными компаниями: в приоритете ПЭК!",
            "НАШ АДРЕС: г. Калуга, ул. Московская, д. 331",
            "Телефон, подробная информация о доставке и оплате - при нажатии на АвтоТехноШик!",
            "Отправляем наложенным платежом ТК СДЭК (только негабаритные посылки)",
            "Бесплатная доставка до транспортной компании!",
            "Осуществляем бесплатную доставку своим транспортом до следующих городов: Малоярославец, Обнинск, Тула (посёлок Иншинский)"
        };

        public void LoadCookies() {
            _dr.Navigate("https://avito.ru/404");
            _dr.LoadCookies("avito.json");
            Thread.Sleep(1000);
        }

        public void SaveCookies() {
            _dr.Navigate("https://avito.ru/profile");
            _dr.SaveCookies("avito.json");
        }

        public void Quit() {
            _dr?.Quit();
            _dr = null;
        }

        public async Task AvitoStartAsync(List<RootObject> bus) {
            _bus = bus;
            await AuthAsync();
            await EditAllAsync();
            await AddAsync();
            await AvitoUpAsync();
        }

        private async Task AuthAsync() {
            await Task.Factory.StartNew(() => {
                if (_dr == null) {
                    _dr = new Selenium();
                    LoadCookies();
                }
                _dr.Navigate("https://avito.ru/profile");
                if (_dr.GetElementsCount("//a[text()='Мои объявления']") == 0) {
                    _dr.WriteToSelector("input[name='login']", "9106027626@mail.ru");
                    _dr.WriteToSelector("input[name='password']", "rad00239000");
                    _dr.ButtonClick("//button[@type='submit']");
                }
                while (_dr.GetElementsCount(".nav-tabs") == 0) {
                    Thread.Sleep(10000);
                    _dr.ButtonClick("//a[contains(@href,'reload')]");
                    _dr.ButtonClick("//div[contains(@class,'username')]/div/a");
                    
                }
                SaveCookies();
            });
        }

        private async Task EditAllAsync() {
            //новые изменния
            for (int b = 0; b < _bus.Count; b++) {
                if (_bus[b].IsTimeUpDated() &&
                    _bus[b].avito != null &&
                    _bus[b].avito.Contains("http")) {
                    await ChechAuthAsync();
                    if (_bus[b].amount <= 0) {
                        await DeleteAsync(b);
                    } else
                        await EditAsync(b);
                }
            }
        }

        private async Task ChechAuthAsync() {
            while (_dr.GetElementsCount("//a[text()='Мои объявления']") == 0) {
                if (_dr.GetElementsCount("//h1[text()='Сайт временно недоступен']") > 0)
                    _dr.Refresh();
                else {
                    Quit();
                    Thread.Sleep(30000);
                    await AuthAsync();
                }
            }
        }

        private async Task EditAsync(int b) {
            if (_bus[b].price > 0) {  //защита от нулевой цены в базе
                var url = "https://www.avito.ru/items/edit/" + _bus[b].avito.Replace("/", "_").Split('_').Last();
                bool isAlive = true;
                for(int i=0; ; i++) {
                    if (i == 30) throw new Exception("не удалось загрузить страницу редактирования!\n" + _bus[b].name + "url: " + url);
                    await ChechAuthAsync();
                    await _dr.NavigateAsync(url);
                    if (_dr.GetElementsCount("//a[@href='/profile']") > 0 && _dr.GetElementsCount("//div[@data-marker='category']")>0) break;
                    isAlive = await CheckIsOfferAlive(b);
                    if (!isAlive) break;
                }
                if (isAlive) {
                    await Task.Factory.StartNew(() => {
                        SetStatus(b);
                        SetTitle(b);
                        SetPrice(b);
                        SetDesc(b);
                        PressOk();
                    });
                } else {
                    await SaveUrlAsync(b, deleteUrl: true);
                }
            }
        }

        private async Task<bool> CheckIsOfferAlive(int b) {
            var count = 0;
            await Task.Factory.StartNew(() => {
                count = _dr.GetElementsCount("//p[contains(text(),'удалили это объявление') or contains(text(),'неверной ссылке')]");
            });
            if (count > 0 && !_dr.GetUrl().Contains("isDirect=1")) return false;
            return true;
        }
       
        private async Task DeleteAsync(int b) {
            await Task.Factory.StartNew(() => {
                Delete(b);
            });
        }

        private void Delete(int b) {
            _dr.Navigate(_bus[b].avito);
            if (_dr.GetElementsCount("//*[text()='Снять с публикации']") > 0) {
                _dr.ButtonClick("//*[text()='Снять с публикации']/..");
                _dr.ButtonClick("//*[contains(text(),'Другая причина')]/..");
            }
        }

        public async Task AddAsync() {
            for (int b = 0; b < _bus.Count && AddCount > 0; b++) {
                if ((_bus[b].avito == null || !_bus[b].avito.Contains("http")) &&
                    _bus[b].tiu.Contains("http") &&
                    _bus[b].amount > 0 &&
                    _bus[b].price >= _priceLevel &&
                    _bus[b].images.Count > 0) {
                    await ChechAuthAsync();
                    var t = Task.Factory.StartNew(() => {
                        _dr.Navigate("https://avito.ru/additem");
                        SetCategory(b);
                        SetTitle(b);
                        SetOffetType();
                        SetStatus(b);
                        SetDiskParams(b);
                        SetImages(b);
                        SetPrice(b);
                        SetDesc(b);
                        SetAddress();
                        SetPhone();
                        PressOk();
                    });
                    try {
                        AddCount--;
                        await t;
                        await SaveUrlAsync(b);
                    } catch (Exception x) {
                        Debug.WriteLine("AVITO.RU: ОШИБКА ДОБАВЛЕНИЯ!\n" + _bus[b].name + "\n" + x.Message);
                        break;
                    }
                }
            }
        }

        private async Task SaveUrlAsync(int b, bool deleteUrl = false) {
            if (deleteUrl) {
                _bus[b].avito = "";
            } else {
                await Task.Delay(5000); //ждем, потому что объявление не всегда сразу готово
                var id = _dr.GetElementAttribute("//a[contains(@href,'itemId')]", "href").Split('=').Last();
                var url = "https://www.avito.ru/items/" + id;
                await _dr.NavigateAsync(url);
                _bus[b].avito = _dr.GetUrl();
            }
            await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                {"id", _bus[b].id},
                {"name", _bus[b].name},
                {_url, _bus[b].avito}
            });
            await Task.Delay(10000);
        }

        private void SetDiskParams(int b) {
            if (_bus[b].GroupName()== "Шины, диски, колеса") {
                var bn = _bus[b].name.ToLowerInvariant();
                if (bn.Contains("диск")) { //заполняю параметры дисков
                    //тип диска
                    var selector = "//option[contains(text(),'Литые')]/..";
                    if (bn.Contains("штамп")) _dr.WriteToSelector(selector, "шта" + OpenQA.Selenium.Keys.Enter);
                    else if (bn.Contains("кован")) _dr.WriteToSelector(selector, "ков" + OpenQA.Selenium.Keys.Enter);
                    else _dr.WriteToSelector(selector, "лит" + OpenQA.Selenium.Keys.Enter);
                    //диметр
                    selector = "//option[text()='16.5']/..";
                    _dr.WriteToSelector(selector,_bus[b].GetDiskSize() + OpenQA.Selenium.Keys.Enter);
                    //ширина обода
                    selector = "//option[text()='3.5']/..";
                    _dr.WriteToSelector(selector,"5" + OpenQA.Selenium.Keys.Enter); //ставим 5 по дефолту, т.к. нет инфы
                    //количество отверстий
                    selector = "//option[text()='3']/..";
                    if (_bus[b].description.Contains("4 x") || _bus[b].description.Contains("4x")) _dr.WriteToSelector(selector, "4" + OpenQA.Selenium.Keys.Enter);
                    else _dr.WriteToSelector(selector, "5" + OpenQA.Selenium.Keys.Enter);
                    //диаметр отверстий
                    selector = "//option[text()='100']/..";
                    var desc = _bus[b].description.ToLower();
                    var pattern = @"\d\s*(?:\*|x|х)\s*([0-9]+)(?:<|\ |m|м|.|,)";
                    var diam = Regex.Match(desc, pattern).Groups[1].Value;
                    if (diam.Length == 0) diam = "100"; //если не удалось определить с помощью регулярки из описания - ставим 100 по дефолту
                    _dr.WriteToSelector(selector, diam + OpenQA.Selenium.Keys.Enter);
                    //вылет
                    selector = "//option[text()='-98']/..";
                    _dr.WriteToSelector(selector, "0" + OpenQA.Selenium.Keys.Enter);  //редко указан в объявлении, ставим 0 по дефолту
                }
                //РЕЗИНА
                //if (bn.Contains("резина")) {
                //try {
                //    av.FindElement(By.CssSelector("div.form-fieldset.clearfix.clearfix_left.js-fieldset.js-fieldset_733 > div.col-3.form-select-v2.js-form-select-v2")).Click();
                //} catch { }
                //Thread.Sleep(1000);
                //string size = GetDiskSize(bn);
                //size = size ?? "13";
                //foreach (var el in av.FindElements(By.CssSelector("#flt_param_733 > option")))//#flt_param_733 > option
                //{
                //    if (el.Text == size) {
                //        selector = "";
                //        el.Click();
                //        break;
                //    }
                //}
                ////сезонность
                //if (bn.Contains("летн")) {
                //    av.FindElements(By.TagName("option")).Where(w => w.Text.Contains("Летние")).ToList()[0].Click();
                //} else if (bn.Contains("всесезон")) {
                //    av.FindElements(By.TagName("option")).Where(w => w.Text.Contains("Всесезонные")).ToList()[0].Click();
                //} else {
                //    av.FindElements(By.TagName("option")).Where(w => w.Text.Contains("Зимние шипованные")).ToList()[0].Click();
                //}
                //if (bn.Contains("колесо") || bn.Contains("докатка")) {
                //}
                //if (bn.Contains("колпа")) {
                //av.FindElement(By.CssSelector("div.js-parameters__container > div > div.col-3.form-select-v2.js-form-select-v2")).Click();
                //Thread.Sleep(1000);
                //string size = GetDiskSize(bn);
                //size = size ?? "4";
                //foreach (var el in av.FindElements(By.CssSelector("#flt_param_740 > option"))) {
                //    if (el.Text == size) {
                //        selector = "";
                //        el.Click();
                //        break;
                //    }
                //}
                //break;
            }
        }

        private void SetAddress() {
            var s = "Калуга, Московская улица, 331";
            _dr.WriteToSelector("input[id*='params[2851]']",s);
            Actions a = new Actions(_dr._drv);
            a.SendKeys(OpenQA.Selenium.Keys.ArrowDown)
             .SendKeys(OpenQA.Selenium.Keys.Enter)
             .Build().Perform();
        }

        private async Task AvitoUpAsync() {
            var url = "https://www.avito.ru/profile";
            while (_dr.GetElementsCount(".nav-tab-title") == 0) {
                await ChechAuthAsync();
                await _dr.NavigateAsync(url);
            }
            var el = await _dr.FindElementsAsync("//li/span[@class='nav-tab-num']");
            var txt = el.Select(s => s.Text.Replace("\u00A0", "").Replace(" ", "")).ToList();
            var inactive = int.Parse(txt[0]);
            var active = int.Parse(txt[1]);
            var old = int.Parse(txt[2]);
            //проверяю случайную страницу активных объявлений
            await ParsePage("/active", GetRandomPageNum(active));
            await ParsePage("/old", GetRandomPageNum(old));
            //проход страниц неактивных и архивных объявлений будет последовательным, пока не кончатся страницы или количество для подъема
            for (int i = 1; i <= inactive / 10 && CountToUp > 0; i++) { await ParsePage("/inactive", i); }
            for (int i = 1; i <= old / 10 && CountToUp > 0; i++) { await ParsePage("/old", i); }
        }

        private int GetRandomPageNum(int count) {
            return 1 + (rnd.Next(1, 1000) / rnd.Next(1, (int)Math.Pow(1000, 0.5)) / 10);
        }

        private async Task ParsePage(string location, int numPage) {
            //перехожу в раздел
            var url = "https://avito.ru/profile/items" + location + "/rossiya?p=" + numPage;
            await _dr.NavigateAsync(url);
            //проверяю, что страница загрузилась
            while (_dr.GetElementsCount(".nav-tab-title") == 0) {
                await ChechAuthAsync();
                await _dr.NavigateAsync(url);
            }
            //парсинг объявлений на странице
            var items = await _dr.FindElementsAsync("//div[contains(@class,'text-t')]//a");
            var urls = items.Select(s => s.GetAttribute("href")).ToList();
            var ids = urls.Select(s => s.Split('_').Last()).ToList();
            var names = items.Select(s => s.Text).ToList();
            var el = await _dr.FindElementsAsync("//div[contains(@class,'price-root')]/span");
            var prices = el.Select(s => s.Text.Replace(" ", "").TrimEnd('\u20BD')).ToList();
            //проверка результатов парсинга
            if (items.Count == 0) throw new Exception("ошибка парсинга: не найдны ссылки на товары");
            if (items.Count != names.Count || names.Count != prices.Count) throw new Exception("ошибка парсинга: не соответствует количество ссылок и цен");
            //перебираю найденное
            for (int i = 0; i < urls.Count(); i++) {
                //ищу индекс карточки в базе
                var b = _bus.FindIndex(f => f.avito.Contains(ids[i]));
                if (b >= 0) {
                    //проверяю, нужно ли его снять
                    if (location == "/active" && _bus[b].amount <= 0) Delete(b);
                    //если цена или наименование не совпадают надо редактировать
                    if (_bus[b].price != int.Parse(prices[i]) ||
                        !_bus[b].name.ToLowerInvariant().Contains(names[i].ToLowerInvariant())) {
                        await EditAsync(b);
                    }
                    //если объявление в разделе "архив" или "ждут действий", но есть на остатках и цена больше пороговой - поднимаю
                    if (CountToUp > 0 &&
                        _bus[b].price >= _priceLevel &&
                        _bus[b].amount > 0 &&
                        (location == "/old" || location == "/inactive")) {
                        await UpOfferAsync(b);
                    }
                }
            }
            await Task.Delay(30000);
        }

        private async Task UpOfferAsync(int b) {
            var id = _bus[b].avito.Split('_').Last();
            var url = "https://www.avito.ru/account/pay_fee?item_id=" + id;
            await _dr.NavigateAsync(url);
            //проверка наличия формы редактирования
            if (_dr.GetElementsCount("//*[contains(text(),'Состояние')]") > 0) {
                SetStatus(b);
                PressOk();
            }
            //нажимаю активировать и уменьшаю счетчик
            var butOpub = _dr.FindElements("//button[@type='submit']");
            if (butOpub.Count > 0) {
                butOpub.First().Click();
                CountToUp--;
                await Task.Delay(30000);
            }
        }

        private void SetStatus(int b) {
            if (_bus[b].IsNew()) {
                _dr.ButtonClick("//span[contains(text(),'Новое')]/../..");
            } else {
                _dr.ButtonClick("//span[contains(text(),'Б/у')]/../..");
            }
        }

        private void SetPhone() {
            _dr.WriteToSelector("//input[@id='phone']", "9208994545");
        }

        private void PressOk(int count = 2) {
            for (int i = 0; i < count; i++) {
                while (true) {
                    _dr.ButtonClick("//button[contains(@data-marker,'button-next')]");
                    var errorBox = _dr.FindElements("//div[contains(@class,'alert')]/*[@aria-label='Close']");
                    if (errorBox.Count > 0) {
                        _dr.ButtonClick("//div[contains(@class,'alert')]/*[@aria-label='Close']");
                    } else break;
                };
            }
        }

        private void SetImages(int b) {
            WebClient cl = new WebClient();
            cl.Encoding = Encoding.UTF8;
            var num = _bus[b].images.Count > 5 ? 5 : _bus[b].images.Count;
            for (int u = 0; u < num; u++) {
                for (int i = 0; i < 5; i++) {
                    try {
                        byte[] bts = cl.DownloadData(_bus[b].images[u].url);
                        File.WriteAllBytes("avito_" + u + ".jpg", bts);
                        Thread.Sleep(1000);
                        _dr.SendKeysToSelector("input[type=file]", Application.StartupPath + "\\" + "avito_" + u + ".jpg");
                        break;
                    } catch { }
                }
            }
            cl.Dispose();
        }

        void SetDesc(int b) {
            _dr.WriteToSelector("div.DraftEditor-root", sl: GetAvitoDesc(b));
        }

        public List<string> GetAvitoDesc(int b) {
            List<string> s = _bus[b].DescriptionList(2799, _addDesc);
            s.AddRange(_addDesc2);
            return s;
        }

        private void SetPrice(int b) {
            _dr.WriteToSelector("#price", _bus[b].price.ToString());
        }

        private void SetTitle(int b) {
            _dr.WriteToSelector("#title", _bus[b].NameLength(130));
        }

        private void AvitoClickTo(string s) {
            _dr.ButtonClick("//div[@data-marker='category-wizard/button' and text()='" + s + "']");
        }

        public void CheckUrls() {
            //TODO авито реализовать скользящую проверку ссылок из базы

            //кусок старого кода, может пригодится
            //var lastScan = Convert.ToInt32(dSet.Tables["controls"].Rows[0]["controlAvitoUrls"]);
            //lastScan = lastScan + 1 >= bus.Count ? 0 : lastScan;//опрокидываем счетчик и начинаем снова

            //for (int b = lastScan; b < bus.Count; b++) {
            //    if (base_rescan_need || !button_avito_get.Enabled || !button_avito_put.Enabled) break;
            //    if (/*bus[b].tiu.Contains("http") &&*/ bus[b].avito.Contains("http")
            //        && bus[b].amount > 0 && bus[b].price > 0) {
            //        var butNum = 0;
            //        var ch = Task.Factory.StartNew(() => {
            //            av.Navigate().GoToUrl(bus[b].avito);
            //            Thread.Sleep(5000);
            //            butNum = av.FindElements(By.XPath("//h1[@class='title-info-title']")).Count;
            //        });
            //        try {
            //            await ch;
            //            if (av.FindElements(By.XPath("//a[text()='Мои объявления']")).Count == 0) break; //если нет шапки--считаем, что проблемы с загрузкой страницы и прерываем
            //            if (base_rescan_need || !button_avito_get.Enabled || !button_avito_put.Enabled) break;
            //            if (butNum == 0) {
            //                bus[b].avito = " ";
            //                await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>
            //                {
            //                    {"id", bus[b].id},
            //                    {"name", bus[b].name},
            //                    {"209326", bus[b].avito},
            //                });
            //                ToLog("АВИТО УДАЛЕНА БИТАЯ ССЫЛКА ИЗ БАЗЫ!!!!!!!!\n" + bus[b].name);
            //            } else
            //                label_avito.Text = b.ToString() + " (" + b * 100 / bus.Count + "%)";
            //        } catch (Exception ex) {
            //            ToLog("avito_check: " + ex.Message);
            //            break;
            //        }

            //    }
            //    dSet.Tables["controls"].Rows[0]["controlAvitoUrls"] = b;
            //    try {
            //        dSet.WriteXml(fSet);
            //    } catch (Exception x) {
            //        ToLog("ошибка записи файла настроек!\n" + x.Message);
            //    }
            //}
        }

        private void SetOffetType() {
            _dr.WriteToSelector("//option[contains(text(),'на продажу')]/..", OpenQA.Selenium.Keys.ArrowDown + OpenQA.Selenium.Keys.ArrowDown + OpenQA.Selenium.Keys.Enter);
        }

        void SetCategory(int b) {
            AvitoClickTo("Транспорт");
            AvitoClickTo("Запчасти и аксессуары");
            _dr.ButtonClick("//button[contains(@class,'cascader-header-button')]");
            var bn = _bus[b].name.ToLower();
            string selector;
            switch (_bus[b].GroupName()) {
                case "Аудио-видеотехника":
                    selector = "Аудио- и видеотехника";
                    break;
                case "Шины, диски, колеса":
                    selector = "Шины, диски и колёса";
                    break;
                case "Автохимия":
                    selector = "Автокосметика и автохимия";
                    break;
                default:
                    selector = "Запчасти";
                    break;
            }
            AvitoClickTo(selector);
            switch (_bus[b].GroupName()) {
                case "Аудио-видеотехника":
                    selector = "";
                    break;
                case "Шины, диски, колеса":
                    if (bn.Contains("колесо") || bn.Contains("докатка")) {
                        selector = "Колёса";
                    } else if (bn.Contains("колпак")) {
                        selector = "Колпаки";
                    } else if (bn.Contains("диск")) {
                        selector = "Диски";
                    } else if (bn.Contains("шины") || bn.Contains("резина")) {
                        selector = "Шины";
                    }
                    break;
                case "Автохимия":
                    selector = "";
                    break;
                default:
                    selector = "Для автомобилей";
                    break;
            }
            if (selector.Length > 0) {
                AvitoClickTo(selector);
                switch (_bus[b].GroupName()) {
                    case "Ручки и замки кузова":
                    case "Петли":
                    case "Кузовные запчасти":
                    case "Пластик кузова":
                    case "Зеркала":
                    case "Кронштейны, опоры":
                    case "Тросы автомобильные":
                        selector = "Кузов";
                        break;
                    case "Датчики":
                    case "Генераторы":
                    case "Электрика, зажигание":
                    case "Блоки управления":
                    case "Стартеры":
                        selector = "Электрооборудование";
                        break;
                    case "Топливная, выхлопная система":
                        selector = "Топливная и выхлопная системы";
                        break;
                    case "Трансмиссия и привод":
                        selector = "Трансмиссия и привод";
                        break;
                    case "Подвеска и тормозная система":
                        selector = "Подвеска";
                        break;
                    case "Салон":
                        selector = "Салон";
                        break;
                    case "Рулевые рейки, рулевое управление":
                        selector = "Рулевое управление";
                        break;
                    case "Система отопления и кондиционирования":
                    case "Система охлаждения двигателя":
                        selector = "Система охлаждения";
                        break;
                    case "Двигатели":
                    case "Детали двигателей":
                        selector = "Двигатель";
                        break;
                    case "Автостекло":
                        selector = "Стекла";
                        break;
                    case "Световые приборы транспорта":
                        selector = "Автосвет";
                        break;
                    default:
                        selector = "";
                        break;
                }
                if (selector.Length > 0) {
                    AvitoClickTo(selector);
                    switch (_bus[b].GroupName()) {
                        case "Пластик кузова":
                        case "Ручки и замки кузова":
                        case "Петли":
                        case "Кузовные запчасти":
                            _dr.ButtonClick("//span[text()='Тип детали кузова']/../../..//select");
                            if (bn.Contains("накладка") || bn.Contains("молдинг") || bn.Contains("бархот")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "молд" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("бампер")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "бамп" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("балка") || bn.Contains("лонжер") || bn.Contains("подрамник")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "балк" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("брызговик")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "брызг" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("замок") || bn.Contains("личинк") || bn.Contains("замка")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "замк" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("бака")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "креп" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("защита") || bn.Contains("ыльник") || bn.Contains("щит ") || bn.Contains("щиток ")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "защи" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("дверь")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "двер" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("креплен") || bn.Contains("ронштейн") || bn.Contains("ержатель") ||
                                bn.Contains("аправляющ") || bn.Contains("петл")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "креп" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("крыло ")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "крыл" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("крыша")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "кузов" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("крышка") && bn.Contains("багажн")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "крышк" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("капот")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "капот" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("порог")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "порог" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("решет") && bn.Contains("радиатор")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "решет" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "кузов" + OpenQA.Selenium.Keys.Enter);
                            break;
                        case "Зеркала":
                            _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "зерк" + OpenQA.Selenium.Keys.Enter);
                            break;
                        case "Кронштейны, опоры":
                            _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "креп" + OpenQA.Selenium.Keys.Enter);
                            break;
                        case "Тросы автомобильные":
                            _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "кузов" + OpenQA.Selenium.Keys.Enter);
                            break;
                        case "Детали двигателей":
                        case "Двигатели":
                            _dr.ButtonClick("//span[text()='Тип детали двигателя']/../../..//select");
                            if (bn.Contains("цилиндр") || bn.Contains("гбц ") ||
                                bn.Contains("низ ") || bn.Contains("поддон")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "блок" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("двигатель")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "двига" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("ремн") || bn.Contains("грм ") ||
                                bn.Contains("кожух") || bn.Contains("промежут") || bn.Contains("ванос") ||
                                bn.Contains("клапан ") || bn.Contains("клапанной") || bn.Contains("клапана") ||
                                bn.Contains("вал ") || bn.Contains("вала ") || bn.Contains("газорасп") ||
                                bn.Contains("компенсат") || bn.Contains("шестерн") || bn.Contains("шкив")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "ремни" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("коленвал") || bn.Contains("маховик") || bn.Contains("венец")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "колен" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("ролик") || bn.Contains("натяжитель")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "привод" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("крышка") || bn.Contains("плита")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "клапан" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("проклад")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "прокла" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("масло") || bn.Contains("маслян")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "маслян" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("поршен") || bn.Contains("шатун") || bn.Contains("кольц") || bn.Contains("вкладыш")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "поршн" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("вентиля")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "патру" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "блок" + OpenQA.Selenium.Keys.Enter);
                            break;
                    }
                }
            }
        }

    }
}