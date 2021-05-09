using Selen.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Selen.Sites {
    class Tiu {
        Selenium _dr;
        string tiuXmlUrl = "http://xn--80aejmkqfc6ab8a1b.xn--p1ai/yandex_market.xml?html_description=1&hash_tag=3f22d0f72761b35f77efeaffe7f4bcbe&yandex_cpa=0&group_ids=&exclude_fields=&sales_notes=&product_ids=";

        string dopDescTiu = "Дополнительные фотографии по запросу<br />" +
                            "Есть и другие запчасти на данный автомобиль!<br />" +
                            "Вы можете установить данную деталь на нашем АвтоСервисе с доп.гарантией и со скидкой!<br />" +
                            "Перед выездом обязательно уточняйте наличие запчастей по телефону - товар на складе!<br />" +
                            "Отправляем наложенным платежом ТК СДЭК, ПОЧТА, BOXBERRY (только негабаритные посылки),<br />" +
                            "Наложенный платёж ТК ПЭК (габаритные и тяжёлые детали).<br />" +
                            "Бесплатная доставка до транспортной компании!";

        string[] dopDescTiu2 = {
            "Дополнительные фотографии по запросу",
            "Есть и другие запчасти на данный автомобиль!",
            "Вы можете установить данную деталь на нашем АвтоСервисе с доп.гарантией и со скидкой!",
            "Перед выездом обязательно уточняйте наличие запчастей по телефону - товар на складе!",
            "Отправляем наложенным платежом ТК СДЭК, ПОЧТА, BOXBERRY (только негабаритные посылки)",
            "Наложенный платёж ТК ПЭК (габаритные и тяжёлые детали)",
            "Бесплатная доставка до транспортной компании!"
        };

        //главный метод
        public async Task TiuSyncAsync() {
            await TiuHide();
            await TiuMovePricelessToDrafts();
            await AddPhotosToBaseAsync();
            await TiuUploadAsync();
            await AddSupplyAsync();
            await TiuSyncAsync2();
        }
        //перемещаю товары без цен с витрины в черновики
        private async Task TiuMovePricelessToDrafts() {
            try {
                await Task.Factory.StartNew(() => {
                    //запрашиваю опубликованные товары без цены
                    _dr.Navigate().GoToUrl("https://my.tiu.ru/cms/product?search_term=&page=1&per_page=20&status=0&price=without_price");
                    Thread.Sleep(1000);
                    //ставлю галочку выделить все элементы
                    _dr.FindElement(By.CssSelector("input[data-qaid='select_all_chbx']")).Click();
                    Thread.Sleep(2000);
                    //жму Выбор действия
                    _dr.FindElement(By.CssSelector("span[data-qaid='selector_button']")).Click();
                    Thread.Sleep(3000);
                    //жму Изменить видимость
                    var c = _dr.FindElements(By.XPath("//div/span[text()='Изменить видимость']/..")).First();
                    Actions a = new Actions(_dr);
                    a.MoveToElement(c).Perform();
                    Thread.Sleep(1000);
                    a.Click().Perform();
                    Thread.Sleep(1000);
                    //жму Черновики
                    c = _dr.FindElements(By.XPath("//div[text()='Черновики']")).First();
                    a.MoveToElement(c).Perform();
                    Thread.Sleep(1000);
                    c.Click();
                    Thread.Sleep(2000);
                });
            } catch (Exception x) {
                //Log.Add("tiu.ru: ошибка при перемещении товаров без цен в черновики - "+ x.Message);
            }
        }
        //выгрузка на тиу
        async Task TiuUploadAsync() {
            try {
                for (int i = 0; ; i++) {
                    try {
                        newTiuGoods.Clear();
                        await Task.Factory.StartNew(() => {
                            tiuCount = 0;
                            ds.Clear();
                            Log.Add("tiu.ru: запрашиваю каталог xml...");
                            ds.ReadXml(tiuXmlUrl);
                            Log.Add("tiu.ru: каталог получен");
                        });
                        break;
                    } catch (Exception x) {
                        if (i > 9) {
                            Log.Add("tiu.ru: ошибка запроса XML! - " + x.Message);
                            return;
                        }
                    }
                }
                if (DateTime.Now.Hour < 8) { //TODO tiu.ru - добавить в settings время последней отправки файла и время когда отправлять
                    tiuCount = ds.Tables["offer"].Rows.Count;
                    label_tiu.Text = tiuCount.ToString();
                    Log.Add("tiu.ru: получено объявлений " + tiuCount.ToString());
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
                    Workbook wb = Workbook.Load(_fexp);
                    Worksheet ws = wb.Worksheets[0];
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
                            _dr.Quit();
                            _dr = null;
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
        //проверка окна с рекламой
        private void TiuCheckPopup() {
            var el = _dr.FindElements(By.XPath("//div[contains(@class,'sliding-panel__close-btn')]"));
            if (el.Count > 0)
                el.First().Click();
        }
        //редактирование позиции
        private void TiuOfferUpdate(int b) {
            _dr.Navigate().GoToUrl(bus[b].tiu);
            TiuCheckPopup();
            var status = _dr.FindElement(By.CssSelector(".b-product-edit__partition .b-product-edit__partition div.js-toggle"));
            var curStatus = status.FindElement(By.CssSelector("span span")).Text;
            if (bus[b].amount > 0) {
                var displayStatus = _dr.FindElement(By.XPath("//div[@data-qaid='visibility_block']/div[1]//input"));//радио батн опубликован
                displayStatus.Click();
                if (curStatus != "В наличии") {
                    status.Click();
                    Thread.Sleep(3000);
                    var newStatus = _dr.FindElements(By.CssSelector("div[data-qaid='presence_stock_block'] .b-drop-down__list-item"))
                                       .Where(w => w.Text == "В наличии").First();
                    newStatus.Click();
                }
            }
            if (bus[b].amount <= 0) {
                var displayStatus = _dr.FindElement(By.XPath("//div[@data-qaid='visibility_block']/div[3]//input"));//радио батн скрытый
                displayStatus.Click();
                if (curStatus != "Нет в наличии") {
                    status.Click();
                    Thread.Sleep(3000);
                    var newStatus = _dr.FindElements(By.CssSelector("div[data-qaid='presence_stock_block'] .b-drop-down__list-item"))
                                       .Where(w => w.Text == "Нет в наличии").First();
                    newStatus.Click();
                }
            }
            Thread.Sleep(1000);
            WriteToIWebElement(_dr, _dr.FindElement(By.CssSelector("input[data-qaid='product_price_input']")), bus[b].price.ToString());
            //если нулевое количество 0 не пишем, просто удаляем что там указано
            var cnt = Math.Round(bus[b].amount, 0) > 0 ? Math.Round(bus[b].amount, 0).ToString() : OpenQA.Selenium.Keys.Backspace;
            WriteToIWebElement(_dr, _dr.FindElement(By.CssSelector("input[data-qaid='stock_input']")), cnt);
            WriteToIWebElement(_dr, _dr.FindElement(By.XPath("//div[@data-qaid='product_name_input']//input")), bus[b].name);
            WriteToIWebElement(_dr, _dr.FindElement(By.XPath("//*[@id='cke_product']//iframe")), sl: GetTiuDesc(b));
            var but = _dr.FindElement(By.XPath("//button[@data-qaid='save_return_to_list']"));
            but.Click();
            Thread.Sleep(21000);
        }
        //добавляем фото с тиу в карточки товаров
        private async Task AddPhotosToBaseAsync() {
            if (_db.GetParamBool("loadPhotosFromTiuToBusiness")) {
                for (int b = 0; b < bus.Count; b++) {
                    if (bus[b].tiu.Contains("http") && bus[b].images.Count == 0 && bus[b].amount > 0) {
                        var imgUrls = new List<string>();
                        var t = Task.Factory.StartNew(() => {
                            Log.Add("tiu.ru: Нет фотографий в карточке товара! - " + bus[b].name);
                            _dr.Navigate().GoToUrl(bus[b].tiu);
                            Thread.Sleep(2000);
                            imgUrls.AddRange(_dr.FindElements(By.CssSelector(".b-uploader-extend__image-holder-img"))
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
                                Thread.Sleep(3000);
                                Log.Add("tiu.ru: " + bus[b].name + " - успешно загружено " + imgUrls.Count + " фото");
                            } else Log.Add("tiu.ru: Пропущено - у товара нет фотографий!!");
                        } catch (Exception x) {
                            Log.Add("tiu.ru: Ошибка загрузки фотографий с в карточку товара! - " + bus[b].name + x.Message);
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
            if (_dr == null) {
                await Task.Factory.StartNew(() => {
                    ChromeDriverService chromeservice = ChromeDriverService.CreateDefaultService();
                    chromeservice.HideCommandPromptWindow = true;
                    _dr = new ChromeDriver(chromeservice);
                    _dr.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
                    Thread.Sleep(2000);
                    _dr.Navigate().GoToUrl("https://my.tiu.ru/cms");
                    LoadCookies(_dr, "tiu.json");
                });
            }
            Task ts = Task.Factory.StartNew(() => {
                _dr.Navigate().GoToUrl("https://my.tiu.ru/cms/product?status=0&presence=not_avail");
                Thread.Sleep(3000);
                try {
                    _dr.FindElement(By.XPath("//ul/li/b[text()='Продавец']")).Click();
                    Thread.Sleep(3000);
                    _dr.FindElement(By.XPath("//a/b[text()='Войти как продавец']")).Click();
                    Thread.Sleep(3000);
                    //tiu.FindElement(By.Id("phone_email")).SendKeys("9106027626@mail.ru");
                    _dr.FindElement(By.Id("phone_email")).SendKeys("rogachev.aleksey@gmail.com");
                    Thread.Sleep(3000);
                    _dr.FindElement(By.XPath("//button[@id='phoneEmailConfirmButton']")).Click();
                    Thread.Sleep(3000);
                    //tiu.FindElement(By.Id("enterPassword")).SendKeys("RAD00239000");
                    _dr.FindElement(By.Id("enterPassword")).SendKeys("$Drumbotanik122122");
                    Thread.Sleep(3000);
                    _dr.FindElement(By.Id("enterPasswordConfirmButton")).Click();
                    Thread.Sleep(3000);
                    while (_dr.Url.Contains("sign-in")) {
                        Log.Add("tiu.ru: ошибка авторизации! ожидаю вход в кабинет...");
                        Thread.Sleep(60000);
                    }
                    Log.Add("tiu.ru: продолжаю работу...");
                } catch { }
                SaveCookies(_dr, "tiu.json");
                _dr.Navigate().Refresh();
                Thread.Sleep(3000);
                //закрываю окно объявления
                var bat = _dr.FindElements(By.XPath("//button/span[contains(text(),'Хорошо')]/.."));
                if (bat.Count > 0) bat.First().Click();
            });
            try {
                await ts;
                //галочка выделить все элементы
                _dr.FindElement(By.CssSelector("input[data-qaid='select_all_chbx']")).Click();
                Thread.Sleep(3000);
                //жмем кнопку выбора действия
                _dr.FindElement(By.CssSelector("span[data-qaid='selector_button']")).Click();
                Thread.Sleep(5000);
                //жмем изменить видимость
                var c = _dr.FindElements(By.XPath("//div/span[text()='Изменить видимость']/..")).First();
                Actions a = new Actions(_dr);
                a.MoveToElement(c).Perform();
                Thread.Sleep(1000);
                a.Click().Perform();
                Thread.Sleep(1000);
                //жмем скрытые
                c = _dr.FindElements(By.XPath("//div[text()='Скрытые']")).First();
                a.MoveToElement(c).Perform();
                Thread.Sleep(1000);
                c.Click();
                Thread.Sleep(2000);
            } catch (Exception x) {
                if (x.Message.Contains("timed out") ||
                x.Message.Contains("already closed") ||
                x.Message.Contains("invalid session id") ||
                x.Message.Contains("chrome not reachable")) {
                    _dr.Quit();
                    _dr = null;
                }
                //Log.Add("tiu.ru: ошибка - " + x.Message);
            }
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
                        string category_Text = "";
                        try {
                            category_Text = rows[0]["category_Text"].ToString();
                            var offer_id = ds.Tables["offer"].Rows[ti]["offer_id"];
                        } catch { continue; }

                        //проверим, нет ли повторений наименования
                        string[] bus_dubles;
                        do {
                            bus_dubles = bus.Select(tn => tn.name.ToUpper()).Where(f => f == tiuName.ToUpper()).ToArray();
                            if (bus_dubles.Length > 0) {
                                tiuName += tiuName.EndsWith("`") ? "`" : " `";
                                Log.Add("business.ru: исправлен дубль названия в создаваемой карточке \n" + tiuName);
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
                if (newTiuGoods.Count > 0) {
                    Log.Add("business.ru: обнаружено " + newTiuGoods.Count + " новых товаров на тиу.ру:\n"
                        + newTiuGoods.Select(s => s.name).Aggregate((a, b) => a + "\n" + b));
                    for (int i = 0; i < newTiuGoods.Count; i++) {
                        //подгружаем цену, количество и ед имз. для каждого товара
                        try {
                            tiu.Navigate().GoToUrl(newTiuGoods[i].tiu);
                            newTiuGoods[i].price = int.Parse(
                                tiu.FindElement(By.CssSelector("input[data-qaid='product_price_input']"))
                                .GetAttribute("value"));
                            if (newTiuGoods[i].price == 0) {
                                Log.Add("business.ru: ошибка при получении цены товара, пропущена позиция '"
                                    + newTiuGoods[i].name + "'");
                                newTiuGoods.RemoveAt(i--);
                                continue;
                            }
                            newTiuGoods[i].amount = int.Parse(
                                tiu.FindElement(By.CssSelector("input[data-qaid='stock_input']"))
                                .GetAttribute("value"));
                            if (newTiuGoods[i].amount == 0) {
                                Log.Add("business.ru: ошибка при получении количества товара, пропущена позиция '"
                                    + newTiuGoods[i].name + "'");
                                newTiuGoods.RemoveAt(i--);
                                continue;
                            }
                            if (tiu.FindElements(By.CssSelector(".b-uploader-extend__image-holder-img")).Count == 0) {
                                Log.Add("business.ru: ошибка в количестве фотографий, позиция пропущена '"
                                    + newTiuGoods[i].name + "'");
                                newTiuGoods.RemoveAt(i--);
                                continue;
                            }
                        } catch (Exception x) {
                            Log.Add("business.ru: ошибка при получении цены и остатков, пропущена позиция '"
                                + newTiuGoods[i].name + "' - " + x.Message);
                            newTiuGoods.RemoveAt(i--);
                            continue;
                        }
                        var measureTiu = tiu.FindElement(By.CssSelector("div[data-qaid='unit_dd'] span")).Text;
                        newTiuGoods[i].measure_id = measureTiu == "пара"
                            ? "11"
                            : measureTiu == "комплект"
                                ? "13"
                                : measureTiu == "упаковка"
                                    ? "9"
                                    : "1";
                        //создаем карточку товара
                        newTiuGoods[i].id = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                            await Class365API.RequestAsync("post", "goods", new Dictionary<string, string>()
                            {
                            {"name", newTiuGoods[i].name},
                            {"group_id", newTiuGoods[i].group_id},
                            {"description", newTiuGoods[i].description},
                            {"209325", newTiuGoods[i].tiu}
                            })).id;
                        //если единицы измерения не шт, то необходимо поменять привязку ед. изм. в карточке
                        if (newTiuGoods[i].measure_id != "1") {
                            //находим привязку единиц измерения
                            var gm = JsonConvert.DeserializeObject<GoodsMeasures[]>(
                                await Class365API.RequestAsync("get", "goodsmeasures", new Dictionary<string, string>()
                                {
                                {"good_id", newTiuGoods[i].id}
                                }));
                            //меняем значение кода единицы изм
                            var s = await Class365API.RequestAsync("put", "goodsmeasures", new Dictionary<string, string>()
                            {
                            {"id", gm[0].id},
                            {"measure_id", newTiuGoods[i].measure_id}
                        });
                        }
                        Log.Add("business.ru: СОЗДАНА КАРТОЧКА ТОВАРА - " + newTiuGoods[i].name + ",  ост. " + newTiuGoods[i].amount + ", цена " + newTiuGoods[i].price);
                        Thread.Sleep(1000);
                    }
                }

                if (newTiuGoods.Count > 0) {
                    var number = DateTime.Now.ToString().Replace(".", "").Replace(":", "").Replace(" ", "");
                    //сделаем новое поступление
                    var remain = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                        await Class365API.RequestAsync("post", "remains", new Dictionary<string, string>() {
                            { "organization_id", "75519"},//радченко
                            {"store_id", "1204484"},//склад
                            {"number", number},//номер документа
                            {"employee_id", "76221"},//-рогачев 76197-радченко
                            {"currency_id", "1"},
                            {"date", DateTime.Now.ToShortDateString()},
                            {"comment", "Создано автоматически, цена закупки 80% от реализации"}
                    }));
                    Log.Add("business.ru: СОЗДАНЫ ОСТАТКИ ПО СКЛАДУ № " + number);
                    Thread.Sleep(1000);
                    //делаем привязку к поступлению каждого товара
                    foreach (var good in newTiuGoods) {
                        var price = (int)(good.price * 0.8);
                        var res = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                            await Class365API.RequestAsync("post", "remaingoods", new Dictionary<string, string>()
                        {
                            {"remain_id", remain.id},
                            {"good_id", good.id},
                            {"amount", good.amount.ToString()},
                            {"measure_id", good.measure_id},
                            {"price", Convert.ToString(price)},
                            {"sum", Convert.ToString(good.amount*price)}
                        }));
                        Log.Add("business.ru: ПРИВЯЗАНА КАРТОЧКА К ОТСТАКАМ\n" + good.name);
                        Thread.Sleep(1000);
                    }

                    //создаём новый прайс лист
                    var spl = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                        await Class365API.RequestAsync("post", "salepricelists", new Dictionary<string, string>()
                        {
                            {"organization_id", "75519"},//радчено и.г.
                            {"responsible_employee_id", "76221"},//рогачев
                        }));
                    Log.Add("business.ru: СОЗДАН НОВЫЙ ПРАЙС ЛИСТ " + spl.id);
                    Thread.Sleep(1000);
                    //привяжем к нему тип цены
                    var splpt = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                        await Class365API.RequestAsync("post", "salepricelistspricetypes", new Dictionary<string, string>()
                    {
                        {"price_list_id", spl.id},
                        {"price_type_id", "75524"}//розничная
                    }));
                    Log.Add("business.ru: ПРИВЯЗАН ТИП ЦЕН К ПРАЙСУ 75524 (розничная)");
                    Thread.Sleep(1000);

                    //для каждого товара сделаем привязку у прайс листу
                    foreach (var good in newTiuGoods) {
                        var splg = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                            await Class365API.RequestAsync("post", "salepricelistgoods", new Dictionary<string, string>()
                            {
                                {"price_list_id", spl.id},
                                {"good_id", good.id},
                            }));
                        Log.Add("business.ru: КАРТОЧКА ПРИВЯЗАНА К ПРАЙС-ЛИСТУ \n" + good.name);
                        Thread.Sleep(1000);
                        //и назначение цены
                        var splgp = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                            await Class365API.RequestAsync("post", "salepricelistgoodprices", new Dictionary<string, string>()
                            {
                                {"price_list_good_id", splg.id},
                                {"price_type_id", "75524"},
                                {"price", good.price.ToString()}
                            }));
                        Log.Add("business.ru: НАЗНАЧЕНА ЦЕНА К ПРИВЯЗКЕ ПРАЙС ЛИСТА " + good.price);
                        Thread.Sleep(1000);
                    }
                    Log.Add("business.ru: ПОСТУПЛЕНИЕ И ЦЕНЫ УСПЕШНО СОЗДАНЫ НА " + newTiuGoods.Count + " ТОВАРОВ!");
                }

            } catch (Exception x) {
                Log.Add("business.ru: ошибка при создании поступления в AddSupplyAsync - " + x.Message);
            }
            newTiuGoods.Clear();
        }


        //загрузить куки
        public void LoadCookies() {
            if (_dr != null) {
                _dr.Navigate("https://avito.ru/404");
                var c = _db.GetParamStr("avito.cookies");
                _dr.LoadCookies(c);
                Thread.Sleep(_delay / 10);
            }
        }
        //сохранить куки
        public void SaveCookies() {
            if (_dr != null) {
                _dr.Navigate("https://avito.ru/profile");
                var c = _dr.SaveCookies();
                _db.SetParam("avito.cookies", c);
            }
        }
        //закрыть браузер
        public void Quit() {
            _dr?.Quit();
            _dr = null;
        }
    }
}
