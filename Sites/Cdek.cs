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
using System.Windows.Forms;

namespace Selen.Sites {
    class Cdek {
        readonly string _url = "854874";
        readonly string[] _addDesc = new[]
        { "Есть и другие запчасти на данный автомобиль!",
          "Вы можете установить данную деталь на нашем АвтоСервисе с доп.гарантией и со скидкой!" ,
          "При самовывозе обязательно уточняйте наличие запчастей в магазине - часть товара на складе!",
          "ВНИМАНИЕ! Cтоимость доставки при оформлении заказа не окончательная,",
          "т.к. характеристики товара для расчета указаны приблизительно!",
          "Для уточнения стоимости обращайтесь к нашим менеджерам перед оплатой!",
          "Отправляем наложенным платежом (только негабаритные посылки)",
          "Бесплатная доставка до транспортной компании!"
        };
        Selenium _dr;
        List<RootObject> _bus = null;

        public void LoadCookies() {
            _dr.Navigate("https://cdek.market/v/");
            _dr.LoadCookies("cdek.json");
            Thread.Sleep(1000);
        }

        public void SaveCookies() {
            _dr.Navigate("https://cdek.market/v/?dispatch=products.manage");
            _dr.SaveCookies("cdek.json");
        }


        public async Task SyncCdekAsync(List<RootObject> bus, int addCount = 10) {
            _bus = bus;
            await Autorize_async();
            await EditAsync();
            await AddAsync(addCount);
            await DeleteNonActiveAsync();
        }

        private async Task DeleteNonActiveAsync() {
            await Task.Factory.StartNew(() => {
                _dr.Navigate("https://cdek.market/v/?type=simple&pcode_from_q=Y&is_search=Y&category_name=%D0%92%D1%81%D0%B5+%D0%BA%D0%B0%D1%82%D0%B5%D0%B3%D0%BE%D1%80%D0%B8%D0%B8&subcats=N&subcats=Y&status=D&period=A&hint_new_view=%D0%9D%D0%B0%D0%B7%D0%B2%D0%B0%D0%BD%D0%B8%D0%B5&dispatch%5Bproducts.manage%5D=%D0%9D%D0%B0%D0%B9%D1%82%D0%B8");
                _dr.ButtonClick("//input[@name='check_all']");
                _dr.ButtonClick("//span[contains(text(),'Действия')]");
                _dr.ButtonClick("//a[contains(text(),'Удалить выбранные')]");
            });
        }

        private async Task AddAsync(int addCount) {
            int count = 0;
            for (int b = _bus.Count - 1; b > -1 && count < addCount; b--) {
                if ((_bus[b].cdek == null || !_bus[b].cdek.Contains("http")) &&
                    _bus[b].tiu.Contains("http") &&
                    _bus[b].amount > 0 &&
                    _bus[b].price > 0 &&
                    _bus[b].images.Count > 0) {
                    var t = Task.Factory.StartNew(() => {
                        _dr.Navigate("https://cdek.market/v/?dispatch=products.add");
                        _dr.Refresh();
                        SetTitle(b);
                        SetCategory(b);
                        SetDesc(b);
                        SetPrice(b);
                        SetImages(b);
                        SetAmount(b);
                        SetOptions(b);
                        SetTimeToReturn();
                        PressOkButton();
                        SetWeight(b);
                    });
                    try {
                        await t;
                        //сохраняем ссылку
                        var id = _dr.GetUrl();
                        if (id.Contains("product_id")) {
                            id = id.Replace("product_id=","|").Split('|').Last().Split('&').First();
                            var url = "https://cdek.market/v/?dispatch=products.update&product_id=" + id;
                            _bus[b].cdek = url;
                            await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>
                            {
                                    {"id", _bus[b].id},
                                    {"name", _bus[b].name},
                                    {_url, _bus[b].cdek}
                            });
                        }
                        count++;
                        Thread.Sleep(2000);
                    }
                    catch (Exception x) {
                        Debug.WriteLine("CDEK ошибка добавления!\n" + _bus[b].name + "\n" + x.Message);
                        if (x.Message.Contains("timed out")) {
                            Quit();
                            await Autorize_async();
                        }
                    }
                }
            }
        }
        private void SetTimeToReturn() {
            //прокручиваем страницу вверх
            _dr.WriteToSelector("#elm_product_code", OpenQA.Selenium.Keys.PageUp + OpenQA.Selenium.Keys.PageUp);
            _dr.ButtonClick("#addons");
            _dr.WriteToSelector("#return_period", "14");
            //PressOkButton();
        }
        private void SetWeight(int b) {
            if (_bus[b].weight != null && _bus[b].weight > 0) {
                _dr.ButtonClick("#shippings");
                _dr.WriteToSelector("#product_weight", _bus[b].weight.ToString());
                PressOkButton();
            }
        }
        private void SetOptions(int b) {
            //не отслеживать остатки
            _dr.WriteToSelector("#elm_product_tracking",OpenQA.Selenium.Keys.PageDown+OpenQA.Selenium.Keys.Enter);
            //_cdek.WriteToSelector("//option[text()='Не отслеживать']");
        }
        private void SetImages(int b) {
            WebClient cl = new WebClient {
                Encoding = Encoding.UTF8
            };
            try {
                var num = _bus[b].images.Count > 20 ? 20 : _bus[b].images.Count;

                for (int u = 0; u < num; u++) {
                    bool flag; // флаг для проверки успешности
                    int tryNum = 0;
                    do {
                        tryNum++;
                        try {
                            byte[] bts = cl.DownloadData(_bus[b].images[u].url);
                            File.WriteAllBytes("cdek_" + u + ".jpg", bts);
                            flag = true;
                        }
                        catch (Exception ex) {
                            flag = false;
                            Thread.Sleep(5000);
                        }
                        Thread.Sleep(1000);
                    } while (!flag && tryNum < 5);

                    if (flag) _dr.SendKeysToSelector("input[type*='file'][class*='input']", Application.StartupPath + "\\" + "cdek_" + u + ".jpg");
                }
            }
            catch (Exception e) {
                Debug.WriteLine("SetCdekImages: ошибка \n" + e.Message);
            }
            cl.Dispose();
            Thread.Sleep(5000);
        }
        private async Task EditAsync() {
            for (int b = 0; b < _bus.Count; b++) {
                if (!string.IsNullOrEmpty(_bus[b].cdek) && _bus[b].cdek.Contains("http") && 
                    (_bus[b].IsTimeUpDated() || _bus[b].amount <= 0)) {
                    var t = Task.Factory.StartNew(() => {
                        _dr.Navigate(_bus[b].cdek);
                        SetTitle(b);
                        SetPrice(b);
                        SetCategory(b);
                        SetDesc(b);
                        SetStatus(b);
                        SetAmount(b);
                        PressOkButton();
                        SetWeight(b);
                    });
                    try {
                        await t;
                        if (_bus[b].amount <= 0) await DeleteUrl(b);
                    }
                    catch(Exception x) {
                        Debug.WriteLine(x.Message);
                        if (x.Message.Contains("timed out")) {
                            Quit();
                            await Autorize_async();
                        }
                    }
                }
            }
        }

