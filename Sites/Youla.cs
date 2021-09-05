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
                        await ParseAsync();
                        await ActivateAsync();
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
        //активация устаревших объявлений 
        private async Task ActivateAsync() => await Task.Factory.StartNew(() => {
            //перехожу на главную страницу, проверяю наличие кнопки Активные
            _dr.Navigate("https://youla.ru/pro", "//span[text()='Активные']/..");
            //нажимаю кнопку Активные
            _dr.ButtonClick("//span[text()='Активные']/..");
            //выбираю Неактивные
            _dr.ButtonClick("//ul/div[text()='Неактивные']",10000);
            //получаю ссылки на товары
            var a = _dr.FindElements("//a[@data-test-action='B2BProductCardClick']")
                       .Select(s => s.GetAttribute("href"))
                       .ToList();
            //получаю заданное количество для активирования
            int count = _db.GetParamInt("youla.countToUp");
            //ограничиваю количеством реально обнаруженных неактивных
            count = a.Count > count ? count : a.Count;
            //перебираю товары
            for (var i = 0; count > 0 && i < a.Count-1; i++) {
                //выделяю id объявления из ссылки
                var id = a[i].Split('/').Last().TrimStart('p');
                //нахожу карточку в бизнес.ру
                var b = _bus.FindIndex(_ => _.youla.Contains(id));
                //проверяю индекс и остаток товара и цену
                if (b == -1) {
                    Log.Add("youla.ru: " + a[i] + " - объявление не привязано к бизнес.ру");
                    continue;
                }
                //если есть на остатках
                if (_bus[b].amount > 0) {
                    //загружаю объявление
                    _dr.Navigate(a[i]);
                    //если есть кнопка опубликовать повторно
                    if (_dr.GetElementsCount("//button[@data-test-action='RepublishClick']") > 0) {
                        //жму опубликовать повторно
                        _dr.ButtonClick("//button[@data-test-action='RepublishClick']", 5000);
                        Log.Add("youla.ru: " + _bus[b].name + " - объявление активировано (ост. " + --count + ")");
                    }
                } else {
                    //иначе удаляю объявление
                    Delete(b);
                }
            }
        });

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
        //удаление объявления асинхронно
        async Task DeleteAsync(int b) {
            try {
                await Task.Factory.StartNew(() => {
                    Delete(b);
                });
            } catch (Exception x) {
                Log.Add("youla.ru: ошибка удаления! - " + _bus[b].name + " - " + x.Message);
            }
        }
        //удаление объявления
        private void Delete(int b) {
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
            //проверка фото
            var photosCount = _dr.FindElements("//div[@id='images']//button[1]").Count;
            if (photosCount != (_bus[b].images.Count > 10 ? 10 : _bus[b].images.Count)
                && _bus[b].images.Count > 0) {
                Log.Add("youla.ru: " + _bus[b].name + " - обновляю фотографии");
                for(; photosCount > 0; photosCount--) 
                    _dr.ButtonClick("//div[@id='images']//button[1]",2000);
                SetImages(b);
            }
            PressOkButton(2);
            Log.Add("youla.ru: " + _bus[b].name + " - объявление обновлено");
        }
        //указываю телефон
        private void SetPhone() {
            _dr.WriteToSelector("//label[text()='Контактный телефон']/../..//input", 
                OpenQA.Selenium.Keys.ArrowDown + OpenQA.Selenium.Keys.Enter);
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
            _dr.WriteToSelector("//div[@data-name='attributes.auto_brand']//input", m[1] + OpenQA.Selenium.Keys.Enter);
            _dr.ButtonClick("//div[@data-name='attributes.auto_model']");
            _dr.WriteToSelector("//div[@data-name='attributes.auto_model']//input", m[2] + OpenQA.Selenium.Keys.Enter);
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
                     _bus[b].price >= 1000 &&
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
                            SetPhone();
                            SetPart(b);
                            SetMarkAndModel(b);
                            PressOkButton(2);
                            return true;
                        })) {
                            await SaveUrlAsync(b);
                            count--;
                        }
                    } catch (Exception x) {
                        Log.Add("youla.ru: " + _bus[b].name + " - ошибка добавления! - " + x.Message);
                        _dr.Navigate("https://youla.ru/pro");
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
                Log.Add("youla.ru: " + _bus[b].name + " - объявление добавлено");
            } else {
                Log.Add("youla.ru: " + _bus[b].name + " - объявление не удалось добавить!");
                _dr.Navigate("https://youla.ru/pro");
            }

        }
        //заполняю адрес магазина
        private void SetAddr() {
            for (int i=0; ;i++) {
                _dr.WriteToSelector("//div[contains(@class,'_yjs_geolocation-map')]//input", 
                    " Калуга, Московская улица, 331");
                Thread.Sleep(4000);
                _dr.SendKeysToSelector("//div[contains(@class,'_yjs_geolocation-map')]//input",
                    OpenQA.Selenium.Keys.ArrowDown+OpenQA.Selenium.Keys.Enter);
                if (_dr.GetElementAttribute("//input[@placeholder='Введите город, улицу, дом']", "value") == "Россия, Калуга, Московская улица, 331") break;
                if (i > 20) throw new Exception("ошибка - не могу указать адрес!");
            }
        }
        //проверка объявлений (парсинг кабинета)
        async Task ParseAsync() {
            if (DateTime.Now.Hour % 6 != 0) return;
            await _dr.NavigateAsync("https://youla.ru/pro");
            //строка с количеством объявлений
            var span = _dr.GetElementText("//span[contains(@data-test-block,'TotalCount')]");
            //строка количество объявлений
            var str = span.Split(' ').First();
            //число количество страниц
            var n = str.Length == 0 ? 0: int.Parse(str) /20;
            //пробегаюсь по страницам
            for (int i = 1; i < n; i += _rnd.Next(1, 3)) {
                if (_dr.GetElementsCount("//span[@data-test-id='B2BPaginationPageNumber-" + i + "']") == 0) break;
                await ParsePageAsync(i);
            }
            Log.Add("youla.ru: проверка кабинета завершена!");
        }
        //парсинг страницы
        async Task ParsePageAsync(int p) {
            try {
                await Task.Factory.StartNew(() => {
                    _dr.ButtonClick("//span[@data-test-id='B2BPaginationPageNumber-"+p+"']");
                    Thread.Sleep(10000);
                    var names = _dr.FindElements("//div[@data-test-component='B2BProductCard']//p").Select(s => s.Text).ToList();
                    var prices = _dr.FindElements("//span[@data-test-component='B2BPrice']").Select(s => s.Text.Replace("₽", "").Replace("\u205F", "").Trim()).ToList();
                    var ids = _dr.FindElements("//a[@data-test-action='B2BProductCardClick']").Select(s => s.GetAttribute("href")).ToList();
                    if (names.Count != prices.Count ||
                        names.Count != ids.Count) {
                        throw new Exception("количество элементов не совпадает!");
                    }
                    for (int i = 0; i < ids.Count; i++) {
                        //определяю индекс карточки товара
                        var b = _bus.FindIndex(f => f.youla.Contains(ids[i].Split('/').Last().Remove(0,1)));
                        //если индекс не найден удаляю объявление
                        if (b == -1) {
                            _dr.Navigate(ids[i]);
                            //кнопка снять с публикации
                            _dr.ButtonClick("//button[@data-test-action='ProductWithdrawClick']");
                            //кнопка другая причина
                            _dr.ButtonClick("//button[@data-test-action='ArchivateClick']", 3000);
                            Log.Add("youla.ru: " + names[i] + " - потерянное объявление снято");
                            //кнопка удалить объявление
                            _dr.ButtonClick("//button[@data-test-action='ProductDeleteClick']");
                            //кнопка удалить
                            _dr.ButtonClick("//button[@data-test-action='ConfirmModalApply']", 5000);
                            Log.Add("youla.ru: " + names[i] + " - потерянное объявление удалено");
                        } else if (_bus[b].price.ToString() != prices[i] ||
                                  !_bus[b].name.Contains(names[i])) {
                            EditOffer(b);
                        }
                    }
                });
            } catch (Exception x) {
                if (x.Message.Contains("timed out")) throw;
                Log.Add("youla.ru: ошибка парсинга страницы " + p + " - " + x.Message);
            }
        }


        //выбор категории
        void Select(Dictionary<string, string> param = null) {
            foreach(var key in param.Keys) {
                if (key == "avtozapchasti_tip") _dr.ButtonClick("//div[text()='Запчасти']");
                if (key == "shiny_diski_tip") _dr.ButtonClick("//div[text()='Шины и диски']");
                _dr.ButtonClick("//div[@data-name='attributes." + key + "']",2000);
                _dr.ButtonClick("//div[@class='Select-menu-outer']//div[contains(text(),'" + param[key] + "')]", 3000);
            }
        }
        //определение категории
        bool SetCategory(int b) {
            var name = _bus[b].name.ToLowerInvariant();
            //var desc = _bus[b].description.ToLowerInvariant();
            var d = new Dictionary<string, string>();
            //основная категория
            if (name.Contains("кронштейн ") || name.Contains("опора") || name.Contains("креплен") || name.Contains("подушк")) {
            } else if (name.Contains("планк") || name.Contains("молдинг") || name.Contains("обшивк") || name.Contains("накладк") ||
                name.Contains("катафот") || name.Contains("прокладка") || name.Contains("сальник")) {
            } else if ((name.Contains("трубк") || name.Contains("шланг")) && 
                (name.Contains("гур") || name.Contains("гидроусилител") ||
                name.Contains("высокого") || name.Contains("низкого"))) {
                d.Add("avtozapchasti_tip", "Рулевое управление");
                d.Add("kuzovnaya_detal", "Шланг ГУР");
            } else if (name.Contains("трубк") || name.Contains("шланг")) {
            } else if (name.Contains("трос ")) {
            } else if (name.Contains("состав") || name.Contains("масло ") || name.Contains("полирол")) {
            } else if (name.Contains("ступица")) {
                d.Add("avtozapchasti_tip", "Подвеска");
                d.Add("kuzovnaya_detal", "Ступица");
                d.Add("chast_detali", "Ступица");
            } else if (name.Contains("блок") && name.Contains("управлени") &&
                (name.Contains("печко") || name.Contains("отопит") || name.Contains("климат"))) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Блок управления печкой");
            } else if (name.Contains("коммутатор ")) {
                d.Add("avtozapchasti_tip", "Система зажигания");
                d.Add("kuzovnaya_detal", "Коммутатор зажигания");
            } else if (name.Contains("катушка") && name.Contains("зажиган")) {
                d.Add("avtozapchasti_tip", "Система зажигания");
                d.Add("kuzovnaya_detal", "Катушка зажигания");
            } else if (name.Contains("замок") && name.Contains("зажиган")) {
                d.Add("avtozapchasti_tip", "Система зажигания");
                d.Add("kuzovnaya_detal", "Контактная группа");
            } else if (name.Contains("заслонки") && (name.Contains("печк") || name.Contains("отопит"))) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Сервопривод");
            } else if (name.Contains("моторчик ") && (name.Contains("печк") || name.Contains("отопит"))) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Моторчик печки");
            } else if ((name.Contains("корпус ") || name.Contains("крышка ")) && name.Contains("термостат")) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Термостат");
                d.Add("chast_detali", "Термостат");
            } else if (name.Contains("коллектор") && name.Contains("впускной")) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Коллектор впускной");
            } else if (name.Contains("проводка") || name.Contains("жгут провод")) {
                d.Add("avtozapchasti_tip", "Электрооборудование");
                d.Add("kuzovnaya_detal", "Жгут проводов");
            } else if (name.Contains("бампер ") && (name.Contains("перед") || name.Contains("задн"))) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Бампер и комплектующие");
                d.Add("chast_detali", "Бампер");
            } else if (name.Contains("бампер") && name.Contains("усилитель")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Бампер и комплектующие");
                d.Add("chast_detali", "Усилитель бампера");
            } else if (name.Contains("бампер") && name.Contains("абсорбер")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Бампер и комплектующие");
                d.Add("chast_detali", "Абсорбер бампера");
            } else if (name.Contains("замок") && name.Contains("двери")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Замки");
                d.Add("chast_detali", "Замок двери");
            } else if (name.Contains("замок") && name.Contains("капота")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Замки");
                d.Add("chast_detali", "Замок капота");
            } else if (name.Contains("замок") && name.Contains("багажник")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Замки");
                d.Add("chast_detali", "Замок багажника");
            } else if (name.Contains("замка") && name.Contains("компрессор")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Замки");
                d.Add("chast_detali", "Центральный замок");
            } else if (name.Contains("крыло ") && (name.Contains("лев") || name.Contains("прав"))) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Крылья и комплектующие");
                d.Add("chast_detali", "Крылья");
            } else if (name.Contains("крыша ")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Крыша и комплектующие");
                d.Add("chast_detali", "Крыша");
            } else if (name.Contains("вкладыш") && name.Contains("шатун")) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Блок цилиндров и детали");
                d.Add("chast_detali", "Полукольца");
            } else if (name.Contains("насос") && name.Contains("топлив")) {
                d.Add("avtozapchasti_tip", "Топливная система");
                d.Add("kuzovnaya_detal", "Бензонасос");
                d.Add("chast_detali", "Бензонасос");
            } else if (name.Contains("карбюратор")) {
                d.Add("avtozapchasti_tip", "Топливная система");
                d.Add("kuzovnaya_detal", "Карбюратор");
            } else if (name.Contains("шатун")) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Блок цилиндров и детали");
                d.Add("chast_detali", "Шатун");
            } else if (name.Contains("заслонка") && name.Contains("дросс")) {
                d.Add("avtozapchasti_tip", "Топливная система");
                d.Add("kuzovnaya_detal", "Дроссель");
                d.Add("chast_detali", "Дроссельная заслонка");
            } else if (name.Contains("стабилизатор") && (name.Contains("перед") || name.Contains("зад"))) {
                d.Add("avtozapchasti_tip", "Подвеска");
                d.Add("kuzovnaya_detal", "Стабилизатор");
                d.Add("chast_detali", "Стабилизатор");
            } else if (name.Contains("рулев") && (name.Contains("карданч") || name.Contains("вал") || name.Contains("колонк"))) {
                d.Add("avtozapchasti_tip", "Рулевое управление");
                d.Add("kuzovnaya_detal", "Рулевая колонка");
            } else if (name.Contains("трапеция") && (name.Contains("дворник") || name.Contains("очистител"))) {
                d.Add("avtozapchasti_tip", "Система очистки");
                d.Add("kuzovnaya_detal", "Трапеция дворников");
            } else if (name.Contains("рем") && name.Contains("безопас")) {
                d.Add("avtozapchasti_tip", "Безопасность");
                d.Add("kuzovnaya_detal", "Ремни безопасности");
            } else if (name.Contains("расходомер") || name.Contains("дмрв") || name.Contains("кислорода") || name.Contains("лямбда")) {
                d.Add("avtozapchasti_tip", "Выхлопная система");
                d.Add("kuzovnaya_detal", "Датчики");
            } else if (name.Contains("глушитель")) {
                d.Add("avtozapchasti_tip", "Выхлопная система");
                d.Add("kuzovnaya_detal", "Глушитель");
            } else if (name.Contains("коллектор") && name.Contains("выпускной")) {
                d.Add("avtozapchasti_tip", "Выхлопная система");
                d.Add("kuzovnaya_detal", "Коллектор выпускной");
            } else if (name.Contains("клапан") && (name.Contains("егр") || name.Contains("egr"))) {
                d.Add("avtozapchasti_tip", "Выхлопная система");
                d.Add("kuzovnaya_detal", "EGR/SCR система");
            } else if ((name.Contains("блок ") || name.Contains("комплект ")) && 
                (name.Contains("управлени") || name.Contains("комфорта")) || name.Contains("ЭБУ ")) {
                d.Add("avtozapchasti_tip", "Электрооборудование");
                d.Add("kuzovnaya_detal", "Блок управления");
            } else if (name.Contains("привод") && (name.Contains("левый") || name.Contains("правый") || name.Contains("передн") || name.Contains("задни") || name.Contains("полуос"))) {
                d.Add("avtozapchasti_tip", "Трансмиссия, привод");
                d.Add("kuzovnaya_detal", "Привод и дифференциал");
                d.Add("chast_detali", "Приводной вал");
            } else if (name.Contains("маховик") || name.Contains("венец")) {
                d.Add("avtozapchasti_tip", "Трансмиссия, привод");
                d.Add("kuzovnaya_detal", "Сцепление");
                d.Add("chast_detali", "Маховик");
            } else if (name.Contains("диск") && name.Contains("сцеплен")) {
                d.Add("avtozapchasti_tip", "Трансмиссия, привод");
                d.Add("kuzovnaya_detal", "Сцепление");
                d.Add("chast_detali", "Диск сцепления");
            } else if (name.Contains("противотум") && name.Contains("фара")) {
                d.Add("avtozapchasti_tip", "Автосвет, оптика");
                d.Add("kuzovnaya_detal", "Противотуманная фара (ПТФ)");
            } else if ((name.Contains("кулис") || name.Contains("переключения") || name.Contains("селектор")) &&
                name.Contains("кпп")) {
                d.Add("avtozapchasti_tip", "Трансмиссия, привод");
                d.Add("kuzovnaya_detal", "Ручка КПП и кулиса");
            } else if (name.Contains("мкпп")) {
                d.Add("avtozapchasti_tip", "Трансмиссия, привод");
                d.Add("kuzovnaya_detal", "Коробка передач");
                d.Add("chast_detali", "МКПП");
            } else if (name.Contains("переключат") &&
                      (name.Contains("подрулев") || name.Contains("дворник") || 
                       name.Contains("поворот") || name.Contains("стеклоочист"))) {
                d.Add("avtozapchasti_tip", "Электрооборудование");
                d.Add("kuzovnaya_detal", "Подрулевой переключатель");
            } else if (name.Contains("фара")) {
                d.Add("avtozapchasti_tip", "Автосвет, оптика");
                d.Add("kuzovnaya_detal", "Фара");
            } else if (name.Contains("фонарь")) {
                d.Add("avtozapchasti_tip", "Автосвет, оптика");
                d.Add("kuzovnaya_detal", "Задний фонарь");
            } else if (name.Contains("компрессор кондиционера")) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Детали кондиционера");
                d.Add("chast_detali", "Компрессор кондиционера");
            } else if (name.Contains("шкив") && name.Contains("коленвал")) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "ГРМ система и цепь");
                d.Add("chast_detali", "Шкив коленвала");
            } else if (name.Contains("коленвал")) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Блок цилиндров и детали");
                d.Add("chast_detali", "Коленвал");
            } else if (name.Contains("балка ") && 
                      (name.Contains("зад") || name.Contains("передняя")||name.Contains("подмоторная"))) {
                d.Add("avtozapchasti_tip", "Подвеска");
                d.Add("kuzovnaya_detal", "Балка");
            } else if (name.Contains("кулак ") && (name.Contains("зад") || name.Contains("перед") ||
                name.Contains("поворотн") || name.Contains("правый") || name.Contains("левый"))) {
                d.Add("avtozapchasti_tip", "Подвеска");
                d.Add("kuzovnaya_detal", "Поворотный кулак");
            } else if (name.Contains("крышка") && name.Contains("багажник")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Багажник и комплектующие");
                d.Add("chast_detali", "Дверь багажника");
            } else if (name.Contains("стеклоподъемник")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Двери");
                d.Add("chast_detali", "Стеклоподъемный механизм и комплектующие");
            } else if (name.Contains("насос") && name.Contains("гур")) {
                d.Add("avtozapchasti_tip", "Рулевое управление");
                d.Add("kuzovnaya_detal", "Гидроусилитель и электроусилитель");
            } else if (name.Contains("панель") && name.Contains("прибор")) {
                d.Add("avtozapchasti_tip", "Салон, интерьер");
                d.Add("kuzovnaya_detal", "Спидометр");
            } else if (name.Contains("рулевая") && name.Contains("рейка")) {
                d.Add("avtozapchasti_tip", "Рулевое управление");
                d.Add("kuzovnaya_detal", "Рулевая рейка");
                d.Add("chast_detali", "Рулевая рейка");
            } else if (name.Contains("стартер")) {
                d.Add("avtozapchasti_tip", "Электрооборудование");
                d.Add("kuzovnaya_detal", "Стартер");
                d.Add("chast_detali", "Стартер в сборе");
            } else if (name.Contains("генератор")) {
                d.Add("avtozapchasti_tip", "Электрооборудование");
                d.Add("kuzovnaya_detal", "Генератор");
                d.Add("chast_detali", "Генератор в сборе");
            } else if (name.Contains("кулиса")) {
                d.Add("avtozapchasti_tip", "Трансмиссия, привод");
                d.Add("kuzovnaya_detal", "Ручка КПП и кулиса");
            } else if (name.Contains("усилитель") && name.Contains("вакуумный")) {
                d.Add("avtozapchasti_tip", "Тормозная система");
                d.Add("kuzovnaya_detal", "Тормозной цилиндр");
            } else if (name.Contains("подрамник")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Силовые элементы");
                d.Add("chast_detali", "Рама");
            } else if (name.Contains("люк") && (name.Contains("крыш") || name.Contains("электр"))) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Крыша и комплектующие");
                d.Add("chast_detali", "Крыша");
            } else if (name.Contains("зеркал") && (name.Contains("лево") || name.Contains("прав"))) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Зеркала");
                d.Add("chast_detali", "Боковые зеркала заднего вида");
            } else if (name.Contains("форсун") && name.Contains("топлив")) {
                d.Add("avtozapchasti_tip", "Топливная система");
                d.Add("kuzovnaya_detal", "Форсунка топливная");
                d.Add("chast_detali", "Форсунка топливная");
            } else if (name.Contains("вентилятор") && name.Contains("охлаждения")) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Радиатор и детали");
                d.Add("chast_detali", "Вентилятор радиатора");
            } else if (name.Contains("подушка")) {
                d.Add("avtozapchasti_tip", "Безопасность");
                d.Add("kuzovnaya_detal", "Подушка безопасности");
            } else if (name.Contains("руль")) {
                d.Add("avtozapchasti_tip", "Рулевое управление");
                d.Add("kuzovnaya_detal", "Руль");
            } else if (name.Contains("сиденья ") && name.Contains("передние") ||
                       name.Contains("сиденье ") && name.Contains("переднее") ||
                       name.Contains("регулир") && name.Contains("сиденья")||
                       name.Contains("сидень") && name.Contains("водител")) {
                d.Add("avtozapchasti_tip", "Салон, интерьер");
                d.Add("kuzovnaya_detal", "Сиденья");
            } else if (name.Contains("двигатель")) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Двигатель в сборе");
                d.Add("chast_detali", "Двигатель внутреннего сгорания");
            } else if (name.Contains("дверь ")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Двери");
                d.Add("chast_detali", "Дверь боковая");
            } else if (name.Contains("капот ")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Капоты и комплектующие");
                d.Add("chast_detali", "Капот");
            } else if (name.Contains("акпп ")) {
                d.Add("avtozapchasti_tip", "Трансмиссия, привод");
                d.Add("kuzovnaya_detal", "Коробка передач");
                d.Add("chast_detali", "АКПП");
            } else if (name.Contains("трамблер ")) {
                d.Add("avtozapchasti_tip", "Система зажигания");
                d.Add("kuzovnaya_detal", "Трамблер");
            } else if (name.Contains("реле ") && name.Contains("накала")) {
                d.Add("avtozapchasti_tip", "Система зажигания");
                d.Add("kuzovnaya_detal", "Реле свечей накала");
            } else if (name.Contains("стойк") && name.Contains("стабилизат")) {
                d.Add("avtozapchasti_tip", "Подвеска");
                d.Add("kuzovnaya_detal", "Стабилизатор");
                d.Add("chast_detali", "Стойки стабилизатора");
            } else if (name.Contains("втулк") && name.Contains("стабилизат")) {
                d.Add("avtozapchasti_tip", "Подвеска");
                d.Add("kuzovnaya_detal", "Стабилизатор");
                d.Add("chast_detali", "Втулки стабилизатора");
            } else if (name.Contains("стабилизатор ")) {
                d.Add("avtozapchasti_tip", "Подвеска");
                d.Add("kuzovnaya_detal", "Стабилизатор");
                d.Add("chast_detali", "Стабилизатор");
            } else if ((name.Contains("стойка ")|| name.Contains("амортизатор")) && (name.Contains("передн") || name.Contains("задн"))) {
                d.Add("avtozapchasti_tip", "Подвеска");
                d.Add("kuzovnaya_detal", "Амортизаторы");
            } else if (name.Contains("суппорт ") && (name.Contains("передн") || name.Contains("задн"))) {
                d.Add("avtozapchasti_tip", "Тормозная система");
                d.Add("kuzovnaya_detal", "Суппорт");
            } else if (name.Contains("блок ") && name.Contains("abs")) {
                d.Add("avtozapchasti_tip", "Тормозная система");
                d.Add("kuzovnaya_detal", "Система ABS");
            } else if ((name.Contains("стеклоочист") || name.Contains("дворник")) && name.Contains("решетк") || name.Contains("жабо ")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Капоты и комплектующие");
                d.Add("chast_detali", "Решетка капота");
            } else if ((name.Contains("стеклоочист") || name.Contains("дворник")) && name.Contains("мотор")) {
                d.Add("avtozapchasti_tip", "Система очистки");
                d.Add("kuzovnaya_detal", "Мотор стеклоочистителя");
            } else if (name.Contains("блок ") && name.Contains("предохранителей")) {
                d.Add("avtozapchasti_tip", "Электрооборудование");
                d.Add("kuzovnaya_detal", "Блок предохранителей");
            } else if (name.Contains("радиатор ") && name.Contains("охлаждения")) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Радиатор и детали");
                d.Add("chast_detali", "Радиатор охлаждения");
            } else if (name.Contains("радиатор ") && name.Contains("кондиционера")) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Радиатор и детали");
                d.Add("chast_detali", "Радиатор кондиционера");
            } else if (name.Contains("расширительный") && name.Contains("бачок")) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Радиатор и детали");
                d.Add("chast_detali", "Расширительный бачок");
            } else if (name.Contains("бак ") && name.Contains("топливный") || name.Contains("бензобак")) {
                d.Add("avtozapchasti_tip", "Топливная система");
                d.Add("kuzovnaya_detal", "Топливный бак");
            } else if (name.Contains("вискомуфта ")) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Радиатор и детали");
                d.Add("chast_detali", "Вискомуфта");
            } else if (name.Contains("крышка ") && (name.Contains("клапанная") || name.Contains("гбц"))) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Клапанная крышка");
                d.Add("chast_detali", "Клапанная крышка");
            } else if (name.Contains("крышка ") && (name.Contains("двигателя") || name.Contains("двс"))) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Корпус и крышки");
                d.Add("chast_detali", "Крышка двигателя");
            } else if (name.Contains("гбц ")) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Блок цилиндров и детали");
                d.Add("chast_detali", "Головка блока цилиндров");
            } else if (name.Contains("турбина ")) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Турбина, компрессор");
                d.Add("chast_detali", "Турбина");
            } else if (name.Contains("поддон ") && 
                (name.Contains("двс") || name.Contains("двигател") || name.Contains("масл"))) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Прокладки и поддон");
                d.Add("chast_detali", "Поддон картера");
            } else if (name.Contains("турбокомпрессор ")) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Турбина, компрессор");
                d.Add("chast_detali", "Компрессор");
            } else if (name.Contains("корпус ") && (name.Contains("воздушн"))){
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Корпус и крышки");
                d.Add("chast_detali", "Корпус фильтра");
            } else if ((name.Contains("порог ") || name.Contains("пороги ")) && 
                       (name.Contains("левы") || name.Contains("правы"))) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Силовые элементы");
                d.Add("chast_detali", "Пороги");
            } else if (name.Contains("лонжерон ") || name.Contains("лонжероны ") ||
                       (name.Contains("морда"))) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Силовые элементы");
                d.Add("chast_detali", "Лонжероны");
            } else if (name.Contains("стекло ")) {
                d.Add("avtozapchasti_tip", "Стекла");
            } else if (name.Contains("диск ")) {
                d.Add("shiny_diski_tip", "Диски");
                d.Add("shiny_naznachenie", "Для легкового автомобиля");
                d.Add("shiny_diametr", _bus[b].GetDiskSize());
                d.Add("disk_tip", name.Contains(" лит") ? "Литые"
                                                        : name.Contains(" кован") ? "Кованые"
                                                                                  : "Штампованные");
                d.Add("diski_kolichestvo_otverstiy", _bus[b].GetNumberOfHoles());
                d.Add("diski_diametr_raspolozheniya_otverstiy", _bus[b].GetDiameterOfHoles());
            }
            if (d.Count == 0) {
                Log.Add("youla.ru: " + _bus[b].name + " - пропущен, не описана категория (" + b + ")");
                return false;
            }
            //вид транспорта
            d.Add("avtozapchasti_vid_transporta","Для автомобилей");
            //состояние
            if (_bus[b].IsNew())
                d.Add("zapchast_sostoyanie", "Новые");
             else 
                d.Add("zapchast_sostoyanie", "Б/у");
            //тип объявления
            d.Add("type_classified", "Магазин");
            Select(d);
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

