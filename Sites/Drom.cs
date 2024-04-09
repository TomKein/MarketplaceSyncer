using Newtonsoft.Json;
using OpenQA.Selenium;
using Selen.Base;
using Selen.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace Selen.Sites {
    public class Drom {
        readonly string _l = "drom.ru: ";     //префикс для лога
        public Selenium _dr;               //браузер
        string _url;                //ссылка в карточке товара
        string[] _addDesc;          //дополнительное описание
        int _creditPriceMin;        //цены для рассрочки
        int _creditPriceMax;
        string _creditDescription;  //описание для рассрочки
        bool _isAddWeights;           //добавление веса 
        float _defWeigth;           //вес по умолчанию 
        List<GoodObject> _bus;      //ссылка на товары
        readonly string _dromBaseUrl = "https://baza.drom.ru/bulletin/";
        readonly string _fileReserve = @"..\data\drom\reserves.json";     //список сделанных резервов
        bool _needRestart = false;
        Random _rnd = new Random();
        List<string> _reserveList = new List<string>();
        //конструктор
        public Drom() {
        }
        public void LoadCookies() {
            if (_dr != null) {
                _dr.Navigate("https://baza.drom.ru/kaluzhskaya-obl/");
                var c = DB.GetParamStr("drom.cookies");
                _dr.LoadCookies(c);
                Thread.Sleep(1000);
            }
        }
        public void SaveCookies() {
            if (_dr != null) {
                _dr.Navigate("https://baza.drom.ru/kaluzhskaya-obl/");
                var c = _dr.SaveCookies();
                if (c.Length > 20)
                    DB.SetParam("drom.cookies", c);
            }
        }
        public void Quit() {
            _dr?.Quit();
            _dr = null;
        }
        public async Task DromStartAsync(List<GoodObject> bus) {
            try {
                _bus = bus;
                _url = await DB.GetParamStrAsync("drom.url");
                _isAddWeights = await DB.GetParamBoolAsync("drom.addWeights");
                _defWeigth = DB.GetParamFloat("defaultWeigth");
                _addDesc = JsonConvert.DeserializeObject<string[]>(DB.GetParamStr("drom.addDescription"));
                //рассрочка 
                _creditPriceMin = DB.GetParamInt("creditPriceMin");
                _creditPriceMax = DB.GetParamInt("creditPriceMax");
                _creditDescription = DB.GetParamStr("creditDescription");

                Log.Add(_l+"начало выгрузки...");
                await AuthAsync();
                await MakeReserveAsync();
                await GetDromPhotos();
                await UpAsync();
                await EditAsync();
                await CheckPagesAsync();
                await CheckOffersAsync();
                if (Class365API.SyncStartTime.Minute >= 55) {
                    await AddAsync();
                }
                Log.Add(_l+"выгрузка завершена");
            } catch (Exception x) {
                Log.Add(_l+"ошибка синхронизации! - " + x.Message);
                if (x.Message.Contains("timed out") ||
                    x.Message.Contains("already closed") ||
                    x.Message.Contains("HTTP request") ||
                    x.Message.Contains("invalid session id") ||
                    x.Message.Contains("chrome not reachable")) {
                    Log.Add(_l+"ошибка браузера! - " + x.Message);
                    _dr.Quit();
                    _dr = null;
                }
            }
        }
        async Task AuthAsync() {
            await Task.Factory.StartNew(() => {
                if (_needRestart)
                    Quit();
                if (_dr == null) {
                    _dr = new Selenium();
                    LoadCookies();
                }
                _dr.Navigate("http://baza.drom.ru/personal/all/bulletins", "#outerLayout");
                if (_dr.GetElementsCount("#sign") > 0) {//если элементов в левой панели нет
                    _dr.WriteToSelector("#sign", DB.GetParamStr("drom.login")); //ввод логина
                    _dr.WriteToSelector("#password", DB.GetParamStr("drom.password")); //пароля
                    _dr.ButtonClick("#signbutton"); //жмем кнопку входа
                    while (_dr.GetElementsCount("//div[@class='personal-box']") == 0) //если элементов слева нет ждем ручной вход
                        Thread.Sleep(30000);
                }
                SaveCookies();
            });
        }
        //резервирование
        public async Task MakeReserveAsync() {
            try {
                //загружаем список заказов, для которых уже делали резерв
                if (File.Exists(_fileReserve)) {
                    var r = File.ReadAllText(_fileReserve);
                    _reserveList = JsonConvert.DeserializeObject<List<string>>(r);
                }
                //получаю список заказов
                var orders = await GetOrdersAsync();
                Log.Add($"{_l} MakeReserve - получено  заказов: " + orders.Count);
                //для каждого заказа сделать заказ с резервом в бизнес.ру
                foreach (var order in orders) {
                    //готовим список товаров (id, amount)
                    var goodsDict = new Dictionary<string, int>();
                    order.Items.ForEach(i => goodsDict.Add(i.Id, i.Count));
                    var isResMaked = await Class365API.MakeReserve(Selen.Source.Drom, $"Drom (Farpost) order {order.Id}",
                                                                   goodsDict, order.Date);
                    if (isResMaked) {
                        _reserveList.Add(order.Id);
                        if (_reserveList.Count > 1000) {
                            _reserveList.RemoveAt(0);
                        }
                        var rl = JsonConvert.SerializeObject(_reserveList);
                        File.WriteAllText(_fileReserve, rl);
                    }
                }
            } catch (Exception x) {
                Log.Add($"{_l} MakeReserve - " + x.Message);
            }
        }
        public async Task<List<DromOrder>> GetOrdersAsync() {
            await _dr.NavigateAsync("https://baza.drom.ru/personal/sold/bulletins?page=1&type=all", ".deal-list .deal-item");
            var dealList = await _dr.FindElementsAsync(".deal-list .deal-item");
            var orderList = new List<DromOrder>();
            var reg = new Regex(@"(\d+)");   // some digits 1+ e.g. 565156487 in drom url (id)
            foreach (var deal in dealList) {
                var numberElement = deal.FindElement(By.CssSelector(".deal-identifier a"));
                var dealNumber = numberElement.Text.Split(' ')[1];
                //проверяем наличие резерва
                if (_reserveList.Contains(dealNumber)
                    //order.Status == "closed" ||
                    //order.Status == "on_return"
                    )
                    continue;
                var dateElement = deal.FindElement(By.CssSelector(".deal-date"));
                var dateString = dateElement.Text;
                var dealStatusElement = deal.FindElement(By.CssSelector(".deal-select-status span"));
                var dealStatus = dealStatusElement.Text;
                orderList.Add(new DromOrder {
                    Id = dealNumber,
                    Date = dateString,
                    Status = dealStatus,
                    Items = new List<DromOrderItem>()
                });
                var itemsElements = deal.FindElements(By.CssSelector(".deal-target-list .bulletin-of-deal"));
                foreach (var item in itemsElements) {
                    var itemId = item.GetAttribute("data-ref");
                    itemId = itemId.Split(':')[1];
                    itemId = _bus.Find(b => reg.Match(b.drom??"").Value == itemId).id;
                    var itemCountString = item.FindElement(By.CssSelector(".subject-quantity")).GetAttribute("data-quantity");
                    var itemCount = int.Parse(itemCountString);
                    orderList.Last().Items.Add(new DromOrderItem {
                        Id = itemId,
                        Count = itemCount
                    });
                }
            }
            return orderList;
        }

        async Task EditAsync() {
            await Task.Factory.StartNew(() => {
                for (int b = 0; b < _bus.Count; b++) {
                    if (_bus[b].IsTimeUpDated() &&
                        _bus[b].drom != null &&
                        _bus[b].drom.Contains("http")) {
                        try {
                            Edit(_bus[b]);
                        } catch (Exception x) {
                            Debug.WriteLine(x.Message);
                            if (x.Message.Contains("timed out") ||
                                x.Message.Contains("already closed") ||
                                x.Message.Contains("HTTP request") ||
                                x.Message.Contains("invalid session id") ||
                                x.Message.Contains("chrome not reachable")) {
                                throw x;
                            }
                        }
                    }
                }
            });
        }
        void Edit(GoodObject b) {
            if (b.drom.Contains("tin/ht")) {
                Log.Add(_l + b.name + " - ошибка - неверная ссылка!!");
                return;
            }
            _dr.Navigate(b.drom, "//input[@name='subject']");
            Thread.Sleep(1000);
            SetTitle(b);
            CheckPhotos(b);
            SetPrice(b);
            SetOptions(b);
            SetAddress(b);
            SetDesc(b);
            SetPart(b);
            SetAlternativeParts(b);
            SetWeight(b);
            PressOkButton();
            Log.Add(_l + b.name + " - объявление обновлено");
            if (b.Amount <= 0) {
                Delete(b);
            } else
                Up(b);
        }
        //проверка фотографий в объявлении
        private void CheckPhotos(GoodObject b) {
            //селектор фотографий в объявлении
            string selector = "//div[@class='grid-item-wrapper']/img";
            //получаю количество фотографий в объявлении
            var countReal = _dr.GetElementsCount(selector);
            //количество фотографий, которое должно быть в объявлении
            int countMust = b.images.Count;
            //если расхождение и в карточке количество не нулевое
            if (countMust != countReal && countMust > 0) {
                //удаляю все фото, которые есть объявлении
                for (; countReal > 0; countReal--) {
                    _dr.ButtonClick("//a[text()='Удалить']", 10000);
                }
                //загружаю новые фото
                SetImages(b);
                Log.Add(_l + b.name + " - фотографии обновлены");
            }
        }
        //заполнить наименование
        void SetTitle(GoodObject b) {
            var w = _dr.GetElementAttribute("//input[@name='subject']", "value");
            if (string.IsNullOrEmpty(w) || w != b.name)
                _dr.WriteToSelector("//input[@name='subject']", b.name);
        }
        void Delete(GoodObject b) {
            //переход на страницу объявления, если он нужен
            _dr.ButtonClick("//a[contains(text(),'Вернуться на страницу')]");
            if (_dr.GetElementsCount("//a[contains(@class,'doDelete')]") > 0) {
                _dr.ButtonClick("//a[contains(@class,'doDelete')]");
                PressServiseSubmitButton();
                if (_dr.GetElementsCount("//h2[contains(text(),'удалили')]") > 0)
                    Log.Add(_l + b.name+" объявление удалено");
                else 
                    Log.Add(_l + b.name+" ошибка - объявление не удалено!");
            }
        }
        public async Task AddAsync() {
            var count = await DB.GetParamIntAsync("drom.addCount");
            for (int b = _bus.Count - 1; b > -1 && count > 0; b--) {
                if ((_bus[b].drom == null || !_bus[b].drom.Contains("http")) &&
                    !_bus[b].GroupName().Contains("ЧЕРНОВИК") &&
                    _bus[b].Amount > 0 &&
                    _bus[b].Price > 0 &&
                    _bus[b].images.Count > 0) {
                    var t = Task.Factory.StartNew(() => {
                        _dr.Navigate("http://baza.drom.ru/set/city/370?return=http%3A%2F%2Fbaza.drom.ru%2Fadding%3Fcity%3D370", "//input[@name='subject']");
                        if (_dr.GetElementsCount("//div[@class='image-wrapper']/img") > 0)
                            throw new Exception("Черновик уже заполнен!");//если уже есть блок фотографий на странице, то черновик уже заполнен, но не опубликован по какой-то причине, например, номер запчасти похож на телефонный номер - объявление не опубликовано, либо превышен дневной лимит подачи
                        SetTitle(_bus[b]);
                        _dr.ButtonClick("//div[@class='table-control']//label");//Автозапчасти или диски - первая кнопка
                        _dr.ButtonClick("//p[@class='type_caption']"); //одна запчасть или 1 комплект - первая кнопка
                        SetPart(_bus[b]);
                        //SetAlternativeParts(_bus[b]);
                        SetImages(_bus[b]);
                        SetDesc(_bus[b]);
                        SetPrice(_bus[b]);
                        SetOptions(_bus[b]);
                        SetDiam(_bus[b]);
                        SetAudioSize(_bus[b]);
                        SetWeight(_bus[b]);
                        Thread.Sleep(30000);
                        PressPublicFreeButton();
                    });
                    try {
                        await t;
                        await SaveUrlAsync(b);
                        Log.Add(_l+ _bus[b].name+ " объявление добавлено");
                        count--;
                    } catch (Exception x) {
                        Log.Add(_l+ _bus[b].name+ " ошибка добавления! - " + x.Message);
                        break;
                    }
                }
            }
        }
        private void SetWeight(GoodObject b) {
            if (_isAddWeights) {
                var weight = b.weight ?? _defWeigth;
                if (weight == 0)
                    weight = _defWeigth;

                //проверка заполнения веса
                var w = _dr.GetElementAttribute("//input[@name='delivery[postProviderWeight]']", "value");
                if (string.IsNullOrEmpty(w) || float.Parse(w.Replace(".", ",")) != b.GetWeight()) {
                    _dr.WriteToSelector("//input[@name='delivery[postProviderWeight]']",
                    weight.ToString("0.00") + OpenQA.Selenium.Keys.Tab);
                }
            }
        }
        async Task SaveUrlAsync(int b) {
            string new_id = _dr.GetUrl().Split('-').Last().Split('.').First();
            _bus[b].drom = _dromBaseUrl + new_id + "/edit";
            await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                {"id", _bus[b].id},
                {"name", _bus[b].name},
                {_url, _bus[b].drom}
            });
        }
        void SetAudioSize(GoodObject b) {
            if (b.GroupName() == "Аудио-видеотехника") {
                var d = b.description.ToLowerInvariant();
                if (d.Contains("din")) {
                    var s = b.description.ToLowerInvariant().Replace(" din", "din").Replace("din ", "din").Split(' ').First(f => f.Contains("din"));
                    if (s.Contains("2"))
                        _dr.ButtonClick("//label[contains(text(),'2 DIN')]");
                    else if (s.Contains("5"))
                        _dr.ButtonClick("//label[contains(text(),'1,5 DIN')]");
                } else
                    _dr.ButtonClick("//label[contains(text(),'1 DIN')]");
                if (_dr.GetElementsCount("//div[@data-name='model']/div[contains(@class,'annotation') and contains(@style,'none')]") == 0) {
                    _dr.WriteToSelector("//div[@data-name='model']//input[@data-role='name-input']", "штатная");
                }
                //тип акустики
                if (d.Contains("коаксиал"))
                    _dr.ButtonClick("//input[@name='speakerSystemType' and @value='Коаксиальные']");
                else
                    _dr.ButtonClick("//input[@name='speakerSystemType' and @value='Широкополосные']");

            }
        }
        void SetDiam(GoodObject b) {
            if (b.GroupName() == "Шины, диски, колеса") {
                _dr.ButtonClick("//div[@data-name='wheelDiameter']//div[contains(@class,'chosen-container-single')]");
                _dr.WriteToSelector("//div[@data-name='wheelDiameter']//input", b.GetDiskSize() + OpenQA.Selenium.Keys.Enter);
                _dr.WriteToSelector("//input[@name='quantity']", "1");
                string dtype;
                switch (b.DiskType()) {
                    case "Литые":
                        dtype = "Литой";
                        break;
                    case "Кованые":
                        dtype = "Кованый";
                        break;
                    default:
                        dtype = "Литой";
                        break;
                }
                _dr.WriteToSelector("//div[@data-name='model']//input[@data-role='name-input']", dtype + OpenQA.Selenium.Keys.Enter);
            }
        }
        void SetOptions(GoodObject b) {
            //новый/бу - если товар новый, но цвет кнопки не серый, значит надо нажать
            if (b.IsNew()) {
                if (!_dr.GetElementCSSValue("//label[text()='Новый']", "background").Contains("224, 224"))
                    _dr.ButtonClick("//label[text()='Новый']");
            } else
                if (!_dr.GetElementCSSValue("//label[text()='Б/у']", "background").Contains("224, 224"))
                _dr.ButtonClick("//label[text()='Б/у']");
            //аналог или оригинал
            if (b.IsOrigin()) {
                if (!_dr.GetElementCSSValue("//label[text()='Оригинал']", "background").Contains("224, 224"))
                    _dr.ButtonClick("//label[text()='Оригинал']");
            } else
                if (!_dr.GetElementCSSValue("//label[text()='Аналог']", "background").Contains("224, 224"))
                _dr.ButtonClick("//label[text()='Аналог']");
            //наличие
            if (!_dr.GetElementCSSValue("//label[text()='В наличии']", "background").Contains("224, 224"))
                _dr.ButtonClick("//label[text()='В наличии']");
        }
        void SetImages(GoodObject b) {
            WebClient cl = new WebClient();
            cl.Encoding = Encoding.UTF8;
            var num = b.images.Count > 20 ? 20 : b.images.Count;
            for (int u = 0; u < num; u++) {
                for (int i = 0; i < 5; i++) {
                    try {
                        byte[] bts = cl.DownloadData(b.images[u].url);
                        File.WriteAllBytes("drom_" + u + ".jpg", bts);
                        Thread.Sleep(1000);
                        _dr.SendKeysToSelector("//input[@type='file']", Application.StartupPath + "\\" + "drom_" + u + ".jpg");
                        break;
                    } catch { }
                }
            }
            cl.Dispose();
            Thread.Sleep(2000);
        }
        //адрес самовывоза из профиля
        void SetAddress(GoodObject b) {
            if (_dr.GetElementCSSValue("//label[contains(text(),'Применить из профиля')]", "background")
                .Contains("rgb(255, 255, 255)")) {
                _dr.ButtonClick("//label[contains(text(),'Применить из профиля')]");
                Log.Add(_l + b.name + " - установлен адрес самовывоза");
            }
        }
        void SetPart(GoodObject b) {
            var w = _dr.GetElementAttribute("//input[@name='autoPartsOemNumber']", "value");
            if ((string.IsNullOrEmpty(w) && !string.IsNullOrEmpty(b.Part)) || w != b.Part)
                _dr.WriteToSelector("//input[@name='autoPartsOemNumber']", b.Part + OpenQA.Selenium.Keys.Tab);
        }
        void SetAlternativeParts(GoodObject b) {
            var descNums = b.GetDescriptionNumbers();
            var oem = b.GetOEM();
            if (!string.IsNullOrEmpty(oem) && oem != b.part)
                descNums.Add(oem);
            var alt = b.GetAlternatives()?.Split(',');
            if (alt!=null && alt.Any()) 
                descNums.AddRange(alt);

            var t1 = descNums.Where(x => x != b.part)?
                             .Distinct()?
                             .Take(5);
            var numbers = t1.Any() ? t1.Aggregate((n1, n2) => n1 + ", " + n2).Replace("\\","").Replace("/",""):"";
            var w = _dr.GetElementAttribute("//textarea[@name='autoPartsSubstituteNumbers']", "value");
            if (w != numbers)
                _dr.WriteToSelector("//textarea[@name='autoPartsSubstituteNumbers']", numbers + OpenQA.Selenium.Keys.Tab);
        }
        void PressOkButton() {
            _dr.ButtonClick("//button[contains(@class,'submit__button')]");
        }
        void PressServiseSubmitButton() {
            _dr.ButtonClick("//*[@id='serviceSubmit' and not(contains(text(),'платить')) and not(contains(text(),'Поднять объявление —'))]");
        }
        void PressPublicFreeButton() {
            for (int i = 0; i < 2; i++) {
                _dr.ButtonClick("//button[@id='bulletin_publication_free']");
                if (_dr.GetElementsCount("//button[@id='bulletin_publication_free']") == 0)
                    break;
            }
        }
        void SetDesc(GoodObject b) {
            var d = b.DescriptionList(2979, _addDesc);
            var amount = b.Amount > 0 ? b.Amount.ToString() + " " + b.MeasureNameCorrect : "нет";
            d.Insert(0, "В наличии: " + amount);
            if (b.Price >= _creditPriceMin && b.Price <= _creditPriceMax)
                d.Insert(0, _creditDescription);
            //проверяю текст в поле, если он не изменился - пропускаю обновление
            var w = _dr.GetElementAttribute("//textarea[@name='text']", "value");
            if (w == d.Aggregate((r, l) => r + "\r\n" + l))
                return; 
            d.Add(OpenQA.Selenium.Keys.Tab);
            _dr.ClickToSelector("//textarea[@name='text']");
            _dr.WriteToSelector("//textarea[@name='text']", sl: d);
        }
        void SetPrice(GoodObject b) {
            var w = _dr.GetElementAttribute("//input[@name='price']", "value");
            if (string.IsNullOrEmpty(w) || w != b.Price.ToString())

                //    var desc = b.description.ToLowerInvariant();
                //if (desc.Contains("залог:")) {
                //    var price = desc.Replace("залог:", "|").Split('|').Last().Trim().Replace("р", " ").Split(' ').First();
                //    _dr.WriteToSelector("//input[@name='price']", price);
                //} else
                _dr.WriteToSelector("//input[@name='price']", b.Price.ToString());
        }
        void Up(GoodObject b) {
            if (_dr.GetElementsCount("//a[text()='В корзину']") > 0)
                return;
            if (_dr.GetElementsCount("//a[@class='doDelete']") == 0) { //Удалить объявление - если нет такой кнопки, значит удалено и надо восстановить
                _dr.ButtonClick("//a[contains(@class,'freePublishDraft')]");
                _dr.ButtonClick("//a[contains(@class,'doProlong')]");
                _dr.ButtonClick("//a[@data-applier='recoverBulletin']");
                PressServiseSubmitButton();
                _dr.ButtonClick("//a[@data-applier='prolongBulletin']");
                PressServiseSubmitButton();
                _dr.ButtonClick("//a[contains(@href,'publish')]");
                if (_dr.GetElementsCount("//h2[contains(text(),'нельзя продлить')]") > 0) {
                    Log.Add(_l + b.name + " - ошибка - объявление нельзя восстановить, ссылка удалена!");
                    b.drom = "";
                    Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                        {"id", b.id},
                        {"name", b.name},
                        {_url, b.drom}
                    });
                }
            }
        }
        //подъем объявлений
        public async Task UpAsync() {
            try {
                await Task.Factory.StartNew(() => {
                    var pages = GetPagesCount("non_active");
                    //обрабатываем до 40 страниц неактивных объявлений за раз
                    for (int i = pages; i > 0 && pages - i < 40; i--) {
                        _dr.Navigate("https://baza.drom.ru/personal/non_active/bulletins?page=" + i);
                        _dr.ButtonClick("//label[@class='select-all']/input");
                        if (_dr.GetElementsCount("//button[@value='prolongBulletin' and contains(@class,'button on')]") > 0) {
                            _dr.ButtonClick("//button[@value='prolongBulletin' and contains(@class,'button on')]");
                            PressServiseSubmitButton();
                            EditOffersWithErrors();
                        } else
                            break;
                    }
                });
            } catch (Exception x) {
                throw new Exception("ошибка массового подъема!!\n" + x.Message);
            }
        }
        //редактирую объявления с ошибками (например, не указан вес)
        private void EditOffersWithErrors() {
            var drom = new List<GoodObject>();
            //для каждого объявления, в статусе которого есть предупреждение
            foreach (var item in _dr.FindElements("//div[@class='bull-item-content__content-wrapper']//div[contains(@class,'alert')]/p/../../../../../..")) {
                //собираю для обработки только 3,3% объявлений на странице за раз, чтобы не получить бан
                if (_rnd.Next(30) != 1)
                    continue;
                //сохраняю содержимое предупреждения 
                var status = item.FindElement(By.XPath(".//div[contains(@class,'alert')]/p")).Text;
                //сохраняю id объявления
                var id = item.FindElement(By.XPath(".//a[@data-role='bulletin-link']")).GetAttribute("name");
                //добавляю в список
                drom.Add(new GoodObject {
                    id = id,
                    description = status,
                });
            }
            //обрабатываю получившийся список объявлений с ошибками
            foreach (var item in drom.Where(w => w.description.Contains("содержит ошибки"))) {
                var b = _bus.Find(f => f.drom.Contains(item.id));
                Edit(b);
            }
        }
        //получаю колчество страниц объявлений
        private int GetPagesCount(string type) {
            try {
                _dr.Navigate("https://baza.drom.ru/personal/all/bulletins");
                var count = _dr.GetElementsCount("//div/a[contains(@href,'" + type + "/bulletins')]");
                if (count > 0) {
                    var str = _dr.GetElementText("//div/a[contains(@href,'" + type + "/bulletins')]/../small");
                    var num = HttpUtility.HtmlDecode(str);
                    var i = int.Parse(num);
                    return (i / 50) + 1;
                }
            } catch (Exception x) {
                throw new Exception("ошибка запроса количества объявлений на сайте!!\n" + x.Message);
            }
            return 1;
        }
        public async Task CheckPagesAsync() {
            int count = await DB.GetParamIntAsync("drom.checkPagesCount");
            try {
                await Task.Factory.StartNew(() => {
                    var pages = GetPagesCount("all");
                    for (int i = 0; i < count; i++) {
                        try {
                            var drom = ParsePage(_rnd.Next(1, pages)/ _rnd.Next(1, 6));
                            CheckPage(drom);
                        } catch {
                            //i--;
                            _dr.Refresh();
                            Thread.Sleep(10000);
                        }

                    }
                });
            } catch (Exception x) {
                Debug.WriteLine(_l + " ошибка парсинга! - " + x.Message + " - " + x.InnerException?.Message);
            }
        }
        public async Task CheckOffersAsync() {
            try {
                await Task.Factory.StartNew(() => {
                    int b = DB.GetParamInt("drom.checkOfferIndex");
                    int cnt = DB.GetParamInt("drom.checkOffersCount");
                    var checkOtOfStock = DB.GetParamBool("drom.checkOffersOutOfStock");
                    for (int i = 0; i < cnt;) {
                        try {
                            if (b >= _bus.Count)
                                b = 0;
                            if (
                                (_bus[b].Amount > 0 || checkOtOfStock) &&
                                //_bus[b].images.Count > 0 &&
                                _bus[b].drom.Contains("http")) {
                                i++;
                                Log.Add(_l + _bus[b].name + " - проверяю объявление " + (i + 1) + 
                                    " (" + b + " / " + _bus.Count + ")");
                                //Edit(_bus[b]);
                                _dr.Navigate(_bus[b].drom);
                                Thread.Sleep(1000);
                                SetAddress(_bus[b]);
                                SetPart(_bus[b]);
                                SetAlternativeParts(_bus[b]);
                                SetWeight(_bus[b]);
                                SetPrice(_bus[b]);
                                CheckPhotos(_bus[b]);
                                SetDesc(_bus[b]);
                                Thread.Sleep(2000);
                            }
                            DB.SetParam("drom.checkOfferIndex", (++b).ToString());
                        } catch {
                            _dr.Refresh();
                            Thread.Sleep(20000);
                        }

                    }
                });
            } catch (Exception x) {
                Debug.WriteLine(_l + "ошибка! - " + x.Message + " - " + x.InnerException?.Message);
            }
        }

        void CheckPage(List<GoodObject> drom) {
            for (int i = 0; i < drom.Count; i++) {
                CheckItem(drom[i]);
            }
        }
        void CheckItem(GoodObject b) {
            var i = _bus.FindIndex(f => f.drom.Contains(b.id));
            if (i < 0 && !b.description.Contains("далено")) {
                _dr.Navigate(b.drom);
                Delete(b);
            }
            if (i > -1 &&
                ((b.Price != _bus[i].Price && !_bus[i].description.Contains("Залог:")

                    && DateTime.Now.Minute < 50 //ограничитель периода
                                                //&& _bus[i].price > 500

                ) ||
                !b.description.Contains("далено") && _bus[i].Amount <= 0 ||
                (b.description.Contains("далено") /*|| item.description.Contains("старело")*/) && _bus[i].Amount > 0
                )) {
                Edit(_bus[i]);
            }
        }
        private List<GoodObject> ParsePage(int i) {
            var page = "https://baza.drom.ru/personal/all/bulletins?page=" + i;
            _dr.Navigate(page);
            var drom = new List<GoodObject>();
            foreach (var item in _dr.FindElements("//div[@class='bull-item-content__content-wrapper']")) {
                var el = item.FindElements(By.XPath(".//div[@data-role='price']"));
                var price = el.Count > 0 ? int.Parse(el.First().Text.Replace(" ", "").Replace("р.", "")) : 0;
                var name = item.FindElement(By.XPath(".//a[@data-role='bulletin-link']")).Text.Trim().Replace("\u00ad", "");
                var status = item.FindElement(By.XPath(".//div[contains(@class,'bull-item-content__additional')]")).Text;
                var url = item.FindElement(By.XPath(".//div/div/a")).GetAttribute("href");
                var id = url.Split('-').Last().Split('.').First();

                //количество фото
                var img = item.FindElements(By.XPath(".//div[@class='bull-image-overlay']")).Count;
                if (img == 0)
                    price = 0; //если фотографий 0, то зануляю цену, которую спарсили, что спровоцирует обновление объявления и загрузку фото
                drom.Add(new GoodObject {
                    name = name,
                    id = id,
                    Price = price,
                    description = status,
                    drom = url
                });
            }
            return drom;
        }
        //добавляем фото с дром в карточки товаров
        async Task GetDromPhotos() {
            //если загрузка фотографий включена в настройках
            if (await DB.GetParamBoolAsync("loadPhotosFromDromToBusiness")) {
                //перебираю карточки товара
                for (int b = 0; b < _bus.Count; b++) {
                    //если есть ссылка на дром, нет фото, остаток и цена положительные
                    if (_bus[b].drom.StartsWith("http") && _bus[b].images.Count == 0 && _bus[b].Amount > 0 && _bus[b].Price > 0) {
                        var imgUrls = new List<string>();
                        try {
                            await Task.Factory.StartNew(() => {
                                Log.Add(_l + _bus[b].name + " нет фотографий в карточке!");
                                _dr.Navigate(_bus[b].drom);
                                //получаю количество фотографий 
                                var imgCount = _dr.GetElementsCount("//div[@class='grid-item-wrapper']/img[contains(@src,'drom.ru')]");
                                //перехожу на страницу объявления
                                _dr.ButtonClick("//a[contains(text(),'Вернуться на страницу')]");

                                //открываю фото в полный размер, чтобы получить ссылки на полноразмерные фото
                                _dr.ButtonClick(".image-gallery");
                                //прокручиваю фотографии, т.к. в разметке ссылки только на 3 фотографии
                                for (int i = 0; i < imgCount; i++) {
                                    //получаю ссылки на фото
                                    var url = _dr._drv.FindElements(By.XPath("//img[@class='pswp__img']")).Select(c => c.GetAttribute("src"));
                                    //добавляю в список
                                    imgUrls.AddRange(url);
                                    //пролистываю на следующее фото
                                    _dr.ButtonClick("//button[contains(@class,'button--arrow--right')]", sleep: 5000);
                                }
                                //очищаю список от повторов
                                imgUrls = imgUrls.Distinct().OrderBy(a => a).ToList();
                            });
                            if (imgUrls.Count == 0)
                                throw new Exception("сылки для загрузки на фото не найдены!!");
                            //если ссылки на фото найдены
                            for (int i = 0; i < imgUrls.Count; i++) {
                                //прикрепляю фото в карточку товара в бизнес.ру
                                await Class365API.RequestAsync("post", "goodsimages", new Dictionary<string, string>(){
                                    {"good_id", _bus[b].id},
                                    {"name", i.ToString()},
                                    {"url", imgUrls[i]}
                                });
                                Thread.Sleep(5000);
                            }
                            //запрашиваю карточку
                            var s = await Class365API.RequestAsync("get", "goods", new Dictionary<string, string>() {
                                    {"id", _bus[b].id},
                                    {"name", _bus[b].name},
                                    {"with_additional_fields", "1"}
                               });
                            //проверяю фото
                            var biz = JsonConvert.DeserializeObject<GoodObject[]>(s);
                            if (biz[0].images.Count != imgUrls.Count)
                                throw new Exception("количество фото в карточке не совпадает с ожиданием "
                                    + biz[0].images.Count);
                            //обновляю ссылку на фото
                            _bus[b].images = biz[0].images;
                            Log.Add(_l + imgUrls.Count + " фото обновлено!");
                        } catch (Exception x) {
                            Log.Add(_l + _bus[b].name + " - ошибка загрузки фотографий - " + x.Message);
                        }
                    }
                }
            }
        }
    }

    ////////////////////////////
    /// вспомогательные типы ///
    ////////////////////////////
    public class DromOrder { 
        public string Id { get; set; }
        DateTime date;
        public string Date { 
            get {
                return date.ToString();
            }
            set {
                var nowDate = DateTime.Now.Date;
                var tmpStr = value.Replace("сегодня", nowDate.ToString("d MMMM"))
                                  .Replace("вчера", nowDate.AddDays(-1).ToString("d MMMM"));
                var year = nowDate.Year.ToString();
                if (!tmpStr.EndsWith(year))
                    tmpStr += " " + year;
                //todo it's better to replace this parsing with culture invariant one! this works only with "ru-RU" culture
                date = DateTime.ParseExact(tmpStr, "HH:mm, d MMMM yyyy",CultureInfo.CurrentCulture);
            } }
        public string Status { get; set; }
        public List<DromOrderItem> Items { get; set; }
    }
    public class DromOrderItem {
        public string Id { get; set; }
        public int Count { get; set; }
    }


}