using Newtonsoft.Json;
using Selen.Base;
using Selen.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Selen.Sites {
    class Youla {
        Selenium _dr;               //браузер
        DB _db;                     //база данных
        string _url;                //ссылка в карточке товара     //402489
        string[] _addDesc;          //дополнительное описание
        List<RootObject> _bus;      //ссылка на товары
        Random _rnd = new Random(); //генератор случайных чисел
        //конструктор
        public Youla() {
            _db = DB._db;
        }
        //загрузка кукис
        public void LoadCookies() {
            if (_dr != null) {
                _dr.Navigate("https://youla.ru/");
                var c = _db.GetParamStr("youla.cookies");
                _dr.LoadCookies(c);
                Thread.Sleep(1000);
            }
        }
        //сохранение кукис
        public void SaveCookies() {
            if (_dr != null) {
                _dr.Navigate("https://youla.ru/");
                var c = _dr.SaveCookies();
                if (c.Length > 20)
                    _db.SetParam("youla.cookies", c);
            }
        }
        //закрытие браузера
        public void Quit() {
            _dr?.Quit();
            _dr = null;
        }
        //старт главного цикла синхронизации
        public async Task<bool> StartAsync(List<RootObject> bus) {
            if (await _db.GetParamBoolAsync("youla.syncEnable")) {
                Log.Add("youla.ru: начало выгрузки...");
                _bus = bus;
                for (int i = 0; ; i++) {
                    try {
                        await AuthAsync();
                        //await EditAsync();
                        //await AddAsync();
                        //await ParseAsync();
                        //await CheckUrls();
                        Log.Add("youla.ru: выгрузка завершена");
                        return true;
                    } catch (Exception x) {
                        Log.Add("youla.ru: ошибка синхронизации! - " + x.Message);
                        if (x.Message.Contains("timed out") ||
                        x.Message.Contains("already closed") ||
                        x.Message.Contains("invalid session id") ||
                        x.Message.Contains("chrome not reachable")) {
                            Log.Add("youla.ru: ошибка браузера! - " + x.Message);
                            _dr.Quit();
                            _dr = null;
                        }
                        if (i >= 2) break;
                        await Task.Delay(10000);
                    }
                }
            }
            return false;
        }
        //авторизация
        async Task AuthAsync() {
            await Task.Factory.StartNew(() => {
                _url = _db.GetParamStr("youla.url");
                _addDesc = JsonConvert.DeserializeObject<string[]>(
                    _db.GetParamStr("youla.addDescription"));
                if (_dr == null) {
                    _dr = new Selenium();
                    LoadCookies();
                }
                _dr.Navigate("https://youla.ru/user/5d96f7f822a4496dac117a69");
                _dr.ButtonClick("//div[@data-test-action='CloseClick']/i");
                //если есть кнопка входа - пытаюсь залогиниться
                if (_dr.GetElementsCount("//a[@href='/login' and @data-test-action='LoginClick']") > 0) {
                    _dr.ButtonClick("//a[@href='/login' and @data-test-action='LoginClick']");
                    var login = _db.GetParamStr("youla.login");
                    _dr.WriteToSelector("//input[@name='phone']", login);
                    //если время подходящее, запрос кода в смс
                    if (DateTime.Now.Hour > 8 && DateTime.Now.Hour < 22) {
                        _dr.ButtonClick("//button[@type='button' and @data-test-action='SubmitPhone']");
                    }
                    //ждем вход в кабинет
                    for (int i = 1; ; i++) {
                        //если есть кнопка Войти, значит в кабиет не попали
                        if (_dr.GetElementsCount("//a[@href='/login' and @data-test-action='LoginClick']")>0) {
                            Log.Add("youla.ru: ошибка авторизации в кабинете ("+i+")...");
                            Thread.Sleep(60000);
                        } else break;
                        if (i > 30) throw new Exception("timed out - ошибка! превышено время ожидания авторизации!");
                    }
                }
                SaveCookies();
            });
        }





    }

    //private async void button_youla_get_Click(object sender, EventArgs e) {
    //    button_youla_get.Enabled = false;
    //    button_youla_get.BackColor = Color.Yellow;
    //    await CheckYoulaAutorization();Ч
    //    while (base_rescan_need) {
    //        ToLog("юла ожидает загрузку базы... ");
    //        await Task.Delay(180000);
    //    }
    //    //проверяем объявления по ссылкам из базы
    //    await CheckYoulaBaseUrls();
    //    //выводим статистику         
    //    label_youla.Text = bus.Count(c => c.youla.Length > 4).ToString();
    //    button_youla_get.Enabled = true;
    //    button_youla_get.BackColor = Color.GreenYellow;
    //    button_youla_put.PerformClick();
    //}

    //async Task CheckYoulaAutorization() {
    //    if (ul == null) {
    //        ul = new ChromeDriver();
    //        ul.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
    //    }
    //    Task ts = Task.Factory.StartNew(() => {
    //        ul.Navigate().GoToUrl("https://youla.io/login");
    //        ul.FindElement(By.CssSelector("a[class*=button--vk]")).Click();
    //        Thread.Sleep(1500);
    //        ul.FindElement(By.CssSelector("input[name=email]"))
    //          .SendKeys("rad.i.g@list.ru");
    //        Thread.Sleep(1500);
    //        ul.FindElement(By.CssSelector("input[name=pass]"))
    //          .SendKeys("RAD00239000");
    //        ul.FindElement(By.CssSelector("#install_allow")).Click();
    //        Thread.Sleep(500);
    //    });
    //    try {
    //        await ts;
    //    }
    //    catch { }
    //}

    //public async Task CheckYoulaBaseUrls() {
    //    while (base_rescan_need) {
    //        ToLog("юла ожидает загрузку базы... ");
    //        await Task.Delay(90000);
    //    }
    //    //перебираем те карточки, у которых есть привязка в базе
    //    for (int b = 0; b < bus.Count; b++) {
    //        if (bus[b].youla.Contains("http")) {
    //            //удаляем те, которых нет на остатках
    //            if (bus[b].amount <= 0) {
    //                var t = Task.Factory.StartNew(() => {
    //                    ul.Navigate().GoToUrl(bus[b].youla);
    //                    if (ul.FindElements(By.CssSelector("#app > div.base > div > div > div > p"))
    //                            .Where(w => w.Text.Contains("объявление было удалено")).Count() == 0) {
    //                        PressYulaOkButton();
    //                        var butUdal = ul.FindElements(By.TagName("button"))
    //                            .Where(w => w.GetAttribute("class")
    //                                .Contains("product_card__delete"));
    //                        if (butUdal.Count() > 0)
    //                            foreach (var but in butUdal) {
    //                                but.Click();
    //                                var tmp = ul.FindElements(By.TagName("button"))
    //                                    .Where(w => w.Text == "Удалить")
    //                                    .ToList()[0];
    //                                var a = new Actions(ul);
    //                                a.MoveToElement(tmp).Perform();
    //                                Thread.Sleep(100);
    //                                a.Click().Perform();
    //                                Thread.Sleep(1000);
    //                                break;
    //                            }
    //                        else {
    //                            //опубликовать
    //                            ul.FindElement(By.CssSelector(
    //                                    "div.product_buttons_container._product_action_buttons > div > div > div:nth-child(1) > button"))
    //                                .Click();
    //                            Thread.Sleep(1000);
    //                            ul.FindElement(By.CssSelector(
    //                                    "div.buttons_wrapper.buttons_wrapper--full.text-center > div:nth-child(3) > button"))
    //                                .Click();
    //                            Thread.Sleep(1000);

    //                            //кнопка удалить
    //                            ul.FindElement(By.CssSelector(
    //                                    "div:nth-child(3) > div > div > div:nth-child(1) > div > div > button"))
    //                                .Click();
    //                            Thread.Sleep(1000);
    //                            ul.FindElement(By.CssSelector("div.buttons_wrapper > div:nth-child(2) > button"))
    //                                .Click();
    //                        }
    //                    }

    //                });


    //                try {
    //                    await t;
    //                }
    //                catch (Exception x) { ToLog("юла: " + x.Message); }

    //                //убиваем ссылку из базы в любом случае, потому что удалим физически, проверив отстатки после парсинга и подъема неактивных
    //                await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>
    //                {
    //                    {"id", bus[b].id},
    //                    {"name", bus[b].name},
    //                    {"402489", " "}
    //                });
    //                ToLog("Юла - удалена ссылка из карточки\n" + bus[b].name);
    //                bus[b].youla = " ";
    //                Thread.Sleep(1000);
    //            }
    //            else if (bus[b].price > 0 && bus[b].IsTimeUpDated()) {// редактируем

    //                var t = Task.Factory.StartNew(() => {
    //                    ul.Navigate().GoToUrl(bus[b].youla);
    //                    SetYoulaTitle(b);
    //                    Thread.Sleep(1000);
    //                    SetYoulaPrice(b);
    //                    Thread.Sleep(1000);
    //                    SetYoulaDesc(b);
    //                    Thread.Sleep(1000);
    //                    PressYulaOkButton();
    //                    Thread.Sleep(1000);
    //                });
    //                try {
    //                    await t;
    //                }
    //                catch (Exception e) {
    //                    ToLog("юла: " + e.Message);
    //                }
    //            }
    //        }
    //    }
    //}

    ////поднимаем юлу
    //async void button_youla_put_Click(object sender, EventArgs e) {
    //    button_youla_put.Enabled = false;
    //    button_youla_put.BackColor = Color.Yellow;
    //    if (checkBox_youlaUp.Checked) {
    //        while (base_rescan_need) {
    //            ToLog("юла ожидает загрузку базы... ");
    //            await Task.Delay(180000);
    //        }
    //        var t = Task.Factory.StartNew(() => {
    //            ul.Navigate().GoToUrl("https://youla.io/user/59954d65bd36c094f65f7072/archive");
    //            //собираем ссылки со всех неактивных объяв на странице
    //            var urls = ul.FindElements(By.CssSelector("section > div > ul > li > a"))
    //                         .Select(s => s.GetAttribute("href"))
    //                         .ToList();
    //            //проверяем каждую - поднять или удалить
    //            foreach (var url in urls) {
    //                var b = bus.FindIndex(f => f.youla.Contains(url.Split('-').Last()));
    //                //если найден индекс в базе, есть остатки и цена - поднимаем
    //                if (b > -1 && bus[b].amount > 0 && bus[b].price > 0) {
    //                    try {
    //                        ul.Navigate().GoToUrl(url);
    //                        Thread.Sleep(1000);
    //                        //нажмем кнопку, содержащую слово опубликовать
    //                        ul.FindElements(By.CssSelector("._product_action_buttons > div > div > div > button"))
    //                            .Where(w => w.FindElements(By.TagName("span")).Where(w_ => w_.Text.Contains("Опубликовать")).Count() > 0)
    //                            .ToList()[0]
    //                            .Click();
    //                        Thread.Sleep(2000);
    //                    }
    //                    catch (Exception x) {
    //                        ToLog("button_youla_put_Click: " + x.Message);
    //                    }
    //                }
    //                //иначе удаляем ненужные обявления
    //                else {
    //                    try {
    //                        ul.Navigate().GoToUrl(url);
    //                        Thread.Sleep(1000);
    //                        //кнопка удалить
    //                        ul.FindElement(By.CssSelector("div:nth-child(3) > div > div > div:nth-child(1) > div > div > button")).Click();
    //                        Thread.Sleep(1000);
    //                        ul.FindElement(By.CssSelector("div.buttons_wrapper > div:nth-child(2) > button")).Click();
    //                        Thread.Sleep(1000);
    //                    }
    //                    catch (Exception x) {
    //                        ToLog("button_youla_put_Click: " + x.Message);
    //                    }
    //                }

    //            }
    //        });
    //        try {
    //            await t;
    //        }
    //        catch (Exception x) {
    //            ToLog("button_youla_put_Click: " + x.Message);
    //        }
    //    }

    //    button_youla_put.Enabled = true;
    //    button_youla_put.BackColor = Color.GreenYellow;
    //    button_youla_add.PerformClick();
    //}

    ////добавляем юлу
    //private async void button_youla_add_Click(object sender, EventArgs e) {
    //    button_youla_add.Enabled = false;
    //    button_youla_add.BackColor = Color.Yellow;
    //    //подождем обработку базы
    //    while (base_rescan_need) {
    //        ToLog("юла ожидает загрузку базы... ");
    //        await Task.Delay(30000);
    //    }
    //    var bak = numericUpDown_youla.Value;
    //    for (int b = 0; b < bus.Count; b++) {
    //        if (numericUpDown_youla.Value <= 0) break;

    //        if (bus[b].tiu.Contains("http") &&
    //            bus[b].amount > 0 &&
    //            bus[b].price > 0 &&
    //            bus[b].images.Count > 0 &&
    //           !bus[b].youla.Contains("http")) {
    //            var add = Task.Factory.StartNew(() => {
    //                Actions a = new Actions(ul);
    //                //кнопка подать объявление
    //                ul.FindElement(By.CssSelector("div.header_bar__add._header_add_container.hidden-tablet > button")).Click();
    //                Thread.Sleep(1000);
    //                //категория                       
    //                SetYoulaCategory(b, a);
    //                Thread.Sleep(1000);
    //                //тип
    //                SetYoulaType(b, ul.FindElement(By.ClassName("Select-placeholder")));
    //                Thread.Sleep(1000);
    //                SetYoulaTitle(b);
    //                Thread.Sleep(1000);
    //                SetYoulaPrice(b);
    //                Thread.Sleep(1000);
    //                SetYoulaDesc(b);
    //                Thread.Sleep(1000);
    //                SetYulaImages(b);
    //                Thread.Sleep(1000);
    //                SetYulaAddress();
    //                Thread.Sleep(1000);
    //                PressYulaOkButton();
    //                Thread.Sleep(5000);

    //            });
    //            try {
    //                await add;

    //                string youId = ul.Url;

    //                //string youId = ul.FindElement(By.CssSelector(
    //                //                    "div.page_title.page_title--main.visible-md.visible-lg > p > a:nth-child(1)"))
    //                //                 .GetAttribute("href");

    //                if (!String.IsNullOrEmpty(youId) && youId.Contains("promotion")) {
    //                    bus[b].youla = youId.Replace("promotion", "update");
    //                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>
    //                    {
    //                        {"id", bus[b].id},
    //                        {"name", bus[b].name},
    //                        {"402489", bus[b].youla}
    //                    });

    //                    ToLog("юла выложено и привязано объявление\n" + b + " " + bus[b].name);
    //                    numericUpDown_youla.Value--;
    //                }
    //                else {
    //                    ToLog("ЮЛА ОШИБКА ПРИВЯЗКИ НОВОГО ОБЪЯВЛЕНИЯ! " + bus[b].name);
    //                    numericUpDown_youla.Value = 0;
    //                    bak = 0;
    //                }

    //            }
    //            catch (Exception ex) {
    //                ToLog("button_youla_add_Click: " + ex.Message);
    //                numericUpDown_youla.Value = 0;
    //                bak = 0;
    //            }
    //        }
    //    }
    //    numericUpDown_youla.Value = bak;

    //    button_youla_add.Enabled = true;
    //    button_youla_add.BackColor = Color.GreenYellow;
    //}

    //////выбор категории
    ////public void SetYoulaCategory()
    ////{

    ////}

    ////выбор подкатегории юлы
    //public void SetYoulaCategory(int b, Actions a) {
    //    int busGrInd = busGr.FindIndex(t => t.id == bus[b].group_id);
    //    switch (busGr[busGrInd].name) {
    //        case "Аудио-видеотехника":
    //            ul.FindElements(By.XPath("//div/div/div/div/div/div/div/div/div"))
    //              .Where(w => w.Text == "Аудио и видео")
    //              .ToList()[0].Click();
    //            break;
    //        case "Шины, диски, колеса":
    //            if (bus[b].name.ToLower().Contains("диск"))
    //                ul.FindElements(By.XPath("//div/div/div/div/div/div/div/div/div"))
    //                  .Where(w => w.Text == "Диски")
    //                  .ToList()[0].Click();
    //            else if (bus[b].name.ToLower().Contains("колесо"))
    //                ul.FindElements(By.XPath("//div/div/div/div/div/div/div/div/div"))
    //                  .Where(w => w.Text == "Колеса, колпаки и камеры")
    //                  .ToList()[0].Click();
    //            else if (bus[b].name.ToLower().Contains("резина"))
    //                ul.FindElements(By.XPath("//div/div/div/div/div/div/div/div/div"))
    //                    .Where(w => w.Text == "Шины")
    //                    .ToList()[0].Click();
    //            break;

    //        case "Автохимия":
    //            ul.FindElements(By.XPath("//div/div/div/div/div/div/div/div/div"))
    //              .Where(w => w.Text == "Масла и автохимия")
    //              .ToList()[0].Click();
    //            break;
    //        default:
    //            ul.FindElements(By.XPath("//div/div/div/div/div/div/div/div/div"))
    //              .Where(w => w.Text == "Запчасти")
    //              .ToList()[0].Click();
    //            break;
    //    }
    //    //Запчасти
    //    //            if (busGr[busGrInd].name == ) needToPress = 5;
    //    //if (bus[b].name.ToLower().Contains("диск") && busGr[busGrInd].name == "Шины, диски, колеса") needToPress = 10;
    //    //if (bus[b].name.ToLower().Contains("колесо") && busGr[busGrInd].name == "Шины, диски, колеса") needToPress = 9;
    //    //if (busGr[busGrInd].name == "Автохимия") needToPress = 8;
    //    //if (busGr[busGrInd].name == "Корневая группа") needToPress = 0;
    //    //int needToPress = 4;//запчасти



    //    //for (int i = 0; i < needToPress; i++)
    //    //{
    //    //    a.SendKeys(OpenQA.Selenium.Keys.ArrowUp);//.Perform();
    //    //    //Thread.Sleep(1000);
    //    //}
    //    //a.SendKeys(OpenQA.Selenium.Keys.Enter).Perform();
    //}

    ////кликнуть в элемент


    ////тип запчасти юла
    //public void SetYoulaType(int b, /*Actions a*/ IWebElement we) {
    //    we.Click();
    //    int busGrInd = busGr.FindIndex(t => t.id == bus[b].group_id);
    //    int needToPress = 0;
    //    var bn = bus[b].name.ToLower();
    //    switch (busGr[busGrInd].name) {
    //        case "Пластик кузова":
    //        case "Ручки и замки кузова":
    //        case "Петли":
    //        case "Кузовные запчасти":
    //            needToPress = 7;
    //            break;
    //        case "Топливная, выхлопная система":
    //            if (bn.Contains("выпускной") ||
    //                bn.Contains("глушител") ||
    //                bn.Contains("егр ") ||
    //                bn.Contains("резонатор") ||
    //                bn.Contains("площадка") ||
    //                bn.Contains("приемная ")) {
    //                needToPress = 4;
    //            }
    //            else {
    //                needToPress = 5;
    //            }
    //            break;
    //        case "Трансмиссия и привод":
    //            needToPress = 14;
    //            break;
    //        case "Электрика, зажигание":
    //            if (bn.Contains("замок") && bn.Contains("зажиган") ||
    //                bn.Contains("катушк") && bn.Contains("зажиг") ||
    //                bn.Contains("коммутатор") ||
    //                bn.Contains("трамбл")) {
    //                needToPress = 3;
    //            }
    //            else {
    //                needToPress = 15;
    //            }
    //            break;
    //        case "Подвеска и тормозная система":
    //            if (bn.Contains("барабан") ||
    //                bn.Contains("тормоз") ||
    //                bn.Contains("abs ") ||
    //                bn.Contains("диск ") ||
    //                bn.Contains("ручник") ||
    //                bn.Contains("суппорт") ||
    //                bn.Contains("вакуум") ||
    //                bn.Contains("колод")) {
    //                needToPress = 6;
    //            }
    //            else {
    //                needToPress = 10;
    //            }
    //            break;
    //        case "Салон": needToPress = 9; break;
    //        case "Рулевые рейки, рулевое управление": needToPress = 8; break;
    //        case "Система отопления и кондиционирования": needToPress = 12; break;
    //        case "Система охлаждения двигателя": needToPress = 12; break;
    //        case "Зеркала": needToPress = 7; break;
    //        case "Двигатели": needToPress = 3; break;
    //        case "Датчики": needToPress = 15; break;
    //        case "Генераторы": needToPress = 15; break;
    //        case "Аудио-видеотехника":
    //            if (bn.Contains("динамик")) {
    //                needToPress = 3;
    //            }
    //            else if (bn.Contains("магнитола")) {
    //                needToPress = 4;
    //            }
    //            else if (bn.Contains("рамка") || bn.Contains("панель")) {
    //                needToPress = 9;
    //            }
    //            break;
    //        case "Шины, диски, колеса":
    //            if (bn.Contains("колесо")) {
    //                needToPress = 1;
    //            }
    //            break;
    //        case "Автостекло": needToPress = 13; break;
    //        case "Световые приборы транспорта": needToPress = 1; break;
    //        case "Кронштейны, опоры": needToPress = 7; break;
    //        case "Стартеры": needToPress = 15; break;
    //        case "Тросы автомобильные":
    //            if (bn.Contains("акпп ") || bn.Contains("газа") || bn.Contains("спидом") || bn.Contains("сцепл")) {
    //                needToPress = 14;
    //            }
    //            else if (bn.Contains("багажника") || bn.Contains("двери") || bn.Contains("капот")) {
    //                needToPress = 7;
    //            }
    //            else if (bn.Contains("печк") || bn.Contains("панель")) {
    //                needToPress = 12;
    //            }
    //            else if (bn.Contains("ручник") || bn.Contains("тормоз")) {
    //                needToPress = 6;
    //            }
    //            break;
    //        case "Автохимия":
    //            if (bn.Contains("паст") && bn.Contains("очищающ")) {
    //                needToPress = 15;
    //            }
    //            break;

    //    }
    //    Actions a = new Actions(ul);
    //    a.MoveToElement(we);
    //    a.Click();
    //    if (needToPress > 0) {
    //        for (int i = 0; i < needToPress; i++) {
    //            a.SendKeys(OpenQA.Selenium.Keys.ArrowDown);
    //            //Thread.Sleep(1000);
    //        }
    //        a.SendKeys(OpenQA.Selenium.Keys.Enter).Perform();
    //        //вид транспорта
    //        if (!(busGr[busGrInd].name.Contains("Аудио-видеотехника") ||
    //              busGr[busGrInd].name.Contains("Автохимия"))) {
    //            SetYoulaVid(b, ul.FindElement(By.ClassName("Select-placeholder")));
    //            Thread.Sleep(1000);
    //        }
    //    }
    //}

    ////вид транспорта
    //public void SetYoulaVid(int b, IWebElement we) {
    //    we.Click();
    //    Actions a = new Actions(ul);
    //    a.SendKeys(OpenQA.Selenium.Keys.ArrowDown);
    //    a.SendKeys(OpenQA.Selenium.Keys.ArrowUp);
    //    a.SendKeys(OpenQA.Selenium.Keys.Enter).Perform();
    //}

    ////пишем заголовок
    //public void SetYoulaTitle(int b) {
    //    var name = bus[b].name;
    //    while (name.Length > 50) {
    //        name = name.Remove(name.LastIndexOf(' '));
    //    }
    //    foreach (var el in ul.FindElements(By.TagName("input"))) {
    //        if (el.GetAttribute("name").Contains("name")) {
    //            WriteToIWebElement(ul, el, name);
    //            break;
    //        }
    //    }
    //}
    ////пишем цену
    //public void SetYoulaPrice(int b) {
    //    foreach (var el in ul.FindElements(By.TagName("input"))) {
    //        if (el.GetAttribute("name").Contains("price")) {
    //            WriteToIWebElement(ul, el, bus[b].price.ToString());
    //            //el.SendKeys(bus[b].price.ToString());
    //            break;
    //        }
    //    }
    //}
    ////пишем описание
    //public void SetYoulaDesc(int b) {
    //    //готовим описание
    //    var s = Regex.Replace(bus[b].description
    //                .Replace("Есть и другие", "|")
    //                .Split('|')[0]
    //                .Replace("\n", "|")
    //                .Replace("<br />", "|")
    //                .Replace("<br>", "|")
    //                .Replace("</p>", "|")
    //                .Replace("&nbsp;", " ")
    //                .Replace("&quot;", "")
    //                .Replace("&gt;", "")
    //                .Replace(" &gt", ""),
    //            "<[^>]+>", string.Empty)
    //        .Split(new[] { "|" }, StringSplitOptions.RemoveEmptyEntries)
    //        .Select(ta => ta.Trim())
    //        .Where(tb => tb.Length > 1)
    //        .ToList();
    //    if (bus[b].IsGroupValid()) {
    //        s.AddRange(dopDescYoula);
    //    }
    //    //контролируем длину описания
    //    var newSLength = 0;
    //    for (int u = 0; u < s.Count; u++) {
    //        if (s[u].Length + newSLength > 2998) {
    //            s.Remove(s[u]);
    //            u = u - 1;
    //        }
    //        else {
    //            newSLength = newSLength + s[u].Length;
    //        }
    //    }
    //    //заполняем
    //    foreach (var el in ul.FindElements(By.TagName("textarea"))) {
    //        if (el.GetAttribute("name").Contains("description")) {
    //            WriteToIWebElement(ul, el, sl: s);
    //            break;
    //        }
    //    }
    //}

    ////грузим картинки на юлу из базы
    //public void SetYulaImages(int b) {
    //    System.Net.WebClient cl = new System.Net.WebClient();
    //    cl.Encoding = Encoding.UTF8;
    //    var num = bus[b].images.Count > 4 ? 4 : bus[b].images.Count;
    //    for (int u = 0; u < num; u++) {
    //        bool flag; // флаг для проверки успешности
    //        do {
    //            try {
    //                byte[] bts = cl.DownloadData(bus[b].images[u].url);
    //                System.IO.File.WriteAllBytes("youla_" + u + ".jpg", bts);
    //                flag = true;
    //            }
    //            catch (Exception ex) {
    //                ToLog("button_youla_add_Click: " + ex.ToString() + "\nошибка загрузки фото " + bus[b].images[u].url);
    //                flag = false;
    //            }
    //            Thread.Sleep(6000);
    //        } while (!flag);
    //        //отправим фото на сайт
    //        foreach (var fe in ul.FindElements(By.TagName("input"))) {
    //            if (fe.GetAttribute("type").Contains("file")) {
    //                fe.SendKeys(Application.StartupPath + "\\" + "youla_" + u + ".jpg");
    //                break;
    //            }
    //        }
    //    }
    //}
    ////пишем адрес
    //public void SetYulaAddress() {
    //    Actions a = new Actions(ul);
    //    foreach (var el in ul.FindElements(By.XPath("//form/div[2]/div/button/span/span"))) {
    //        a.MoveToElement(el).Build().Perform();
    //        break;
    //    }
    //    foreach (var el in ul.FindElements(By.TagName("input"))) {
    //        if (el.GetAttribute("name").Contains("location")) {
    //            a.MoveToElement(el, offsetX: 0, offsetY: 350).Build().Perform();
    //            WriteToIWebElement(ul, el, "Калуга, Россия Московская улица, 331");
    //            //el.SendKeys("Калуга, Калужская область, Россия Московская улица, 331");
    //            Thread.Sleep(1000);
    //            a.SendKeys(OpenQA.Selenium.Keys.ArrowDown)
    //                .SendKeys(OpenQA.Selenium.Keys.Enter)
    //                .Build()
    //                .Perform();
    //            //a.MoveToElement(el, offsetX:0,offsetY:50).Click().Build().Perform();
    //            //a.Click();
    //            //a.SendKeys(OpenQA.Selenium.Keys.ArrowDown);
    //            Thread.Sleep(1000);
    //            //a.SendKeys(OpenQA.Selenium.Keys.Enter).Perform();
    //            break;
    //        }
    //    }
    //}
    ////жмем далее
    //public void PressYulaOkButton() {
    //    try {
    //        foreach (var el in ul.FindElements(By.TagName("button"))) {
    //            if (el.GetAttribute("type").Contains("submit")) {
    //                el.Click();
    //                break;
    //            }
    //        }
    //        Thread.Sleep(1000);
    //        foreach (var bu in ul.FindElements(By.TagName("button"))) {
    //            if (bu.GetAttribute("class").Contains("button_submit")) {
    //                bu.Click();
    //                break;
    //            }
    //        }
    //    }
    //    catch { }
    //}
    ////проверка ссылок на юлу
    //private async void button_YoulaCheck_Click(object sender, EventArgs e) {
    //    button_YoulaCheck.BackColor = Color.Yellow;
    //    button_YoulaCheck.Enabled = false;

    //    try {
    //        ul.Navigate().GoToUrl("https://youla.ru/user/59954d65bd36c094f65f7072/archive");
    //        var pages = 1 + Convert.ToInt32(ul.FindElement(By.CssSelector("a[href*='archive'] span[class='tab_item_count']")).Text) / 40;
    //        var num = 0;
    //        //спарсим 
    //        for (int i = 0; i < pages; i++) {
    //            if (num >= 30 || base_rescan_need) break;
    //            ul.Navigate().GoToUrl("https://youla.ru/user/59954d65bd36c094f65f7072/archive?page=" + (i + 1));
    //            await Task.Delay(3000);
    //            var urls = ul.FindElements(By.CssSelector(".product_item a"))
    //                .Select(s => s.GetAttribute("href")).ToList();
    //            foreach (var url in urls) {
    //                if (num > 50 || base_rescan_need) break;
    //                var id = url.Split('-').Last();
    //                var b = bus.FindIndex(f => f.youla.Contains(id));
    //                if (b > -1 && (bus[b].price < 6000 || bus[b].amount <= 0)) {
    //                    bus[b].youla = "";
    //                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string> {
    //                        {"id", bus[b].id},
    //                        {"name", bus[b].name},
    //                        {"402489", bus[b].youla}
    //                    });
    //                    num++;
    //                    ToLog(num + " удалено объявление с юлы!\n" + bus[b].name);
    //                    b = -1;
    //                }
    //                if (b == -1) {
    //                    ul.Navigate().GoToUrl(url);
    //                    await Task.Delay(3000);
    //                    ul.FindElement(By.CssSelector("button[data-test-action='ProductDeleteClick']"))
    //                      .Click();
    //                    await Task.Delay(3000);
    //                    foreach (var but in ul.FindElements(By.CssSelector("div[data-test-component='ConfirmModal'] button"))) {
    //                        if (but.FindElement(By.CssSelector("span")).Text.Contains("Удалить")) {
    //                            but.Click();
    //                        }
    //                    }
    //                    await Task.Delay(3000);
    //                }
    //            }
    //        }
    //    }
    //    catch (Exception x) {
    //        ToLog(x.Message);
    //    }
    //    button_YoulaCheck.BackColor = Color.GreenYellow;
    //    button_YoulaCheck.Enabled = true;
    //}
}
