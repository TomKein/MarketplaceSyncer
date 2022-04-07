using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Selen.Base;
using Selen.Tools;
using Newtonsoft.Json;

namespace Selen.Sites {
    class Kupiprodai {
        public Selenium _dr;               //браузер
        DB _db;                     //база данных
        string _url;                //ссылка в карточке товара
        string[] _addDesc;          //дополнительное описание
        List<RootObject> _bus;      //ссылка на товары
        Random _rnd = new Random(); //генератор случайных чисел
        //конструктор
        public Kupiprodai() {
            _db = DB._db;
        }
        //загрузка кукис
        public void LoadCookies() {
            if (_dr != null && 
                _dr.Navigate("https://kupiprodai.ru/404", tryCount:1)) {
                var c = _db.GetParamStr("kupiprodai.cookies");
                _dr.LoadCookies(c);
            }
        }
        //сохранение кукис
        public void SaveCookies() {
            if (_dr != null && 
                _dr.Navigate("https://vip.kupiprodai.ru/", tryCount: 1)) {
                var c = _dr.SaveCookies();
                if (c.Length > 20)
                    _db.SetParam("kupiprodai.cookies", c);
            }
        }
        //закрытие браузера
        public void Quit() {
            _dr?.Quit();
            _dr = null;
        }
        //старт главного цикла синхронизации
        public async Task<bool> StartAsync(List<RootObject> bus) {
            Log.Add("kupiprodai.ru: начало выгрузки...");
            try {
                _bus = bus;
                _url = await _db.GetParamStrAsync("kupiprodai.url");
                _addDesc = JsonConvert.DeserializeObject<string[]>(
                    _db.GetParamStr("kupiprodai.addDescription"));
                await AuthAsync();
                await EditAsync();
                await DelNoActiveAsync();
                await ParseAsync();
                await CheckUrls();
            } catch (Exception x) {
                Log.Add("kupiprodai.ru: ошибка синхронизации! - " + x.Message);
                if (x.Message.Contains("timed out") ||
                    x.Message.Contains("already closed") ||
                    x.Message.Contains("invalid session id") ||
                    x.Message.Contains("chrome not reachable")) {
                    _dr.Quit();
                }
                return false;
            }
            Log.Add("kupiprodai.ru: выгрузка успешно завершена");
            return true;
        }
        //авторизация
        async Task AuthAsync() {
            await Task.Factory.StartNew(() => {
                if (_dr == null) {
                    _dr = new Selenium();
                    LoadCookies();
                }
                //если в кабинет не попали - ждем ручной вход
                if (!_dr.Navigate("https://kupiprodai.ru/login", "//span[@id='nickname']")) {
                    _dr.WriteToSelector("//input[@name='login']", _db.GetParamStr("kupiprodai.login"));
                    _dr.WriteToSelector("//input[@name='pass']", _db.GetParamStr("kupiprodai.password"));
                    _dr.ButtonClick("//input[@value='Войти']");
                    for (int i = 0; i < 10; i++) {
                        if (_dr.GetElementsCount("//span[@id='nickname']") > 0) { 
                            SaveCookies();
                            return;
                        }
                        Log.Add("kupiprodai.ru: ожидаю вход ("+ i + ")");
                        Thread.Sleep(60000);
                    }
                    throw new Exception("kupiprodai.ru: ошибка авторизации!");
                }
            });
        }
        //обновление объявлений
        async Task EditAsync() {
            for (int b = 0; b < _bus.Count; b++) {
                if (_bus[b].kp != null &&
                    _bus[b].kp.Contains("http")) {
                    //удаляю если нет на остатках
                    if (_bus[b].amount <= 0) {
                        await DeleteAsync(b);
                        //убираю ссылку из карточки товара
                        await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                            { "id", _bus[b].id },
                            { "name", _bus[b].name },
                            { _url, _bus[b].kp }
                        });
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
                    var url = _bus[b].kp.Replace("editmsg", "delmsg");
                    _dr.Navigate(url);
                    _bus[b].kp = " ";
                    Thread.Sleep(3000);
                    //TODO добавить проверку удаления
                    Log.Add("kupiprodai.ru: " + _bus[b].name + " - объявление удалено");
                });
            } catch (Exception x) {
                Log.Add("kupiprodai.ru: ошибка удаления! - " + _bus[b].name + " - " + x.Message);
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
            try {
                _dr.Navigate(_bus[b].kp);
                SetTitle(b);
                SetPrice(b);
                SetDesc(b);
                //проверка фото
                var photos = _dr.FindElements("//div[@id='images']/div/span");
                if (photos.Count != (_bus[b].images.Count > 10 ? 10 : _bus[b].images.Count)
                    && _bus[b].images.Count > 0) {
                    Log.Add("kupiprodai.ru: " + _bus[b].name + " - обновляю фотографии");
                    foreach (var photo in photos) {
                        photo.Click();
                        Thread.Sleep(1000);
                    }
                    SetImages(b);
                }
                PressOkButton();
            } catch (Exception x) {
                Log.Add("kupiprodai.ru: EditOffer - " + _bus[b].name + " - ошибка обновления! - " + x.Message);
            }
        }
        //пишу название
        void SetTitle(int b) {
            _dr.WriteToSelector("input[name*='title']", _bus[b].NameLength(80));
        }
        //пишу цену
        void SetPrice(int b) {
            _dr.WriteToSelector("input[name*='price']", _bus[b].price.ToString());
        }
        //пишу описание
        void SetDesc(int b) {
            _dr.WriteToSelector(".form_content_long2 textarea", sl: _bus[b].DescriptionList(2999, _addDesc));
        }
        //жму кнопку ок
        void PressOkButton() {
            do {
                _dr.ButtonClick("input[name='captcha']");
                while (_dr.GetElementsCount("input[name='captcha']") > 0 && _dr.GetElementAttribute("input[name='captcha']", "value").Length != 6)
                    Thread.Sleep(1000);
                _dr.ButtonClick("input[type='submit']");
            } while (_dr.GetElementsCount("input[name='captcha']") > 0);
        }
        //выкладываю объявления
        public async Task AddAsync() {
            var count = await _db.GetParamIntAsync("kupiprodai.addCount");
            for (int b = _bus.Count - 1; b > -1 && count > 0; b--) {
                if ((_bus[b].kp == null || !_bus[b].kp.Contains("http")) &&
                    !_bus[b].GroupName().Contains("ЧЕРНОВИК") &&
                    _bus[b].amount > 0 &&
                    _bus[b].price >= 0 &&
                    _bus[b].images.Count > 0) {
                    try {
                        await Task.Factory.StartNew(() => {
                            _dr.Navigate("https://vip.kupiprodai.ru/add/");
                            SetTitle(b);
                            SetCategory(b);
                            SetImages(b);
                            SetDesc(b);
                            SetPrice(b);
                            PressOkButton();
                        });
                        //сохраняем ссылку
                        await SaveUrlAsync(b);
                        count--;
                        Log.Add("kupiprodai.ru: " + _bus[b].name + " - объявление добавлено, осталось " + count);
                    } catch (Exception e) {
                        Log.Add("kupiprodai.ru: ошибка добавления! - " + _bus[b].name + " - " + e.Message);
                        break;
                    }
                }
            }
        }
        //сохраняю ссылки на объявление в карточку
        async Task SaveUrlAsync(int b) {
            var url = await Task.Factory.StartNew(() => {
                return _dr.GetElementAttribute(
                             "//a[contains(text(),'" + GetDryName(b) + "')]/../a[contains(@href,'delmsg')]",
                             "href")
                          .Replace("delmsg", "editmsg")
                          .TrimEnd('/');
            });
            if (url.Contains("editmsg")) {
                _bus[b].kp = url;
                await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>                                {
                                {"id", _bus[b].id},
                                {"name",_bus[b].name},
                                {_url, _bus[b].kp}
                            });
            } else
                throw new Exception("ссылка на объявление не найдена");
        }
        //очистка названия от апостофов
        string GetDryName(int b) {
            var name = _bus[b].name;
            while (name.EndsWith("'") || name.EndsWith("`")) {
                name = name.TrimEnd('\'').TrimEnd('`').TrimEnd(' ');
            }
            return name;
        }
        //выбор категории объявления
        void SetCategory(int b) {
            try {
                var name = _bus[b].name.ToLowerInvariant();
                var desc = _bus[b].description.ToLowerInvariant();
                _dr.FindElements("option[value='7_6']").First().Click();
                switch (_bus[b].GroupName()) {
                    case "Автохимия":
                        _dr.FindElements("option[value='622']").First().Click();
                        break;
                    case "Аудио-видеотехника":
                        _dr.FindElements("option[value='623']").First().Click();
                        break;
                    case "Шины, диски, колеса":
                        _dr.FindElements("option[value='629']").First().Click();
                        break;
                    default:
                        _dr.FindElements("option[value='619']").First().Click();
                        _dr.FindElements("option[value='632']").First().Click();
                        break;
                }
            } catch (Exception x) {
                Log.Add("kupiprodai: ошибка выбора категории - " + x.Message);
            }
        }
        //загрузка фотографий
        void SetImages(int b) {
            WebClient cl = new WebClient();
            cl.Encoding = Encoding.UTF8;
            var num = _bus[b].images.Count > 10 ? 10 : _bus[b].images.Count;
            for (int u = 0; u < num; u++) {
                try {
                    byte[] bts = cl.DownloadData(_bus[b].images[u].url);
                    File.WriteAllBytes("kp_" + u + ".jpg", bts);
                    Thread.Sleep(100);
                    _dr.SendKeysToSelector("//input[@type='file']", Application.StartupPath + "\\" + "kp_" + u + ".jpg ");
                } catch (Exception x) {
                    Log.Add("kupiprodai.ru: ошибка загрузки фото! - " + _bus[b].name + " - " + x.Message);
                    Thread.Sleep(3000);
                }
            }
            cl.Dispose();
        }
        //удаление тех, что нет на остатаках из папки неактивные
        async Task DelNoActiveAsync() {
            await Task.Factory.StartNew(() => {
                _dr.Navigate("https://vip.kupiprodai.ru/noactive/");
                foreach (var e in _dr.FindElements("input[name='chek[]']")) {
                    var id = e.GetAttribute("value");
                    var b = _bus.FindIndex(f => f.kp != null && f.kp.Contains(id));
                    if (b == -1 || _bus[b].amount <= 0) {
                        e.Click();
                        Thread.Sleep(1000);
                    }
                }
                _dr.ButtonClick("input[class='delete']");
            });
        }
        //парсинг объявлений
        async Task ParseAsync() {
            //парсинг случайных страниц
            var ElementString = _dr.GetElementText("//*[@id='tag_act']/*");
            var pageCountString = Regex.Match(ElementString, @"\d+").Groups[0].Value;
            var pageCount = string.IsNullOrEmpty(pageCountString) ? 0 : int.Parse(pageCountString) / 20;
            var checkPagesProcent = await _db.GetParamIntAsync("kupiprodai.checkPagesProcent");
            for (int i = 0; i < pageCount; i++) {
                //пропуск страниц
                if (_rnd.Next(100) >= checkPagesProcent)
                    continue;
                await ParsePage(i + 1);
            }
        }
        //парсинг отдельной страницы
        async Task ParsePage(int p) {
            try {
                await Task.Factory.StartNew(() => {
                    _dr.Navigate("https://vip.kupiprodai.ru/active/page" + p);
                    var items = _dr.FindElements("//div[@class='grd_act']/a[1]");
                    var names = items.Select(s => s.Text).ToList();
                    var urls = items.Select(s => s.GetAttribute("href")).ToList();
                    var ids = urls.Select(s => s.Split('_').Last()).ToList();
                    var prices = _dr.FindElements("//div[@class='price']").Select(s => s.Text).ToList();
                    if (items.Count != names.Count() ||
                        items.Count != ids.Count() ||
                        items.Count != urls.Count() ||
                        items.Count != prices.Count())
                        throw new Exception("kupiprodai.ru: ошибка парсинга! - количество элементов не совпадает!");
                    for (int i = 0; i < items.Count; i++) {
                        var b = _bus.FindIndex(f => f.kp.Contains(ids[i]));
                        if (b == -1) {
                            _dr.Navigate("https://vip.kupiprodai.ru/delmsg/" + ids[i]);
                        } else if (_bus[b].price.ToString() != prices[i].Split('р').First().Replace(" ", "")

                                        && DateTime.Now.Minute < 45 //ограничение периода

                        ||
                                  !_bus[b].name.Contains(names[i])) {
                            EditOffer(b);
                        }
                    }
                });
            } catch (Exception x) {
                Log.Add("kupiprodai.ru: ошибка парсинга страницы - " + x.Message);
            }
        }
        //проверка случайных ссылок
        async Task CheckUrls() {
            try {
                await Task.Factory.StartNew(() => {
                    var n = _db.GetParamInt("kupiprodai.checkUrlCount");
                    for (int i = 0; i < n;) {
                        var b = _rnd.Next(_bus.Count);
                        if (string.IsNullOrEmpty(_bus[b].kp))
                            continue;
                        _dr.Navigate(_bus[b].kp);
                        //проверка загрузки страницы
                        if (_dr.GetElementsCount("//input[@name='bbs_title']") == 0) {
                            Log.Add("kupiprodai.ru: ошибка загрузки объявления! - " + _bus[b].name);
                            continue;
                        }
                        //проверка названия и цены
                        var name = _dr.GetElementAttribute("//input[@name='bbs_title']", "value");
                        var price = int.Parse(_dr.GetElementAttribute("//input[@name='bbs_price']", "value"));
                        var photos = _dr.FindElements("//div[@id='images']/div/span");
                        if (name.Length <= 5 ||
                            !_bus[b].name.Contains(name) ||
                            _bus[b].price != price ||
                            photos.Count != (_bus[b].images.Count > 10 ? 10 : _bus[b].images.Count)) {
                            Log.Add("kupiprodai.ru: " + _bus[b].name + " - обновляю объявление");
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
