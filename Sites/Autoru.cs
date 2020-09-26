using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Selen.Sites {
    class Autoru {

        private async void button_auto_get_Click(object sender, EventArgs e) {
            ChangeStatus(sender, ButtonStates.NoActive);
            Log.Add("парсим авто.ру...");
            //откроем браузер
            if (au == null) {
                ChromeDriverService chromeservice = ChromeDriverService.CreateDefaultService();
                chromeservice.HideCommandPromptWindow = true;
                au = new ChromeDriver(chromeservice);
                au.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
                au.Navigate().GoToUrl("https://auto.ru/404");
                LoadCookies(au, "auto.json");
            }
            try {
                await AutoAuthAsync();

                while (base_rescan_need || !button_base_get.Enabled) {
                    Log.Add("авто ожидает загрузку базы... ");
                    await Task.Delay(60000);
                }
                //выводим статистику
                var busCnt = bus.Count(c => c.auto.Length > 4);
                label_auto.Text = busCnt.ToString();
                Log.Add("авто.ру: ссылок в базе " + busCnt);
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
                                Log.Add("auto: ошибка удаления \n" + bus[b].name + "\n" + bus[b].auto);
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
                        Log.Add("авто удалено объявление - нет на остатках\n" + b + " " + bus[b].name);
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
                            Log.Add("Ошибка обновления auto.ru\n" + bus[b].name + "\n" + x.Message);
                        }
                    }
                    //catch
                    //{
                    //    //wasErrors = true;  //авто ру не будем пока учитывать
                    //    Log.Add("АВТО ОШИБКА ПРИ ВНЕСЕНИИ ИЗМЕНЕНИЙ! " + bus[b].name);
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
                        Log.Add("авто.ру ошибка парсинга сайта, страница " + i + "\n" + ex.Message);
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
                        Log.Add("авто привязано объявление " + a + " " + bus[iBus].name);
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
                //                        Log.Add("auto.ru удалена ссылка из базы " + (i+1) + "\n" + bus[b].name);
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
                                    Log.Add("auto.ru удалена ссылка из базы\n" + bus[b].name);
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
                Log.Add("auto.ru:ошибка загрузки страницы!/n" + x.Message);
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
                    var el = au.FindElements(By.CssSelector("i[class*='WhatsNew__close']"));
                    if (el.Count > 0) {
                        Actions a = new Actions(au);
                        a.MoveToElement(el.First()).Perform();
                        Thread.Sleep(1000);
                        a.Click().Perform();
                    }
                    //проверяю авторизацию, если ссылка содержит...
                    if (au.Url.Contains("parts.auto.ru")) {
                        //сохраняю куки
                        SaveCookies(au, "auto.json");
                    }
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
                Log.Add("авто.ру ошибка при выборе группы!");
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
                                        Log.Add("авто.ру: " + ex.ToString() + "\nошибка загрузки фото " + i + "\n" + bus[b].name + "\n" + bus[b].images[u].url);
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
                                    if (webElements.Count >= 1) {
                                        //TODO добавить проверку названия перед привязкой ссылки - var name = webElements[0].Text...
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
                                            Log.Add("авто добавлено новое объявление " + (newCount + 1) + "\n" + b + " " + bus[b].name);
                                            newCount++;
                                            break;
                                        }
                                    }
                                    await Task.Delay(5000);
                                } while (true);
                            } catch (Exception x) {
                                Log.Add("auto.ru ошибка при привязке объявления в базу\n" + bus[b].name + "\n" + x.Message);
                            }
                            label_auto.Text = bus.Count(c => c.auto.Length > 4).ToString();
                        } catch (Exception ex) {
                            Log.Add("авто.ру ошибка при добавлении объявления!\n" + ex.Message);
                            break;
                        }
                    }
                }
            } catch (Exception x) {
                Log.Add(x.Message + "\n\n" + x.InnerException.Message);
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
    }
}
