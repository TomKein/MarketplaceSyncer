using Newtonsoft.Json;
using OpenQA.Selenium.Interactions;
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
using ClosedXML.Excel;

namespace Selen.Sites {
    class Avito {
        public Selenium _dr;
        DB _db;
        List<RootObject> _bus = null;
        Random _rnd = new Random();
        string _url;
        int _delay;
        int _priceLevel;
        bool _editAfterUp;

        public int CountToUp { get; set; }
        public int AddCount { get; set; }

        string[] _addDesc;
        string[] _addDesc2;

        //конструктор
        public Avito() {
            _db = DB._db;
        }
        //главный цикл синхронизации
        public async Task StartAsync(List<RootObject> bus) {
            Log.Add("avito.ru: начало выгрузки...");
            GetParams(bus);//            await GenerateXml();
            await AuthAsync();
            await AddAsync();
            await EditAllAsync();
            await AvitoUpAsync();
            await RemoveDraftAsync();
            await CheckUrlsAsync();
            Log.Add("avito.ru: выгрузка завершена");
        }
        //загружаю параметры
        private void GetParams(List<RootObject> bus) {
            _bus = bus;
            //устанавливаю цену отсечки для подъема/подачи
            _priceLevel = _db.GetParamInt("avito.priceLevel");
            //получаю номер ссылки в карточке
            _url = _db.GetParamStr("avito.url");
            //базовая задержка в милисекундах
            _delay = _db.GetParamInt("avito.delaySeconds") * 1000;
            //редактирование объявлений после активирования
            _editAfterUp = _db.GetParamBool("avito.editAfterUp");
            //дополнительное описание
            _addDesc = JsonConvert.DeserializeObject<string[]>(_db.GetParamStr("avito.addDescription"));
            _addDesc2 = JsonConvert.DeserializeObject<string[]>(_db.GetParamStr("avito.addDescription2"));
            //проверяю время, нужно ли поднимать объявления
            if (DateTime.Now.Hour >= _db.GetParamInt("avito.upFromHour") &&
                DateTime.Now.Hour < _db.GetParamInt("avito.upToHour"))
                CountToUp = _db.GetParamInt("avito.countToUp");
            else
                CountToUp = 0;
            //сколько новых объявлений подавать каждый час
            if (DateTime.Now.Hour >= _db.GetParamInt("avito.addFromHour") &&
                DateTime.Now.Hour < _db.GetParamInt("avito.addToHour"))
                AddCount = _db.GetParamInt("avito.countToAdd");
            else
                AddCount = 0;
        }