        private async Task DeleteUrl(int b) {
            _bus[b].cdek = "";
            await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                                {"id", _bus[b].id},
                                {"name", _bus[b].name},
                                {_url, _bus[b].cdek}
            });
        }

        private void SetStatus(int b) {
            if (_bus[b].amount <= 0) {
                _dr.ButtonClick("//input[@id='elm_product_status_0_d']");
            }
        }

        private void SetAmount(int b) {
            _dr.WriteToSelector("#elm_in_stock", _bus[b].amount.ToString());
        }

        void PressOkButton() {
            _dr.SendKeysToSelector("//body",OpenQA.Selenium.Keys.Home);
            _dr.ButtonClick(".cm-product-save-buttons");
        }

        void SetDesc(int b) {
            _dr.SwitchToFrame("iframe[title*='product_full_desc']");
            _dr.WriteToSelector(".cke_editable", sl: _bus[b].DescriptionList(2999,_addDesc));
            _dr.SwitchToMainFrame();
        }

        private void SetPrice(int b) {
            _dr.WriteToSelector("#elm_price_price", _bus[b].price.ToString());
        }

        private void SetTitle(int b) {
            _dr.WriteToSelector("#product_description_product", _bus[b].NameLength(130));
        }

        private async Task Autorize_async() {
            try {
                await Task.Factory.StartNew(() => {
                    if (_dr == null) {
                        _dr = new Selenium();
                        LoadCookies();
                        _dr.Refresh();
                    }
                    _dr.Navigate("https://cdek.market/v/");
                    if (_dr.GetElementsCount("#mainrightnavbar") == 0) {
                        _dr.WriteToSelector("#username", "rad.i.g@list.ru");
                        _dr.WriteToSelector("#password", "rad701242");
                        _dr.ButtonClick("input[type='submit']");
                    }
                    while (_dr.GetElementsCount("//*[@class='admin-content']") == 0) //если элементов слева нет ждем ручной вход
                        Thread.Sleep(60000);
                    SaveCookies();
                });
            } catch (Exception x) {
                if (x.Message.Contains("timed out")) throw x; //сайт не отвечает - кидаю исключение дальше и завершаю текущий цикл
                if (x.Message.Contains("invalid session")) { //перезапуск браузера до победного
                    Quit();
                    await Autorize_async();
                }
            }
        }

        void SetCategory(int b) {
            var cat = GetCategory(b);
            Thread.Sleep(1000);
            for (int i = 0; ; i++) {
                _dr.ButtonClick(".select2-selection__choice__remove");
                _dr.WriteToSelector("input[class*='select2-search__field']", cat);
                Thread.Sleep(1000);
                _dr.ButtonClick("li[class*='select2-results__option--highlighted']");
                Thread.Sleep(500);
                var el = _dr.GetElementText("li.select2-selection__choice");
                if (el.Contains(cat)) break;
                if (i == 20) throw new Exception("ошибка выбора категории товара!");
            }
        }

        public async Task<int> CheckUrlsAsync(int num, int count = 10) {
            var newB = num + count;
            for (int b = num; b < _bus.Count && b < newB; b++) {
                if (!string.IsNullOrEmpty(_bus[b].cdek) && _bus[b].cdek.Contains("http") && await IsUrlDead(b)) {
                    await DeleteUrl(b);
                }
            }
            if (newB < _bus.Count) return newB;
            return 0;
        }

        public async Task<bool> IsUrlDead(int b) {
            var isDead = false;
            await Task.Factory.StartNew(() => {
                _dr.Navigate(_bus[b].cdek);
                Thread.Sleep(5000);
                isDead = _dr.GetElementsCount("//label[@for='product_description_product']") == 0 &&
                          _dr.GetElementText("h2").StartsWith("40");
                if (!isDead && IsImageDead()) {
                    _dr.ButtonClick("//input[@id='elm_product_status_0_d']");
                    PressOkButton();
                    isDead = true;
                }else if (!isDead && IsCategoryDead()) {
                    SetTitle(b);
                    SetPrice(b);
                    SetCategory(b);
                    SetDesc(b);
                    SetStatus(b);
                    SetAmount(b);
                    PressOkButton();
                }
            });
            return isDead;
        }

        private bool IsCategoryDead() {
            return _dr.GetElementsCount("//div[@class='text-error controls']") > 0;
        }

        public bool IsImageDead() {
            return _dr.GetElementsCount("//img[contains(@src,'http')]") == 0;
        }

        string GetCategory(int b) {
            switch (_bus[b].GroupName()) {
                case "Двигатели":
                    return "Запчасти для КПП";
                case "Детали двигателей":
                    return "Головки блока цилиндров двигателя";
                case "Генераторы":
                    return "Генераторы для спецтехники";
                case "Стартеры":
                    return "Стартеры для спецтехники";
                case "Автохимия":
                    return "Присадки и промывки";
                case "Аудио-видеотехника":
                    if (_bus[b].name.Contains("Магнитол")) return "Автомагнитолы";
                    if (_bus[b].name.Contains("Динамик")) return "Колонки для авто";
                    if (_bus[b].name.Contains("Камера")) return "Камеры заднего вида";
                    return "Колонки для авто";
                case "Зеркала":
                    return "Зеркала заднего вида";
                case "Инструменты":
                    return "Специнструменты";
                case "Кронштейны, опоры":
                    return "Опоры двигателей";
                case "Световые приборы транспорта":
                    return "Фары";
                case "Подвеска и тормозная система":
                    if (_bus[b].name.Contains("Блок ABS") ||
                        _bus[b].name.Contains("Барабан") ||
                        _bus[b].name.Contains("Суппорт") ||
                        _bus[b].name.Contains("ормоз") ||
                        _bus[b].name.Contains("ручник") ||
                        _bus[b].name.Contains("вакуумн") ||
                        _bus[b].name.Contains("колод")
                        ) return "Тормозная система";
                    if (_bus[b].name.Contains("задн")) return "Задний мост";
                    return "Передний мост";
                case "Рулевые рейки, рулевое управление":
                    return "Рулевое управление";
                case "Система охлаждения двигателя":
                    return "Системы охлаждения для двигателей";
                case "Топливная, выхлопная система":
                    return "Топливная система";
                case "Трансмиссия и привод":
                    return "Запчасти для КПП";
                case "Электрика, зажигание":
                    return "Кабели для автопроводки";
                case "Шины, диски, колеса":
                    return "Колесные диски";
                default:
                    return "Наборы запчастей для ТО";
            }
        }

        public async Task ParseSiteAsync(int pageCount = 1) {
            await Task.Factory.StartNew(() => {
                _dr.Navigate("https://cdek.market/v/?dispatch=products.manage");
                var numberOfPages = int.Parse(_dr.GetElementAttribute("//i[contains(@class,'double-angle-right')]/..","href").Split('=').Last());
                var rnd = new Random();
                for (int i=0; i<pageCount; i++) {
                    CheckPage(rnd.Next(1, numberOfPages));
                }
            });
        }

        private void CheckPage(int p) {
            _dr.Navigate("https://cdek.market/v/?dispatch=products.manage&page="+p);
            var productList = _dr.FindElements("//td[@class='product-name-column']/a")
                                     .Select(s => s.GetAttribute("href"))
                                     .Where(w=>_bus.FindIndex(f=>f.cdek.Contains(w.Split('=').Last())) == -1)
                                     .ToList();
            foreach (var url in productList) {
                _dr.Navigate(url);
                SetCategory(0);
                _dr.ButtonClick("//input[@id='elm_product_status_0_d']");
                PressOkButton();
            }
        }

        public void Quit() {
            _dr?.Quit();
            _dr = null;
        }
    }
}