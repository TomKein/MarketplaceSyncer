using Newtonsoft.Json;
using Selen.Base;
using Selen.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Selen.Sites {
    class GdeRu {
        Selenium _dr;               //браузер
        DB _db;                     //база данных
        string _url;                //ссылка в карточке товара
        string[] _addDesc;          //дополнительное описание
        List<RootObject> _bus;      //ссылка на товары
        Random _rnd = new Random(); //генератор случайных чисел
        //конструктор
        public GdeRu() {
            _db = DB._db;
        }
        //загрузка кукис
        public void LoadCookies() {
            if (_dr != null) {
                _dr.Navigate("https://kaluga.gde.ru/user/login");
                var c = _db.GetParamStr("gde.cookies");
                _dr.LoadCookies(c);
                Thread.Sleep(1000);
            }
        }
        //сохранение кукис
        public void SaveCookies() {
            if (_dr != null) {
                _dr.Navigate("https://kaluga.gde.ru/user/login");
                var c = _dr.SaveCookies();
                if (c.Length > 20)
                    _db.SetParam("gde.cookies", c);
            }
        }
        //закрытие браузера
        public void Quit() {
            _dr?.Quit();
            _dr = null;
        }
        //старт главного цикла синхронизации
        public async Task<bool> StartAsync(List<RootObject> bus) {
            if (await _db.GetParamBoolAsync("gde.syncEnable")) {
                Log.Add("gde.ru: начало выгрузки...");
                _bus = bus;
                for (int i = 0; ; i++) {
                    try {
                        await AuthAsync();
                        await EditAsync();
                        await AddAsync();
                        await ParseAsync();
                        await CheckUrls();
                        Log.Add("gde.ru: выгрузка завершена");
                        return true;
                    } catch (Exception x) {
                        Log.Add("gde.ru: ошибка синхронизации! - " + x.Message);
                        if (x.Message.Contains("timed out") ||
                        x.Message.Contains("already closed") ||
                        x.Message.Contains("invalid session id") ||
                        x.Message.Contains("chrome not reachable")) {
                            Log.Add("gde.ru: ошибка браузера! - " + x.Message);
                            _dr.Quit();
                            _dr = null;
                        }
                        if (i >= 10) break;
                        await Task.Delay(10000);
                    }
                }
            }
            return false;
        }
        //авторизация
        async Task AuthAsync() {
            await Task.Factory.StartNew(() => {
                _url = _db.GetParamStr("gde.url");
                _addDesc = JsonConvert.DeserializeObject<string[]>(
                    _db.GetParamStr("gde.addDescription"));
                if (_dr == null) {
                    _dr = new Selenium();
                    LoadCookies();
                }
                _dr.Navigate("https://kaluga.gde.ru/user/login");
                var login = _db.GetParamStr("gde.login");
                if (!_dr.GetElementText("#LoginForm_email").Contains(login)) {
                    _dr.WriteToSelector("#LoginForm_email", login);
                    _dr.WriteToSelector("#LoginForm_password", _db.GetParamStr("gde.password"));
                    _dr.ButtonClick("//fieldset//input[@type='submit']");
                    //если в кабинет не попали - ждем ручной вход
                    for (int i = 0; ; i++) {
                        if (_dr.GetUrl().Contains("user/login")) {
                            Log.Add("gde.ru: ошибка авторизации в кабинете, ждем ручной вход...");
                            Thread.Sleep(60000);
                        } else break;
                        if (i > 10) throw new Exception("timed out - превышено время ожидания!");
                    }
                }
                SaveCookies();
            });
        }
        //обновление объявлений
        async Task EditAsync() {
            for (int b = 0; b < _bus.Count; b++) {
                if (_bus[b].gde != null &&
                    _bus[b].gde.Contains("http")) {
                    //удаляю если нет на остатках
                    if (_bus[b].amount <= 0) {
                        await DeleteAsync(b);
                        //убираю ссылку из карточки товара
                        _bus[b].gde = " ";
                        await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                            { "id", _bus[b].id },
                            { "name", _bus[b].name },
                            { _url, _bus[b].gde }
                        });
                        Log.Add("gde.ru: " + _bus[b].name + " - ссылка из карточки удалена");
                    } else if (_bus[b].IsTimeUpDated() && _bus[b].price > 0) {
                        await EditOfferAsync(b);
                    }
                }
            }
        }
        //удаление объявления
        async Task DeleteAsync(int b) {
            try {
                await Task.Factory.StartNew(() => {
                    var url = _bus[b].gde.Replace("/update", "/delete");
                    _dr.Navigate(url);
                    //TODO добавить проверку удаления
                    Log.Add("gde.ru: " + _bus[b].name + " - объявление удалено");
                    Thread.Sleep(3000);
                });
            } catch (Exception x) {
                Log.Add("gde.ru: ошибка удаления! - " + _bus[b].name + " - " + x.Message);
            }
        }
        //редактирование объявления асинхронно
        async Task EditOfferAsync(int b) {
            await Task.Factory.StartNew(() => {
                EditOffer(b);
            });
        }
        //редактирование объявления синхронно
        void EditOffer(int b) {
            _dr.Navigate(_bus[b].gde);
            SetTitle(b);
            SetPrice(b);
            SetDesc(b);
            //проверка фото
            var photos = _dr.FindElements("//a[text()='Удалить']");
            if (photos.Count != (_bus[b].images.Count > 20 ? 20 : _bus[b].images.Count)) {
                Log.Add("gde.ru: " + _bus[b].name + " - обновляю фотографии");
                foreach (var photo in photos) {
                    photo.Click();
                    Thread.Sleep(1000);
                }
                SetImages(b);
            }
            PressOkButton();
            Log.Add("gde.ru: " + _bus[b].name + " - объявление обновлено");
        }
        //пишу название
        private void SetTitle(int b) {
            _dr.WriteToSelector("#AInfoForm_title", _bus[b].NameLength(80));
        }
        //пишу цену
        private void SetPrice(int b) {
            _dr.WriteToSelector("#AInfoForm_price", _bus[b].price.ToString());
        }
        //пишу описание
        void SetDesc(int b) {
            _dr.WriteToSelector("#AInfoForm_content", sl: _bus[b].DescriptionList(2900, _addDesc));
        }
        //загрузка фото
        void SetImages(int b) {
            WebClient cl = new WebClient();
            cl.Encoding = Encoding.UTF8;
            var num = _bus[b].images.Count > 20 ? 20 : _bus[b].images.Count;
            for (int u = 0; u < num; u++) {
                try {
                    byte[] bts = cl.DownloadData(_bus[b].images[u].url);
                    File.WriteAllBytes("gde_" + u + ".jpg", bts);
                    Thread.Sleep(1000);
                    _dr.SendKeysToSelector("input[name=file]", Application.StartupPath + "\\" + "gde_" + u + ".jpg ");
                } catch (Exception x) {
                    Log.Add("gde.ru: " + _bus[b].name + " - ошибка загрузки фото - " + _bus[b].images[u].url);
                    Thread.Sleep(1000);
                }
            }
            Thread.Sleep(5000);
            cl.Dispose();
        }
        //жму кнопку ок
        void PressOkButton() {
            _dr.ButtonClick("#post-item");
        }
        //пишу адрес
        private void SetAddr() {
            _dr.WriteToSelector("#AInfoForm_address", _db.GetParamStr("gde.address"));
        }
        //пишу телефон
        private void SetPhone() {
            _dr.WriteToSelector("#AInfoForm_phone", _db.GetParamStr("gde.phone"));
        }
        //галочка "бесплатно"
        private void CheckFreeOfCharge() {
            _dr.ButtonClick("//span[contains(text(),'Бесплатно')]");
        }
        //проверка наличия объявления в заблокированных
        bool IsNonActive(int b) {
            _dr.Navigate("https://kaluga.gde.ru/cabinet/ads/index/status/notactive");
            var items = _dr.FindElements(".title a");
            var names = items.Select(s => s.Text).ToList();
            if (names.Exists(s => s == _bus[b].name)) return true;
            return false;
        }
        //выкладываю объявления
        public async Task AddAsync() {
            var count = _db.GetParamInt("gde.addCount");
            for (int b = 0; b < _bus.Count && count > 0; b++) {
                if ((_bus[b].gde == null || !_bus[b].gde.Contains("http")) &&
                     _bus[b].tiu.Contains("http") &&
                     _bus[b].amount > 0 &&
                     _bus[b].price >= 0 &&
                     _bus[b].images.Count > 0) {

                    var t = Task.Factory.StartNew(() => {
                        if (!IsNonActive(b)) {
                            _dr.Navigate("https://kaluga.gde.ru/post");
                            SetTitle(b);
                            SetPhone();
                            SetCategory(b);
                            SetDesc(b);
                            SetPrice(b);
                            SetAddr();
                            SetImages(b);
                            CheckFreeOfCharge();
                            PressOkButton();
                        }
                    });
                    try {
                        await t;
                        await SaveUrlAsync(b);
                        count--;
                        Log.Add("gde.ru: " + _bus[b].name + " - объявление добавлено, осталось (" + count + ")");
                    } catch (Exception x) {
                        Log.Add("gde.ru: " + _bus[b].name + " - ошибка добавления! - " + x.Message);
                        break;
                    }
                }
            }
        }
        //сохраняю ссылку на объявление
        async Task SaveUrlAsync(int b) {
            if (_dr.GetUrl().Contains("postLast")) {
                var url = @"https://kaluga.gde.ru/cabinet/item/update?id=" + _dr.GetUrl().Split('/').Last();
                _bus[b].gde = url;
                await Class365API.RequestAsync("put", "goods", new Dictionary<string, string> {
                                {"id", _bus[b].id},
                                {"name", _bus[b].name},
                                {_url, _bus[b].gde}
                            });
            }
        }
        //выбор категории
        private void SetCategory(int b) {
            var name = _bus[b].name.ToLowerInvariant();
            var desc = _bus[b].description.ToLowerInvariant();
            //выбираем Автомобили и мотоциклы
            _dr.ButtonClick("//li[text()='Автомобили и мотоциклы']");
            //подгруппы
            switch (_bus[b].GroupName()) {
                case "Шины, диски, колеса":
                    _dr.ButtonClick("//li[contains(text(),'Шины и диски')]");
                    break;
                default:
                    _dr.ButtonClick("//li[contains(text(),'Запчасти и аксессуары')]");
                    break;
            }
            //далее подгруппы
            Thread.Sleep(2000);
            switch (_bus[b].GroupName()) {
                case "Аудио-видеотехника":
                    _dr.ButtonClick("//li[contains(text(),'Тюнинг и автозвук')]");
                    break;
                case "Автохимия":
                    _dr.ButtonClick("//li[contains(text(),'Средства для ухода за авто')]");
                    break;
                case "Шины, диски, колеса":
                    if (name.Contains("диск"))
                        _dr.ButtonClick("//li[text()='Диски']");
                    else if (name.Contains("шины") || name.Contains("шина"))
                        _dr.ButtonClick("//li[text()='Шины']");
                    else
                        _dr.ButtonClick("//li[text()='Колеса']");
                    Thread.Sleep(2000);
                    if (name.Contains("лит"))
                        _dr.ButtonClick("//li[text()='Литые']");
                    else if (name.Contains("кован"))
                        _dr.ButtonClick("//li[text()='Кованные']");
                    else
                        _dr.ButtonClick("//li[text()='Штампованные']");
                        _dr.ButtonClick("//li[text()='Всесезонные']");
                    //размер диска
                    var size = _bus[b].GetDiskSize();
                    _dr.ButtonClick("//span[contains(text(),'Выберите вариант')]/..");
                    _dr.ButtonClick("//div[contains(@class,'selectmenu-open')]//li[text()='" + size + "']");
                    //ширина обода
                    _dr.ButtonClick("//span[contains(text(),'Выберите вариант')]/..");
                    _dr.ButtonClick("//div[contains(@class,'selectmenu-open')]//li[text()='10']");
                    //количество отверстий
                    _dr.ButtonClick("//span[contains(text(),'Выберите вариант')]/..");
                    _dr.ButtonClick("//div[contains(@class,'selectmenu-open')]//li[text()='"+_bus[b].GetNumberOfHoles()+"']");
                    //диам отверстий
                    _dr.ButtonClick("//span[contains(text(),'Выберите вариант')]/..");
                    _dr.ButtonClick("//div[contains(@class,'selectmenu-open')]//li[text()='"+_bus[b].GetDiameterOfHoles()+"']");
                    //вылет
                    _dr.WriteToSelector("//label[text()='Вылет (ЕТ)']/..//input", "10");
                    //модель
                    _dr.ButtonClick("//span[contains(text(),'Выберите вариант')]/..");
                    var stamp = name.Contains("лит") || name.Contains("кован") ? "Прочие" : "Штампованные";
                    _dr.ButtonClick("//div[contains(@class,'menu-open')]//li[text()='" + stamp + "']");
                    //дополнительно
                    _dr.ButtonClick("//div[contains(@class,'menu-open')]//li[text()='155']");
                    _dr.ButtonClick("//span[contains(text(),'Выберите вариант')]/..");
                    _dr.ButtonClick("//div[contains(@class,'menu-open')]//li[text()='50']");
                    break;
                default:
                    _dr.ButtonClick("//li[text()='Запчасти']");
                    break;
            }
            Thread.Sleep(6000);
        }
        //парсинг объявлений
        async Task ParseAsync() {
            //парсинг случайных страниц
            _dr.Navigate("https://kaluga.gde.ru/cabinet/ads/index");
            var ElementString = _dr.GetElementText("//ul[@class='tabs-list']/li[@class='active']");
            var pageCountString = Regex.Match(ElementString, @"\d+").Groups[0].Value;
            var pageCount = string.IsNullOrEmpty(pageCountString) ? 0 : int.Parse(pageCountString) / 20;
            var checkPagesProcent = _db.GetParamInt("gde.checkPagesProcent");
            for (int i = 0; i < pageCount; i++) {
                //пропуск страниц
                if (_rnd.Next(100) > checkPagesProcent) continue;
                await ParsePage(i + 1);
            }
        }
        //парсинг отдельной страницы
        async Task ParsePage(int p) {
            try {
                await Task.Factory.StartNew(() => {
                    _dr.Navigate("https://gde.ru/cabinet/items/" + p, tryCount: 2);
                    var names = _dr.FindElements("//strong[@class='title']/a").Select(s => s.Text).ToList();
                    var prices = _dr.FindElements("//strong[@class='price']").Select(s => s.Text.Split(' ').First()).ToList();
                    var ids = _dr.FindElements("//span[@class='id']").Select(s => s.Text.Split(' ').Last()).ToList();
                    if (names.Count() != prices.Count() ||
                        names.Count() != ids.Count()) {
                        throw new Exception("количество элементов не совпадает!");
                    }
                    for (int i = 0; i < ids.Count; i++) {
                        var b = _bus.FindIndex(f => f.gde.Contains(ids[i]));
                        if (b == -1) {
                            _dr.Navigate("https://kaluga.gde.ru/cabinet/item/delete?id=" + ids[i]);
                        } else if (_bus[b].price.ToString() != prices[i] ||
                                  !_bus[b].name.Contains(names[i])) {
                            EditOffer(b);
                        }
                    }
                });
            } catch (Exception x) {
                if (x.Message.Contains("timed out")) throw;
                Log.Add("gde.ru: ошибка парсинга страницы - " + x.Message);
            }
        }
        //проверка случайных ссылок
        async Task CheckUrls() {
            try {
                await Task.Factory.StartNew(() => {
                    var n = _db.GetParamInt("gde.checkUrlCount");
                    for (int i = 0; i < n;) {
                        var b = _rnd.Next(_bus.Count);
                        if (string.IsNullOrEmpty(_bus[b].gde)) continue;
                        _dr.Navigate(_bus[b].gde);
                        //проверка загрузки страницы
                        if (_dr.GetElementsCount("//input[@id='AInfoForm_title']") == 0) {
                            Log.Add("gde.ru: ошибка загрузки объявления! - " + _bus[b].name);
                            continue;
                        }
                        //проверка названия и цены
                        var name = _dr.GetElementAttribute("//input[@id='AInfoForm_title']", "value");
                        var price = int.Parse(_dr.GetElementAttribute("//input[@id='AInfoForm_price']", "value"));
                        var photos = _dr.FindElements("//a[text()='Удалить']");
                        if (name.Length <= 5 ||
                            !_bus[b].name.Contains(name) ||
                            _bus[b].price != price ||
                            photos.Count != (_bus[b].images.Count > 20 ? 20 : _bus[b].images.Count)) {
                            EditOffer(b);
                        }
                        i++;
                    }
                });
            } catch (Exception x) {
                Log.Add("kupiprodai.ru: ошибка при проверке ссылок - " + x.Message);
            }
        }
    }
}
