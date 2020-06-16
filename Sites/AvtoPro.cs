using OpenQA.Selenium;
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
        List<RootObject> _bus;
        private readonly string _url = "1437133"; //поле ссылки в карточке бизнес.ру 
        readonly string[] _addDesc = new[]
        {
            "Дополнительные фото по запросу. ",
            "Вы можете установить данную деталь на нашем АвтоСервисе с доп.гарантией и со скидкой! ",
            "Отправляем наложенным платежом ТК СДЭК (только негабаритные посылки) ",
            "Бесплатная доставка до транспортной компании!"
        };
        Selenium _dr;

        bool _needRestart = false;//TODO never used?

        string _part;

        Random rnd = new Random();

        public async Task AvtoProStartAsync(List<RootObject> bus, int addCount = 10, int chkCount = 10) {
            _bus = bus;
            await AuthAsync();
            await MassEditAsync();
            await AddAsync(addCount);
            //await CheckAsync(chkCount);
        }

        public void LoadCookies() {
            _dr.Navigate("https://avto.pro/warehouses/79489");
            _dr.LoadCookies("avtopro.json");
            Thread.Sleep(1000);
        }

        public void SaveCookies() {
            _dr.Navigate("https://avto.pro");
            _dr.SaveCookies("avtopro.json");
        }

        public void Quit() {
            _dr?.Quit();
            _dr = null;
        }

        public async Task AuthAsync() {
            await Task.Factory.StartNew(() => {
                if (_needRestart) Quit();
                if (_dr == null) {
                    _dr = new Selenium();
                    LoadCookies();
                    _dr.Refresh();
                }
                if (_dr.GetElementsCount("//a[@href='/warehouses/']") == 0) {//если нет ссылки на склады в левом меню
                    _dr.WriteToSelector("//input[@name='Login']", "9106027626@mail.ru"); //ввод логина
                    _dr.WriteToSelector("//input[@name='Password']", "33107173"); //пароля
                    _dr.ButtonClick("//button[contains(@class,'--submit')]"); //жмем кнопку входа
                    while (_dr.GetElementsCount("//a[@href='/warehouses/']") == 0) //если элементов слева нет ждем ручной вход
                        Thread.Sleep(60000);
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
                        Debug.WriteLine(x.Message);
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
            var auto = File.ReadAllLines(Application.StartupPath + "\\auto.txt");
            var dict = new Dictionary<string, int>();
            for (int i = 0; i < auto.Length; i++) {
                dict.Add(auto[i], 0);
                foreach (var word in auto[i].Split(';')) {
                    if (desc.Contains(word))
                        dict[auto[i]]++;
                }
            }
            var best = dict.OrderByDescending(o => o.Value).Where(w => w.Value >= 3).Select(s => s.Key).ToList();
            string manuf;
            if (best.Count > 0) {
                var s = best[0].Split(';');
                manuf = Regex.Replace(s[0], "[^0-9a-zA-Z]", "");
            } else manuf = "VAG";//если не определили - пишем VAG
            _dr.ButtonClick("//div[text()='Производитель']");
            _dr.SendKeysToSelector("//input[@class='pro-select__search']", manuf+OpenQA.Selenium.Keys.Enter);
        }

        async Task DeleteAsync(RootObject b)  {
            _part = b.avtopro.Split(';').First().Split('/').Last();//получаем номер запчасти из ссылки
            FindOffer();
            _dr.ButtonClick("//a[contains(@href,'delete')]");
            PressSubmitButton();
            b.avtopro = "";
            await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                {"id", b.id},
                {"name", b.name},
                {_url, b.avtopro}
            });
        }

        public async Task AddAsync(int count = 10) {
            for (int b = _bus.Count - 1; b > -1 && count > 0; b--) {
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
                        count--; //TODO сдэк сделать переменную из параметра публичным полем и добавить в обработчик контрола
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
            do {
                do {
                    FindOffer();
                } while (_dr.GetElementsCount("//td[@data-col='category.name']") != 1);
                _dr.ButtonClick("//td[@data-col='category.name']");
                while (_dr.GetElementsCount("//div[@class='pro-loader']") > 0) {Thread.Sleep(1000);}
            } while(_dr.GetElementText("//textarea[@name='Info']").Length == 0);
            b.avtopro = _dr.GetUrl();
            await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                {"id", b.id},
                {"name", b.name},
                {_url, b.avtopro}
            });
            await Task.Delay(2000);
        }

        private void FindOffer() {
            do {
                try {
                    _dr.Navigate("https://avto.pro/warehouses/79489/");
                    while (_dr.GetElementsCount("//div[@class='pro-loader']") > 0 ||
                        _dr.GetElementsCount("//div[@class='er_code']") > 0) {
                        _dr.Refresh();
                        Thread.Sleep(5000);
                    }
                    _dr.ButtonClick("//div[text()='Поиск']");
                    _dr.SendKeysToSelector("//input[@class='pro-select__search']", _part + OpenQA.Selenium.Keys.Enter);
                    while (_dr.GetElementsCount("//div[@class='pro-loader']") > 0) Thread.Sleep(5000);
                    break;
                } catch { }
            } while (true);
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
            _part = !string.IsNullOrEmpty(b.part) ? b.part : b.id;
            _part = Regex.Replace(_part, "[^0-9a-zA-Z]", "");
            FindOffer();
            if (_dr.GetElementsCount("//td[@data-col='category.name']") > 0) _part = b.id;
            _dr.ButtonClick("//a[@href='/warehouses/79489/add']");
            _dr.WriteToSelector("//input[@name='Code']", _part);
        }

        void PressSubmitButton() {
            _dr.ButtonClick("//div/div/button[@type='submit']");
        }

        void PressSaveButton() {
            while (_dr.GetElementsCount("//button[text()='Сохранить']")>0) {
                _dr.ButtonClick("//button[text()='Сохранить']");
                if (_dr.GetElementsCount("//span[contains(text(),'уже есть на складе')]") > 0) break;
                while (_dr.GetElementsCount("//div[@class='pro-loader']") > 0) Thread.Sleep(2000);
            }
        }

        void SetDesc(RootObject b) {
            _dr.WriteToSelector("//textarea[@name='Info']", sl: b.DescriptionList(999, _addDesc));
        }

        void SetPrice(RootObject b) {
            var desc = b.description.ToLowerInvariant();
            if (desc.Contains("залог:")) {
                var price = desc.Replace("залог:", "|").Split('|').Last().Trim().Replace("р", " ").Split(' ').First();
                _dr.WriteToSelector("//input[@name='Price']", price);
            } else
                _dr.WriteToSelector("//input[@name='Price']", b.price.ToString());
        }

        public async Task UpAsync() {
            try {
                await Task.Factory.StartNew(() => {
                    var pages = GetPagesCount("non_active");
                    for (int i = pages; i > 0; i--) {
                        _dr.Navigate("https://baza.drom.ru/personal/non_active/bulletins?page=" + i);
                        _dr.ButtonClick("//input[@id='selectAll']");
                        Thread.Sleep(2000);
                        if (_dr.GetElementsCount("//button[@value='prolongBulletin' and contains(@class,'button on')]") > 0) {
                            _dr.ButtonClick("//button[@value='prolongBulletin' and contains(@class,'button on')]");
                            Thread.Sleep(2000);
                            PressSubmitButton();
                            Thread.Sleep(10000);
                            _dr.Refresh();
                        } else
                            break;
                    }
                });
            } catch (Exception x) {
                Debug.WriteLine("DROM.RU: ОШИБКА МАССОВОГО ПОДЪЕМА\n" + x.Message + "\n" + x.InnerException.Message);
            }
        }

        private int GetPagesCount(string type) {
            try {
                //_dr.Navigate("https://baza.drom.ru/personal/all/bulletins");
                var count = _dr.GetElementsCount("//div/a[contains(@href,'" + type + "/bulletins')]");
                if (count > 0) {
                    var str = _dr.GetElementText("//div/a[contains(@href,'" + type + "/bulletins')]/../small");
                    var num = HttpUtility.HtmlDecode(str);
                    var i = int.Parse(num);
                    return (i / 50) + 1;
                }
            } catch { }
            return 1;
        }

        public async Task CheckAsync(int count = 10) {
            try {
                await Task.Factory.StartNew(() => {
                    //var pages = GetPagesCount("all");
                    for (int i = 0; i < count; i++) {
                        try {
                            //var drom = ParsePage(rnd.Next(1, pages));
                            //await CheckPageAsync(drom);
                        } catch {
                            i--;
                            _dr.Refresh();
                            Thread.Sleep(10000);
                        }
                    }
                });
            } catch (Exception x) {
                Debug.WriteLine("DROM.RU: ОШИБКА ПАРСИНГА\n" + x.Message + "\n" + x.InnerException.Message);
            }
        }

        async Task CheckPageAsync(List<RootObject> drom) {
            for (int i = 0; i < drom.Count; i++) {
                await CheckItemAsync(drom[i]);
            }
        }

        async Task CheckItemAsync(RootObject b) {
            var i = _bus.FindIndex(f => f.drom.Contains(b.id));
            if (i < 0 && !b.description.Contains("далено")) {
                _dr.Navigate(b.avtopro);
                //DeleteAsync();
            }
            if (i > -1 &&
                ((b.price != _bus[i].price && !_bus[i].description.Contains("Залог:")) ||
                !b.description.Contains("далено") && _bus[i].amount <= 0 ||
                (b.description.Contains("далено") /*|| item.description.Contains("старело")*/) && _bus[i].amount > 0
                )) {
                await EditAsync(i);
            }
        }

        private List<RootObject> ParsePage(int i) {
            var page = "https://avto.pro/warehouses/79489?page=" + i;
            _dr.Navigate(page);
            var avtopro = new List<RootObject>();
            foreach (var item in _dr.FindElements("//div[@class='bull-item-content__content-wrapper']")) {
                var el = item.FindElements(By.XPath(".//span[@data-role='price']"));
                var price = el.Count > 0 ? int.Parse(el.First().Text.Replace(" ", "").Replace("₽", "")) : 0;
                var name = item.FindElement(By.XPath(".//a[@data-role='bulletin-link']")).Text.Trim().Replace("\u00ad", "");
                var status = item.FindElement(By.XPath(".//div[contains(@class,'bulletin-additionals_right-column')]")).Text;
                var id = item.FindElement(By.XPath(".//a[@data-role='bulletin-link']")).GetAttribute("name");
                var url = item.FindElement(By.XPath(".//div/div/a")).GetAttribute("href");
                avtopro.Add(new RootObject {
                    name = name,
                    id = id,
                    price = price,
                    description = status,
                    avtopro = url
                });
            }
            return avtopro;
        }
    }
}
