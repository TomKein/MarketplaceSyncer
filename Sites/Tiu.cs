using Selen.Base;
using Selen.Tools;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExcelLibrary.SpreadSheet;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using Newtonsoft.Json;

namespace Selen.Sites {
    class Tiu {
        public Selenium _dr;
        int _tiuCount = 0;
        List<RootObject> _newOffers = new List<RootObject>();
        DataSet _ds;
        DB _db;
        string _fexp = "ex3.xls";
        string _ftemp = "tmpl.xls";
        List<RootObject> _bus = null;

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
        public async Task TiuSyncAsync(List<RootObject> bus, DataSet ds) {
            _bus = bus;
            _ds = ds;
            _db = DB._db;
            await AuthAsync();
            await TiuHide();
            await TiuMovePricelessToDrafts();
            await AddPhotosToBaseAsync();
            await TiuUploadAsync();
            await TiuSyncAsync2();
            await AddSupplyAsync();
        }
        //авторизация
        private async Task AuthAsync() {
            await Task.Factory.StartNew(() => {
                if (_dr == null) {
                    _dr = new Selenium();
                }
                LoadCookies();
                _dr.Navigate("https://my.tiu.ru/cms/product?status=0&presence=not_avail");
                if (_dr.GetUrl().Contains("source=redirect")) {
                    _dr.WriteToSelector("//*[@id='phone_field']", _db.GetParamStr("tiu.login"));
                    _dr.ButtonClick("//button[@id='phoneConfirmButton']");
                    _dr.WriteToSelector("//*[@id='enterPassword']", _db.GetParamStr("tiu.password"));
                    _dr.ButtonClick("//*[@id='enterPasswordConfirmButton']");
                    Thread.Sleep(5000);
                    _dr.Navigate("https://my.tiu.ru/cms/product?status=0&presence=not_avail");
                }
                for (int i=0; _dr.GetUrl().Contains("source=redirect");i++) {
                    if (i > 10) throw new Exception("tiu.ru: ошибка авторизации! превышено количество попыток!");
                    Log.Add("tiu.ru: ошибка авторизации! ожидаю вход в кабинет (" + i + ")...");
                    Thread.Sleep(60000);
                }
                Log.Add("tiu.ru: продолжаю работу...");
                SaveCookies();
                //закрываю окно объявления
                _dr.ButtonClick("//button/span[contains(text(),'Хорошо')]/..");
            });
        }
        //перемещаю товары без цен с витрины в черновики
        private async Task TiuMovePricelessToDrafts() {
            try {
                await Task.Factory.StartNew(() => {
                    //запрашиваю опубликованные товары без цены
                    _dr.Navigate("https://my.tiu.ru/cms/product?search_term=&page=1&per_page=20&status=0&price=without_price");
                    //ставлю галочку выделить все элементы
                    _dr.ButtonClick("input[data-qaid='select_all_chbx']");
                    //жму Выбор действия
                    _dr.ButtonClick("span[data-qaid='selector_button']");
                    //жму Изменить видимость
                    _dr.ButtonClick("//div/span[text()='Изменить видимость']/..");
                    //жму Черновики
                    _dr.ButtonClick("//div[text()='Черновики']");
                });
            } catch (Exception x) {
                Log.Add("tiu.ru: ошибка при перемещении товаров без цен в черновики - "+ x.Message);
            }
        }
        //выгрузка на тиу
        async Task TiuUploadAsync() {
            try {
                for (int i = 0; ; i++) {
                    try {
                        _newOffers.Clear();
                        await Task.Factory.StartNew(() => {
                            _tiuCount = 0;
                            _ds.Clear();
                            Log.Add("tiu.ru: запрашиваю каталог xml...");
                            _ds.ReadXml(_db.GetParamStr("tiu.xmlUrl"));
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
                    _tiuCount = _ds.Tables["offer"].Rows.Count;
                    Log.Add("tiu.ru: получено объявлений " + _tiuCount.ToString());
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
                    for (int i = 0; i < _bus.Count; i++) {
                        //есть привязка к тиу и есть цена, значит выгружаем на тиу
                        if (_bus[i].tiu.Contains("tiu.ru")) {
                            //получаем id товара тиу из ссылки
                            string idTiu = _bus[i].tiu.Replace("/edit/", "|").Split('|')[1];
                            //ищем строку с таким товаром
                            var tRow = _ds.Tables["offer"].Select("id = '" + idTiu + "'");
                            //пишем строку в экспорт если
                            if (tRow.Length > 0
                                 || (_bus[i].amount > 0 && _bus[i].price > 0)
                            ) {
                                iRow++;
                                //запишем в экспорт новую строку
                                string s = _bus[i].description.Replace("&nbsp;", " ")
                                    .Replace("&quot;", "")
                                    .Replace(" &gt", "")
                                    .Replace("Есть и другие", "|")
                                    .Split('|')[0];
                                if (_bus[i].IsGroupValid()) {
                                    s += dopDescTiu;
                                }
                                string m = _bus[i].measure_id == "11"
                                    ? "пара"
                                    : _bus[i].measure_id == "13"
                                        ? "комплект"
                                        : _bus[i].measure_id == "9"
                                            ? "упаковка"
                                            : "шт.";
                                string a = _bus[i].amount > 0 ? "+" : "-";
                                if (!string.IsNullOrEmpty(_bus[i].part)) ws.Cells[iRow, 0] = new Cell(_bus[i].part.Split(',').First());
                                ws.Cells[iRow, 1] = new Cell(_bus[i].name);
                                ws.Cells[iRow, 3] = new Cell(s);
                                ws.Cells[iRow, 5] = new Cell(_bus[i].price > 0 ? _bus[i].price : 100);
                                ws.Cells[iRow, 6] = new Cell("RUB");
                                ws.Cells[iRow, 7] = new Cell(m);
                                ws.Cells[iRow, 12] = new Cell(a);
                                ws.Cells[iRow, 13] = new Cell(_bus[i].amount > 0 ? (int)_bus[i].amount : 0);
                                ws.Cells[iRow, 20] = new Cell(idTiu);
                            } else {
                                if (_bus[i].amount > 0)
                                    Log.Add("tiu.ru: ошибка! - tiu_ID = " + idTiu + " из ссылки карточки не найден в выгрузке XML - " + _bus[i].name);
                            }
                        }
                    }
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
            var goods = _bus.Where(w => w.images.Count > 0 && w.amount > 0 && !w.tiu.Contains("tiu.ru"))
                .Select(s => s.name)
                .ToList();
            foreach (var good in goods) {
                Log.Add("tiu.ru: ошибка! - карточка есть на остатках, с фотографиями, но нет ссылки на объявление! - " + good);
            }
        }
        //редактируем объявления
        private async Task TiuSyncAsync2() {
            for (int b = 0; b < _bus.Count; b++) {
                if (_bus[b].tiu != null && _bus[b].tiu.Contains("http") &&
                    _bus[b].IsTimeUpDated() && _bus[b].price > 0) {
                    try {
                        await Task.Factory.StartNew(() => {
                            TiuOfferUpdate(b);
                        });
                    } catch (Exception x) {
                        Log.Add("tiu.ru: ошибка обновления позиции! "+ _bus[b].name + " - " + x.Message);
                    }
                }
            }
        }
        //готовим описание для tiu.ru
        private List<string> GetTiuDesc(int b) {
            var s = Regex.Replace(_bus[b].description
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

            if (_bus[b].IsGroupValid()) {
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
        //редактирование позиции
        private void TiuOfferUpdate(int b) {
            _dr.Navigate(_bus[b].tiu);
            //закрываю окно с рекламой
            _dr.ButtonClick("//div[contains(@class,'sliding-panel__close-btn')]");
            //проверяю статус объявления
            var status = _dr.GetElementText("//div[@data-qaid='presence_stock_block']//span/span");
            if (_bus[b].amount > 0) {
                _dr.ButtonClick("//div[@data-qaid='visibility_block']/div[1]//input");//радио батн опубликован
                if (status != "В наличии") {
                    _dr.ButtonClick("//div[@data-qaid='presence_stock_block']//div[contains(@class,'drop-down')]");
                    _dr.ButtonClick("//div[contains(@class,'drop-down')]//li[text()='В наличии']");
                }
            }
            if (_bus[b].amount <= 0) {
                _dr.ButtonClick("//div[@data-qaid='visibility_block']/div[3]//input");//радио батн скрытый
                if (status != "Нет в наличии") {
                    _dr.ButtonClick("//div[@data-qaid='presence_stock_block']//div[contains(@class,'drop-down')]");
                    _dr.ButtonClick("//div[contains(@class,'drop-down')]//li[text()='Нет в наличии']");
                }
            }
            _dr.WriteToSelector("input[data-qaid='product_price_input']", _bus[b].price.ToString());
            //если нулевое количество 0 не пишем, просто удаляем что там указано
            var cnt = Math.Round(_bus[b].amount, 0) > 0 ? Math.Round(_bus[b].amount, 0).ToString() : OpenQA.Selenium.Keys.Backspace;
            _dr.WriteToSelector("input[data-qaid='stock_input']", cnt);
            _dr.WriteToSelector("//div[@data-qaid='product_name_input']//input", _bus[b].name);
            _dr.WriteToSelector("//*[@id='cke_product']//iframe", sl: GetTiuDesc(b));
            _dr.ButtonClick("//button[@data-qaid='save_return_to_list']");
            Thread.Sleep(21000);
        }
        //добавляем фото с тиу в карточки товаров
        private async Task AddPhotosToBaseAsync() {
            if (_db.GetParamBool("loadPhotosFromTiuToBusiness")) {
                for (int b = 0; b < _bus.Count; b++) {
                    if (_bus[b].tiu.Contains("http") && _bus[b].images.Count == 0 && _bus[b].amount > 0) {
                        var imgUrls = new List<string>();
                        var t = Task.Factory.StartNew(() => {
                            Log.Add("tiu.ru: Нет фотографий в карточке товара! - " + _bus[b].name);
                            _dr.Navigate(_bus[b].tiu);
                            imgUrls.AddRange(_dr._drv.FindElements(By.CssSelector(".b-uploader-extend__image-holder-img"))
                                               .Select(s => s.GetAttribute("src")
                                               .Split('?')
                                               .First()
                                               .Replace("w100_h100", "w1280_h1280")));
                        });
                        try {
                            await t;
                            if (imgUrls.Count > 0) {
                                for (int i = 0; i < imgUrls.Count; i++) {
                                    _bus[b].images.Add(new Image() {
                                        name = imgUrls[i].Split('/').Last(),
                                        url = imgUrls[i]
                                    });
                                }
                                var s = await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>(){
                                        {"id", _bus[b].id},
                                        {"name", _bus[b].name},
                                        {"images", JsonConvert.SerializeObject(_bus[b].images.ToArray())}
                                });
                                Thread.Sleep(3000);
                                Log.Add("tiu.ru: " + _bus[b].name + " - успешно загружено " + imgUrls.Count + " фото");
                            } else Log.Add("tiu.ru: Пропущено - у товара нет фотографий!!");
                        } catch (Exception x) {
                            Log.Add("tiu.ru: Ошибка загрузки фотографий с в карточку товара! - " + _bus[b].name + x.Message);
                        }
                    }
                }
            }
        }
        public async Task WaitTiuAsync() {
            while (_tiuCount == 0) {
                Log.Add("ожидаем загрузку tiu...");
                await Task.Factory.StartNew(() => { Thread.Sleep(10000); });
            }
        }
        //скрываем на тиу объявления, которых нет в наличии
        private async Task TiuHide() {
            await Task.Factory.StartNew(() => {
                _dr.Navigate("https://my.tiu.ru/cms/product?filterSetId=21433693&search_term=&page=1&status=0&presence=not_avail");
                //выделяю элементы
                _dr.ButtonClick("input[data-qaid='select_all_chbx']");
                //жму кнопку выбора действия
                _dr.ButtonClick("span[data-qaid='selector_button']");
                //жму изменить видимость
                _dr.ButtonClick("//div/span[text()='Изменить видимость']/..");
                //жму скрытые
                _dr.ButtonClick("//div[text()='Скрытые']");
            });
        }
        //поступление товаров с тиу - создаю карточки, остатки и цены
        public async Task AddSupplyAsync() {
            try {
                //проверяем, для каких товаров нужно сделать новые карточки и поступление
                _tiuCount = _ds.Tables.Count > 0 ? _ds.Tables["offer"].Rows.Count : 0;

                await Task.Factory.StartNew(() => {
                    for (int ti = 0; ti < _tiuCount; ti++) {
                        string tiuName = _ds.Tables["offer"].Rows[ti]["name"].ToString();
                        string tiuId = _ds.Tables["offer"].Rows[ti]["id"].ToString();
                        int indBus = _bus.FindIndex(f => !String.IsNullOrEmpty(f.tiu) && f.tiu.Contains(tiuId));
                        if (indBus == -1) {
                            string desc = Regex.Replace(_ds.Tables["offer"].Rows[ti]["description"].ToString()
                                        .Replace("Есть и другие", "|").Split('|')[0]
                                        .Replace("&nbsp;", " ")
                                        .Replace("&", " ")
                                        .Replace("<br />", "|")
                                        .Replace("</p>", "|"),
                                    "<[^>]+>", string.Empty)
                                .Replace("|", "<br />")
                                .Replace("\n", "")
                                .Replace("Есть и другие", "|").Split('|')[0];

                            string categoryId = _ds.Tables["offer"].Rows[ti]["categoryId"].ToString();
                            var rows = _ds.Tables["category"].Select("id = '" + categoryId + "'");
                            string category_Text = "";
                            try {
                                category_Text = rows[0]["category_Text"].ToString();
                                var offer_id = _ds.Tables["offer"].Rows[ti]["offer_id"];
                            } catch { continue; }

                            //проверим, нет ли повторений наименования
                            string[] bus_dubles;
                            do {
                                bus_dubles = _bus.Select(tn => tn.name.ToUpper()).Where(f => f == tiuName.ToUpper()).ToArray();
                                if (bus_dubles.Length > 0) {
                                    tiuName += tiuName.EndsWith("`") ? "`" : " `";
                                    Log.Add("business.ru: исправлен дубль названия в создаваемой карточке \n" + tiuName);
                                }
                            } while (bus_dubles.Length > 0);

                            //добавим в список товаров, на которые нужно сделать поступление и цены
                            string group_id = null;
                            try {
                                group_id = RootObject.Groups.Find(f => f.name.Contains(category_Text.Trim())).id ?? "169326";
                            } catch(Exception x) {
                                Log.Add("ошибка сопоставления групп! (AddSupplyAsync)");
                                group_id = "169326";
                            }

                            _newOffers.Add(new RootObject() {
                                name = tiuName,
                                tiu = "https://my.tiu.ru/cms/product/edit/" + tiuId,
                                description = desc,
                                group_id = group_id
                            });
                        }
                    }
                });
                if (_newOffers.Count > 0) {
                    Log.Add("business.ru: обнаружено " + _newOffers.Count + " новых товаров на тиу.ру:\n"
                        + _newOffers.Select(s => s.name).Aggregate((a, b) => a + "\n" + b));
                    for (int i = 0; i < _newOffers.Count; i++) {
                        //подгружаем цену, количество и ед имз. для каждого товара
                        try {
                            await _dr.NavigateAsync(_newOffers[i].tiu);
                            _newOffers[i].price = int.Parse(
                                _dr.GetElementAttribute("input[data-qaid='product_price_input']","value"));
                            if (_newOffers[i].price == 0) {
                                Log.Add("business.ru: ошибка при получении цены товара, пропущена позиция '"
                                    + _newOffers[i].name + "'");
                                _newOffers.RemoveAt(i--);
                                continue;
                            }
                            _newOffers[i].amount = int.Parse(
                                _dr.GetElementAttribute("input[data-qaid='stock_input']","value"));
                            if (_newOffers[i].amount == 0) {
                                Log.Add("business.ru: ошибка при получении количества товара, пропущена позиция '"
                                    + _newOffers[i].name + "'");
                                _newOffers.RemoveAt(i--);
                                continue;
                            }
                            if (_dr.GetElementsCount(".b-uploader-extend__image-holder-img") == 0) {
                                Log.Add("business.ru: ошибка в количестве фотографий, позиция пропущена '"
                                    + _newOffers[i].name + "'");
                                _newOffers.RemoveAt(i--);
                                continue;
                            }
                        } catch (Exception x) {
                            Log.Add("business.ru: ошибка при получении цены и остатков, пропущена позиция '"
                                + _newOffers[i].name + "' - " + x.Message);
                            _newOffers.RemoveAt(i--);
                            continue;
                        }
                        var measureTiu = _dr.GetElementText("div[data-qaid='unit_dd'] span");
                        _newOffers[i].measure_id = measureTiu == "пара"
                            ? "11"
                            : measureTiu == "комплект"
                                ? "13"
                                : measureTiu == "упаковка"
                                    ? "9"
                                    : "1";
                        //создаем карточку товара
                        _newOffers[i].id = JsonConvert.DeserializeObject<PostSuccessAnswer>(
                            await Class365API.RequestAsync("post", "goods", new Dictionary<string, string>(){
                                {"name", _newOffers[i].name},
                                {"group_id", _newOffers[i].group_id},
                                {"description", _newOffers[i].description},
                                {"measure_id", _newOffers[i].measure_id},//добавил единицы измерений в параметры запроса
                                {"209325", _newOffers[i].tiu }
                            })).id;
                        Log.Add("business.ru: СОЗДАНА КАРТОЧКА ТОВАРА - " + _newOffers[i].name + ",  ост. " + _newOffers[i].amount +" ("+ _newOffers[i].measure_id + "), цена " + _newOffers[i].price);
                        Thread.Sleep(2000);
                    }
                }
                //сделаем новое поступление
                if (_newOffers.Count > 0) {
                    var number = DateTime.Now.ToString().Replace(".", "").Replace(":", "").Replace(" ", "");
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
                    foreach (var good in _newOffers) {
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
                    foreach (var good in _newOffers) {
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
                    Log.Add("business.ru: ПОСТУПЛЕНИЕ И ЦЕНЫ УСПЕШНО СОЗДАНЫ НА " + _newOffers.Count + " ТОВАРОВ!");
                }

            } catch (Exception x) {
                Log.Add("business.ru: ошибка при создании поступления в AddSupplyAsync - " + x.Message);
            }
            _newOffers.Clear();
        }
        //загрузить куки
        public void LoadCookies() {
            if (_dr != null) {
                _dr.Navigate("https://my.tiu.ru/cms/product");
                var c = _db.GetParamStr("tiu.cookies");
                _dr.LoadCookies(c);
                Thread.Sleep(2000);
            }
        }
        //сохранить куки
        public void SaveCookies() {
            if (_dr != null) {
                _dr.Navigate("https://my.tiu.ru/cms/product");
                var c = _dr.SaveCookies();
                _db.SetParam("tiu.cookies", c);
            }
        }
        //закрыть браузер
        public void Quit() {
            _dr?.Quit();
            _dr = null;
        }
    }
}