        //удаление черновиков
        private async Task RemoveDraftAsync() {
            try {
                //загружаю страницу черновиков
                await _dr.NavigateAsync("https://www.avito.ru/profile/items/draft");
                //проверка загрузки
                await ChechAuthAsync();
                await Task.Factory.StartNew(() => {
                    //если попали на страницу (проверяю url)
                    if (_dr.GetUrl().Contains("/profile/items/draft")) {
                        //нажимаю первый черновик меню
                        _dr.ButtonClick("//button[contains(@data-marker,'item-info')]");
                        //нажимаю удалить
                        _dr.ButtonClick("//button[contains(@data-marker,'remove-draft')]");
                    }
                });
            } catch (Exception x) {
                Log.Add("avito.ru: ошибка при удалении черновика! - " + x.Message);
                if (x.Message.Contains("timed out") ||
                    x.Message.Contains("already closed") ||
                    x.Message.Contains("invalid session id") ||
                    x.Message.Contains("chrome not reachable"))
                    throw;
            }
        }
        //авторизация
        private async Task AuthAsync() => await Task.Factory.StartNew(() => {
            if (_dr == null) {
                _dr = new Selenium();
                LoadCookies();
            }
            _dr.Navigate("https://avito.ru/profile");
            if (_dr.GetElementsCount("//a[text()='Мои объявления']") == 0) {
                _dr.WriteToSelector("input[name='login']", _db.GetParamStr("avito.login"));
                _dr.WriteToSelector("input[name='password']", _db.GetParamStr("avito.password"));
                _dr.ButtonClick("//button[@type='submit']");
            }
            for (int i = 0; _dr.GetElementsCount(".profile-tabs") == 0; i++) {
                Thread.Sleep(_delay * 5);
                _dr.ButtonClick("//a[contains(@href,'reload')]");
                _dr.ButtonClick("//div[contains(@class,'username')]/div/a");
                _dr.ButtonClick("//button[@id='reload-button']");
                if (_dr.GetElementsCount("//h1[contains(text(),'502')]") > 0)
                    _dr.Refresh("https://www.avito.ru/profile");
                if (i >= 10)
                    throw new Exception("ошибка входа в личный кабинет");
            }
            SaveCookies();
        });
        //массовая проверка товаров
        private async Task EditAllAsync() {
            for (int b = 0; b < _bus.Count; b++) {
                if (_bus[b].IsTimeUpDated() &&
                    _bus[b].avito != null &&
                    _bus[b].avito.Contains("http")) {
                    if (_bus[b].amount <= 0) {
                        await DeleteAsync(b);
                    } else
                        await EditAsync(b);
                }
            }
        }
        //проверка авторизации
        private async Task ChechAuthAsync() => await Task.Factory.StartNew(() => {
            //закрываю рекламу
            _dr.ButtonClick("//button[contains(@class,'popup-close')]");
            while (_dr.GetElementsCount("//p/a[contains(text(),'обновить страницу')]") > 0)
                _dr.ButtonClick("//p/a[contains(text(),'обновить страницу')]");
            //проверяю элемент Мои объявления
            ////a[text()='Вход и регистрация']
            while (_dr.GetElementsCount("//a[text()='Мои объявления']") == 0) {
                if (_dr.GetElementsCount("//h1[text()='Сайт временно недоступен']") > 0)
                    _dr.Refresh();
                else {
                    throw new Exception("ошибка загрузки сайта!");
                }
            }
        });
        //редактирование объявления
        private async Task EditAsync(int b) {
            if (_bus[b].price > 0) {  //защита от нулевой цены в базе
                await ChechAuthAsync();
                await UnDeleteAsync(b);
                var url = "https://www.avito.ru/items/edit/" + _bus[b].avito.Replace("/", "_").Split('_').Last();
                try {
                    await _dr.NavigateAsync(url, "//button[contains(@data-marker,'button-next')]");
                } catch (Exception x) {
                    if (_dr.GetElementText("//h1").Contains("на нашем сайте нет") || //если в заголовке указано что объявления нет на сайте
                        _dr.GetElementsCount("//p[contains(text(),'удалили это объявление')]") > 0) {//сообщение, что оно удалено
                        await SaveUrlAsync(b, deleteUrl: true);
                        Log.Add("avito.ru: " + _bus[b].name + " - ссылка на объявление удалена из карточки!");
                        return;
                    }
                }
                await Task.Factory.StartNew(() => {
                    SetTitle(b);
                    SetStatus(b);
                    SetPartNumber(b);
                    SetManufacture(b);
                    SetDesc(b);
                    CheckPhotos(b);
                    SetPrice(b);
                    SetGeo(b);
                    PressOk();
                });
            }
        }
        //проверка фотографий в объявлении
        private void CheckPhotos(int b) {
            //определяю, был ли вызван метод на странице редактирования или просмотра объявления
            bool isEdit = !_dr.GetUrl().Contains("_");
            //селектор фотографий для каждого случая свой
            string selector = isEdit ? "//div[contains(@class,'uploader-item')]/img"
                                     : "//div[contains(@class,'gallery-imgs-container')]/div";
            //получаю количество фотографий в объявлении
            var countReal = _dr.GetElementsCount(selector);
            //количество фотографий, которое должно быть в объявлении
            int countMust = _bus[b].images.Count > 10 ? 10 : _bus[b].images.Count;
            //если расхождение и в карточке количество не нулевое
            if (countMust != countReal && countMust > 1) {
                //перехожу в режим редактирования, если были не в нем
                if (!isEdit) {
                    var url = "https://www.avito.ru/items/edit/" + _bus[b].avito.Replace("/", "_").Split('_').Last();
                    _dr.Navigate(url, "//button[contains(@data-marker,'button-next')]");
                }
                //удаляю все фото, которые есть объявлении
                for (; countReal > 0; countReal--) {
                    _dr.ButtonClick("//button[@title='Удалить']", 3000);
                }
                //загружаю новые фото
                SetImages(b);
                //нажимаю сохранить, если метод был вызван не из редактирования
                if (!isEdit)
                    PressOk();
                Log.Add("avito.ru: " + _bus[b].name + " - фотографии обновлены");
            }
        }

