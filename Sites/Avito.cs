using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using Selen.Base;
using Selen.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Selen.Sites {
    class Avito {
        Selenium _dr;
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
            //сохраняю ссылку для работы с базой данных
            _db = DB._db;
        }
        //главный цикл синхронизации
        public async Task StartAsync(List<RootObject> bus) {
            Log.Add("avito.ru: начало выгрузки...");
            GetParams(bus);
            await AuthAsync();
            await RemoveDraftAsync();
            await EditAllAsync();
            await AddAsync();
            await AvitoUpAsync();
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
            else CountToUp = 0;
            //сколько новых объявлений подавать каждый час
            if (DateTime.Now.Hour >= _db.GetParamInt("avito.addFromHour") &&
                DateTime.Now.Hour < _db.GetParamInt("avito.addToHour"))
                AddCount = _db.GetParamInt("avito.countToAdd");
            else AddCount = 0;
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
                        _dr.ButtonClick("//button[contains(@class,'actions-dropdown')]");
                        //нажимаю удалить
                        _dr.ButtonClick("//button[contains(@data-marker,'remove-draft')]");
                    }
                });
            } catch (Exception x) {
                Log.Add("avito.ru: ошибка при удалении черновика! - " + x.Message);
                if (x.Message.Contains("timed out") ||
                    x.Message.Contains("already closed") ||
                    x.Message.Contains("invalid session id") ||
                    x.Message.Contains("chrome not reachable")) throw;
            }
        }
        //авторизация
        private async Task AuthAsync() {
            await Task.Factory.StartNew(() => {
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
                for(int i =0; _dr.GetElementsCount(".profile-tabs") == 0; i++) {
                    Thread.Sleep(_delay * 5);
                    _dr.ButtonClick("//a[contains(@href,'reload')]");
                    _dr.ButtonClick("//div[contains(@class,'username')]/div/a");
                    _dr.ButtonClick("//button[@id='reload-button']");
                    if (_dr.GetElementsCount("//h1[contains(text(),'502')]") > 0) _dr.Refresh("https://www.avito.ru/profile");
                    if (i >= 10) throw new Exception("ошибка входа в личный кабинет");
                }
                SaveCookies();
            });
        }
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
        private async Task ChechAuthAsync() {
            await Task.Factory.StartNew(() => {
                //закрываю рекламу
                _dr.ButtonClick("//button[contains(@class,'popup-close')]");
                while (_dr.GetElementsCount("//p/a[contains(text(),'обновить страницу')]") > 0)
                    _dr.ButtonClick("//p/a[contains(text(),'обновить страницу')]");
                //проверяю элемент Мои объявления
                while (_dr.GetElementsCount("//a[text()='Мои объявления']") == 0) {
                    if (_dr.GetElementsCount("//h1[text()='Сайт временно недоступен']") > 0)
                        _dr.Refresh();
                    else {
                        throw new Exception("ошибка загрузки сайта!");
                    }
                }
            });
        }
        //редактирование объявления
        private async Task EditAsync(int b) {
            if (_bus[b].price > 0) {  //защита от нулевой цены в базе

                var isDeleted = await Task.Factory.StartNew(() => {
                    _dr.Navigate(_bus[b].avito);
                    //есть на остатках, но страница без номера объявления - значит нужно восстановить
                    if (_bus[b].amount > 0 && _dr.GetElementsCount("//span[@data-marker='item-view/item-id']") == 0)
                        _dr.ButtonClick("//button[text()='Восстановить']");
                    //нет номера объявления и есть строка вы удалили это объявление навсегда - удаляю ссылку из карточки
                    if (_dr.GetElementsCount("//span[@data-marker='item-view/item-id']") == 0 && 
                        _dr.GetElementsCount("//p[contains(text(),'объявление навсегда')]") > 0) {
                        SaveUrlAsync(b, deleteUrl: true);
                        Thread.Sleep(1000);
                        return true;
                    }
                    return false;
                });
                if (isDeleted) return;
                var url = "https://www.avito.ru/items/edit/" + _bus[b].avito.Replace("/", "_").Split('_').Last();
                bool isAlive = true;
                for(int i=0; ; i++) {
                    await ChechAuthAsync();
                    await _dr.NavigateAsync(url, "//button[contains(@data-marker,'button-next')]");
                    if (_dr.GetElementsCount("//a[@href='/profile']") > 0 && _dr.GetElementsCount("//div[@data-marker='category']")>0) break;
                    isAlive = await CheckIsOfferAlive(b);
                    if (!isAlive) break;
                    if (i == 10) {
                        Log.Add("avito.ru: ошибка! не удается загрузить объявление! - " + _bus[b].name + "  url: " + url);
                        break;
                    }
                }
                if (isAlive) {
                    await Task.Factory.StartNew(() => {
                        SetStatus(b);
                        SetPartNumber(b);
                        SetManufacture(b);
                        SetTitle(b);
                        SetPrice(b);
                        SetDesc(b);
                        PressOk();
                    });
                } else {
                    await SaveUrlAsync(b, deleteUrl: true);
                }
            }
        }

        private void SetManufacture(int b) {
            //производителя авито определяет сам
        }

        private void SetPartNumber(int b) {
            var elem = _dr.FindElements("//span[text()='Номер запчасти']/../../..//input");
            if (elem.Count > 0)
                try {
                    _dr.WriteToIWebElement(elem.First(), _bus[b].part.Split(',').First());
                } catch { }
        }

        //проверка активно ли объявление
        private async Task<bool> CheckIsOfferAlive(int b) {
            var count = 0;
            await Task.Factory.StartNew(() => {
                count = _dr.GetElementsCount("//p[contains(text(),'удалили это объявление') or contains(text(),'неверной ссылке')]");
            });
            if (count > 0 && !_dr.GetUrl().Contains("isDirect=1")) return false;
            return true;
        }
        //удалить объявление асинхронно
        private async Task DeleteAsync(int b) {
            await Task.Factory.StartNew(() => {
                Delete(b);
            });
        }
        //удалить объявление
        private void Delete(int b) {
            for (int i = 0; i < 10; i++) {
                _dr.Navigate(_bus[b].avito);
                //ChechAuthAsync();
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
                    else continue; 
                }
                //закрываю окно опроса
                _dr.ButtonClick("//img[@alt='close']");
                //если появилась кнопка удалить - нажимаю
                _dr.ButtonClick("//button/*[text()='Удалить']");
                //есть кнопка восстановить - объявление уже удалено
                if (_dr.GetElementsCount("//button[text()='Восстановить']") > 0) return;
                //если объявление удалено окончательно - нужно удалить ссылку из карточки товара
                if (_dr.GetElementsCount("//div[@class='item-view-warning-content']/p[contains(text(),'навсегда')]") > 0) {
                    SaveUrlAsync(b, deleteUrl: true);
                    return;
                }
            }
            Log.Add("avito.ru: ошибка при снятии объявления! - " + _bus[b].name);
        }
        //добавить объявление асинхронно
        public async Task AddAsync() {
            await ChechAuthAsync();
            for (int b = _bus.Count - 1; b > -1  && AddCount > 0; b--) {
                if ((_bus[b].avito == null || !_bus[b].avito.Contains("http")) &&
                    _bus[b].tiu.Contains("http") &&
                    _bus[b].amount > 0 &&
                    _bus[b].price >= _priceLevel &&
                    _bus[b].images.Count > 0) {
                    var t = Task.Factory.StartNew(() => {
                        _dr.Navigate("https://avito.ru/additem", "//span[text()='Категория']");
                        SetCategory(b);
                        SetAddress();
                        SetImages(b);
                        SetTitle(b);
                        SetOfferType();
                        SetStatus(b);
                        SetDiskParams(b);
                        SetPrice(b);
                        SetDesc(b);
                        //SetDesc(b, minDesc:true);
                        //SetPartNumber(b);
                        SetManufacture(b);
                        SetPhone();
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
                Log.Add("avito: удаляю ссылку из карточки "+ _bus[b].name+"  --  "+_bus[b].avito);
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
                    if (!url.Contains("avito.ru/items")) break;
                    if (i > 9) throw new Exception("ссылка на объявление не найдена!");
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
            if (_bus[b].GroupName()== "Шины, диски, колеса") {
                var bn = _bus[b].name.ToLowerInvariant();
                if (bn.Contains("диск")) { //заполняю параметры дисков
                    //тип диска
                    var selector = "//option[contains(text(),'Литые')]/..";
                    if (bn.Contains("штамп")) _dr.WriteToSelector(selector, "шта" + OpenQA.Selenium.Keys.Enter);
                    else if (bn.Contains("кован")) _dr.WriteToSelector(selector, "ков" + OpenQA.Selenium.Keys.Enter);
                    else _dr.WriteToSelector(selector, "лит" + OpenQA.Selenium.Keys.Enter);
                    //диметр
                    selector = "//option[text()='16.5']/..";
                    _dr.WriteToSelector(selector,_bus[b].GetDiskSize() + OpenQA.Selenium.Keys.Enter);
                    //ширина обода
                    selector = "//option[text()='3.5']/..";
                    _dr.WriteToSelector(selector,"5" + OpenQA.Selenium.Keys.Enter); //ставим 5 по дефолту, т.к. нет инфы
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
            _dr.WriteToSelector("input[id*='params[2851]']",s);
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
                var inactiveString = _dr.GetElementText("//li[@data-marker='tabs/inactive']/span[2]");
                inactive = inactiveString.Length > 0 ? int.Parse(inactiveString) :0;
                var activeString = _dr.GetElementText("//li[@data-marker='tabs/active']/span[2]");
                active = activeString.Length > 0 ? int.Parse(activeString) : 0;
                var oldString = _dr.GetElementText("//li[@data-marker='tabs/old']/span[2]");
                old = oldString.Length > 0 ? int.Parse(oldString) : 0;
                var archivedString = _dr.GetElementText("//li[@data-marker='tabs/archived']/span[2]");
                archived = archivedString.Length > 0 ? int.Parse(archivedString) : 0;
            });
            //процент страниц для проверки
            var checkPagesProcent = await _db.GetParamIntAsync("avito.checkPagesProcent");
            //перебираю номера страниц
            for (int i = 0; i < active/50; i++) {
                //пропуск страниц
                if (_rnd.Next(100) > checkPagesProcent) continue;
                //проверить данную страницу
                await ParsePage("/active", i+1);
                //проверить также страницу снятых, если номер в пределах
                if (i < old / 50) await ParsePage("/old", i+1);
                //проверить также страницу удаленных, если номер в пределах
                if (i < archived / 50) await ParsePage("/archived", i + 1);
            }
            //проход страниц неактивных и архивных объявлений будет последовательным, пока не кончатся страницы или количество для подъема
            for (int i = 0; i <= inactive / 50 && CountToUp > 0; i++) { await ParsePage("/inactive", i+1); }
            for (int i = 0; i <= old / 50 && CountToUp > 0; i++) { await ParsePage("/old", i+1); }
            for (int i = 0; i <= archived / 50 && CountToUp > 0; i++) { await ParsePage("/archived", i + 1); }
        }
        //проверка объявлений на странице
        private async Task ParsePage(string location, int numPage) {
            try {
                //перехожу в раздел
                var url = "https://avito.ru/profile/items" + location + "/rossiya?p=" + numPage;
                await _dr.NavigateAsync(url, ".profile-tabs");
                //парсинг объявлений на странице
                var items = await _dr.FindElementsAsync("//div[contains(@class,'text-t')]//a");
                var urls = items.Select(s => s.GetAttribute("href")).ToList();
                var ids = urls.Select(s => s.Split('_').Last()).ToList();
                var names = items.Select(s => s.Text).ToList();
                var el = await _dr.FindElementsAsync("//div[contains(@class,'price-root')]/span");
                var prices = el.Select(s => s.Text.Replace(" ", "").TrimEnd('\u20BD')).ToList();
                //проверка результатов парсинга
                if (items.Count == 0) throw new Exception("ошибка парсинга: не найдны ссылки на товары");
                if (items.Count != names.Count || names.Count != prices.Count) throw new Exception("ошибка парсинга: не соответствует количество ссылок и цен");
                //перебираю найденное
                for (int i = 0; i < urls.Count(); i++) {
                    //ищу индекс карточки в базе
                    var b = _bus.FindIndex(f => f.avito.Contains(ids[i]));
                    if (b >= 0) {
                        //проверяю, нужно ли его снять
                        //if (location == "/active" && _bus[b].amount <= 0) Delete(b);
                        if (_bus[b].amount <= 0 && location != "/archived") await DeleteAsync(b);
                        //если объявление в разделе "архив" или "неопубликованные", но есть на остатках и цена больше пороговой - поднимаю
                        if (CountToUp > 0 &&
                            _bus[b].price >= _priceLevel &&
                            _bus[b].amount > 0 &&
                            (location != "/active")){
                            //если удалено - восстанавливаю, без этого не активируется
                            if (location == "/archived") {
                                await _dr.NavigateAsync(_bus[b].avito, ".title-info-title");
                                _dr.ButtonClick("//button[@name='restore']");
                            }
                            //теперь активирую
                            if (await UpOfferAsync(b)) {
                                await Task.Delay(_delay);
                                if (_editAfterUp) await EditAsync(b);
                            }
                        }
                    }
                }
            } catch (Exception x) {
                Log.Add("avito.ru: ошибка парсинга страницы! - " + x.Message);
            }
        }
        //активация объявления
        private async Task<bool> UpOfferAsync(int b) {
            var succ = false;
            await Task.Factory.StartNew(()=> {
                var id = _bus[b].avito.Replace("/", "_").Split('_').Last();
                var url = "https://www.avito.ru/account/pay_fee?item_id=" + id;
                _dr.Navigate(url, "//h1[contains(text(),'Размещение')]");
                //проверка наличия формы редактирования
                if (_dr.GetElementsCount("//*[contains(text(),'Состояние')]") > 0) {
                    SetStatus(b);
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
            if (succ) return true;
            return false;
        }
        //выбор статуса
        private void SetStatus(int b) {
            if (_bus[b].IsNew()) {
                _dr.ButtonClick("//span[contains(text(),'Новое')]/../..");
            } else {
                _dr.ButtonClick("//span[contains(text(),'Б/у')]/../..");
            }
        }
        //указываю телефон
        private void SetPhone() {
            _dr.WriteToSelector("//input[@id='phone']", "9208994545");
        }
        //нажимаю ОК
        private void PressOk(int count = 2) {
            for (int i = 0; i < count; i++) {
                while (true) {
                    _dr.ButtonClick("//button[contains(@data-marker,'button-next')]");
                    var errorBox = _dr.FindElements("//div[contains(@class,'alert')]/*[@aria-label='Close']");
                    if (errorBox.Count > 0) {
                        _dr.ButtonClick("//div[contains(@class,'alert')]/*[@aria-label='Close']");
                    } else break;
                };
            }
        }
        //загрузка фото
        private void SetImages(int b) {
            WebClient cl = new WebClient();
            cl.Encoding = Encoding.UTF8;
            var num = _bus[b].images.Count > 10 ? 10 : _bus[b].images.Count;
            for (int u = 0; u < num; u++) {
                for (int t = 0; ; t++) {
                    byte[] bts = cl.DownloadData(_bus[b].images[u].url);
                    File.WriteAllBytes("avito_" + u + ".jpg", bts);
                    Thread.Sleep(_delay / 10);
                    _dr.SendKeysToSelector("input[type=file]", Application.StartupPath + "\\" + "avito_" + u + ".jpg");
                    if (_dr.GetElementsCount("//div[contains(@class,'alert-content')]/../*[@role='button']") > 0)
                        _dr.ButtonClick("//div[contains(@class,'alert-content')]/../*[@role='button']");
                    else
                        break;
                    if (t > 10) throw new Exception("ошибка загрузки фото!");
                }
            }
            cl.Dispose();
        }
        //заполнение описание
        void SetDesc(int b, bool minDesc=false) {
            _dr.WriteToSelector("div.DraftEditor-root", sl: GetAvitoDesc(b, minDesc:minDesc));
        }
        //создаю описание с дополнением
        public List<string> GetAvitoDesc(int b, bool minDesc=false) {
            List<string> s = _bus[b].DescriptionList(2799, _addDesc);
            if (!minDesc) s.AddRange(_addDesc2);
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
        public async Task CheckUrlsAsync() {
            await Task.Factory.StartNew(() => {
                try {
                    //проверяю ссылки в карточках на корректность
                    var reg = "http.+([0-9]+)$";
                    foreach (var item in _bus.Where(w=>w.avito.Contains("avito"))) {
                        if (!Regex.IsMatch(item.avito, reg))
                            Log.Add("avito.ru: ошибка! неверная ссылка! - " + item.name + " - " + item.avito);
                    }
                    //проверяю объявление по ссылке в случайной карточке с положительным остатком checkUrlsCount раз
                    var checkUrlsCount = _db.GetParamInt("avito.checkUrlsCount");
                    if (checkUrlsCount > 0) Log.Add("avito.ru: проверяю " + checkUrlsCount + " ссылок");
                    for (int i = 0; checkUrlsCount > 0; i++) {
                        //выбираю случайный индекс
                        var b = _rnd.Next(_bus.Count);
                        //если нет ссылки на авито или нет на остатках - пропускаю
                        if (!_bus[b].avito.Contains("http") || _bus[b].amount <= 0) continue;
                        _dr.Navigate(_bus[b].avito);
                        //не найден заголовок объявления на странице - возможно проблема с интернетом, пока пропускаем
                        if (_dr.GetElementsCount(".title-info-title") == 0) {
                            Log.Add("avito.ru: ошибка загрузки страницы для проверки ссылки " + _bus[b].name + "  --  " + _bus[b].avito);
                            continue;
                        }
                        //если удалено - восстанавливаю, т.к. есть положительный остаток
                        _dr.ButtonClick("//button[@name='restore']");
                        //если найден элемент "удалено навсегда" - удаляю ссылку из карточки товара (асинхронно без ожидания)
                        if (_dr.GetElementsCount("//p[contains(text(),'объявление навсегда')]") > 0) SaveUrlAsync(b, deleteUrl: true);
                        checkUrlsCount--;
                    }
                } catch (Exception x) {
                    Log.Add("avito.ru: ошибка при проверке ссылок - "+x.Message);
                }
            });
        }
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
                Thread.Sleep(_delay/10);
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
                case "Автохимия":
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