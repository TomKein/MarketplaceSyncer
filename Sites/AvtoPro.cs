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
using System.Web;
using System.Windows.Forms;

namespace Selen.Sites {
    class AvtoPro {
        public int AddCount { get; set; }
        List<RootObject> _bus;
        private readonly string _url = "1437133"; //поле ссылки в карточке бизнес.ру 
        readonly string[] _addDesc = new[]
        {
            "Отправляем наложенным платежом ТК СДЭК, ПОЧТА, BOXBERRY только негабаритные посылки",
            "Наложенный платёж ТК ПЭК габаритные и тяжёлые детали",
            "Бесплатная доставка до ТК"
        };
        Selenium _dr;
        DB _db = DB._db;

        string _part;

        public async Task AvtoProStartAsync(List<RootObject> bus) {
            _bus = bus;
            await AuthAsync();
            await MassEditAsync();
            await AddAsync();
        }

        public void LoadCookies() {
            if (_dr != null) {
                _dr.Navigate("https://avto.pro/warehouses/79489");
                _dr.LoadCookies("avtopro.json");
                Thread.Sleep(1000);
            }
        }

        public void SaveCookies() {
            if (_dr != null) {
                _dr.Navigate("https://avto.pro");
                _dr.SaveCookies("avtopro.json");
            }
        }

        public void Quit() {
            _dr?.Quit();
            _dr = null;
        }

        public async Task AuthAsync() {
            await Task.Factory.StartNew(() => {
                if (_dr == null) {
                    _dr = new Selenium();
                    LoadCookies();
                }
                _dr.Refresh();
                if (_dr.GetElementsCount("//a[@href='/warehouses/']") == 0) {//если нет ссылки на склады в левом меню
                    _dr.WriteToSelector("//input[@name='Login']", "9106027626@mail.ru"); //ввод логина
                    _dr.WriteToSelector("//input[@name='Password']", "33107173"); //пароля
                    _dr.ButtonClick("//button[contains(@class,'--submit')]"); //жмем кнопку входа
                    for (int i = 0; _dr.GetElementsCount("//a[@href='/warehouses/']") == 0; i++) {//если элементов слева нет ждем ручной вход 10 мин
                        Thread.Sleep(60*1000);
                        if (i >= 10) throw new Exception("проблема авторизации, не могу попасть в личный кабинет!");
                    }
                    SaveCookies();
                }
            });
        }
        public async Task MassEditAsync() {
            for (int b = 0; b < _bus.Count; b++) {
                if (_bus[b].avtopro != null &&
                    _bus[b].avtopro.Contains("http") &&
                    (_bus[b].IsTimeUpDated() || _bus[b].amount <= 0)) {
                    try {
                        if (_bus[b].amount > 0) await EditAsync(b);
                        else await DeleteAsync(_bus[b]);
                    } catch (Exception x) {
                        if (x.Message.Contains("timed out") ||
                            x.Message.Contains("already closed") ||
                            x.Message.Contains("invalid session id") ||
                            x.Message.Contains("chrome not reachable")) throw;
                    }
                }
            }
        }

        async Task EditAsync(int b) {
            await Task.Factory.StartNew(() => {
                _dr.Navigate(_bus[b].avtopro);
                Thread.Sleep(3000);
                while (_dr.GetElementsCount("//div[@class='pro-loader']") > 0 || 
                        _dr.GetElementsCount("//div[@class='er_code']")>0) {
                    _dr.Refresh();
                    Thread.Sleep(10000);
                }
                SetDesc(_bus[b]);
                SetPrice(_bus[b]);
                SetCount(_bus[b]);
                PressSaveButton();
                Thread.Sleep(10000);
            });
        }

        void SetManufacturer(RootObject b) {
            var desc = b.name.ToLowerInvariant() + " " + b.description.ToLowerInvariant();
            var auto = File.ReadAllLines("..\\auto.txt")
                           .Where(w => w.Length > 0 && w.Contains(";")).ToList();
            var dict = new Dictionary<string, int>();
            for (int i = 0; i < auto.Count; i++) {
                dict.Add(auto[i], 0);
                foreach (var word in auto[i].Split(';')) {
                    if (desc.Contains(word))
                        dict[auto[i]]++;
                }
            }
            var best = dict.OrderByDescending(o => o.Value).Where(w => w.Value >= 1);
            string manuf;
            if (best.Count() > 0) {
                var s = best.First().Key.Split(';').First();
                manuf = Regex.Replace(s, "[^0-9a-zA-Z]", "");
                if (manuf.Length == 0) manuf = "VAG";
            } else manuf = "VAG";
            _dr.ButtonClick("//div[text()='Производитель']");
            _dr.SendKeysToSelector("//input[@class='pro-select__search']", manuf+OpenQA.Selenium.Keys.Enter);
        }