        //восстановление удаленного объявления
        private async Task UnDeleteAsync(int b) => await Task.Factory.StartNew(() => {
            _dr.Navigate(_bus[b].avito);
            if (_bus[b].amount > 0 && _dr.GetElementsCount("//span[@data-marker='item-view/item-id']") == 0) {
                _dr.ButtonClick("//button[text()='Восстановить']");
            }
        });
        //производство, марка и модель
        private void SetManufacture(int b) {
            //производителя авито определяет самостоятельно
            //определяю марку и модель
            var m = _bus[b].GetNameMarkModel();
            //если не удалось - пропуск
            if (m == null)
                return;
            //проверяю поле в объявлении
            if (_dr.GetElementAttribute("//input[@id='params[111075]']", "value") == "") {
                //1. заполняю марку
                _dr.ButtonClick("//input[@id='params[111075]']");
                string mrk = m[1].Substring(0, 1).ToUpper() + m[1].Substring(1);
                _dr.WriteToSelector("//input[@id='params[111075]']", mrk);
                _dr.ButtonClick("//div[@data-marker='params[111075]']//span[text()='"+mrk+"']");
                _dr.ButtonClick("//div[@data-marker='params[111075]']//div[contains(@class,'root_active')]//span");
                //2. заполняю модель
                string mod = m[2].Substring(0, 1).ToUpper() + m[2].Substring(1);
                _dr.WriteToSelector("//select[@id='params[111076]']", mod+OpenQA.Selenium.Keys.Enter);

                //3. заполняю поколение, если доступно (доработать auto.txt)
                if (_dr.GetElementsCount("//select[@id='params[111078]']/../../*[contains(@class,'withoutValue')]") > 0) {
                    _dr.ButtonClick("//select[@id='params[111078]']");
                    string pok = m[3].Substring(0, 1).ToUpper() + m[3].Substring(1) + " ";
                    _dr.WriteToSelector("//select[@id='params[111078]']", pok + OpenQA.Selenium.Keys.Enter);
                }
                Log.Add("avito: SetManufacture - заполнено авто - " + _bus[b].name + " [ " + m[1] + "," + m[2] + "," + m[3] + " ]");
            }
            //проверка корректности
            if (_dr.GetElementsCount("//div[contains(@class,'withoutValue') or contains(@class,'select-error')]") > 0
                || _dr.GetElementsCount("//label[@for='params[111078]']")==0) { //есть поле пустое - ошибка
                //убираю марку
                _dr.ButtonClick("//input[@id='params[111075]']/..//div/*");
                Log.Add("avito: SetManufacture - ошибка! авто не заполнено - " + _bus[b].name+ " [ "+m[1]+","+m[2]+","+m[3]+" ]");
                return;
            }
        }
        //заполняю номер запчасти из артикула
        private void SetPartNumber(int b) {
            if (!string.IsNullOrEmpty(_bus[b].part))
                _dr.WriteToSelector("//span[text()='Номер запчасти']/../../..//input", _bus[b].part.Split(',').First());
        }
        //удалить объявление асинхронно
        private async Task DeleteAsync(int b) => await Task.Factory.StartNew(() => {
            Delete(b);
        });
        //удалить объявление
        private void Delete(int b) {
            _dr.Navigate(_bus[b].avito, "//a[text()='Мои объявления']");
            //если кнопки действия есть на странице - пробую снять
            if (_dr.GetElementsCount("//*[contains(@class,'app-container')]") > 0) {
                Log.Add("avito.ru: " + _bus[b].name + " - снимаю объявление");
                _dr.ButtonClick("//button[@aria-busy='false']/span[text()='Хорошо']/..");
                _dr.ButtonClick("//*[text()='Снять с публикации']/..");
                _dr.ButtonClick("//*[contains(text(),'Другая причина')]/..");
                _dr.ButtonClick("//button[@data-marker='save-reason']");
                Thread.Sleep(_delay);
                //проверяю наличие кнопок действия, если кнопки на месте - повторяю с начала
                if (_dr.GetElementsCount("//*[text()='Снять с публикации']") == 0)
                    Log.Add("avito.ru: " + _bus[b].name + " - объявление снято");
            }
            //закрываю окно опроса
            _dr.ButtonClick("//img[@alt='close']");
            //если появилась кнопка удалить - нажимаю
            _dr.ButtonClick("//button/*[text()='Удалить']");
            //есть кнопка восстановить - объявление уже удалено
            if (_dr.GetElementsCount("//button[text()='Восстановить']") > 0)
                return;
            //если объявление удалено окончательно - нужно удалить ссылку из карточки товара
            if (_dr.GetElementsCount("//div[@class='item-view-warning-content']/p[contains(text(),'навсегда')]") > 0 ||
                _dr.GetElementsCount("//h1[contains(text(),'страницы на нашем сайте нет')]") > 0) {
                SaveUrlAsync(b, deleteUrl: true);
                return;
            }
            Log.Add("avito.ru: ошибка при снятии объявления! - " + _bus[b].name);
        }
        //добавить объявление асинхронно
        public async Task AddAsync() {
            await ChechAuthAsync();
            for (int b = _bus.Count - 1; b > -1 && AddCount > 0; b--) {
                if ((_bus[b].avito == null || !_bus[b].avito.Contains("http")) &&
                    !_bus[b].GroupName().Contains("ЧЕРНОВИК") &&
                    _bus[b].amount > 0 &&
                    _bus[b].price >= _priceLevel &&
                    _bus[b].images.Count > 0) {
                    var t = Task.Factory.StartNew(() => {
                        _dr.Navigate("https://avito.ru/additem", "//span[text()='Категория']");
                        SetCategory(b);
                        SetTitle(b);
                        SetStatus(b);
                        SetOfferType();
                        SetDiskParams(b);
                        //SetPartNumber(b);
                        SetManufacture(b);
                        SetImages(b);
                        //SetDesc(b);
                        SetDesc(b, minDesc: true);
                        SetPrice(b);
                        SetAddress();
                        SetPhone();
                        SetGeo(b);
                        PressOk();
                    });
                    try {
                        await t;
                        await SaveUrlAsync(b);
                        Log.Add("avito.ru: " + _bus[b].name + " - объявление добавлено, осталось " + --AddCount);
                    } catch (Exception x) {
                        Log.Add("avito.ru: " + _bus[b].name + " - ошибка добавления! " + x.Message);
                        break;
                    }
                }
            }
        }
        //сохранение ссылки
        private async Task SaveUrlAsync(int b, bool deleteUrl = false) {
            if (deleteUrl) {
                Log.Add("avito: удаляю ссылку из карточки " + _bus[b].name + "  --  " + _bus[b].avito);
                _bus[b].avito = "";
                await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                    { "id", _bus[b].id},
                    { "name", _bus[b].name},
                    { _url, _bus[b].avito}
                });
            } else {
                await Task.Delay(_delay); //ждем, потому что объявление не всегда сразу готово
                var id = _dr.GetUrl().Split('[')[1].Split(']')[0];
                var url = "https://www.avito.ru/items/" + id;
                for (int i = 0; ; i++) {
                    await _dr.NavigateAsync(url, ".title-info-main");
                    url = _dr.GetUrl();
                    if (!url.Contains("avito.ru/items"))
                        break;
                    if (i > 9)
                        throw new Exception("ссылка на объявление не найдена!");
                }
                //проверяем ссылку
                if (url.Contains("https://www.avito.ru/kaluga")) {
                    _bus[b].avito = _dr.GetUrl();
                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                        {"id", _bus[b].id},
                        {"name", _bus[b].name},
                        {_url, _bus[b].avito}
                    });
                }
            }
            await Task.Delay(_delay);
        }
        //параметры колесных дисков
        private void SetDiskParams(int b) {
            if (_bus[b].GroupName() == "Шины, диски, колеса") {
                var bn = _bus[b].name.ToLowerInvariant();
                if (bn.Contains("диск")) { //заполняю параметры дисков
                    //тип диска
                    var selector = "//option[contains(text(),'Литые')]/..";
                    if (bn.Contains("штамп"))
                        _dr.WriteToSelector(selector, "шта" + OpenQA.Selenium.Keys.Enter);
                    else if (bn.Contains("кован"))
                        _dr.WriteToSelector(selector, "ков" + OpenQA.Selenium.Keys.Enter);
                    else
                        _dr.WriteToSelector(selector, "лит" + OpenQA.Selenium.Keys.Enter);
                    //диметр
                    selector = "//option[text()='16.5']/..";
                    _dr.WriteToSelector(selector, _bus[b].GetDiskSize() + OpenQA.Selenium.Keys.Enter);
                    //ширина обода
                    selector = "//option[text()='3.5']/..";
                    _dr.WriteToSelector(selector, "5" + OpenQA.Selenium.Keys.Enter); //ставим 5 по дефолту, т.к. нет инфы
                    //количество отверстий
                    selector = "//option[text()='3']/..";
                    _dr.WriteToSelector(selector, _bus[b].GetNumberOfHoles() + OpenQA.Selenium.Keys.Enter);
                    //диаметр отверстий
                    selector = "//option[text()='100']/..";
                    _dr.WriteToSelector(selector, _bus[b].GetDiameterOfHoles() + OpenQA.Selenium.Keys.Enter);
                    //вылет
                    selector = "//option[text()='-98']/..";
                    _dr.WriteToSelector(selector, "0" + OpenQA.Selenium.Keys.Enter);  //TODO возможно стоит указать 30, или парсить.. редко указан в объявлении, ставим 0 по дефолту
                }
                //РЕЗИНА
                //if (bn.Contains("резина")) {
                //try {
                //    av.FindElement(By.CssSelector("div.form-fieldset.clearfix.clearfix_left.js-fieldset.js-fieldset_733 > div.col-3.form-select-v2.js-form-select-v2")).Click();
                //} catch { }
                //Thread.Sleep(1000);
                //string size = GetDiskSize(bn);
                //size = size ?? "13";
                //foreach (var el in av.FindElements(By.CssSelector("#flt_param_733 > option")))//#flt_param_733 > option
                //{
                //    if (el.Text == size) {
                //        selector = "";
                //        el.Click();
                //        break;
                //    }
                //}
                ////сезонность
                //if (bn.Contains("летн")) {
                //    av.FindElements(By.TagName("option")).Where(w => w.Text.Contains("Летние")).ToList()[0].Click();
                //} else if (bn.Contains("всесезон")) {
                //    av.FindElements(By.TagName("option")).Where(w => w.Text.Contains("Всесезонные")).ToList()[0].Click();
                //} else {
                //    av.FindElements(By.TagName("option")).Where(w => w.Text.Contains("Зимние шипованные")).ToList()[0].Click();
                //}
                //if (bn.Contains("колесо") || bn.Contains("докатка")) {
                //}
                //if (bn.Contains("колпа")) {
                //av.FindElement(By.CssSelector("div.js-parameters__container > div > div.col-3.form-select-v2.js-form-select-v2")).Click();
                //Thread.Sleep(1000);
                //string size = GetDiskSize(bn);
                //size = size ?? "4";
                //foreach (var el in av.FindElements(By.CssSelector("#flt_param_740 > option"))) {
                //    if (el.Text == size) {
                //        selector = "";
                //        el.Click();
                //        break;
                //    }
                //}
                //break;
            }
        }
        //устанавливка адреса
        private void SetAddress() {
            var s = "Калуга, Московская улица, 331";
            _dr.WriteToSelector("input[id*='params[2851]']", s);
            Actions a = new Actions(_dr._drv);
            a.SendKeys(OpenQA.Selenium.Keys.ArrowDown)
             .SendKeys(OpenQA.Selenium.Keys.Enter)
             .Build().Perform();
        }
        //подъем объявлений
        private async Task AvitoUpAsync() {
            var url = "https://www.avito.ru/profile";
            await _dr.NavigateAsync(url, ".profile-tabs");
            int inactive = 0, active = 0, old = 0, archived = 0;
            await Task.Factory.StartNew(() => {
                var inactiveString = _dr.GetElementText("//button[@data-marker='tabs/inactive']/span/span").Replace(" ", "");
                inactive = inactiveString.Length > 0 ? int.Parse(inactiveString) : 0;
                var activeString = _dr.GetElementText("//button[@data-marker='tabs/active']/span/span").Replace(" ", "");
                active = activeString.Length > 0 ? int.Parse(activeString) : 0;
                var oldString = _dr.GetElementText("//button[@data-marker='tabs/old']/span/span").Replace(" ", "");
                old = oldString.Length > 0 ? int.Parse(oldString) : 0;
                var archivedString = _dr.GetElementText("//button[@data-marker='tabs/archived']/span/span").Replace(" ", "");
                archived = archivedString.Length > 0 ? int.Parse(archivedString) : 0;
            });
            //процент страниц для проверки
            var checkPagesProcent = await _db.GetParamIntAsync("avito.checkPagesProcent");
            //перебираю номера страниц
            for (int i = 0; i < active / 50; i++) {
                //пропуск страниц
                if (_rnd.Next(100) > checkPagesProcent)
                    continue;
                //проверить данную страницу
                if (i < 101)
                    await ParsePage("/active", i + 1);
                //проверить также страницу снятых, если номер в пределах
                if (i < old / 50)
                    await ParsePage("/old", i + 1);
                //проверить также страницу удаленных, если номер в пределах
                if (i < archived / 50)
                    await ParsePage("/archived", i + 1);
            }
            //проход страниц неактивных и архивных объявлений будет последовательным, пока не кончатся страницы или количество для подъема
            for (int i = 0; i <= old / 50 && CountToUp > 0; i++) { await ParsePage("/old", i + 1); }
            for (int i = 0; i <= inactive / 50 && CountToUp > 0; i++) { await ParsePage("/inactive", i + 1); }
            for (int i = 0; i <= archived / 50 && CountToUp > 0; i+=10) { await ParsePage("/archived", i + 1); }
        }
        //проверка объявлений на странице
        private async Task ParsePage(string location, int numPage) {
            try {
                if (DateTime.Now.Minute > 50)
                    return; //ограничитель периода
                //перехожу в раздел
                var url = "https://avito.ru/profile/items" + location + "/rossiya?p=" + numPage;
                if (DateTime.Now.Second % 2 == 0)
                    url += "&s=5";
                await _dr.NavigateAsync(url, ".profile-tabs");
                //парсинг объявлений на странице
                var items = await _dr.FindElementsAsync("//div[contains(@class,'item-body-root')]//a");
                var urls = items.Select(s => s.GetAttribute("href")).ToList();
                var ids = urls.Select(s => s.Split('_').Last()).ToList();
                var names = items.Select(s => s.Text).ToList();
                var el = await _dr.FindElementsAsync("//div[contains(@class,'price-root')]/span");
                var prices = el.Select(s => s.Text.Replace(" ", "").TrimEnd('\u20BD')).ToList();
                //проверка результатов парсинга
                if (items.Count == 0)
                    throw new Exception("ошибка парсинга: не найдны ссылки на товары");
                if (items.Count != names.Count || names.Count != prices.Count)
                    throw new Exception("ошибка парсинга: не соответствует количество ссылок и цен");
                //перебираю найденное
                for (int i = 0; i < urls.Count(); i++) {
                    //ищу индекс карточки в базе
                    var b = _bus.FindIndex(f => f.avito.Contains(ids[i]));
                    if (b >= 0) {
                        //проверяю, нужно ли его снять
                        //if (location == "/active" && _bus[b].amount <= 0) Delete(b);
                        if (_bus[b].amount <= 0 && location != "/archived")
                            await DeleteAsync(b);
                        //проверяю цены
                        if (_bus[b].price.ToString() != prices[i] && location != "/archived")
                            await EditAsync(b);
                        //если объявление в разделе "архив" или "неопубликованные", но есть на остатках и цена больше пороговой - поднимаю
                        if (CountToUp > 0 &&
                            _bus[b].price >= _priceLevel &&
                            _bus[b].amount > 0 &&
                            (location != "/active")) {
                            //если удалено - восстанавливаю, без этого не активируется
                            if (location == "/archived") {
                                await _dr.NavigateAsync(_bus[b].avito, ".title-info-title");
                                _dr.ButtonClick("//button[@name='restore']");
                            }
                            //если на вкладке архив
                            if (location == "/old") {
                                _dr.Navigate(_bus[b].avito);
                                _dr.ButtonClick("//button[@name='start']");
                                _dr.ButtonClick("//button[@type='submit']/span[text()='Активировать']/..", _delay);
                            }
                            //активирую неопубликованное
                            else if (await UpOfferAsync(b)) {
                                await Task.Delay(_delay);
                                await RandomClicksAsync();
                                if (_editAfterUp)
                                    await EditAsync(b);
                            }
                        }
                    }
                }
            } catch (Exception x) {
                Log.Add("avito.ru: ошибка парсинга страницы! - " + x.Message);
            }
        }
        //случайные клики
        async Task RandomClicksAsync() => await Task.Factory.StartNew(() => {
            if (_rnd.Next(4) == 1) {
                try {
                    var a = _dr.FindElements("//a[not(contains(@href,'/exit'))]").ToList();
                    a[_rnd.Next(a.Count)].Click();
                    Thread.Sleep(_rnd.Next(_delay * 10));
                } catch (Exception x) {
                    Log.Add("avito.ru: неудачный клик - " + x.Message);
                }
            }
            if (_rnd.Next(4) == 1) {
                if (_rnd.Next(3) == 1)
                    _dr._drv.Navigate().Back();
                if (_rnd.Next(3) == 1)
                    _dr._drv.Navigate().Forward();
                _dr.Navigate("https://avito.ru/profile");
            }
        });
        //активация объявления
        private async Task<bool> UpOfferAsync(int b) {
            var succ = false;
            await Task.Factory.StartNew(() => {
                var id = _bus[b].avito.Replace("/", "_").Split('_').Last();
                var url = "https://www.avito.ru/account/pay_fee?item_id=" + id;
                _dr.Navigate(url, "//div");
                //проверка наличия формы редактирования
                if (_dr.GetElementsCount("//*[contains(text(),'Состояние')]") > 0) {
                    SetStatus(b);
                    SetManufacture(b);
                    PressOk();
                }
                //нажимаю активировать и уменьшаю счетчик
                var butOpub = _dr.FindElements("//button[@type='submit']/span[text()='Активировать']/..");
                if (butOpub.Count > 0) {
                    butOpub.First().Click();
                    Log.Add("avito.ru: " + _bus[b].name + " - объявление  активировано, осталось " + --CountToUp);
                    succ = true;
                    Thread.Sleep(_delay);
                }
            });
            if (succ)
                return true;
            return false;
        }
        //выбор статуса
        private void SetStatus(int b) {
            //новый или б/у
            if (_bus[b].IsNew())
                _dr.ButtonClick("//label/span[contains(text(),'Новое')]/../..");
            else
                _dr.ButtonClick("//span[contains(text(),'Б/у')]/../..");
            //оригинальность
            if (_bus[b].IsOrigin())
                _dr.ButtonClick("//span[text()='Оригинал']/../..");
            else
                _dr.ButtonClick("//span[text()='Аналог']/../..");
        }
        //указываю телефон
        private void SetPhone() {
            _dr.WriteToSelector("//input[@id='phone']", "9208994545");
        }
        //расширение гео
        private void SetGeo(int b) {
            var g = _bus[b].GroupName();
            var n = _bus[b].name.ToLowerInvariant();
            //отключаю Россия
            if ((g == "Шины, диски, колеса"||
                g == "Инструменты" ||
                g == "ИНСТРУМЕНТЫ (НОВЫЕ)"||
                g == "Аудио-видеотехника" ||
                g == "Датчики" ||
                g == "Генераторы" ||
                g == "Электрика, зажигание" ||
                g == "Блоки управления" ||
                g == "Стартеры" ||
                g == "Салон" ||
                g == "Ручки и замки кузова"||
                g == "Петли" ||
                g == "Кузовные запчасти" ||
                g == "Пластик кузова" ||
                g == "Зеркала" ||
                g == "Кронштейны, опоры" ||
                g == "Тросы автомобильные"||
                g == "Подвеска и тормозная система"||
                g == "Топливная, выхлопная система"||
                g == "Система охлаждения двигателя"||
                g == "Система отопления и кондиционирования"||
                g == "Двигатели"||
                g == "Детали двигателей") && !_bus[b].IsNew()
                ||
                !(g == "Шины, диски, колеса"||
                g == "Аудио-видеотехника" ||
                g == "Ручки и замки кузова"||
                g == "Петли"||
                g == "Кузовные запчасти"||
                g == "Пластик кузова"||
                g == "Зеркала"||
                g == "Кронштейны, опоры"||
                g == "Тросы автомобильные"||
                g == "Датчики"||
                g == "Генераторы"||
                g == "Электрика, зажигание"||
                g == "Блоки управления"||
                g == "Стартеры"||
                g == "Топливная, выхлопная система"||
                g == "Трансмиссия и привод"||
                g == "Подвеска и тормозная система"||
                g == "Салон"||
                g == "Рулевые рейки, рулевое управление"||
                g == "Система отопления и кондиционирования"||
                g == "Система охлаждения двигателя"||
                g == "РАДИАТОРЫ ОХЛАЖДЕНИЯ (НОВЫЕ)"||
                g == "Двигатели"||
                g == "Детали двигателей"||
                g == "Автостекло"||
                g == "Световые приборы транспорта") && _bus[b].IsNew()
                ) {
                for (int i = 0; i < 5; i++) {
                    if (_dr.GetElementsCount("//label[contains(@class,'checkbox-checked-')]/..//*[text()='Россия']") > 0)
                        _dr.ButtonClick("//span[text()='Россия']", _delay);
                    else break;
                }
            } else {
                //включаю Россия
                for (int i = 0; i < 5; i++) {
                    if (_dr.GetElementsCount("//label[contains(@class,'checkbox-checked-')]/..//*[text()='Россия']") == 0)
                        _dr.ButtonClick("//span[text()='Россия']", _delay);
                    else break;
                }
            }
        }
        //нажимаю ОК
        private void PressOk(int count = 2) {
            int err = 0;
            for (int i = 0; i < count;) {
                if (err > 10)
                    throw new Exception("не нажимается кнопка ОК");
                _dr.ButtonClick("//button[contains(@data-marker,'button-next')]");
                //всплывающее окно с ошибкой
                if (_dr.GetElementsCount("//*[@role='button' and @name='close']") > 0) {
                    _dr.ButtonClick("//*[@role='button' and @name='close']", 15000);
                    err++;
                    //проверка корректности заполнения марки
                } else if (_dr.GetElementsCount("//div[contains(@class,'withoutValue') or contains(@class,'select-error')]") > 0) {
                    _dr.ButtonClick("//input[@id='params[111075]']/..//div/*", 15000);
                } else
                    i++;
            }
        }
        //загрузка фото
        private void SetImages(int b) {
            WebClient cl = new WebClient();
            cl.Encoding = Encoding.UTF8;
            var num = _bus[b].images.Count > 10 ? 10 : _bus[b].images.Count;
            for (int u = 0; u < num; u++) {
                for (int t = 0; ; t++) {
                    try {
                        byte[] bts = cl.DownloadData(_bus[b].images[u].url);
                        File.WriteAllBytes("avito_" + u + ".jpg", bts);
                        Thread.Sleep(_delay / 10);
                        _dr.SendKeysToSelector("input[type=file]", Application.StartupPath + "\\" + "avito_" + u + ".jpg");
                    } catch (Exception x) {
                        Log.Add("avito: SetImages - " + x.Message);
                    }
                    if (_dr.GetElementsCount("//div[contains(@class,'alert-content')]/../*[@role='button']") > 0)
                        _dr.ButtonClick("//div[contains(@class,'alert-content')]/../*[@role='button']");
                    else
                        break;
                    if (t > 10)
                        throw new Exception("ошибка загрузки фото!");
                }
            }
            cl.Dispose();
        }
        //заполнение описание
        void SetDesc(int b, bool minDesc = false) {
            _dr.WriteToSelector("div.DraftEditor-root", sl: GetAvitoDesc(b, minDesc: minDesc));
        }
        //создаю описание с дополнением
        public List<string> GetAvitoDesc(int b, bool minDesc = false) {
            List<string> s = _bus[b].DescriptionList(2799, _addDesc);
            if (!minDesc)
                s.AddRange(_addDesc2);
            return s;
        }
        //указываю цену
        private void SetPrice(int b) {
            _dr.WriteToSelector("#price", _bus[b].price.ToString());
        }
        //указываю заголовок
        private void SetTitle(int b) {
            _dr.WriteToSelector("#title", _bus[b].NameLength(130));
        }
        //кликнуть в..
        private void AvitoClickTo(string s) {
            _dr.ButtonClick("//div[@data-marker='category-wizard/button' and text()='" + s + "']");
        }
        //проверяю ссылки
        public async Task CheckUrlsAsync() => await Task.Factory.StartNew(() => {
            try {
                //проверяю ссылки в карточках на корректность
                var reg = "http.+([0-9]+)$";
                foreach (var item in _bus.Where(w => w.avito.Contains("avito"))) {
                    if (!Regex.IsMatch(item.avito, reg))
                        Log.Add("avito.ru: ошибка! неверная ссылка! - " + item.name + " - " + item.avito);
                }
                //проверяю объявление по ссылке в случайной карточке с положительным остатком checkUrlsCount раз
                var checkUrlsCount = _db.GetParamInt("avito.checkUrlsCount");
                if (checkUrlsCount > 0)
                    Log.Add("avito.ru: проверяю " + checkUrlsCount + " ссылок");
                //ссылки на карточки без остатков с фото и ссылкой на авито, которые нужно проверить на живучесть
                var urls = _bus.Where(w => w.amount<=0 &&w.images.Count>0 && w.avito.Contains("http")).ToList();
                Log.Add("avito.ru: ссылок для проверки "+urls.Count+"\n"+urls.Select(s=>s.name).Aggregate((a1,a2)=>a1+"\n"+a2)+"=========\n");
                //перебираю ссылки
                for (int b = 0; b<urls.Count; b++) {
                    var err = false;
                    try {
                        _dr.Navigate(_bus[b].avito, ".title-info-title");
                        if (_dr.GetElementsCount("//p[contains(text(),'объявление навсегда')]") > 0)
                            err=true;
                    } catch (Exception x) {
                        err=true;
                    }
                    if (err) SaveUrlAsync(b, deleteUrl: true);
                    //проверяю фотографии в объявлении
                    CheckPhotos(b);
                }
            } catch (Exception x) {
                Log.Add("avito.ru: ошибка при проверке ссылок - " + x.Message);
                if (x.Message.Contains("timed out"))
                    throw;
            }
        });
        //указываю тип товара
        private void SetOfferType() {
            _dr.WriteToSelector("//option[contains(text(),'на продажу')]/..", OpenQA.Selenium.Keys.ArrowDown + OpenQA.Selenium.Keys.ArrowDown + OpenQA.Selenium.Keys.Enter);
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
                if (c.Length > 20)
                    _db.SetParam("avito.cookies", c);
            }
        }
        //закрыть браузер
        public void Quit() {
            _dr?.Quit();
            _dr = null;
        }
        //выбор категории
        void SetCategory(int b) {
            AvitoClickTo("Транспорт");
            AvitoClickTo("Запчасти и аксессуары");
            _dr.ButtonClick("//button[contains(@class,'cascader-header-button')]");
            var bn = _bus[b].name.ToLower();
            string selector;
            switch (_bus[b].GroupName()) {
                case "Аудио-видеотехника":
                    selector = "Аудио- и видеотехника";
                    break;
                case "Шины, диски, колеса":
                    selector = "Шины, диски и колёса";
                    break;
                case "АВТОХИМИЯ":
                    selector = "Автокосметика и автохимия";
                    break;
                default:
                    selector = "Запчасти";
                    break;
            }
            AvitoClickTo(selector);
            switch (_bus[b].GroupName()) {
                case "Аудио-видеотехника":
                    selector = "";
                    break;
                case "Шины, диски, колеса":
                    if (bn.Contains("колесо") || bn.Contains("докатка")) {
                        selector = "Колёса";
                    } else if (bn.Contains("колпак")) {
                        selector = "Колпаки";
                    } else if (bn.Contains("диск")) {
                        selector = "Диски";
                    } else if (bn.Contains("шины") || bn.Contains("резина")) {
                        selector = "Шины";
                    }
                    break;
                case "Автохимия":
                    selector = "";
                    break;
                default:
                    selector = "Для автомобилей";
                    break;
            }
            if (selector.Length > 0) {
                AvitoClickTo(selector);
                switch (_bus[b].GroupName()) {
                    case "Ручки и замки кузова":
                    case "Петли":
                    case "Кузовные запчасти":
                    case "Пластик кузова":
                    case "Зеркала":
                    case "Кронштейны, опоры":
                    case "Тросы автомобильные":
                        selector = "Кузов";
                        break;
                    case "Датчики":
                    case "Генераторы":
                    case "Электрика, зажигание":
                    case "Блоки управления":
                    case "Стартеры":
                        selector = "Электрооборудование";
                        break;
                    case "Топливная, выхлопная система":
                        selector = "Топливная и выхлопная системы";
                        break;
                    case "Трансмиссия и привод":
                        selector = "Трансмиссия и привод";
                        break;
                    case "Подвеска и тормозная система":
                        selector = "Подвеска";
                        break;
                    case "Салон":
                        selector = "Салон";
                        break;
                    case "Рулевые рейки, рулевое управление":
                        selector = "Рулевое управление";
                        break;
                    case "Система отопления и кондиционирования":
                    case "Система охлаждения двигателя":
                    case "РАДИАТОРЫ ОХЛАЖДЕНИЯ (НОВЫЕ)":
                        selector = "Система охлаждения";
                        break;
                    case "Двигатели":
                    case "Детали двигателей":
                        selector = "Двигатель";
                        break;
                    case "Автостекло":
                        selector = "Стекла";
                        break;
                    case "Световые приборы транспорта":
                        selector = "Автосвет";
                        break;
                    default:
                        selector = "Запчасти для ТО";
                        break;
                }
                if (selector.Length > 0) {
                    AvitoClickTo(selector);
                    switch (_bus[b].GroupName()) {
                        case "Пластик кузова":
                        case "Ручки и замки кузова":
                        case "Петли":
                        case "Кузовные запчасти":
                            _dr.ButtonClick("//span[text()='Тип детали кузова']/../../..//select");
                            if (bn.Contains("накладка") || bn.Contains("молдинг") || bn.Contains("бархот")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "молд" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("бампер")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "бамп" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("балка") || bn.Contains("лонжер") || bn.Contains("подрамник")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "балк" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("брызговик")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "брызг" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("замок") || bn.Contains("личинк") || bn.Contains("замка")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "замк" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("бака")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "креп" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("защита") || bn.Contains("ыльник") || bn.Contains("щит ") || bn.Contains("щиток ")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "защи" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("дверь")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "двер" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("креплен") || bn.Contains("ронштейн") || bn.Contains("ержатель") ||
                                bn.Contains("аправляющ") || bn.Contains("петл")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "креп" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("крыло ")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "крыл" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("крыша")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "кузов" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("крышка") && bn.Contains("багажн")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "крышк" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("капот")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "капот" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("порог")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "порог" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("решет") && bn.Contains("радиатор")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "решет" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "кузов" + OpenQA.Selenium.Keys.Enter);
                            break;
                        case "Зеркала":
                            _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "зерк" + OpenQA.Selenium.Keys.Enter);
                            break;
                        case "Кронштейны, опоры":
                            _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "креп" + OpenQA.Selenium.Keys.Enter);
                            break;
                        case "Тросы автомобильные":
                            _dr.WriteToSelector("//option[contains(text(),'Двери')]/..", "кузов" + OpenQA.Selenium.Keys.Enter);
                            break;
                        case "Детали двигателей":
                        case "Двигатели":
                            _dr.ButtonClick("//span[text()='Тип детали двигателя']/../../..//select");
                            if (bn.Contains("цилиндр") || bn.Contains("гбц ") ||
                                bn.Contains("низ ") || bn.Contains("поддон")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "блок" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("двигатель")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "двига" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("ремн") || bn.Contains("грм ") ||
                                bn.Contains("кожух") || bn.Contains("промежут") || bn.Contains("ванос") ||
                                bn.Contains("клапан ") || bn.Contains("клапанной") || bn.Contains("клапана") ||
                                bn.Contains("вал ") || bn.Contains("вала ") || bn.Contains("газорасп") ||
                                bn.Contains("компенсат") || bn.Contains("шестерн") || bn.Contains("шкив")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "ремни" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("коленвал") || bn.Contains("маховик") || bn.Contains("венец")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "колен" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("ролик") || bn.Contains("натяжитель")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "привод" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("крышка") || bn.Contains("плита")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "клапан" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("проклад")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "прокла" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("масло") || bn.Contains("маслян")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "маслян" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("поршен") || bn.Contains("шатун") || bn.Contains("кольц") || bn.Contains("вкладыш")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "поршн" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            if (bn.Contains("вентиля")) {
                                _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "патру" + OpenQA.Selenium.Keys.Enter);
                                break;
                            }
                            _dr.WriteToSelector("//option[contains(text(),'Двигатель')]/..", "блок" + OpenQA.Selenium.Keys.Enter);
                            break;
                    }
                }
            }
        }

    }
}