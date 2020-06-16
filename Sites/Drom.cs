//изменен метод SetPrice для товаров с залоговой стоимостью

using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
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
    class Drom {
        readonly string _url = "209334"; //поле ссылки в карточке бизнес.ру
        readonly string[] _addDesc = new[]
        {
            "Дополнительные фотографии по запросу",
            "Вы можете установить данную деталь в нашем АвтоСервисе с доп.гарантией и со скидкой!",
            "Перед выездом обязательно уточняйте наличие запчастей (кнопка Спросить) - запчасти на складе!",
            "Отправляем наложенным платежом ТК СДЭК (только негабаритные посылки)"
        };
        Selenium _dr;
        List<RootObject> _bus;
        string _dromUrlStart = "https://baza.drom.ru/bulletin/";
        bool _needRestart = false;
        Random rnd = new Random();

        public void LoadCookies() {
            _dr.Navigate("https://baza.drom.ru/kaluzhskaya-obl/");
            _dr.LoadCookies("drom.json");
            Thread.Sleep(1000);
        }

        public void SaveCookies() {
            _dr.Navigate("https://baza.drom.ru/kaluzhskaya-obl/");
            _dr.SaveCookies("drom.json");
        }

        public void Quit() {
            _dr?.Quit();
            _dr = null;
        }
        public async Task DromStartAsync(List<RootObject> bus, int addCount = 10, int chkCount = 10) {
            _bus = bus;
            await AuthAsync();
            await UpAsync();
            await EditAsync();
            await AddAsync(addCount);
            await CheckAsync(chkCount);
        }
        public async Task AuthAsync() {
            await Task.Factory.StartNew(() => {
                if (_needRestart) Quit();
                if (_dr == null) {
                    _dr = new Selenium();
                    LoadCookies();
                }
                _dr.Navigate("http://baza.drom.ru/personal/all/bulletins");
                if (_dr.GetElementsCount("//div[@class='personal-box']") == 0) {//если элементов в левой панели нет
                    _dr.WriteToSelector("#sign", "rad.i.g@list.ru"); //ввод логина
                    _dr.WriteToSelector("#password", "rad00239000"); //пароля
                    _dr.ButtonClick("#signbutton"); //жмем кнопку входа
                    while (_dr.GetElementsCount("//div[@class='personal-box']") == 0) //если элементов слева нет ждем ручной вход
                        Thread.Sleep(30000);
                    SaveCookies();
                }
            });
        }
        public async Task EditAsync() {
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
            _dr.Navigate(b.drom);
            SetTitle(b);
            SetDesc(b);
            SetPrice(b);
            SetPart(b);
            PressOkButton();
            Thread.Sleep(10000);
            if (b.amount <= 0) Delete();
            else Up();
        }

        void SetTitle(RootObject b) {
            _dr.WriteToSelector("//input[@name='subject']", b.name);
        }

        void Delete() {
            _dr.ButtonClick("//a[@class='doDelete']");
            PressServiseSubmitButton();
        }

        public async Task AddAsync(int count = 10) {
            for (int b = 0; b < _bus.Count && count > 0; b++) {
                if ((_bus[b].drom == null || !_bus[b].drom.Contains("http")) &&
                    _bus[b].tiu.Contains("http") &&
                    _bus[b].amount > 0 &&
                    _bus[b].price > 0 &&
                    _bus[b].images.Count > 0) {
                    var t = Task.Factory.StartNew(() => {
                        _dr.Navigate("http://baza.drom.ru/set/city/370?return=http%3A%2F%2Fbaza.drom.ru%2Fadding%3Fcity%3D370");
                        if (_dr.GetElementsCount("//div[@class='image-wrapper']/img") > 0) throw new Exception("Черновик уже заполнен!");//если уже есть блок фотографий на странице, то черновик уже заполнен, но не опубликован по какой-то причине, например, номер запчасти похож на телефонный номер - объявление не опубликовано, либо превышен дневной лимит подачи
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
                        PressPublicFreeButton();
                    });
                    try {
                        await t;
                        await SaveUrlAsync(b);
                        count--;
                    } catch (Exception x) {
                        Debug.WriteLine("DROM.RU: ОШИБКА ДОБАВЛЕНИЯ!\n" + _bus[b].name + "\n" + x.Message);
                        break;
                    }
                }
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
            await Task.Delay(2000);
        }
        void SetAudioSize(RootObject b) {
            if (b.GroupName() == "Аудио-видеотехника") {
                var d = b.description.ToLowerInvariant();
                if (d.Contains("din")) {
                    var s = b.description.ToLowerInvariant().Replace(" din","din").Replace("din ", "din").Split(' ').First(f => f.Contains("din"));
                    if (s.Contains("2")) _dr.ButtonClick("//label[contains(text(),'2 DIN')]");
                    else if (s.Contains("5")) _dr.ButtonClick("//label[contains(text(),'1,5 DIN')]");
                    else _dr.ButtonClick("//label[contains(text(),'1 DIN')]");
                }
                if (_dr.GetElementsCount("//div[@data-name='model']/div[contains(@class,'annotation') and contains(@style,'none')]") == 0) {
                    _dr.WriteToSelector("//div[@data-name='model']//input[@data-role='name-input']", "штатная");
                }
            }
        }
        void SetDiam(RootObject b) {
            if (b.GroupName() == "Шины, диски, колеса") {
                _dr.ButtonClick("//div[@data-name='wheelDiameter']//div[contains(@class,'chosen-container-single')]");
                _dr.WriteToSelector("//div[@data-name='wheelDiameter']//input", b.GetDiskSize() + OpenQA.Selenium.Keys.Enter);
                _dr.WriteToSelector("//input[@name='quantity']", "1");
                _dr.WriteToSelector("//div[@data-name='model']//input[@data-role='name-input']", b.DiskType());
            }
        }
        void SetOptions(RootObject b) {
            if (b.IsNew()) _dr.ButtonClick("//label[text()='Новый']"); //новый или б/у
            else _dr.ButtonClick("//label[text()='Б/у']");

            if (b.IsOrigin()) _dr.ButtonClick("//label[text()='Оригинал']"); //аналог или оригинал
            else _dr.ButtonClick("//label[text()='Аналог']");

            _dr.ButtonClick("//label[text()='В наличии']"); //кнопка в наличии
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
                    } catch {}
                }
            }
            cl.Dispose();
        }
        void SetPart(RootObject b) {
            _dr.WriteToSelector("//input[@name='autoPartsOemNumber']", b.part);
            if (_dr.GetElementsCount("//div[contains(@class,'error_annotation')]") > 0) {
                _dr.WriteToSelector("//input[@name='autoPartsOemNumber']", "");
            }
        }
        void PressOkButton() {
            _dr.ButtonClick("//button[contains(@class,'submit__button')]");
        }
        void PressServiseSubmitButton() {
            _dr.ButtonClick("//*[@id='serviceSubmit']");
        }
        void PressPublicFreeButton() {
            _dr.ButtonClick("//button[@id='bulletin_publication_free']");
        }
        void SetDesc(RootObject b) {
            _dr.WriteToSelector("//textarea[@name='text']", sl: b.DescriptionList(2999, _addDesc));
        }
        void SetPrice(RootObject b) {
            var desc = b.description.ToLowerInvariant();
            if (desc.Contains("залог:")) { 
                var price = desc.Replace("залог:", "|").Split('|').Last().Trim().Replace("р", " ").Split(' ').First();
                _dr.WriteToSelector("//input[@name='price']", price);
            }
            else
                _dr.WriteToSelector("//input[@name='price']", b.price.ToString());
        }
        void Up() {
            if (_dr.GetElementsCount("//a[@class='doDelete']") == 0) { //Удалить объявление - если нет такой кнопки, значит удалено и надо восстановить
                _dr.ButtonClick("//a[@class='doProlong']");
                _dr.ButtonClick("//a[@data-applier='recoverBulletin']");
                PressServiseSubmitButton();
                _dr.ButtonClick("//a[@data-applier='prolongBulletin']");
                PressServiseSubmitButton();
            }
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
                            PressServiseSubmitButton();
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
                _dr.Navigate("https://baza.drom.ru/personal/all/bulletins");
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
                    var pages = GetPagesCount("all");
                    for (int i = 0; i < count; i++) {
                        try {
                            var drom = ParsePage(rnd.Next(1, pages));
                            CheckPage(drom);
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
            } if (i > -1 &&
                  ((item.price != _bus[i].price && !_bus[i].description.Contains("Залог:")) ||
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
                var status = item.FindElement(By.XPath(".//div[contains(@class,'bulletin-additionals_right-column')]")).Text;
                var id = item.FindElement(By.XPath(".//a[@data-role='bulletin-link']")).GetAttribute("name");
                var url = item.FindElement(By.XPath(".//div/div/a")).GetAttribute("href");
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
    }
}