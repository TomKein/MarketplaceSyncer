using Selen.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Selen.Sites {
    class AutoRu {
        //поле для ссылки в карточке товара
        readonly string _url = "313971";
        //дополнительное описание
        string[] _dopDesc = new[] {
            "Дополнительные фотографии по запросу",
            "Есть и другие запчасти на данный автомобиль!",
            "Вы можете установить данную деталь в нашем АвтоСервисе с доп.гарантией и со скидкой!",
            "Перед выездом обязательно уточняйте наличие запчастей по телефону",
            "за 30 минут перед выездом (можно бесплатно через Viber) - товар на складе!",
            "Звонить строго с 9:00 до 19:00 (воскресенье - выходной)",
            "Бесплатная доставка до транспортной компании!",
            "Отправляем наложенным платежом ТК СДЭК (только негабаритные посылки)"
        };
        //браузер
        Selenium _dr;
        //список товаров
        List<RootObject> _bus = null;
        //по сколько объявлений добавлять за раз
        public int AddCount { get; set; } = 1;
        //загрузка куки
        public void LoadCookies() {
            if (_dr != null) {
                _dr.Navigate("https://auto.ru/404");
                _dr.LoadCookies("auto.json");
                Thread.Sleep(1000);
                _dr.Refresh();
            }
        }
        //сохранение куки
        public void SaveCookies() {
            if (_dr != null) {
                _dr.SaveCookies("auto.json");
            }
        }
        //завершение работы
        public void Quit() {
            _dr?.Quit();
            _dr = null;
        }
        //главный метод запуска синхронизации
        public async Task AutoRuStartAsync(List<RootObject> bus) {
            _bus = bus;
            await AuthAsync();
            await MassCheckAsync();
            await AddAsync();
            await CheckAsync();
        }
        //метод авторизации на сайте
        private async Task AuthAsync() {
            await Task.Factory.StartNew(() => {
                if (_dr == null) {
                    Log.Add("auto.ru: запуск нового браузера...");
                    _dr = new Selenium();
                    LoadCookies();
                    _dr.Navigate("https://parts.auto.ru/lk");
                }
                if (!_dr.GetUrl().Contains("parts.auto.ru/lk")) {
                    _dr.Navigate("https://auto.ru/parts/lk?rgid=6");
                    _dr.Navigate("https://auth.auto.ru/login/?r=https%3A%2F%2Fauto.ru%2Fparts%2Flk%3Frgid%3D6");
                    _dr.WriteToSelector("input[name=login]", "9106027626@mail.ru");
                    _dr.ClickToSelector("button[class*=blue]");
                    //отправлено письмо с паролем на вход
                    var p = new Pop3();
                    var pas = p.GetPass();
                    _dr.WriteToSelector("div[class*='FormCodeInput'] input", pas);
                }
                //закрываю всплывающее окно
                _dr.ButtonClick("i[class*='WhatsNew__close']");
                //проверяю авторизацию, сохраняю куки
                if (_dr.GetUrl().Contains("parts.auto.ru/lk")) SaveCookies();
                //закрытие всплывающей рекламы
                _dr.ButtonClick("//div[@aria-hidden='false']//div[@class='Modal__closer']");
                _dr.Refresh();
                //нажимаю кнопку обновить
                _dr.ButtonClick("//span[@class='Button__text' and text()='Обновить']/../..");
            });
        }
        //метод массовой проверки товаров
        private async Task MassCheckAsync() {
            for (int b = 0; b < _bus.Count; b++) {
                if (_bus[b].auto != null &&
                    _bus[b].auto.Contains("http") &&
                    (_bus[b].IsTimeUpDated() || _bus[b].amount <= 0)) {
                    try {
                        if (_bus[b].amount > 0) await EditAsync(b);
                        else await DeleteAsync(b);
                    } catch (Exception x) {
                        Log.Add("auto.ru: ошибка " + x.Message);
                        //если ошибка браузера - кидаю исключение дальше
                        if (x.Message.Contains("timed out") ||
                            x.Message.Contains("already closed") ||
                            x.Message.Contains("invalid session id") ||
                            x.Message.Contains("chrome not reachable")) throw;
                    }
                }
            }
        }
        //метод удаления объявлений
        private async Task DeleteAsync(int b) {
            try {
                await Task.Factory.StartNew(() => {
                    //получаю id объявления
                    var id = _bus[b].auto.Replace("offerId=", "|").Split('|')[1].Split('&')[0];
                    do {
                        //ищу объявление на сайте
                        _dr.Navigate("https://auto.ru/parts/lk?rgid=6&id=" + id);
                        //нажимаю кнопку удалить объявление
                        _dr.ButtonClick("//button[@title='Удалить объявление']");
                        //повторяю, пока не будет точно удалено (удаление срабатывает не всегда с первого раза)
                        Thread.Sleep(10000);
                    } while (_dr.GetElementsCount("//button[@title='Удалить объявление']") > 0);
                });
                //удаляю ссылку из карточки товара
                _bus[b].auto = "";
                await Class365API.RequestAsync("put", "goods", new Dictionary<string, string> {
                    {"id", _bus[b].id},
                    {"name", _bus[b].name},
                    {_url, _bus[b].auto}
                });
                Log.Add("auto.ru: " + _bus[b].name + " - объявление удалено!");
            } catch (Exception x) {
                throw new Exception("удаления " + _bus[b].name + " - " + x.Message);
            }
        }
        //метод редактирования объявлений
        private async Task EditAsync(int b) {
            try {
                await Task.Factory.StartNew(() => {
                    //перехожу на страницу объявления
                    _dr.Navigate(_bus[b].auto);
                    SetPrice(b);
                    SetPart(b);
                    SetDesc(b);
                    PressOkButton();
                });
            } catch (Exception x) {
                throw new Exception("редактирования " + _bus[b].name + " - " + x.Message);
            }
        }
        //добавить объявления на сайт
        private async Task AddAsync() {
            for (int b = _bus.Count - 1; b > -1 && AddCount > 0; b--, AddCount--) {                        
                if ((_bus[b].auto == null || !_bus[b].auto.Contains("http")) &&
                    _bus[b].tiu.Contains("http") &&
                    _bus[b].amount > 0 &&
                    _bus[b].price >= 100 &&
                    _bus[b].images.Count > 0 &&
                    _bus[b].GroupName() != "Автохимия" &&
                    _bus[b].GroupName() != "Инструменты") {
                    try {
                        //определяем авто, если не удалось - пропускаем
                        var m = await SelectAutoAsync(b);
                        if (m == null) continue;
                        await Task.Factory.StartNew(() => {
                            _dr.Navigate("https://auto.ru/parts/user-offer?rgid=6");
                            SelectCategory(b,m);
                            SetImages(b);
                            SetPrice(b);
                            SetPart(number: _bus[b].id);
                            SetDesc(b);
                            SetOffice();
                            SetStatus(b);
                            PressOkButton();
                        });
                        await SaveUrlAsync(b);
                        Log.Add("auto.ru: " + _bus[b].name + " - объявление добавлено!");
                    } catch (Exception x) {
                        Log.Add("auto.ru: ошибка добавления объявления! - " + _bus[b].name + " - " + x.Message);
                        //если ошибка браузера - кидаю исключение дальше
                        if (x.Message.Contains("timed out") ||
                            x.Message.Contains("already closed") ||
                            x.Message.Contains("invalid session id") ||
                            x.Message.Contains("chrome not reachable")) throw;
                        await Task.Delay(20000);
                    }
                }
            }
        }
        //нажимаю Сохранить
        private void PressOkButton() {
            for (int i = 0; ; i++) {
                _dr.ButtonClick("button[name='submit']");
                _dr.ConfirmAlert();
                if (_dr.GetElementsCount("button[name='submit']") == 0) break;
                if (i > 9) throw new Exception("кнопка сохранения не срабатывает!");
                Thread.Sleep(30000);
            }
        }
        //описание объявления
        private void SetDesc(int b) {
            _dr.WriteToSelector("div[class='TextArea__box'] textarea", sl: _bus[b].DescriptionList(dop: _dopDesc));
        }
        //устанавливаю номер запчасти
        private void SetPart(int b = 0, string number = null) {
            number = number ?? _bus[b].part;
            _dr.WriteToSelector("div[class*='FormField_name_oem'] input", number);
        }
        //устанавлиаю цену
        private void SetPrice(int b) {
            _dr.WriteToSelector("label[class*='PriceFormControl'] input", _bus[b].price.ToString());
        }
        //сохранение ссылки в карточку товара
        private async Task SaveUrlAsync(int b) {
            for (int i = 0; ; i++) {
                await _dr.NavigateAsync("https://auto.ru/parts/lk/?text=" + _bus[b].id);
                if (_dr.GetElementsCount("a.UserOffersItem__link") > 0) {
                    var id = _dr.GetElementAttribute("a.UserOffersItem__link", "href")
                                .Replace("offer/", "|").Split('|').Last().Split('/').First();
                    if (id.Length > 4) {
                        _bus[b].auto = "https://auto.ru/parts/user-offer/?offerId=" + id;
                        await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                            {"id", _bus[b].id},
                            {"name", _bus[b].name},
                            {_url, _bus[b].auto}
                        });
                        break;
                    }
                }
                if (i > 9) throw new Exception("ссылка на объявление не найдена!");
                await Task.Delay(30000);
            };
        }
        //установка статуса б/у или новый
        private void SetStatus(int b) {
            if (_bus[b].IsNew()) _dr.ButtonClick("//span[text()='Новый']/../..");
        }
        //загрузка фотографий
        private void SetImages(int b) {
            WebClient cl = new WebClient {
                Encoding = Encoding.UTF8
            };
            var num = _bus[b].images.Count > 5 ? 5 : _bus[b].images.Count;
            for (int u = 0; u < num; u++) {
                for (int i = 0; ; i++) {
                    try {
                        byte[] bts = cl.DownloadData(_bus[b].images[u].url);
                        File.WriteAllBytes("file.jpg", bts);
                        break;
                    } catch (Exception x) {
                        Log.Add("auto.ru: ошибка загрузки фото товара! (попытка " + (i + 1) + ") - " + x.Message);
                    }
                    if (i > 4) throw new Exception("не удалось получить фото из карточки товара!");
                    Thread.Sleep(10000);
                }
                //отправляю фото на сайт
                try {
                    _dr.SendKeysToSelector("//input[@type='file']", Application.StartupPath + "\\" + "file.jpg");
                    //удаляю дубли - глюк авто.ру
                    while (_dr.GetElementsCount("//li/*[contains(@class,'IconSvg_close')]") > u + 1) {
                        _dr.ButtonClick("(//li/*[contains(@class,'IconSvg_close')])[last()]");
                    }
                } catch(Exception x) {
                    throw new Exception("не удается прикрепить фото к объявлению! - " + x.Message);
                }
            }
            cl.Dispose();
        }
        //указываю точку продаж
        private void SetOffice() {
            _dr.ButtonClick("//span[text()='Точки продаж']/../..");
            _dr.ButtonClick("//div[contains(@class,'MenuItem_size_m')]");
        }
        //указываем марку автомобиля
        public async Task<string> SelectAutoAsync(int b) {
            var desc = _bus[b].name.ToLowerInvariant() + " " + _bus[b].description.ToLowerInvariant();
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
        //выбор элемента для селектора категорий
        public void SelectElement(string s) {
            _dr.ButtonClick("//*[contains(@class,'MenuItem') and text()='"+s+"']");
        }
        //указываю категорию товара
        public void Select(string s) {
            _dr.WriteToSelector("div[class='FormParser'] input", s);
            _dr.ButtonClick("button[class*='FormParser__submit']");
        }
        //парсинг и проверка объявлений
        private async Task CheckAsync() {
            await Task.Delay(100);


            //var lastScan = Convert.ToInt32(dSet.Tables["controls"].Rows[0]["controlAuto"]);
            //lastScan = lastScan + 1 >= bus.Count ? 0 : lastScan;//опрокидываем счетчик

            //for (int b = lastScan; b < bus.Count; b++) {
            //    if (base_rescan_need) { break; }
            //    if (bus[b].auto.Contains("http")
            //        && bus[b].amount > 0) {
            //        //колчество элементов на странице
            //        System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> but = null;

            //        var ch = Task.Factory.StartNew(() => {
            //            au.Navigate().GoToUrl(bus[b].auto);
            //            Thread.Sleep(1000);
            //            but = au.FindElements(By.CssSelector("div.FormField.FormField_name_submit > button"));
            //        });
            //        try {
            //            await ch;
            //            //если нет такого
            //            if (but.Count == 0) {
            //                bus[b].auto = " ";

            //                await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>
            //                {
            //                    {"id", bus[b].id},
            //                    {"name", bus[b].name},
            //                    {"313971", bus[b].auto},
            //                });
            //                Log.Add("AUTO.RU УДАЛЕНА БИТАЯ ССЫЛКА ИЗ БАЗЫ!!!!!!!!\n" + bus[b].name);
            //            } else {
            //                label_auto.Text = b.ToString() + " (" + b * 100 / bus.Count + "%)";
            //                dSet.Tables["controls"].Rows[0]["controlAuto"] = b + 1;
            //                try {
            //                    dSet.WriteXml(fSet);
            //                } catch (Exception x) {
            //                    Log.Add("ошибка записи файла настроек!\n" + x.Message);
            //                }

            //                //редактируем цену
            //                var we = au.FindElement(By.CssSelector("label[class*='PriceFormControl'] input"));
            //                WriteToIWebElement(au, we, bus[b].price.ToString());

            //                //описание
            //                //we = au.FindElement(By.CssSelector("div[class='TextArea__box'] textarea"));
            //                //WriteToIWebElement(au, we, sl: GetAutoDesc(b));

            //                //добавим адрес точки продаж
            //                Actions a = new Actions(au);
            //                a.MoveToElement(but[0]).Build().Perform();
            //                await Task.Delay(200);

            //                var el = au.FindElement(By.CssSelector("div[class*='name_stores'] button"));
            //                el.Click();
            //                await Task.Delay(2000);
            //                if (!el.Text.Contains("Московская")) {
            //                    a.SendKeys(OpenQA.Selenium.Keys.Enter).Build().Perform();
            //                    await Task.Delay(500);
            //                    //если адреса нет - выходим, чтобы дать юзеру дозаполнить и сохранить
            //                    a.MoveToElement(au.FindElement(By.CssSelector("div[class*='Form__section_name_category']"))).Perform();
            //                    break;
            //                }
            //                //сохраняем
            //                but[0].Submit();
            //                await Task.Delay(10000);
            //                try {
            //                    au.SwitchTo().Alert().Dismiss();
            //                } catch { }
            //                //but[0].Click();
            //            }
            //        } catch (Exception ex) {
            //            Log.Add("button_autoCheck_Click: " + ex.Message);
            //            break;
            //        }
            //    }

            //}

            //var pages = (int)numericUpDown_auto.Value;
            //var pages = 0;
            //auto.Clear();
            //for (int i = pages; i > 0; i--) {
            //    var tb = Task.Factory.StartNew(() => {
            //        _dr.Navigate().GoToUrl("https://auto.ru/parts/lk?page=" + i + "&pageSize=50&rgid=6");
            //        //парсим элементы
            //        foreach (var li in _dr.ButtonClicks(By.CssSelector("li.UserOffers__list-item"))) {
            //            var el_name = li.FindElement(By.ClassName("UserOffersItem__title"));
            //            var name = el_name.Text;
            //            var id = el_name.FindElement(By.CssSelector("a[class*=UserOffersItem]"))
            //                .GetAttribute("href")
            //                .Replace("/offer/", "|")
            //                .Split('|')[1]
            //                .TrimEnd('/');
            //            var price = Convert.ToInt32(li.FindElement(By.CssSelector("span[class*=UserOffersItem__price]"))
            //                .Text
            //                .TrimEnd('Р')
            //                .Replace(" ", "")
            //                .Replace("\u2009", ""));
            //            var image = li.FindElement(
            //                    By.CssSelector("img[class*=Item__image]"))
            //                .GetAttribute("src")
            //                .Replace("/small", "/big");
            //            var status = true;
            //            //li.FindElement(By.CssSelector("div.user-offers__offer-cell.user-offers__offer-cell_name_status > span")).Text;

            //            auto.Add(new AutoObject {
            //                name = name,
            //                price = price,
            //                image = image,
            //                status = status,
            //                id = id
            //            });
            //        }
            //    });
            //    try {
            //        await tb;
            //    } catch (Exception ex) {
            //        Log.Add("авто.ру ошибка парсинга сайта, страница " + i + "\n" + ex.Message);
            //    }
            //    if (base_rescan_need) { break; }
            //}            
        }
        //выбор категории
        private void SelectCategory(int b, string m) {
            var n = _bus[b].name.ToLower();
            try {
                switch (_bus[b].GroupName()) {
                    case "Стартеры": Select("стартер" + m); break;

                    case "Автостекло":
                        if (n.Contains("заднее") && !n.Contains(" l ") && !n.Contains(" r ")
                            && !n.Contains("лев") && !n.Contains("прав")
                            && !n.Contains("l/r") && !n.Contains(@"l\r")
                            && !n.Contains("r/l") && !n.Contains(@"r\l")) { Select("стекло заднее" + m); break; }
                        if (n.Contains("лобов")) { Select("стекло лобовое" + m); break; }
                        Select("стекло боковое" + m);
                        if (n.Contains("задн")) {
                            _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div");
                            SelectElement("Заднее");
                        }
                        if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                            _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                            SelectElement("Правая");
                        }
                        break;

                    case "Генераторы":
                        if (n.Contains("щетки")) { Select("щетки генератора" + m); break; }
                        Select("генератор и комплектующие" + m); break;

                    case "Рулевые рейки, рулевое управление":
                        if (n.Contains("насос")) { Select("насос гидроусилителя" + m); break; }
                        if (n.Contains("шкив")) { Select("шкив насоса гидроусилителя" + m); break; }
                        if (n.Contains("бачок")) { Select("бачок гидроусилителя руля" + m); break; }
                        if (n.Contains("рейка")) { Select("рулевая рейка" + m); break; }
                        if (n.Contains("вал")) { Select("вал рулевой" + m); break; }
                        if (n.Contains("кардан")) { Select("кардан рулевой" + m); break; }
                        if (n.Contains("руль")) { Select("Рулевое колесо" + m); break; }
                        if (n.Contains("колонка")) { Select("Колонки рулевые в сборе" + m); break; }
                        if (n.Contains("шланг")) { Select("Шланг гидроусилителя" + m); break; }
                        if (n.Contains("радиатор")) { Select("Радиатор гидроусилителя" + m); break; }
                        if (n.Contains("трубк")) { Select("трубка гидроусилителя" + m); break; }
                        if (n.Contains("тяг")) { Select("Рулевая тяга" + m); break; }
                        Select("Рулевое управление" + m); break;

                    case "Трансмиссия и привод":
                        if (n.Contains("гидротрансформатор")) { Select("Гидротрансформатор АКПП" + m); break; }
                        if (n.Contains("привод")) {
                            Select("Приводной вал" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("кулиса")) { Select("Кулиса КПП" + m); break; }
                        if (n.Contains("ыжимной")) { Select("Подшипник выжимной" + m); break; }
                        if (n.Contains("подшипн")) {
                            Select("Подшипник ступицы" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("ступиц")) {
                            Select("Ступица колеса" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("мкпп ")) { Select("МКПП" + m); break; }
                        if (n.Contains("акпп ")) { Select("АКПП" + m); break; }
                        if (n.Contains("шрус")) { Select("ШРУС" + m); break; }
                        if (n.Contains("кардан")) { Select("Карданный вал" + m); break; }
                        if (n.Contains("корзина")) { Select("Корзина сцепления" + m); break; }
                        if (n.Contains("главный")) { Select("Цилиндр сцепления главный" + m); break; }
                        if (n.Contains("рабочий")) { Select("Цилиндр сцепления рабочий" + m); break; }
                        if (n.Contains("редуктор")) { Select("Редуктор" + m); break; }
                        if (n.Contains("сцеплени")) { Select("Комплект сцепления" + m); break; }
                        if (n.Contains("фильтракпп")) { Select("Фильтр АКПП" + m); break; }
                        if (n.Contains("раздаточн")) { Select("Раздаточная коробка" + m); break; }
                        if (n.Contains("кулис")) { Select("Кулиса КПП" + m); break; }
                        if (n.Contains("кольц")) { Select("Кольцо упорное КПП" + m); break; }
                        Select("Трансмиссия" + m); break;

                    case "Зеркала":
                        if (n.Contains("салон")) { Select("Салонное зеркало заднего вида" + m); break; }
                        if (n.Contains("элемент")) {
                            Select("Зеркальный элемент" + m);
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("зеркало")) {
                            Select("Боковые зеркала заднего вида" + m);
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        Select("Зеркала" + m); break;

                    case "Автохимия":
                        if (n.Contains("паста") && n.Contains("рук")) { Select("Очиститель для рук"); break; }
                        Select("Автохимия"); break;

                    case "Аудио-видеотехника":
                        if (n.Contains("магнитола")) { Select("Магнитола" + m); break; }
                        if (n.Contains("динамик")) { Select("Динамики" + m); break; }
                        Select("Аудиотехника" + m); break;

                    case "Датчики":
                        if (n.Contains("abs")) { Select("Датчик ABS" + m); break; }
                        if (n.Contains("давления")) { Select("Датчик абсолютного давления" + m); break; }
                        if (n.Contains("масл")) { Select("Датчик давления масла" + m); break; }
                        if (n.Contains("кислород") || n.Contains("ямбда")) { Select("Датчик кислорода" + m); break; }
                        if (n.Contains("детонац")) { Select("Датчик детонации" + m); break; }
                        if (n.Contains("коленвала")) { Select("Датчик положения коленвала" + m); break; }
                        if (n.Contains("распредвала")) { Select("Датчик положения распредвала" + m); break; }
                        if (n.Contains("температуры")) { Select("Датчик температуры охл. жидкости" + m); break; }
                        if (n.Contains("расх")) { Select("Датчик массового расхода воздуха" + m); break; }
                        if (n.Contains("топлив")) { Select("Датчик уровня топлива" + m); break; }
                        if (n.Contains("парков")) { Select("Парковочная система" + m); break; }
                        if (n.Contains("подуш") && n.Contains("bag")) { Select("Датчик подушки безопасности" + m); break; }
                        Select("Датчик износа тормозных колодок" + m); break;

                    case "Детали двигателей":
                    case "Двигатели":
                        if (n.Contains("двигатель")) { Select("Двигатель в сборе" + m); break; }
                        if (n.Contains("цилиндров")) { Select("Блок цилиндров" + m); break; }
                        if (n.Contains("маховик")) { Select("Маховик" + m); break; }
                        if (n.Contains("промежуточный")) { Select("Вал промежуточный" + m); break; }
                        if (n.Contains("лапан") && n.Contains("впуск")) { Select("Клапан впускной" + m); break; }
                        if (n.Contains("клапан ")) { Select("Клапан выпускной" + m); break; }
                        if (n.Contains("кожух")) { Select("Крышка ремня ГРМ" + m); break; }
                        if (n.Contains("прокладкагбц")) { Select("Прокладка головки блока цилиндров" + m); break; }
                        if (n.Contains("лапанн") && n.Contains("крышк")) { Select("Крышка головки блока цилиндров" + m); break; }
                        if (n.Contains("гбц") && _bus[b].price > 500) { Select("Головка блока цилиндров" + m); break; }
                        if (n.Contains("ольца")) { Select("Кольца поршневые" + m); break; }
                        if (n.Contains("грм") && n.Contains("крыш")) { Select("Крышка ремня ГРМ" + m); break; }
                        if (n.Contains("грм") && n.Contains("защит")) { Select("Крышка ремня ГРМ" + m); break; }
                        if (n.Contains("оддон") && n.Contains("двигател")) { Select("Поддон двигателя" + m); break; }
                        if (n.Contains("маслоотражатель")) { Select("Маслоотражатель турбины" + m); break; }
                        if (n.Contains("крышка") && n.Contains("двигател")) { Select("Крышка двигателя декоративная" + m); break; }
                        if (n.Contains("шатун") || n.Contains("оршен")) { Select("Коленвал, поршень, шатун" + m); break; }
                        if (n.Contains("шкив") && n.Contains("колен")) { Select("Шкив коленвала" + m); break; }
                        if (n.Contains("распредвал")) { Select("Распредвал и клапаны" + m); break; }
                        if (n.Contains("ролик")) { Select("Натяжитель приводного ремня" + m); break; }
                        if (n.Contains("шкив")) { Select("Шкив коленвала" + m); break; }
                        if (n.Contains("шестерн") && n.Contains("баланс")) { Select("Шестерня вала балансирного" + m); break; }
                        if (n.Contains("шестерн")) { Select("Шестерня коленвала" + m); break; }
                        if (n.Contains("гидрокомпенсат")) { Select("Гидрокомпенсатор" + m); break; }
                        if (n.Contains("ланг") && n.Contains("вентиляц")) { Select("Патрубок вентиляции картерных газов" + m); break; }
                        if (n.Contains("ружин") && n.Contains("клапан")) { Select("Пружина клапана" + m); break; }
                        if (n.Contains("поддон") && n.Contains("проклад")) { Select("Прокладка поддона двигателя" + m); break; }
                        if (n.Contains("пыльник") && n.Contains("двигат")) { Select("Пыльник двигателя" + m); break; }
                        if (n.Contains("поддон")) { Select("Поддон двигателя" + m); break; }
                        if (n.Contains("вал") && n.Contains("насос")) { Select("Вал промежуточный" + m); break; }
                        if (n.Contains("ванос") || (n.Contains("егуляторфаз"))) { Select("Муфта изменения фаз ГРМ" + m); break; }
                        if (n.Contains("крыш") && n.Contains("коленв")) { Select("Крышка коленвала" + m); break; }
                        if (n.Contains("кладыш") && n.Contains("корен")) { Select("Вкладыши коленвала" + m); break; }
                        if (n.Contains("коленвал")) { Select("Коленвал" + m); break; }
                        if (n.Contains("идронатяжитель") || n.Contains("прокладк")) { Select("Натяжитель ремня ГРМ" + m); break; }
                        if (n.Contains("роклад") && n.Contains("пер")) { Select("Прокладка крышки блока цилиндров" + m); break; }
                        if (n.Contains("роклад") && n.Contains("крыш")) { Select("Прокладка клапанной крышки" + m); break; }
                        if (n.Contains("компл") && n.Contains("прокл")) { Select("Комплект прокладок двигателя" + m); break; }
                        if (n.Contains("маслопри")) { Select("Маслоприемник" + m); break; }
                        Select("Двигатель и система зажигания" + m); break;

                    case "Кронштейны, опоры":
                        if (n.Contains("гур") || n.Contains("гидроус")) { Select("Кронштейн гидроусилителя руля" + m); break; }
                        if (n.Contains("генератор")) { Select("Крепление генератора" + m); break; }
                        if (n.Contains("двигател") || n.Contains("двс")) { Select("Опора двигателя" + m); break; }
                        if (n.Contains("компресс") || n.Contains("кондицион")) { Select("Крепление компрессора кондиционера" + m); break; }
                        if (n.Contains("маслян")) { Select("Крепление масляного фильтра" + m); break; }
                        if (n.Contains("аккумулят")) { Select("Крепление аккумулятора" + m); break; }
                        if (n.Contains("кпп")) { Select("Кронштейн КПП" + m); break; }
                        if (n.Contains("бампер")) {
                            Select("Кронштейн бампера" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("консол")) { Select("Консоль" + m); break; }
                        if (n.Contains("амортиз") && _bus[b].price > 200) { Select("Опора амортизатора" + m); break; }
                        if (n.Contains("редукт")) { Select("Опора редуктора" + m); break; }
                        if (n.Contains("балк")) { Select("Кронштейн подрамника" + m); break; }
                        if (n.Contains("бак")) { Select("Кронштейн топливного бака" + m); break; }
                        if (n.Contains("безопас")) { Select("Крепление ремня безопасности" + m); break; }
                        if (n.Contains("опор")) { Select("Опора двигателя" + m); break; }
                        Select("Кузовные запчасти" + m); break;

                    case "Пластик кузова":
                    case "Ручки и замки кузова":
                    case "Петли":
                    case "Кузовные запчасти":
                        if (n.Contains("бампер ")) {
                            Select("Бампер" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            break;
                        }
                        if (n.Contains("бампер") && n.Contains("решет") && _bus[b].price >= 500) {
                            Select("Решётка бампера" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("бампер") && n.Contains("усилител")) {
                            Select("Усилитель бампера" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            break;
                        }
                        if (n.Contains("бампер") && (n.Contains("сорбер") || n.Contains("аполнит"))) {
                            Select("Абсорбер бампера" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            break;
                        }
                        if (n.Contains("бампер") && (n.Contains("олдинг") || n.Contains("кладка") || n.Contains("губа"))) { Select("Молдинг бампера" + m); break; }
                        if (n.Contains("бампер") && n.Contains("аправляющ")) {
                            Select("Направляющая бампера" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            break;
                        }
                        if (n.Contains("подрамник")) {
                            Select("подрамник" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            break;
                        }

                        if (n.Contains("двер") && (n.Contains("олдинг") || n.Contains("акладка"))) {
                            Select("Молдинг двери" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("крыл") && (n.Contains("олдинг") || n.Contains("хром"))) {
                            Select("Молдинг крыла" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("петл") && (n.Contains("двер") || n.Contains("перед"))) {
                            Select("Петля двери" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("петл") && n.Contains("капот")) {
                            Select("Петля капота" + m);
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Правая");//верный селектор
                            }
                            break;
                        }
                        if (n.Contains("лобов") && n.Contains("олдинг")) { Select("Молдинг лобового стекла" + m); break; }
                        if (n.Contains("петл") && n.Contains("багаж")) { Select("Петля багажника" + m); break; }
                        if (n.Contains("крыш") && n.Contains("олдинг")) { Select("Молдинг крыши" + m); break; }
                        if (n.Contains("дверь ") && _bus[b].price >= 1000) {
                            Select("Дверь боковая" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("подкрыл") || n.Contains("локер")) {
                            Select("Подкрылок" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("ручка") && n.Contains("внутр")) { Select("Ручка двери внутренняя" + m); break; }
                        if (n.Contains("ручка") && (n.Contains("наружн") || n.Contains("внешн") || n.Contains("двери"))) {
                            Select("Ручка двери наружная" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("ручка") && n.Contains("багаж")) { Select("Ручка багажника" + m); break; }
                        if ((n.Contains("решетка") || n.Contains("решётка")) && n.Contains("радиат")) { Select("Решётка радиатора" + m); break; }
                        if (n.Contains("ручка") && n.Contains("капот")) { Select("Ручка открывания капота" + m); break; }
                        if (n.Contains("порог ")) {
                            Select("Порог кузовной" + m);
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("бачок") && n.Contains("омыв")) { Select("Бачок стеклоомывателя" + m); break; }
                        if (n.Contains("жабо") || n.Contains("дождевик") || n.Contains("водяной") || n.Contains("водосток")) {
                            Select("Панель под лобовое стекло" + m); break;
                        }
                        if (n.Contains("замок") && n.Contains("двер") || n.Contains("замка") && (n.Contains("част") || n.Contains("клапан"))) {
                            Select("Замок двери" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("трапеция")) { Select("Трапеция стеклоочистителя" + m); break; }
                        if (n.Contains("дворник")) {
                            Select("Поводок стеклоочистителя" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if ((n.Contains("замок") || n.Contains("замк")) && n.Contains("багаж")) { Select("Замок багажника в сборе" + m); break; }
                        if (n.Contains("амортиз") && n.Contains("багаж")) { Select("Амортизатор двери багажника" + m); break; }
                        if (n.Contains("амортиз") && n.Contains("капот")) { Select("Амортизатор капота" + m); break; }
                        if (n.Contains("замок") && n.Contains("капот")) { Select("Замок капота" + m); break; }
                        if (n.Contains("брызговик")) {
                            Select("Брызговик" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("крыло ")) {
                            Select("Крыло" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if ((n.Contains("ержатель") || n.Contains("упор")) && n.Contains("капот")) { Select("Упор капота" + m); break; }
                        if (n.Contains("ащита") && (n.Contains("двс") || n.Contains("гравий"))) { Select("Защита картера двигателя" + m); break; }
                        if (n.Contains("рышка") && n.Contains("багажник")) { Select("Крышка багажника" + m); break; }
                        if (n.Contains("лючок") && n.Contains("бак")) { Select("Лючок топливного бака" + m); break; }
                        if (n.Contains("крыша")) { Select("Панель крыши" + m); break; }
                        if (n.Contains("капот ")) { Select("Капот" + m); break; }
                        if (n.Contains("стеклопод")) {
                            Select("Стеклоподъемный механизм" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            if (!n.Contains("элек")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[4]/div/div/button");
                                SelectElement("Механический");
                            }
                            break;
                        }
                        if ((n.Contains("уплотнител") || n.Contains("резинк")) && n.Contains("двер")) {
                            Select("Уплотнитель двери" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("акладка") && n.Contains("порог")) {
                            Select("Накладка порога" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[1]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("акладка") && n.Contains("крыл")) {
                            Select("Накладка крыла" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("акладка") && n.Contains("багажн")) { Select("Накладка багажника" + m); break; }
                        if (n.Contains("четверть") || n.Contains("лонжер")) {
                            Select("Четверть кузова" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("гайка") && n.Contains("колес")) { Select("Гайка колесная" + m); break; }
                        if (n.Contains("кнопк") && n.Contains("багаж")) { Select("Замок багажника в сборе" + m); break; }
                        if ((n.Contains("балка") && n.Contains("радиат")
                            || n.Contains("панел") && n.Contains("передн")
                            || n.Contains("телевизор")) && _bus[b].price >= 1000) { Select("Рамка радиатора" + m); break; }
                        if (n.Contains("боковина")) {
                            Select("Боковина" + m);
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if ((n.Contains("резинка") || n.Contains("уплотнитель")) && n.Contains("задн") && n.Contains("стекл")) {
                            Select("Уплотнитель заднего стекла" + m); break;
                        }
                        if ((n.Contains("резинка") || n.Contains("уплотнитель")) && n.Contains("лобов") && n.Contains("стекл")) {
                            Select("Уплотнитель лобового стекла" + m); break;
                        }
                        if ((n.Contains("резинка") || n.Contains("уплотнитель")) && n.Contains("боков") && n.Contains("стекл")) {
                            Select("Уплотнитель бокового стекла" + m); break;
                        }
                        if (n.Contains("личинк") && n.Contains("замк")) {
                            Select("Личинка замка" + m); break;
                        }
                        if (n.Contains("задня") && n.Contains("част") && n.Contains("кузов")) {
                            Select("Задняя часть автомобиля" + m); break;
                        }
                        if (n.Contains("крюк") && n.Contains("буксир")) {
                            Select("Петля буксировочная" + m); break;
                        }
                        if (n.Contains("эмблем")) { Select("Эмблема" + m); break; }
                        if ((n.Contains("резинка") || n.Contains("уплотнитель")) && n.Contains("багажн")) {
                            Select("Уплотнитель крышки багажника" + m); break;
                        }
                        if ((n.Contains("защита") || n.Contains("экран")) && n.Contains("коллект")) {
                            Select("Тепловые экраны" + m); break;
                        }
                        if (n.Contains("молдинг") || n.Contains("накладка") || n.Contains("хром")) {
                            Select("молдинги" + m); break;
                        }
                        if (n.Contains("ограничител") && n.Contains("двер")) {
                            Select("Ограничитель двери" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("арк") && n.Contains("обшив")) {
                            Select("Арка колеса" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("пыльник") && n.Contains("двигат")) { Select("Пыльник двигателя" + m); break; }
                        if (n.Contains("пыльник") && n.Contains("рулев")) { Select("Пыльник рулевой рейки" + m); break; }
                        if (n.Contains("пыльник") && n.Contains("шрус")) { Select("Пыльник ШРУСа" + m); break; }
                        if (n.Contains("крышка") && (n.Contains("фонар") || n.Contains("фар"))) {
                            Select("Крышка фары" + m); break;
                        }
                        if (n.Contains("крышка") && (n.Contains("предохр") || n.Contains("фар"))) {
                            Select("Крышка блока предохранителей" + m); break;
                        }
                        if (n.Contains("крышка") && (n.Contains("заливн") || n.Contains("масл"))) {
                            Select("Крышка маслозаливной горловины" + m); break;
                        }
                        if (n.Contains("ронштейн") && n.Contains("замка")) { Select("Кронштейн замка двери" + m); break; }
                        if (n.Contains("корпус") && n.Contains("блока")) { Select("Корпус блока предохранителей" + m); break; }
                        if (n.Contains("люк ")) { Select("люк" + m); break; }
                        if (n.Contains("треуголь") && n.Contains("зерка")) { Select("Треугольники зеркал" + m); break; }
                        if (n.Contains("треуголь") && n.Contains("двер")) {
                            Select("Накладка двери" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("щит") && n.Contains("опорн")) {
                            Select("Щит опорный" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if ((n.Contains("щит") || n.Contains("защит")) && (n.Contains("диск") || n.Contains("торм"))) {
                            Select("Щиток тормозного диска" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("ролик") && n.Contains("двер")) { Select("Ролик сдвижной двери" + m); break; }
                        if (n.Contains("торсион")) { Select("Торсион крышки багажника" + m); break; }
                        if ((n.Contains("обшивка") || n.Contains("изоляция")) && n.Contains("капот")) {
                            Select("Обшивка капота" + m); break;
                        }
                        if (n.Contains("спойлер")) { Select("Спойлер" + m); break; }
                        if (n.Contains("уплотнит") && n.Contains("капот")) { Select("Уплотнитель капота" + m); break; }
                        if (n.Contains("рейлинг")) { Select("Рейлинги" + m); break; }
                        Select("Кузовные запчасти" + m);
                        break;

                    case "Подвеска и тормозная система":
                        if (n.Contains("амортизатор ") || n.Contains("амортизаторы")) {
                            Select("Амортизатор в сборе" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Сзади");
                            }
                            _dr.ButtonClick("//div[2]/fieldset[1]/div[1]/div/div/button");
                            SelectElement("Газо-масляный");
                            break;
                        }
                        if (n.Contains("суппорт ")) {
                            Select("Суппорт тормозной" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("кулак ")) { Select("Кулак поворотный" + m); break; }
                        if (n.Contains("блок") && n.Contains("abs")) { Select("Блок ABS гидравлический" + m); break; }
                        if (n.Contains("балка ") || n.Contains("подрамник")) {
                            Select("Подрамник" + m); break;
                        }
                        if (n.Contains("диск") && (n.Contains("тормоз") || n.Contains("вент"))) {
                            Select("Диск тормозной" + m); break;
                        }
                        if (n.Contains("барабан") && (n.Contains("тормоз") || n.Contains("задн"))) {
                            Select("Барабан тормозной" + m); break;
                        }
                        if (n.Contains("пружина ") || n.Contains("пружины ")) {
                            Select("Пружина подвески" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("рычаг")) {
                            Select("Рычаг подвески" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("стабилизатор")) {
                            Select("Стабилизатор" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            break;
                        }
                        if (n.Contains("стойка ") || n.Contains("стойки")) {
                            Select("Стойка амортизатора" + m);
                            _dr.ButtonClick("//div[2]/fieldset[1]/div[1]/div/div/button");
                            SelectElement("Газо-масляный");
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[4]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("вакуумн")) { Select("Вакуумный усилитель тормозов" + m); break; }
                        if (n.Contains("главн") && n.Contains("тормоз") && n.Contains("цилин")) {
                            Select("Главный тормозной цилиндр" + m); break;
                        }
                        if (n.Contains("тяга ")) {
                            Select("Тяга подвески" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            break;
                        }
                        if (n.Contains("бачок") && n.Contains("тормоз") && n.Contains("цилин")) {
                            Select("Бачок главного тормозного цилиндра" + m); break;
                        }
                        if (n.Contains("ступица")) {
                            Select("Ступица колеса" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("шланг") && n.Contains("тормоз")) {
                            Select("Шланг тормозной" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("рем") && n.Contains("компл") && n.Contains("колод")) {
                            Select("Комплект монтажный колодок" + m); break;
                        }
                        if (n.Contains("рем") && n.Contains("компл") && n.Contains("ручн")) {
                            Select("Ремкомплект стояночного тормоза" + m); break;
                        }
                        if (n.Contains("скоб") && n.Contains("суппорт")) {
                            Select("Скоба суппорта" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("отбойник") && n.Contains("аморт")) {
                            Select("Отбойник амортизатора" + m); break;
                        }
                        if (n.Contains("колодк") && n.Contains("тормозн")) {
                            Select("Тормозные колодки" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("бараб")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[1]/div/div/button");
                                SelectElement("Барабанные");
                            }
                            break;
                        }
                        if ((n.Contains("регулят") || n.Contains("распределитель")) && n.Contains("тормоз")) {
                            Select("Тормозная система" + m); break;
                        }
                        Select("Подвеска" + m);
                        break;

                    case "Топливная, выхлопная система":
                        if (n.Contains("абсорбер ") || n.Contains("паров")) { Select("Абсорбер" + m); break; }
                        if (n.Contains("бак ")) { Select("Бак топливный" + m); break; }
                        if (n.Contains("коллектор ") && n.Contains("впуск")) { Select("Коллектор впускной" + m); break; }
                        if (n.Contains("коллектор ") && n.Contains("выпуск")) { Select("Коллектор выпускной" + m); break; }
                        if (n.Contains("заслонк") && n.Contains("дроссел")) { Select("Заслонка дроссельная" + m); break; }
                        if (n.Contains("насос") && n.Contains("топлив")) { Select("Топливный насос" + m); break; }
                        if (n.Contains("бачок") && n.Contains("вакуум")) { Select("Бачок вакуумный" + m); break; }
                        if (n.Contains("воздуховод") || n.Contains("воздухозаборник") || n.Contains("гофра ")
                            || n.Contains("патрубок ") && n.Contains("возд")) { Select("Воздухозаборник двигателя" + m); break; }
                        if (n.Contains("глушитель")) { Select("глушитель" + m); break; }
                        if (n.Contains("горловина") && n.Contains("бака")) { Select("Горловина топливного бака" + m); break; }
                        if (n.Contains("гофр") && n.Contains("площад") && n.Contains("ремон")) { Select("Гофра глушителя" + m); break; }
                        if (n.Contains("катализатор")) { Select("Катализатор" + m); break; }
                        if (n.Contains("клапан") && n.Contains("вакуумн")) { Select("Клапан абсорбера" + m); break; }
                        if (n.Contains("клапан") && n.Contains("бака")) { Select("Клапан вентиляционный бака" + m); break; }
                        if (n.Contains("клапан") && (n.Contains("егр") || n.Contains("egr"))) { Select("Клапан системы EGR" + m); break; }
                        if (n.Contains("клапан") && n.Contains("электр")) { Select("Клапан пневматический" + m); break; }
                        if (n.Contains("клапан") && n.Contains("холост")) { Select("Регулятор холостого хода" + m); break; }
                        if (n.Contains("корпус") && n.Contains("фильт")) { Select("Корпус воздушного фильтра" + m); break; }
                        if ((n.Contains("патрубок") || n.Contains("трубка")) && n.Contains("топл") || n.Contains("шланг")) {
                            Select("Шланг топливный" + m); break;
                        }
                        if ((n.Contains("патрубок") || n.Contains("трубка")) && (n.Contains("абсорб") || n.Contains("вакуум"))) {
                            Select("Трубка адсорбера" + m); break;
                        }
                        if ((n.Contains("патрубок") || n.Contains("трубка")) && n.Contains("картер")) {
                            Select("Патрубок вентиляции картерных газов" + m); break;
                        }
                        if (n.Contains("резонатор") && (n.Contains("воздуш") || n.Contains("филь"))) {
                            Select("Резонатор воздушного фильтра" + m); break;
                        }
                        if (n.Contains("резонатор ")) { Select("Резонатор" + m); break; }
                        if (n.Contains("форсунк") || n.Contains("моновпрыск")) { Select("Топливная форсунка" + m); break; }
                        if (n.Contains("сапун")) { Select("Сапун" + m); break; }
                        if (n.Contains("крышка") && n.Contains("бака")) { Select("Крышка топливного бака" + m); break; }
                        if (n.Contains("насос") && n.Contains("вакуум")) { Select("Вакуумный насос" + m); break; }
                        if (n.Contains("топливн")) { Select("Топливная система" + m); break; }
                        if (n.Contains("приемная") && n.Contains("труба")) { Select("Приемная труба" + m); break; }
                        if (n.Contains("регулятор") && n.Contains("топл")) { Select("Регулятор давления топлива" + m); break; }
                        if (n.Contains("ресивер")) { Select("Ресивер пневмосистемы" + m); break; }
                        if (n.Contains("труб") && n.Contains("турб")) { Select("Трубка турбокомпрессора" + m); break; }
                        if (n.Contains("труб") && (n.Contains("тнвд") || n.Contains("выс"))) {
                            Select("Трубка топливного насоса" + m); break;
                        }
                        if (n.Contains("турбина ")) { Select("Турбокомпрессор" + m); break; }
                        Select("Топливная система" + m);
                        break;

                    case "Электрика, зажигание":
                        if (n.Contains("актуатор") && n.Contains("замк")) { Select("Привод замка двери" + m); break; }
                        if (n.Contains("антенн")) { Select("Антенна" + m); break; }
                        if (n.Contains("блок") && n.Contains("airbag")) { Select("Блок управления подушки безопасности" + m); break; }
                        if (n.Contains("кноп") && n.Contains("рул")) { Select("Блок кнопок руля" + m); break; }
                        if (n.Contains("переключат") && n.Contains("подрул")) { Select("Подрулевой переключатель" + m); break; }
                        if (n.Contains("блок") && (n.Contains("комфорт") || n.Contains("климат"))) { Select("Блок комфорта" + m); break; }
                        if (n.Contains("блок") && n.Contains("предохранител")) { Select("Блок предохранителей аккумулятора" + m); break; }
                        if (n.Contains("корпус") && n.Contains("предохранител")) { Select("Корпус блока предохранителей" + m); break; }
                        if (n.Contains("блок") && (n.Contains("двс") || n.Contains("двигател"))) { Select("Блок управления ДВС" + m); break; }
                        if (n.Contains("блок") && n.Contains("акпп")) { Select("Блок управления АКПП" + m); break; }
                        if (n.Contains("катушк") && n.Contains("зажиг")) { Select("Катушка зажигания" + m); break; }
                        if (n.Contains("блок") && n.Contains("srs")) { Select("Блок SRS" + m); break; }
                        if (n.Contains("управления") && n.Contains("свет")) { Select("Блок управления светом" + m); break; }
                        if (n.Contains("управления") && n.Contains("стеклопод")) { Select("Блок переключателей подъемников" + m); break; }
                        if ((n.Contains("кноп") || n.Contains("выключател")) && n.Contains("стеклопод")) {
                            Select("Переключатель стеклоподъемника" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("мотор") && n.Contains("стеклопод")) {
                            Select("Мотор стеклоподъемника" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("выключатель") && n.Contains("стоп")) { Select("Концевик педали тормоза" + m); break; }
                        if (n.Contains("кнопк") && n.Contains("аварий")) { Select("Кнопка аварийной сигнализации" + m); break; }
                        if (n.Contains("кнопк")) { Select("Клипса многофункциональная" + m); break; }
                        if (n.Contains("трамбл") || n.Contains("аспределитель")) { Select("Трамблер" + m); break; }
                        if (n.Contains("замок") && n.Contains("зажиг")) { Select("Замок зажигания" + m); break; }
                        if (n.Contains("панель") && n.Contains("прибор")) { Select("Приборная панель" + m); break; }
                        if ((n.Contains("подушка") || n.Contains("шторк")) && n.Contains("безоп")) {
                            Select("Подушка безопасности" + m); break;
                        }
                        if (n.Contains("клаксон") || n.Contains("зуммер")) { Select("Сигнал звуковой в сборе" + m); break; }
                        if (n.Contains("жгут") || n.Contains("проводк") || n.Contains("кабел")) {
                            Select("Жгут проводки" + m); break;
                        }
                        if (n.Contains("корректор")) { Select("Корректор фар" + m); break; }
                        if (n.Contains("мотор")) { Select("Мотор стеклоочистителя" + m); break; }
                        if (n.Contains("реле")) { Select("Блок реле" + m); break; }
                        if (n.Contains("провод") && n.Contains("акб")) { Select("Провода аккумулятора" + m); break; }
                        if ((n.Contains("насос") || n.Contains("привод") || n.Contains("компрессор")) && n.Contains("зам")) {
                            Select("Компрессор центрального замка" + m); break;
                        }
                        if (n.Contains("резистор") || n.Contains("еостат")) { Select("Резистор отопителя" + m); break; }
                        if (n.Contains("патрон")) { Select("Патрон лампы" + m); break; }
                        if (n.Contains("коммутатор")) { Select("Коммутатор системы зажигания" + m); break; }
                        if (n.Contains("сирена")) { Select("Сирена сигнализации" + m); break; }
                        if (n.Contains("шлейф") && n.Contains("рул")) { Select("Шлейф подрулевой" + m); break; }
                        if (n.Contains("свеч") && n.Contains("накал")) { Select("Свеча накала" + m); break; }
                        if (n.Contains("блок") && (n.Contains("управления") || n.Contains("электронный"))) {
                            Select("Блок управления ДВС" + m); break;
                        }
                        if (n.Contains("парктрон")) { Select("Парковочная система" + m); break; }
                        if (n.Contains("разъем") || n.Contains("фишка")) { Select("Разъем" + m); break; }
                        if (n.Contains("круиз") || n.Contains("курсов")) { Select("Блок круиз-контроля" + m); break; }
                        if (n.Contains("насос") && n.Contains("омыв")) { Select("Насос стеклоомывателя" + m); break; }
                        if (n.Contains("клемма")) { Select("Клемма аккумулятора" + m); break; }
                        if (n.Contains("дворн")) { Select("Мотор стеклоочистителя" + m); break; }
                        if (n.Contains("форсун") && n.Contains("омыв")) { Select("Форсунка стеклоомывателя" + m); break; }
                        Select("Электрика и свет" + m);
                        break;

                    case "Салон":
                        if (n.Contains("педал") && n.Contains("сцеплен")) { Select("Педаль сцепления" + m); break; }
                        if (n.Contains("педал") && n.Contains("тормоз")) { Select("Педаль тормоза" + m); break; }
                        if ((n.Contains("педал") && n.Contains("узел")) || n.Contains("педали")) {
                            Select("Педальный узел" + m); break;
                        }
                        if (n.Contains("педал")) { Select("Педаль акселератора" + m); break; }
                        if (n.Contains("бардач")) { Select("Бардачок" + m); break; }
                        if (n.Contains("блок") && n.Contains("печк")) { Select("Блок управления отопителем" + m); break; }
                        if ((n.Contains("обшивк") || n.Contains("наклад")) && n.Contains("багажн")) {
                            Select("Обшивка багажника" + m); break;
                        }
                        if (n.Contains("полка")) { Select("Полка багажника" + m); break; }
                        if ((n.Contains("обшивк") || n.Contains("накл")) && n.Contains("двер")) {
                            Select("Обшивка двери" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("кожух") && n.Contains("рулев")) { Select("Кожух рулевой колонки" + m); break; }
                        if (n.Contains("козыр") && n.Contains("солнц")) { Select("Солнцезащитные козырьки" + m); break; }
                        if ((n.Contains("решет") || n.Contains("дефлек")) && n.Contains("возд") && _bus[b].price >= 800) {
                            Select("Дефлектор торпедо" + m); break;
                        }
                        if (n.Contains("пепел")) { Select("Пепельница" + m); break; }
                        if (n.Contains("воздух")) { Select("Воздуховод отопителя" + m); break; }
                        if (n.Contains("карман")) { Select("Карман двери" + m); break; }
                        if (n.Contains("ков")) { Select("Коврик багажника" + m); break; }
                        if (n.Contains("стойк")) { Select("Обшивка салона" + m); break; }
                        if (n.Contains("крыш") && n.Contains("предохр")) { Select("Корпус блока предохранителей" + m); break; }
                        if (n.Contains("порог")) { Select("Накладка порога" + m); break; }
                        if (n.Contains("ручк") && n.Contains("двер")) { Select("Ручка двери внутренняя" + m); break; }
                        if ((n.Contains("кожух") || n.Contains("обшив") || n.Contains("чехол") || n.Contains("крыш")) && n.Contains("рем")) {
                            Select("Кожух ремня безопасности" + m); break;
                        }
                        if ((n.Contains("регуля") || n.Contains("салаз")) && n.Contains("ремн")) {
                            Select("Направляющая ремня безопасности" + m); break;
                        }
                        if (n.Contains("натяж") && n.Contains("ремн")) { Select("Преднатяжитель ремня безопасности" + m); break; }
                        if ((n.Contains("обшив") || n.Contains("накл")) && (n.Contains("потол") || n.Contains("крыш"))) {
                            Select("Обшивка потолка" + m); break;
                        }
                        if ((n.Contains("ремень") || n.Contains("ремн")) && (n.Contains("безоп") || n.Contains("передн"))) {
                            Select("Ремень безопасности" + m); break;
                        }
                        if (n.Contains("ручк") && n.Contains("потол")) { Select("Ручка потолочная" + m); break; }
                        if (n.Contains("ручк")) { Select("Ручка двери внутренняя" + m); break; }
                        if ((n.Contains("накл") || n.Contains("стакан")) && n.Contains("торпедо")) {
                            Select("Накладка торпедо" + m); break;
                        }
                        if (n.Contains("торпедо") && _bus[b].price > 700) { Select("Основа торпедо" + m); break; }
                        if (n.Contains("магнитол")) { Select("Рамка автомагнитолы" + m); break; }
                        if (n.Contains("ручник ")) { Select("Рычаг стояночного тормоза" + m); break; }
                        if (n.Contains("сидень") && n.Contains("наклад") ||
                            (n.Contains("чехол") && n.Contains("салаз"))) { Select("Крышка крепления сиденья" + m); break; }
                        if (n.Contains("сидень")) { Select("Сиденья" + m); break; }
                        if (n.Contains("часы")) { Select("часы" + m); break; }
                        if (n.Contains("кулис")) { Select("Кулиса КПП" + m); break; }
                        if (n.Contains("подголовн")) {
                            Select("Подголовник" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            break;
                        }
                        if (n.Contains("подлокот")) { Select("Подлокотник" + m); break; }
                        if (n.Contains("ковр")) { Select("Коврики салонные" + m); break; }
                        if (n.Contains("стакан")) { Select("Подстаканник" + m); break; }
                        if (n.Contains("куриват")) { Select("Прикуриватель" + m); break; }
                        if (n.Contains("консол") || n.Contains("кожух") || ((n.Contains("наклад") || n.Contains("панел")) && n.Contains("ручник"))) {
                            Select("Консоль" + m); break;
                        }
                        Select("Накладки салонные" + m);
                        break;

                    case "Световые приборы транспорта":
                        if (n.Contains("катафот")) {
                            Select("Катафоты" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("плафон") || n.Contains("светильник")) { Select("Светильник салона" + m); break; }
                        if (n.Contains("поворотник") || n.Contains("повторитель")) {
                            Select("Указатель поворота" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("туман")) {
                            Select("Фара противотуманная" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("фара") && _bus[b].price > 600) {
                            Select("Фары в сборе" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            break;
                        }
                        if (n.Contains("фара")) { Select("Фары и комплектующие" + m); break; }
                        if ((n.Contains("фонарь") || n.Contains("подсвет")) && n.Contains("номер")) {
                            Select("Фонарь освещения номерного знака" + m); break;
                        }
                        if (n.Contains("стоп")) { Select("Стоп-сигнал" + m); break; }
                        if (n.Contains("фары") || n.Contains("стекло")) {
                            Select("Стекло фары" + m);
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("плата")) {
                            Select("Плата фонаря" + m);
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("фонарь")) {
                            Select("Фонарь задний" + m);
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        if (n.Contains("лампа") && n.Contains("ксенон")) { Select("Лампа ксеноновая" + m); break; }
                        if (n.Contains("патрон")) { Select("Патрон лампы" + m); break; }
                        Select("Электрика и свет" + m);
                        break;

                    case "Система отопления и кондиционирования":
                        if (n.Contains("воздуховод")) { Select("Воздуховод отопителя" + m); break; }
                        if (n.Contains("компрессор")) { Select("Компрессор кондиционера" + m); break; }
                        if ((n.Contains("корп") || n.Contains("крышк")) && n.Contains("фильтр")) { Select("Корпус воздушного фильтра" + m); break; }
                        if (n.Contains("корп") && (n.Contains("печк") || n.Contains("отопит"))) { Select("Корпус отопителя" + m); break; }
                        if (n.Contains("корп") && (n.Contains("мотор") || n.Contains("вентил"))) { Select("Корпус моторчика печки" + m); break; }
                        if (n.Contains("мотор") && n.Contains("заслон")) { Select("Привод заслонок отопителя" + m); break; }
                        if ((n.Contains("трубк") || n.Contains("шланг")) && n.Contains("кондиц")) { Select("Трубка кондиционера" + m); break; }
                        if ((n.Contains("мотор") || n.Contains("вентил")) && (n.Contains("печк") || n.Contains("отопит"))) {
                            Select("Мотор отопителя" + m); break;
                        }
                        if (n.Contains("радиатор") && (n.Contains("отопит") || n.Contains("печк"))) { Select("Радиатор отопителя" + m); break; }
                        if ((n.Contains("испарит") || n.Contains("осушит")) && n.Contains("кондиц")) { Select("Испаритель кондиционера" + m); break; }
                        if (n.Contains("радиат") && n.Contains("кондиц")) { Select("Радиатор кондиционера" + m); break; }
                        if (n.Contains("вентил") && n.Contains("кондиц")) { Select("Мотор охлаждения кондиционера" + m); break; }
                        if (n.Contains("труб")) { Select("Патрубок системы охлаждения" + m); break; }

                        Select("Кондиционер и отопитель" + m);
                        break;

                    case "Система охлаждения двигателя":
                        if (n.Contains("бачок") && n.Contains("расшир")) { Select("Расширительный бачок" + m); break; }
                        if (n.Contains("вентилятор ")) { Select("Вентилятор охлаждения радиатора" + m); break; }
                        if (n.Contains("вискомуфта")) { Select("Вискомуфта" + m); break; }
                        if (n.Contains("шкив") && n.Contains("помпы")) { Select("Шкив помпы" + m); break; }
                        if (n.Contains("помп") || (n.Contains("насос") && (n.Contains("вод") || n.Contains("охлажд")))) {
                            Select("Помпа водяная" + m); break;
                        }
                        if (n.Contains("корпус") && n.Contains("термост")) { Select("Корпус термостата" + m); break; }
                        if (n.Contains("насос") && n.Contains("масл")) { Select("Насос масляный" + m); break; }
                        if (n.Contains("труб") && n.Contains("масл")) { Select("Трубка масляная" + m); break; }
                        if (n.Contains("труб") || n.Contains("шланг")) { Select("Патрубок системы охлаждения" + m); break; }
                        if ((n.Contains("радиат") || n.Contains("охладит")) && n.Contains("масл")) {
                            Select("Радиатор масляный" + m); break;
                        }
                        if ((n.Contains("радиат") || n.Contains("теплооб")) && (n.Contains("охлажд") ||
                            n.Contains("двс")) || n.Contains("нтеркулер")) { Select("Радиатор двигателя" + m); break; }
                        if (n.Contains("крыльчат")) { Select("Крыльчатка вентилятора охлаждения" + m); break; }
                        if (n.Contains("мотор") && n.Contains("охлаж")) { Select("Мотор вентилятора охлаждения" + m); break; }
                        if (n.Contains("тройн") && n.Contains("охлаж")) { Select("Система охлаждения" + m); break; }
                        if (n.Contains("фланец")) { Select("Фланец системы охлаждения" + m); break; }
                        if (n.Contains("диффуз")) { Select("Диффузор" + m); break; }
                        if (n.Contains("щуп") && n.Contains("масл")) { Select("Щуп масляный" + m); break; }
                        Select("Система охлаждения" + m);
                        break;

                    case "Тросы автомобильные":
                        if (n.Contains("газ")) { Select("Тросик акселератора" + m); break; }
                        if (n.Contains("кпп")) { Select("Трос КПП" + m); break; }
                        if (n.Contains("капота")) { Select("Тросик замка капота" + m); break; }
                        if (n.Contains("ручн")) { Select("Трос стояночного тормоза" + m); break; }
                        if (n.Contains("багаж")) { Select("Трос открывания багажника" + m); break; }
                        if (n.Contains("сцепл")) { Select("Тросик сцепления" + m); break; }
                        if (n.Contains("печк")) { Select("Трос отопителя" + m); break; }
                        if (n.Contains("спидом")) { Select("Тросик спидометра" + m); break; }
                        if (n.Contains("замка")) {
                            Select("Тяга замка" + m);
                            if (n.Contains("задн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Сзади");
                            }
                            if (n.Contains("прав") || n.Contains(" r ") || n.EndsWith(" r")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Правая");
                            }
                            break;
                        }
                        Select("Трос подсоса" + m);
                        break;

                    case "Шины, диски, колеса":
                        if (n.Contains("ступ")) { Select("Крышка ступицы" + m); break; }
                        if (n.Contains("окатка")) { Select("Колесо запасное" + m); break; }
                        if (n.Contains("колпак") && _bus[b].price > 400) {
                            Select("Колпаки" + m);
                            if (n.Contains(" r1")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[1]/div/div/button");
                                var size = n.Split(' ')
                                            .Where(w => w.StartsWith("r"))
                                            .ToList()[0]
                                            .TrimStart('r');
                                SelectElement(size);
                            }
                            break;
                        }
                        if (n.Contains("колпак")) { Select("Комплектующие колёс" + m); break; }


                        if (n.Contains("диск")) {
                            Select("Колёсные диски" + m);
                            if (n.Contains(" r1")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[1]/div/div/button");
                                var size = n.Split(' ')
                                            .Where(w => w.StartsWith("r"))
                                            .ToList()[0]
                                            .TrimStart('r');
                                SelectElement(size);
                            }
                            if (_bus[b].description.ToLower().Contains("штамп")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Штампованный");
                            } else if (_bus[b].description.ToLower().Contains("литые")
                                  || _bus[b].description.ToLower().Contains("литой")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Литой");
                            } else if (_bus[b].description.ToLower().Contains("кован")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[2]/div/div/button");
                                SelectElement("Кованый");
                            }
                            break;
                        }
                        if (n.Contains("диск")) {
                            Select("Колёса в сборе" + m);
                            if (n.Contains(" r1")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[1]/div/div/button");
                                var size = n.Split(' ')
                                    .Where(w => w.StartsWith("r"))
                                    .ToList()[0]
                                    .TrimStart('r');
                                SelectElement(size);
                            }
                            if (_bus[b].description.ToLower().Contains("летн")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Летние");
                            } else if (_bus[b].description.ToLower().Contains("всесезон")) {
                                _dr.ButtonClick("//div[2]/fieldset[1]/div[3]/div/div/button");
                                SelectElement("Всесезонные");
                            }
                            break;
                        }
                        Select("Колёса" + m);
                        break;

                    default:
                        if (n.Contains("домкрат")) { Select("Домкрат" + m); break; }
                        Select(n + m);
                        break;
                }
            } catch (Exception x){
                throw new Exception("при выборе категории - " + x.Message);
            }

        }
    }
}
