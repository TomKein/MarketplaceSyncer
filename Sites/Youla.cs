using Newtonsoft.Json;
using Selen.Base;
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
                _dr.Navigate("https://youla.ru/pro");
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
                        await EditAsync();
                        await AddAsync();
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
                _dr.Navigate("https://youla.ru/pro");
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
                        if (_dr.GetElementsCount("//a[@href='/login' and @data-test-action='LoginClick']") > 0) {
                            Log.Add("youla.ru: ошибка авторизации в кабинете (" + i + ")...");
                            Thread.Sleep(60000);
                        } else break;
                        if (i > 30) throw new Exception("timed out - ошибка! превышено время ожидания авторизации!");
                    }
                }
                SaveCookies();
            });
        }
        //обновление объявлений
        async Task EditAsync() {
            for (int b = 0; b < _bus.Count; b++) {
                if (_bus[b].youla != null &&
                    _bus[b].youla.Contains("http")) {
                    //удаляю если нет на остатках
                    if (_bus[b].amount <= 0) {
                        await DeleteAsync(b);
                        //убираю ссылку из карточки товара
                        _bus[b].youla = " ";
                        await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                            { "id", _bus[b].id },
                            { "name", _bus[b].name },
                            { _url, _bus[b].youla }
                        });
                        Log.Add("youla.ru: " + _bus[b].name + " - ссылка из карточки удалена");
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
                    _dr.Navigate(_bus[b].youla);
                    //кнопка снять с публикации
                    _dr.ButtonClick("//button[@data-test-action='ProductWithdrawClick']");
                    //кнопка другая причина
                    _dr.ButtonClick("//button[@data-test-action='ArchivateClick']", 3000);
                    Log.Add("youla.ru: " + _bus[b].name + " - объявление снято");
                    //кнопка удалить объявление
                    _dr.ButtonClick("//button[@data-test-action='ProductDeleteClick']");
                    //кнопка удалить
                    _dr.ButtonClick("//button[@data-test-action='ConfirmModalApply']", 5000);
                    Log.Add("youla.ru: " + _bus[b].name + " - объявление удалено");
                });
            } catch (Exception x) {
                Log.Add("youla.ru: ошибка удаления! - " + _bus[b].name + " - " + x.Message);
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
            _dr.Navigate(_bus[b].youla,"//h2");
            _dr.ButtonClick("//a[@data-test-action='ProductEditClick']", 2000);
            SetTitle(b);
            SetPrice(b);
            SetDesc(b);
            SetPart(b);
            SetMarkAndModel(b);
            _dr.ButtonClick("//div[@data-name='attributes.type_classified']");
            _dr.ButtonClick("//div[@class='Select-menu-outer']//div[text()='Магазин']");
            //проверка фото
            var photos = _dr.FindElements("//div[@id='images']//button[1]");
            if (photos.Count != (_bus[b].images.Count > 10 ? 10 : _bus[b].images.Count)) {
                Log.Add("youla.ru: " + _bus[b].name + " - обновляю фотографии");
                foreach (var photo in photos) {
                    photo.Click();
                    Thread.Sleep(1000);
                }
                SetImages(b);
            }
            PressOkButton();
            Log.Add("youla.ru: " + _bus[b].name + " - объявление обновлено");
        }
        //пишу название
        private void SetTitle(int b) {
            _dr.WriteToSelector("//input[@name='name']", _bus[b].NameLength(50)+OpenQA.Selenium.Keys.PageDown);
        }
        //пишу цену
        private void SetPrice(int b) {
            _dr.WriteToSelector("//input[@name='price']", _bus[b].price.ToString());
        }
        //пишу описание
        void SetDesc(int b) {
            _dr.WriteToSelector("//textarea[@name='description']", sl: _bus[b].DescriptionList(2990, _addDesc));
        }
        //пишу артикул
        void SetPart(int b) {
            _dr.WriteToSelector("//input[@name='attributes.part_number']", 
                !string.IsNullOrEmpty(_bus[b].part) ? _bus[b].part.Split(',').First() :"");
        }
        //пишу марку и модель
        void SetMarkAndModel(int b) {
            var m = _bus[b].GetNameMarkModel();
            if (m == null) return;
            _dr.ButtonClick("//div[@data-name='attributes.auto_brand']");
            _dr.WriteToSelector("//div[@data-name='attributes.auto_brand']//input",m[1]);
            _dr.ButtonClick("//div[@data-name='attributes.auto_model']");
            _dr.WriteToSelector("//div[@data-name='attributes.auto_model']//input", m[2]);
        }
        //загрузка фото
        void SetImages(int b) {
            WebClient cl = new WebClient();
            cl.Encoding = Encoding.UTF8;
            var num = _bus[b].images.Count > 10 ? 10 : _bus[b].images.Count;
            for (int u = 0; u < num; u++) {
                try {
                    byte[] bts = cl.DownloadData(_bus[b].images[u].url);
                    File.WriteAllBytes("youla_" + u + ".jpg", bts);
                    Thread.Sleep(200);
                    _dr.SendKeysToSelector("//input[@type='file']", Application.StartupPath + "\\" + "youla_" + u + ".jpg ");
                } catch (Exception x) {
                    Log.Add("youla.ru: " + _bus[b].name + " - ошибка загрузки фото - " + _bus[b].images[u].url);
                    Thread.Sleep(1000);
                }
            }
            Thread.Sleep(1000);
            cl.Dispose();
        }
        //жму кнопку ок
        void PressOkButton(int i=1) {
            _dr.ButtonClick("//button[@type='submit']", 5000);
            if (i == 2) _dr.ButtonClick("//span[text()='Опубликовать объявление']/..", 5000);
        }
        //выкладываю объявления
        public async Task AddAsync() {
            var count = await _db.GetParamIntAsync("youla.addCount");
            for (int b = _bus.Count - 1; b > -1 && count > 0; b--) {
                if ((_bus[b].youla == null || !_bus[b].youla.Contains("http")) &&
                     _bus[b].tiu.Contains("http") &&
                     _bus[b].amount > 0 &&
                     _bus[b].price >= 2000 &&
                     _bus[b].images.Count > 0) {
                    try {
                        if (await Task.Factory.StartNew(() => {
                            if (_dr.GetUrl()!= "https://youla.ru/product/create")
                                _dr.Navigate("https://youla.ru/product/create");
                            if (!SetCategory(b)) return false;
                            SetTitle(b);
                            SetImages(b);
                            SetDesc(b);
                            SetPrice(b);
                            SetAddr();
                            SetPart(b);
                            SetMarkAndModel(b);
                            PressOkButton(2);
                            return true;
                        })) {
                            await SaveUrlAsync(b);
                            Log.Add("youla.ru: " + _bus[b].name + " - объявление добавлено, осталось (" + --count + ")");
                        }
                    } catch (Exception x) {
                        Log.Add("youla.ru: " + _bus[b].name + " - ошибка добавления! - " + x.Message);
                        break;
                    }
                }
            }
        }
        //сохраняю ссылку на объявление
        async Task SaveUrlAsync(int b) {
            var url = _dr.GetElementAttribute("//a[text()='перейти к объявлению']", "href");
            if (url.Contains("youla.ru")) {
                _bus[b].youla = url;
                await Class365API.RequestAsync("put", "goods", new Dictionary<string, string> {
                    {"id", _bus[b].id},
                    {"name", _bus[b].name},
                    {_url, _bus[b].youla}
                });
            }
        }
        //заполняю адрес магазина
        private void SetAddr() {
            while (_dr.GetElementAttribute("//input[@placeholder='Введите город, улицу, дом']","value")!= "Россия, Калуга, Московская улица, 331") {
                _dr.WriteToSelector("//div[contains(@class,'_yjs_geolocation-map')]//input", " Калуга, Московская улица, 331");
                Thread.Sleep(4000);
                _dr.SendKeysToSelector("//div[contains(@class,'_yjs_geolocation-map')]//input",
                    OpenQA.Selenium.Keys.ArrowDown+OpenQA.Selenium.Keys.Enter);
            }
        }
        //выбор категории
        void Select(string seltype="", string type="", string selgroup="", string group="", string selname="", string name="") {
            //тип
            if (seltype.Length > 0) {
                _dr.ButtonClick("//div[@data-name='attributes." + seltype + "']",2000);
                _dr.ButtonClick("//div[@class='Select-menu-outer']//div[text()='" + type + "']", 3000);
            }
            //группа деталей
            if (selgroup.Length > 0) {
                _dr.ButtonClick("//div[@data-name='attributes." + selgroup + "']", 2000);
                _dr.ButtonClick("//div[@class='Select-menu-outer']//div[text()='" + group + "']", 3000);
            }
            //название детали
            if (selname.Length > 0) {
                _dr.ButtonClick("//div[@data-name='attributes." + selname + "']", 2000);
                _dr.ButtonClick("//div[@class='Select-menu-outer']//div[text()='" + name + "']", 3000);
            }
        }
        //выбор категории
        bool SetCategory(int b) {
            var name = _bus[b].name.ToLowerInvariant();
            var desc = _bus[b].description.ToLowerInvariant();
            //основная категория
            switch (_bus[b].GroupName()) {
                case "Шины, диски, колеса":
                    //_dr.ButtonClick("//div[text()='Шины и диски']");
                    return false;
                case "Аудио-видеотехника":
                    //_dr.ButtonClick("//div[text()='Аудио и видео']");
                    return false;
                case "Автохимия":
                    //_dr.ButtonClick("//div[text()='Масла и автохимия']");
                    return false;
                default:
                    _dr.ButtonClick("//div[text()='Запчасти']");
                    if (name.Contains("ступица")) Select("avtozapchasti_tip", "Подвеска", "kuzovnaya_detal", "Ступица", "chast_detali", "Ступица");
                    else if (name.Contains("насос") && name.Contains("гур")) Select("avtozapchasti_tip", "Рулевое управление", "kuzovnaya_detal", "Гидроусилитель и электроусилитель");
                    else if (name.Contains("рулевая") && name.Contains("рейка")) Select("avtozapchasti_tip", "Рулевое управление", "kuzovnaya_detal", "Рулевая рейка", "chast_detali", "Рулевая рейка");
                    else if (name.Contains("стартер")) Select("avtozapchasti_tip", "Электрооборудование", "kuzovnaya_detal", "Стартер", "chast_detali", "Стартер в сборе");
                    else if (name.Contains("генератор")) Select("avtozapchasti_tip", "Электрооборудование", "kuzovnaya_detal", "Генератор", "chast_detali", "Генератор в сборе");
                    else if (name.Contains("маховик")) Select("avtozapchasti_tip", "Трансмиссия, привод", "kuzovnaya_detal", "Сцепление", "chast_detali", "Маховик");
                    else if (name.Contains("усилитель") && name.Contains("вакуумный")) Select("avtozapchasti_tip", "Тормозная система", "kuzovnaya_detal", "Тормозной цилиндр");
                    else if (name.Contains("подрамник")) Select("avtozapchasti_tip", "Кузовные запчасти", "kuzovnaya_detal", "Силовые элементы", "chast_detali", "Рама");
                    else if (name.Contains("люк") && (name.Contains("крыш") || name.Contains("электр"))) Select("avtozapchasti_tip", "Кузовные запчасти", "kuzovnaya_detal", "Крыша и комплектующие", "chast_detali", "Крыша");
                    else if (name.Contains("двигатель")) Select("avtozapchasti_tip", "Двигатель, ГРМ, турбина", "kuzovnaya_detal", "Двигатель в сборе", "chast_detali", "Двигатель внутреннего сгорания");
                    else if (name.Contains("дверь")) Select("avtozapchasti_tip", "Кузовные запчасти", "kuzovnaya_detal", "Двери", "chast_detali", "Дверь боковая");
                    else {
                        Log.Add("youla.ru: "+_bus[b].name+" - пропущен, не описана категория ("+b+")");
                        return false; }
                    break;
            }
            //вид транспорта
            _dr.ButtonClick("//div[@data-name='attributes.avtozapchasti_vid_transporta']");
            _dr.ButtonClick("//div[@class='Select-menu-outer']//div[text()='Для автомобилей']");
            //состояние
            _dr.ButtonClick("//div[@data-name='attributes.zapchast_sostoyanie']");
            if (_bus[b].IsNew()) {
                _dr.ButtonClick("//div[@class='Select-menu-outer']//div[text()='Новые']");
            } else {
                _dr.ButtonClick("//div[@class='Select-menu-outer']//div[text()='Б/у']");
            }
            //тип объявления
            _dr.ButtonClick("//div[@data-name='attributes.type_classified']");
            _dr.ButtonClick("//div[@class='Select-menu-outer']//div[text()='Магазин']");
            return true;
        }
    }
}






////поднимаем юлу
//async void button_youla_put_Click(object sender, EventArgs e) {
//    if (checkBox_youlaUp.Checked) {
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
//    button_youla_add.PerformClick();
//}

////добавляем юлу
//private async void button_youla_add_Click(object sender, EventArgs e) {
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
//}