        async Task DeleteAsync(RootObject b)  {
            await Task.Factory.StartNew(() => {
                //_dr.Navigate(b.avtopro.Replace("edit", "delete"));

                _part = b.avtopro.Split(';').First().Split('/').Last(); //получаем номер запчасти из ссылки
                FindOffer();                                            //поиск объявления
                _dr.SendKeysToSelector("//body", OpenQA.Selenium.Keys.Escape);
                _dr.ButtonClick("//a[contains(@href,'delete')]");
                PressSubmitButton();
                b.avtopro = "";
            });
            await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                {"id", b.id},
                {"name", b.name},
                {_url, b.avtopro}
            });
        }

        public async Task AddAsync() {
            AddCount = await _db.GetParamIntAsync("autoPro.addCount");
            for (int b = _bus.Count - 1; b > -1 && AddCount > 0; b--) {
                if ((_bus[b].avtopro == null || !_bus[b].avtopro.Contains("http")) &&
                    _bus[b].tiu.Contains("http") &&
                    _bus[b].amount > 0 &&
                    _bus[b].price > 0 &&
                    _bus[b].images.Count > 0 &&
                    _bus[b].GroupName() != "Автохимия" &&
                    _bus[b].GroupName() != "Инструменты") { 
                    var t = Task.Factory.StartNew(() => {
                        SetPart(_bus[b]);
                        SetManufacturer(_bus[b]);
                        SetDesc(_bus[b]);
                        SetPrice(_bus[b]);
                        SetCount(_bus[b]);
                        SetOptions(_bus[b]);
                        PressSaveButton();
                    });
                    try {
                        await t;
                        await SaveUrlAsync(_bus[b]);
                        await Task.Factory.StartNew(() => {
                            SetImages(_bus[b]);
                            PressSaveButton();
                        });
                        AddCount--;
                    } catch (Exception x) {
                        Debug.WriteLine("AVTO.PRO: ОШИБКА ДОБАВЛЕНИЯ!\n" + _bus[b].name + "\n" + x.Message);
                        break;
                    }
                }
            }
        }

        private void SetCount(RootObject b) {
            _dr.WriteToSelector("//input[@name='Quantity']", b.amount.ToString("F0"));
        }

        async Task SaveUrlAsync(RootObject b) {
            await Task.Factory.StartNew(() => {
                for (int j = 1; ; j++) {
                    for (int i = 1; ; i++) {
                        Log.Add("avto.pro: поиск объявления для привязки (" + i + ")...");
                        FindOffer();
                        var elementsCount = _dr.GetElementsCount("//td[@data-col='category.name']");
                        Log.Add("avto.pro: найдено " + i);
                        if (elementsCount == 1) break;
                        if (i >= 100) throw new Exception("не могу найти объявление для привязки! - ссылка не была сохранена!");
                    };
                    _dr.ButtonClick("//td[@data-col='category.name']");
                    for (int i = 1; _dr.GetElementsCount("//div[@class='pro-loader']") > 0; i++) {
                        Log.Add("avto.pro: ожидаю загрузку страницы ("+i+")...");
                        if (i >= 100) throw new Exception("ошибка ожидания загрузки страницы - ссылка не была сохранена!");
                        Thread.Sleep(1000);
                    }
                    var elementText = _dr.GetElementText("//textarea[@name='Info']");
                    if (!string.IsNullOrEmpty(elementText)) break;
                    if (j >= 100) throw new Exception("ошибка проверки текста формы - ссылка не была сохранена!");
                }
                b.avtopro = _dr.GetUrl();
            });
            await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                {"id", b.id},
                {"name", b.name},
                {_url, b.avtopro}
            });
            Log.Add("avto.pro: ссылка на объявление успешно сохранена (" + b.avtopro + ")");
            await Task.Delay(2000);
        }

        private void FindOffer() {
            for (int j = 1; ; j++) {
                try {
                    Log.Add("avto.pro: поиск номера " + _part + " ("+j+")");
                    _dr.Navigate("https://avto.pro/warehouses/79489/");
                    for (int i = 1; 
                        _dr.GetElementsCount("//div[@class='pro-loader']") > 0 ||
                        _dr.GetElementsCount("//div[@class='er_code']") > 0; i++) {
                        if (i >= 10) throw new Exception("timed out - не удается загрузить страницу склада");
                            Log.Add("avto.pro: обновляю страницу склада (" + i + ")");
                            _dr.Refresh();
                            Thread.Sleep(20000);
                    }
                    _dr.ButtonClick("//div[text()='Поиск']");
                    _dr.SendKeysToSelector("//input[@class='pro-select__search']", _part + OpenQA.Selenium.Keys.Enter);
                    for (int i = 1; _dr.GetElementsCount("//div[@class='pro-loader']") > 0; i++) {
                        if (i >= 50) throw new Exception("timed out - не удается дождаться поиска "+ _part);
                        Log.Add("avto.pro: ожидаю загрузку страницы (" + i + ")");
                        Thread.Sleep(1000);
                    }
                    break;
                } catch (Exception x) {
                    if (x.Message.Contains("timed out") ||
                        x.Message.Contains("already closed") ||
                        x.Message.Contains("invalid session id") ||
                        x.Message.Contains("chrome not reachable")) throw;
                }
                if (j == 10) {
                    throw new Exception("timed out - не удается выполнить поиск номера");
                }
            };
        }

        void SetOptions(RootObject b) {
            if (!b.IsNew()) _dr.ButtonClick("//input[@name='IsUsed']/../.."); //новый или б/у
        }

        void SetImages(RootObject b) {
            WebClient cl = new WebClient();
            cl.Encoding = Encoding.UTF8;
            try {
                var num = b.images.Count > 10 ? 10 : b.images.Count;

                for (int u = 0; u < num; u++) {
                    bool flag; // флаг для проверки успешности загрузки фото
                    int tryNum = 0;
                    do {
                        tryNum++;
                        try {
                            byte[] bts = cl.DownloadData(b.images[u].url);
                            File.WriteAllBytes("avtopro_" + u + ".jpg", bts);
                            flag = true;
                        } catch (Exception ex) {
                            flag = false;
                            Thread.Sleep(20000);
                        }
                        Thread.Sleep(1000);
                    } while (!flag && tryNum < 5);

                    if (flag) {
                        do {
                            _dr.SendKeysToSelector("//input[@type='file']", Application.StartupPath + "\\" + "avtopro_" + u + ".jpg");

                        } while (false);//TODO add control photo load
                    }
                }
            } catch (Exception e) {
                Debug.WriteLine("AVTO.PRO: ОШИБКА ЗАГРУЗКИ ФОТО!\n" + e.Message);
            }
            cl.Dispose();
            Thread.Sleep(5000);
        }

        void SetPart(RootObject b) {
            _part = !string.IsNullOrEmpty(b.part) ? b.part.Split(',').First() : b.id;
            _part = Regex.Replace(_part, "[^0-9a-zA-Z]", "");
            FindOffer();
            if (_dr.GetElementsCount("//td[@data-col='category.name']") > 0) _part = b.id;
            _dr.ButtonClick("//a[@href='/warehouses/79489/add']");
            _dr.WriteToSelector("//input[@name='Code']", _part);
        }

        void PressSubmitButton() {
            _dr.ButtonClick("//div/div/button[@type='submit']");
            Thread.Sleep(3000);
        }

        void PressSaveButton() {
            for (int j = 1; _dr.GetElementsCount("//button[text()='Сохранить']") > 0; j++) {
                if (j >= 100) throw new Exception("кнопка Сохранить не срабатывает!");
                Log.Add("avto.pro: жму кнопку сохранить (" + j + ")...");
                _dr.ButtonClick("//button[text()='Сохранить']");
                if (_dr.GetElementsCount("//span[contains(text(),'уже есть на складе')]") > 0)
                    throw new Exception("не срабатывает кнопка Сохраненить - данный товар уже есть на складе!");
                for (int i = 0; _dr.GetElementsCount("//div[@class='pro-loader']") > 0; i++) {
                    if (i >= 100) throw new Exception("ошибка ожидания загрузки страницы после нажатия Сохранить!");
                    Log.Add("avto.pro: ожидаю загрузку страницы (" + i + ")...");
                    Thread.Sleep(1000);
                }
            }
        }

        void SetDesc(RootObject b) {
            _dr.WriteToSelector("//textarea[@name='Info']", sl: b.DescriptionList(255, _addDesc, removeSpec: true));
        }

        void SetPrice(RootObject b) {
            var desc = b.description.ToLowerInvariant();
            if (desc.Contains("залог:")) {
                var price = desc.Replace("залог:", "|").Split('|').Last().Trim().Replace("р", " ").Split(' ').First();
                _dr.WriteToSelector("//input[@name='Price']", price);
            } else
                _dr.WriteToSelector("//input[@name='Price']", b.price.ToString());
        }

        public async Task CheckAsync() {
            await Task.Factory.StartNew(() => {
                //меняю формат выдачи - по 1000 шт
                Log.Add("avto.pro: загружаю склад...");
                _dr.Navigate("https://avto.pro/warehouses/79489");
                for(int i=0; _dr.GetElementsCount("//div[contains(text(),'Выводить')]") == 0; i++) {
                    if (i >= 100) throw new Exception("не могу дождаться элемент Выводить");
                    Thread.Sleep(1000);
                }
                _dr.ButtonClick("//div[contains(text(),'Выводить')]");
                _dr.ButtonClick("//div[@class='pro-select__option' and contains(text(),'1000')]");
                //ожидаю, пока прогрузится список (проверяю наличие шестеренки загрузки)
                for (int i = 0; _dr.GetElementsCount("//span[@class='pro-form__frame__preloader']") > 0; i++) {
                    if(i>=100) throw new Exception("не могу дождаться элемент Выводить");
                    Thread.Sleep(1000);
                }
                //страховка от зависания цикла разворачивания списка, когда кнопка "показать еще" активна, но ничего не добавляет
                //обчно такая кнопка становится неактивной, если список развернут полностью, но на авто.про случается и не такое
                //запаса в 2 раза более чем достаточно
                var maxNum = _bus.Count(b => b.avtopro.Contains("http"))/500;
                //раскрываем список полностью
                while (_dr.GetElementsCount("//button[contains(@class,'show')]") > 0 && maxNum-- > 0) {
                    //нажимаю кнопку "показать еще"
                    _dr.ButtonClick("//button[contains(@class,'show')]");
                    //пока кнопка неактивна - ожидаю
                    for (int i = 0; _dr.GetElementsCount("//button[contains(@class,'show') and  @disabled='']") > 0; i++) {
                        if(i>=100) throw new Exception("не могу дождаться кнопку Показать еще!");
                        Thread.Sleep(1000);
                    }
                }
                //получаю товары сайта
                var offerList = _dr.FindElements("//tr/td[contains(@data-col,'code')]/..").ToList();
                var indexList = new List<int>();
                foreach (var item in offerList) {
                    //кнопка удаления объявления
                    var deleteButton = item.FindElement(By.XPath("./td/a"));
                    //получаю ссылку на объявление
                    var url = deleteButton.GetAttribute("href").Replace("delete", "edit");
                    //получаю цену
                    var price = item.FindElement(By.XPath("./td[contains(@data-col,'priceoriginal')]//b")).Text.Split('.').First().Replace(" ", "");
                    //проверяю наличие фото
                    var photo = item.FindElements(By.XPath(".//img"));
                    //ищу в базе карточку
                    var b = _bus.FindIndex(f => f.avtopro.Contains(url));
                    if (b >= 0 && !indexList.Contains(b)) indexList.Add(b); //если индекс найден и не содержится в списке найденных - добавляю
                    if (b < 0 ||                                            //если индекс не найден или
                        (price.Length == 0 || _bus[b].price != int.Parse(price)) ||                //не совпадает цена или
                        _bus[b].amount <= 0 ||                              //нет на остатках или
                        (photo.Count == 0 && _bus[b].images.Count > 0)) {   //нет фото, хотя в карточке есть - удаляю объявление
                        Actions a = new Actions(_dr._drv);
                        a.MoveToElement(deleteButton);
                        deleteButton.Click();
                        Thread.Sleep(3000);
                        PressSubmitButton();
                        for (int i = 0; _dr.GetElementsCount("//div/div/button[@type='submit']") > 0; i++) {
                            if (i>=100) throw new Exception("не могу дождаться закрытия диалога после нажатия Удалить!");
                            Thread.Sleep(1000);
                        }

                        if (b >= 0) {                                       //если была ссылка в карточке - тоже удаляю
                            _bus[b].avtopro = "";
                            Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                                {"id", _bus[b].id},
                                {"name", _bus[b].name},
                                {_url, _bus[b].avtopro}
                            });
                        }
                    }
                }
                //список карточек к базе, имеющих ссылку на авто.про и не найденных на сайте
                var busList = _bus.Where(b => b.avtopro.Contains("http") && !indexList.Contains(_bus.IndexOf(b))).ToList();
                if (busList.Count > 100) throw new Exception("слишком много расхождений ("+ busList.Count + "), возможно проблема парсинга!");
                foreach (var bus in busList) {
                    bus.avtopro = "";
                    Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                                {"id", bus.id},
                                {"name", bus.name},
                                {_url, bus.avtopro}
                            });
                    Thread.Sleep(10000);
                }
            });
        }
    }
}
