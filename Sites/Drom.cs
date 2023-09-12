using Newtonsoft.Json;
using OpenQA.Selenium;
using Selen.Base;
using Selen.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace Selen.Sites {
    class Drom {
        public Selenium _dr;               //браузер
        DB _db;                     //база данных
        string _url;                //ссылка в карточке товара
        string[] _addDesc;          //дополнительное описание
        int _creditPriceMin;        //цены для рассрочки
        int _creditPriceMax;
        string _creditDescription;  //описание для рассрочки
        bool _addWeights;           //добавление веса 
        float _defWeigth;           //вес по умолчанию 
        List<RootObject> _bus;      //ссылка на товары
        string _dromUrlStart = "https://baza.drom.ru/bulletin/";
        bool _needRestart = false;
        Random _rnd = new Random();
        //конструктор
        public Drom() {
            _db = DB._db;
        }
        public void LoadCookies() {
            if (_dr != null) {
                _dr.Navigate("https://baza.drom.ru/kaluzhskaya-obl/");
                var c = _db.GetParamStr("drom.cookies");
                _dr.LoadCookies(c);
                Thread.Sleep(1000);
            }
        }
        public void SaveCookies() {
            if (_dr != null) {
                _dr.Navigate("https://baza.drom.ru/kaluzhskaya-obl/");
                var c = _dr.SaveCookies();
                if (c.Length > 20)
                    _db.SetParam("drom.cookies", c);
            }
        }
        public void Quit() {
            _dr?.Quit();
            _dr = null;
        }
        public async Task DromStartAsync(List<RootObject> bus) {
            try {
                //сохраняю список товаров
                _bus = bus;
                //получаю номер ссылки в карточке
                _url = await _db.GetParamStrAsync("drom.url");
                //заполнение веса
                _addWeights = await _db.GetParamBoolAsync("drom.addWeights");
                //вес по умолчанию
                _defWeigth = _db.GetParamFloat("defaultWeigth");
                //дополнительное описание
                _addDesc = JsonConvert.DeserializeObject<string[]>(_db.GetParamStr("drom.addDescription"));
                //рассрочка 
                _creditPriceMin = _db.GetParamInt("creditPriceMin"); 
                _creditPriceMax = _db.GetParamInt("creditPriceMax");
                _creditDescription = _db.GetParamStr("creditDescription"); 

                Log.Add("drom.ru: начало выгрузки...");
                await AuthAsync();
                await GetDromPhotos();
                await UpAsync();
                await EditAsync();
                await AddAsync();
                await CheckPagesAsync();
                await CheckOffersAsync();
                Log.Add("drom.ru: выгрузка завершена");
            } catch (Exception x) {
                Log.Add("drom.ru: ошибка синхронизации! - " + x.Message);
                if (x.Message.Contains("timed out") ||
                    x.Message.Contains("already closed") ||
                    x.Message.Contains("HTTP request") ||
                    x.Message.Contains("invalid session id") ||
                    x.Message.Contains("chrome not reachable")) {
                    Log.Add("drom.ru: ошибка браузера! - " + x.Message);
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
                    _dr.WriteToSelector("#sign", _db.GetParamStr("drom.login")); //ввод логина
                    _dr.WriteToSelector("#password", _db.GetParamStr("drom.password")); //пароля
                    _dr.ButtonClick("#signbutton"); //жмем кнопку входа
                    while (_dr.GetElementsCount("//div[@class='personal-box']") == 0) //если элементов слева нет ждем ручной вход
                        Thread.Sleep(30000);
                }
                SaveCookies();
            });
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
                        }
                    }
                }
            });
        }

        void Edit(RootObject b) {
            _dr.Navigate(b.drom, "//input[@name='subject']");
            SetTitle(b);
            CheckPhotos(b);
            SetPrice(b);
            SetOptions(b);
            SetDesc(b);
            SetPart(b);
            SetWeight(b);
            PressOkButton();
            Log.Add("drom.ru: " + b.name + " - объявление обновлено");
            if (b.amount <= 0) {
                Delete();
            } else
                Up(b);
        }
        //проверка фотографий в объявлении
        private void CheckPhotos(RootObject b) {
            //селектор фотографий в объявлении
            string selector = "//div[@class='grid-item-wrapper']/img";
            //получаю количество фотографий в объявлении
            var countReal = _dr.GetElementsCount(selector);
            //количество фотографий, которое должно быть в объявлении
            int countMust = b.images.Count;
            //если расхождение и в карточке количество не нулевое
            if (countMust != countReal && countMust > 1) {
                //удаляю все фото, которые есть объявлении
                for (; countReal > 0; countReal--) {
                    _dr.ButtonClick("//a[text()='Удалить']", 10000);
                }
                //загружаю новые фото
                SetImages(b);
                Log.Add("drom.ru: " + b.name + " - фотографии обновлены");
            }
        }
        //заполнить наименование
        void SetTitle(RootObject b) {
            _dr.WriteToSelector("//input[@name='subject']", b.name);
        }

        void Delete() {
            //переход на страницу объявления, если он нужен
            _dr.ButtonClick("//a[contains(text(),'Вернуться на страницу')]");
            if (_dr.GetElementsCount("//a[contains(@class,'doDelete')]") > 0) {
                Log.Add("drom.ru: удаляю объявление...");
                _dr.ButtonClick("//a[contains(@class,'doDelete')]");
                PressServiseSubmitButton();
                if (_dr.GetElementsCount("//h2[contains(text(),'удалили')]") > 0)
                    Log.Add("drom.ru: объявление удалено");
            }
        }

        public async Task AddAsync() {
            var count = await _db.GetParamIntAsync("drom.addCount");
            for (int b = _bus.Count - 1; b > -1 && count > 0; b--) {
                if ((_bus[b].drom == null || !_bus[b].drom.Contains("http")) &&
                    !_bus[b].GroupName().Contains("ЧЕРНОВИК") &&
                    _bus[b].amount > 0 &&
                    _bus[b].price > 0 &&
                    _bus[b].images.Count > 0) {
                    var t = Task.Factory.StartNew(() => {
                        _dr.Navigate("http://baza.drom.ru/set/city/370?return=http%3A%2F%2Fbaza.drom.ru%2Fadding%3Fcity%3D370", "//input[@name='subject']");
                        if (_dr.GetElementsCount("//div[@class='image-wrapper']/img") > 0)
                            throw new Exception("Черновик уже заполнен!");//если уже есть блок фотографий на странице, то черновик уже заполнен, но не опубликован по какой-то причине, например, номер запчасти похож на телефонный номер - объявление не опубликовано, либо превышен дневной лимит подачи
                        SetTitle(_bus[b]);
                        _dr.ButtonClick("//div[@class='table-control']//label");//Автозапчасти или диски - первая кнопка
                        _dr.ButtonClick("//p[@class='type_caption']"); //одна запчасть или 1 комплект - первая кнопка
                        SetPart(_bus[b]);
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
                        Log.Add("drom.ru: добавлено объявление - " + _bus[b].name);
                        count--;
                    } catch (Exception x) {
                        Log.Add("drom.ru: ошибка добавления! - " + _bus[b].name + " - " + x.Message);
                        break;
                    }
                }
            }
        }
        private void SetWeight(RootObject b) {
            if (_addWeights) {
                var weight = b.weight ?? _defWeigth;
                if (weight == 0)
                    weight = _defWeigth;
                _dr.WriteToSelector("//input[@name='delivery[postProviderWeight]']",
                    weight.ToString("0.00").Replace(",", ".") + OpenQA.Selenium.Keys.Tab);
            }
        }
        async Task SaveUrlAsync(int b) {
            string new_id = _dr.GetUrl().Split('-').Last().Split('.').First();
            _bus[b].drom = _dromUrlStart + new_id + "/edit";
            await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                {"id", _bus[b].id},
                {"name", _bus[b].name},
                {_url, _bus[b].drom}
            });
        }
        void SetAudioSize(RootObject b) {
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
        void SetDiam(RootObject b) {
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
        void SetOptions(RootObject b) {
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
        void SetImages(RootObject b) {
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
        }
        void SetPart(RootObject b) {
            _dr.WriteToSelector("//input[@name='autoPartsOemNumber']", b.part?.Split(',').First() + OpenQA.Selenium.Keys.Tab);
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
        void SetDesc(RootObject b) {
            var d = b.DescriptionList(2999, _addDesc);
            if (b.price >= _creditPriceMin && b.price <= _creditPriceMax)
                d.Insert(0, _creditDescription);
            d.Add(OpenQA.Selenium.Keys.Tab);
            _dr.WriteToSelector("//textarea[@name='text']", sl: d);
        }
        void SetPrice(RootObject b) {
            var desc = b.description.ToLowerInvariant();
            if (desc.Contains("залог:")) {
                var price = desc.Replace("залог:", "|").Split('|').Last().Trim().Replace("р", " ").Split(' ').First();
                _dr.WriteToSelector("//input[@name='price']", price);
            } else
                _dr.WriteToSelector("//input[@name='price']", b.price.ToString());
        }
        void Up(RootObject b) {
            if (_dr.GetElementsCount("//a[text()='Купить']") > 0)
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
                    Log.Add("drom.ru: " + b.name + "+ - ошибка, объявление нельзя восстановить, удаляю ссылку!");
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
            var drom = new List<RootObject>();
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
                drom.Add(new RootObject {
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
            int count = await _db.GetParamIntAsync("drom.checkPagesCount");
            try {
                await Task.Factory.StartNew(() => {
                    var pages = GetPagesCount("all") / 2;//todo убрать
                    for (int i = 0; i < count; i++) {
                        try {
                            var drom = ParsePage(_rnd.Next(1, pages));
                            CheckPage(drom);
                        } catch {
                            //i--;
                            _dr.Refresh();
                            Thread.Sleep(10000);
                        }

                    }
                });
            } catch (Exception x) {
                Debug.WriteLine("drom.ru: ошибка парсинга! - " + x.Message + " - " + x.InnerException.Message);
            }
        }
        public async Task CheckOffersAsync() {
            try {
                await Task.Factory.StartNew(() => {
                    int b = _db.GetParamInt("drom.checkOfferIndex");
                    int cnt = _db.GetParamInt("drom.checkOffersCount");
                    for (int i = 0; i < cnt; i++) {
                        try {
                            if (b >= _bus.Count)
                                b = 0;
                            if (_bus[b].amount > 0 &&
                                _bus[b].images.Count > 0 &&
                                _bus[b].drom.Contains("http")) {
                                Log.Add("drom.checkOffers: " + _bus[b].name + " - проверяю объявление "+(i+1)+" (" + b + " / " + _bus.Count + ")");
                                _dr.Navigate(_bus[b].drom);
                                //адрес самовывоза из профиля
                                if (_dr.GetElementCSSValue("//label[contains(text(),'Применить из профиля')]", "background").Contains("rgb(255, 255, 255)")) { 
                                    _dr.ButtonClick("//label[contains(text(),'Применить из профиля')]");
                                    Log.Add("drom.checkOffers: " + _bus[b].name + " - установлен адрес самовывоза");
                                }
                                //проверка заполнения веса
                                var w = _dr.GetElementAttribute("//input[@name='delivery[postProviderWeight]']", "value").Replace(".", ",");
                                if (string.IsNullOrEmpty(w) || float.Parse(w) != _bus[b].GetWeight()) {
                                    SetWeight(_bus[b]);
                                    Log.Add("drom.checkOffers: " + _bus[b].name + " - установлен вес " + _bus[b].weight);
                                    Thread.Sleep(5000);
                                }
                                //проверка фото
                                CheckPhotos(_bus[b]);
                                //проверка описания
                                if (_bus[b].price >= _creditPriceMin && _bus[b].price <= _creditPriceMax) {
                                    var d = _dr.GetElementAttribute("//label[text()='Текст объявления']/../../div/textarea", "value");
                                    if (!d.Contains(_creditDescription))
                                        SetDesc(_bus[b]);
                                }
                                Thread.Sleep(3000);
                            } else
                                i--;
                            _db.SetParam("drom.checkOfferIndex", (++b).ToString());
                        } catch {
                            _dr.Refresh();
                            Thread.Sleep(20000);
                        }

                    }
                });
            } catch (Exception x) {
                Debug.WriteLine("drom.checkOffers: ошибка! - " + x.Message + " - " + x.InnerException.Message);
            }
        }

        void CheckPage(List<RootObject> drom) {
            for (int i = 0; i < drom.Count; i++) {
                CheckItem(drom[i]);
            }
        }
        void CheckItem(RootObject item) {
            var i = _bus.FindIndex(f => f.drom.Contains(item.id));
            if (i < 0 && !item.description.Contains("далено")) {
                _dr.Navigate(item.drom);
                Delete();
            }
            if (i > -1 &&
                ((item.price != _bus[i].price && !_bus[i].description.Contains("Залог:")

                    && DateTime.Now.Minute < 50 //ограничитель периода
                                                //&& _bus[i].price > 500

                ) ||
                !item.description.Contains("далено") && _bus[i].amount <= 0 ||
                (item.description.Contains("далено") /*|| item.description.Contains("старело")*/) && _bus[i].amount > 0
                )) {
                Edit(_bus[i]);
            }
        }
        private List<RootObject> ParsePage(int i) {
            var page = "https://baza.drom.ru/personal/all/bulletins?page=" + i;
            _dr.Navigate(page);
            var drom = new List<RootObject>();
            foreach (var item in _dr.FindElements("//div[@class='bull-item-content__content-wrapper']")) {
                var el = item.FindElements(By.XPath(".//span[@data-role='price']"));
                var price = el.Count > 0 ? int.Parse(el.First().Text.Replace(" ", "").Replace("₽", "")) : 0;
                var name = item.FindElement(By.XPath(".//a[@data-role='bulletin-link']")).Text.Trim().Replace("\u00ad", "");
                var status = item.FindElement(By.XPath(".//div[contains(@class,'bull-item-content__additional')]")).Text;
                var id = item.FindElement(By.XPath(".//a[@data-role='bulletin-link']")).GetAttribute("name");
                var url = item.FindElement(By.XPath(".//div/div/a")).GetAttribute("href");
                //количество фото
                var img = item.FindElements(By.XPath(".//div[@class='bull-image-overlay']")).Count;
                if (img == 0)
                    price = 0; //если фотографий 0, то зануляю цену, которую спарсили, что спровоцирует обновление объявления и загрузку фото
                drom.Add(new RootObject {
                    name = name,
                    id = id,
                    price = price,
                    description = status,
                    drom = url
                });
            }
            return drom;
        }
        //добавляем фото с дром в карточки товаров
        async Task GetDromPhotos() {
            //если загрузка фотографий включена в настройках
            if (await _db.GetParamBoolAsync("loadPhotosFromDromToBusiness")) {
                //перебираю карточки товара
                for (int b = 0; b < _bus.Count; b++) {
                    //если есть ссылка на дром, нет фото, остаток и цена положительные
                    if (_bus[b].drom.StartsWith("http") && _bus[b].images.Count == 0 && _bus[b].amount > 0 && _bus[b].price > 0) {
                        var imgUrls = new List<string>();
                        try {
                            await Task.Factory.StartNew(() => {
                                Log.Add("drom.ru: " + _bus[b].name + " нет фотографий в карточке!");
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
                            var biz = JsonConvert.DeserializeObject<RootObject[]>(s);
                            if (biz[0].images.Count != imgUrls.Count)
                                throw new Exception("количество фото в карточке не совпадает с ожиданием "
                                    + biz[0].images.Count);
                            //обновляю ссылку на фото
                            _bus[b].images = biz[0].images;
                            Log.Add("drom.ru: " + imgUrls.Count + " фото обновлено!");
                        } catch (Exception x) {
                            Log.Add("drom.ru: " + _bus[b].name + " - ошибка загрузки фотографий - " + x.Message);
                        }
                    }
                }
            }
        }
    }
}