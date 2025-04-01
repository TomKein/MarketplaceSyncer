using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;
using Selen.Base;
using Selen.Tools;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Selen.Sites {
    public class YandexMarket {

        readonly string L = "yandex: ";
        readonly string FILE_PRIMARY_XML = @"..\data\yandex\yandex.xml";            //выгрузка основной магазин
        readonly string FILE_EXPRESS_XML = @"..\data\yandex\yandex_express.xml";    //выгрузка на экспресс
        readonly string FILE_COMPAIGNS = @"..\data\yandex\compaigns.json";          //список магазинов
        readonly string FILE_RESERVES = @"..\data\yandex\reserves.json";       //список сделанных резервов
        readonly int EXPRESS_MAX_LENGTH = 90;
        readonly int EXPRESS_MAX_WIDTH = 54;
        readonly int EXPRESS_MAX_HEIGHT = 43;
        readonly int EXPRESS_MAX_WEIGHT = 30;
        // Получение токена https://yandex.ru/dev/market/partner-api/doc/ru/concepts/authorization#token
        readonly string ACCESS_TOKEN = "Bearer y0_AgAAAAAQNtIKAAt1AQAAAAD-gMliAAAepMeJyz9OFY-kuMFylVX5_cYtQQ";
        public static string BasePath = "https://api.partner.market.yandex.ru";
        HttpClient _hc = new HttpClient();
        MarketCampaigns _campaigns;
        List<GoodObject> _bus;
        List<string> _addDesc, _addDesc2;
        float _oldPriceProcent;
        int _maxDiscount;

        //списки исключений
        const string EXCEPTION_GOODS_FILE = @"..\data\yandex\exceptionGoodsList.json";
        const string EXCEPTION_GROUPS_FILE = @"..\data\yandex\exceptionGroupsList.json";
        const string EXCEPTION_BRANDS_FILE = @"..\data\yandex\exceptionBrandsList.json";
        List<string> _exceptionGoods;
        Dictionary<string, string> _exceptionBrands;
        List<string> _exceptionGroups;

        public YandexMarket() {
            _hc.BaseAddress = new Uri(BasePath);
            Task.Factory.StartNew(() => { 
                _oldPriceProcent = DB.GetParamFloat("yandex.oldPriceProcent");
                _maxDiscount = DB.GetParamInt("yandex.maxDiscount");
            });
        }
        //генерация xml
        public async Task StartSync() {
            await GenerateXML();
            await UpdateActions();
        }
        public async Task GenerateXML() {
            if (!await DB.GetParamBoolAsync("yandex.syncEnable")) {
                Log.Add($"{L}GenerateXML: синхронизация отключена!");
                return;
            }
            while (Class365API.Status == SyncStatus.NeedUpdate)
                await Task.Delay(30000);
            _bus = Class365API._bus;
            _exceptionGroups = JsonConvert.DeserializeObject<List<string>>(
                File.ReadAllText(EXCEPTION_GROUPS_FILE));
            _exceptionGoods = JsonConvert.DeserializeObject<List<string>>(
                File.ReadAllText(EXCEPTION_GOODS_FILE));
            _exceptionBrands = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                File.ReadAllText(EXCEPTION_BRANDS_FILE));
            var gen = Task.Factory.StartNew(() => {
                //интервал проверки
                var uploadInterval = DB.GetParamInt("yandex.uploadInterval");
                if (uploadInterval == 0 || DateTime.Now.Hour % uploadInterval != 0)
                    return false;
                //доп. описание
                _addDesc = JsonConvert.DeserializeObject<List<string>>(
                    DB.GetParamStr("yandex.addDescription"));
                _addDesc2 = JsonConvert.DeserializeObject<List<string>>(
                    DB.GetParamStr("yandex.addDescription2"));
                //конвертирую время в необходимый формат 2022-12-11T17:26:06.6560855+03:00
                var timeNow = XmlConvert.ToString(DateTime.Now, XmlDateTimeSerializationMode.Local);
                //старая цена из настроек
                _oldPriceProcent = DB.GetParamFloat("yandex.oldPriceProcent");
                //обновляю вес товара по умолчанию
                GoodObject.UpdateDefaultWeight();
                //обновляю объем товара по умолчанию
                GoodObject.UpdateDefaultVolume();
                //обновляю срок годности товара по умолчанию
                GoodObject.UpdateDefaultValidity();
                //получаю список карточек с положительным остатком и ценой, у которых есть фотографии
                //отсортированный по цене вниз
                var bus = Class365API._bus.Where(w => w.Price > 0 &&
                                                 w.images.Count > 0 &&
                                                 (w.Amount > 0 ||
                                                 DateTime.Parse(w.updated).AddDays(5) > Class365API.LastScanTime ||
                                                 DateTime.Parse(w.updated_remains_prices).AddDays(5) > Class365API.LastScanTime))
                              .OrderByDescending(o => o.Price);
                Log.Add(L + "найдено " + bus.Count() + " потенциальных объявлений");
                var offers = new XElement("offers");
                var offersExpress = new XElement("offers");
                //для каждой карточки
                foreach (var b in bus) {
                    try {
                        var n = b.name.ToLowerInvariant();
                        //исключения
                        if (_exceptionGroups.Contains(b.GroupName)
                         || _exceptionGoods.Any(e => n.StartsWith(e)))
                            continue;
                        var offer = new XElement("offer", new XAttribute("id", b.id));
                        offer.Add(new XElement("categoryId", b.group_id));
                        offer.Add(new XElement("name", b.NameLimit(150, "MARKET_TITLE")));
                        var price = GetPrice2(b);
                        offer.Add(new XElement("price", price));
                        //цена до скидки +10%
                        offer.Add(new XElement("oldprice", ((long) ((price / (1 - _oldPriceProcent / 100)) / 10) * 10).ToString("F0")));
                        offer.Add(new XElement("currencyId", "RUR"));
                        foreach (var photo in b.images.Take(20))
                            offer.Add(new XElement("picture", photo.url));
                        offer.Add(new XElement("description", new XCData(GetDescription(b))));
                        var vendor = b.GetManufacture();
                        //если пороизводитель не пустой и не содержится в исключениях - добавляем в товар
                        if (vendor != null && !_exceptionBrands.Any(a => a.Key == vendor))
                            offer.Add(new XElement("vendor", vendor));
                        //артикул
                        if (!string.IsNullOrEmpty(b.Part))
                            offer.Add(new XElement("vendorCode", b.Part));
                        offer.Add(new XElement("weight", b.WeightString));
                        offer.Add(new XElement("dimensions", b.GetDimentionsString()));
                        //блок ресейл
                        if (!b.New) {
                            //состояние - бывший в употреблении
                            var condition = new XElement("condition", new XAttribute("type", "preowned"));
                            //внешний вид - хороший, есть следы использования
                            condition.Add(new XElement("quality", "good"));
                            condition.Add(new XElement("reason", GetDescription(b, lenght: 390)));
                            //сохраняю в оффер
                            offer.Add(condition);
                        }
                        //срок годности (если группа масла, автохимия, аксессуары)
                        if (!b.IsGroupSolidParts())
                            offer.Add(new XElement("period-of-validity-days", b.GetValidity().Split(' ').First()));
                        //квант продажи
                        int quant = b.GetQuantOfSell();
                        if (quant != 1) {
                            offer.Add(new XElement("min-quantity", quant));
                            offer.Add(new XElement("step-quantity", quant));
                        }

                        //копия оффера для склада Экспресс
                        var offerExpress = new XElement(offer);

                        //количество 
                        offer.Add(new XElement("count", b.Amount));

                        var amountExpress = GetAmountExpress(b);
                        offerExpress.Add(new XElement("count", amountExpress));

                        //если надо снять
                        offer.Add(new XElement("disabled", b.Amount <= 0 ? "true" : "false"));
                        offerExpress.Add(new XElement("disabled", amountExpress <= 0 ? "true" : "false"));
                        //добавляю оффер к офферам
                        offers.Add(offer);
                        offersExpress.Add(offerExpress);
                    } catch (Exception x) {
                        Log.Add(L + b.name + " - ОШИБКА ВЫГРУЗКИ! - " + x.Message);
                        if (DB.GetParamBool("alertSound"))
                            new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
                    }
                }
                Log.Add(L + "выгружено " + offers.Descendants("offer").Count());
                //создаю необходимый элемент <shop>
                var shop = new XElement("shop");
                shop.Add(new XElement("name", "АвтоТехноШик"));
                shop.Add(new XElement("company", "АвтоТехноШик"));
                shop.Add(new XElement("url", "https://автотехношик.рф"));
                shop.Add(new XElement("platform", "Satom.ru"));
                //список групп, в которых нашлись товары
                var groupsIds = bus.Select(s => s.group_id).Distinct();
                //создаю необходимый элемент <categories>
                var categories = new XElement("categories");
                //заполняю категории
                foreach (var groupId in groupsIds) {
                    //исключение
                    if (groupId == "2281135")// Инструменты (аренда)
                        continue;
                    categories.Add(new XElement("category", GoodObject.GetGroupName(groupId), new XAttribute("id", groupId)));
                }
                shop.Add(categories);
                //копия магазина для выгрузки Экспресс
                var shopExpress = new XElement(shop);

                shop.Add(offers);
                shopExpress.Add(offersExpress);
                //создаю корневой элемент 
                var root = new XElement("yml_catalog", new XAttribute("date", timeNow));
                var rootExpress = new XElement(root);
                //добавляю shop в root 
                root.Add(shop);
                rootExpress.Add(shopExpress);
                //создаю новый xml
                var xml = new XDocument();
                var xmlExpress = new XDocument();
                //добавляю root в документ
                xml.Add(root);
                xmlExpress.Add(rootExpress);
                //сохраняю файл
                xml.Save(FILE_PRIMARY_XML);
                xmlExpress.Save(FILE_EXPRESS_XML);
                return true;
            });
            //если файл сгенерирован и его размер ок
            if (await gen && new FileInfo(FILE_PRIMARY_XML).Length > await DB.GetParamIntAsync("yandex.xmlMinSize")) {
                //отправляю файл на сервер
                await SftpClient.FtpUploadAsync(FILE_PRIMARY_XML);
                await SftpClient.FtpUploadAsync(FILE_EXPRESS_XML);
            } else {
                Log.Add(L + "файл не отправлен - ОШИБКА РАЗМЕРА ФАЙЛА!");
                if (DB.GetParamBool("alertSound"))
                    new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
            }
        }
        string GetDescription(GoodObject b, int lenght = 5500) {
            List<string> list = new List<string>();
            list.AddRange(_addDesc);
            if (b.GroupName != "АВТОХИМИЯ" &&
                b.GroupName != "МАСЛА" &&
                b.GroupName != "УСЛУГИ" &&
                b.GroupName != "Кузов (новое)" &&
                b.GroupName != "Аксессуары" &&
                b.GroupName != "Кузовные запчасти" &&
                b.GroupName != "Инструменты (новые)" &&
                b.GroupName != "Инструменты (аренда)" &&
                !b.name.StartsWith("Абсорбер") &&
                !b.name.StartsWith("Балка") &&
                !b.name.StartsWith("Дверь") &&
                !b.name.StartsWith("Задняя часть кузова") &&
                !b.name.StartsWith("Задняя панель кузова") &&
                !b.name.StartsWith("Защита АКПП") &&
                !b.name.StartsWith("Защита дв") &&
                !b.name.StartsWith("Защита картера") &&
                !b.name.StartsWith("Капот") &&
                !b.name.StartsWith("Крыло") &&
                !b.name.StartsWith("Крыша") &&
                !b.name.StartsWith("Крыша") &&
                !b.name.StartsWith("Крышка багажника") &&
                !b.name.StartsWith("Дверь багажника") &&
                !b.name.StartsWith("Лонжерон") &&
                !b.name.StartsWith("Люк ") &&
                !b.name.StartsWith("Панель") &&
                !b.name.StartsWith("Поводок") &&
                !b.name.StartsWith("Подрамник") &&
                !b.name.StartsWith("Порог") &&
                !b.name.StartsWith("Рамка двери") &&
                !b.name.StartsWith("Рейлинги") &&
                !b.name.StartsWith("Стабилизатор") &&
                !b.name.StartsWith("Стеклоподъемник") &&
                !b.name.StartsWith("Трапеция") &&
                (!b.name.StartsWith("Усилитель") && !b.name.Contains("бампера")) &&
                !b.name.StartsWith("Утеплитель капота") &&
                !b.name.StartsWith("Четверть") &&
                !b.name.StartsWith("Четверть")
                )
                list.AddRange(_addDesc2);
            //var d = b.DescriptionList(lenght);
            //добавляем к основному описанию специальное
            //d.AddRange(b.DescriptionList(lenght, list, specDesc: "MARKET_DESCRIPTION"));
            var d = b.DescriptionList(lenght, list, specDesc: "MARKET_DESCRIPTION");
            return d.Aggregate((a1, a2) => a1 + "\r\n" + a2);
        }
        float GetAmountExpress(GoodObject b) {
            var size = b.GetDimentions();
            if (size[0] > EXPRESS_MAX_LENGTH ||
                size[1] > EXPRESS_MAX_WIDTH ||
                size[2] > EXPRESS_MAX_HEIGHT ||
                b.Weight > EXPRESS_MAX_WEIGHT)
                return 0;
            return b.Amount;
        }
        //int GetPrice(GoodObject b) {
        //    var weight = b.Weight;
        //    var d = b.GetDimentions();
        //    var length = d[0] + d[1] + d[2];
        //    //наценка 30% на всё
        //    int overPrice = (int) (b.Price * 0.30);
        //    //если наценка меньше 200 р - округляю
        //    if (overPrice < 200)
        //        overPrice = 200;
        //    //вес от 10 кг или размер от 100 -- наценка 1500 р
        //    if (overPrice < 1500 && (weight >= 10 || length >= 100))
        //        overPrice = 1500;
        //    //вес от 30 кг или размер от 150 -- наценка 2000 р
        //    if (overPrice < 2000 && (weight >= 30 || length >= 150))
        //        overPrice = 2000;
        //    //вес от 50 кг или размер более 200 -- наценка 3000 р
        //    if (overPrice < 3000 && (weight >= 50 || length >= 200))
        //        overPrice = 3000;
        //    //скидка на всё 3% и округление рублей до десятков в меньшую сторону
        //    //таким образом, скидка будет 3% или чуть более
        //    var newPrice = (int) ((0.97 * (b.Price + overPrice)) / 10);
        //    return 10 * newPrice;
        //}
        public int GetPrice2(GoodObject b) {
            // общая наценка для всех товаров на Яндексе + 25% (X)
            float newPrice = (float) (b.Price * 1.25);
            // доп. наценка до ПВЗ = Х + если(до 24.9 кг и сумма сторон до 199см) то + 6 % (не менее 50р.и не больше 400Р
            // иначе + 450р.
            float pvzPrice;
            if (b.Weight < 25 && b.SumDimentions < 200) {
                pvzPrice = (float) (newPrice * 0.06);
                if (pvzPrice < 50)
                    pvzPrice = 50;
                else if (pvzPrice > 400)
                    pvzPrice = 400;
            } else
                pvzPrice = 450;
            // добавляю наценку к новой цене
            newPrice += pvzPrice;
            // доп. наценка за доставку до городов - зависит от объемного веса (см3/5000)
            var vw = b.VolumeWeight;
            if (vw >= 150)
                newPrice += 3500;
            else if (vw >= 50)
                newPrice += 1600;
            else if (vw >= 35)
                newPrice += 1400;
            else if (vw >= 30)
                newPrice += 1200;
            else if (vw >= 25)
                newPrice += 1000;
            else if (vw >= 20)
                newPrice += 800;
            else if (vw >= 15)
                newPrice += 600;
            else if (vw >= 12)
                newPrice += 500;
            else if (vw >= 10)
                newPrice += 400;
            else if (vw >= 8)
                newPrice += 300;
            else if (vw >= 6)
                newPrice += 250;
            else if (vw >= 4)
                newPrice += 180;
            else if (vw >= 2)
                newPrice += 100;
            else if (vw >= 1)
                newPrice += 70;
            else if (vw >= 0.5)
                newPrice += 65;
            else if (vw >= 0.2)
                newPrice += 60;
            else
                newPrice += 55;
            return (int) (newPrice / 10) * 10; //округление цен до 10 р
        }
        //работа с api
        public async Task<string> PostRequestAsync(string apiRelativeUrl, Dictionary<string, string> query = null, object body=null, string method = "GET") {
            try {
                if (query != null) {
                    var qstr = QueryStringBuilder.BuildQueryString(query);
                    apiRelativeUrl += "?" + qstr;
                }
                HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod(method), apiRelativeUrl);
                requestMessage.Headers.Add("Authorization", ACCESS_TOKEN);
                if (body != null) { 
                    var httpContent = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
                    requestMessage.Content = httpContent;
                }

                var response = await _hc.SendAsync(requestMessage);
                await Task.Delay(500);
                if (response.StatusCode == HttpStatusCode.OK) {
                    var json = await response.Content.ReadAsStringAsync();
                    return json;
                } else 
                    throw new Exception(response.StatusCode + " " + response.ReasonPhrase + " " + await response.Content.ReadAsStringAsync());
            } catch (Exception x) {
                Log.Add(" ошибка запроса! - " + x.Message);
                throw;
            }
        }
        public async Task<T> PostRequestAsync<T>(string apiRelativeUrl, Dictionary<string, string> query = null, object body=null, string method = "GET") {
            try {
                var response = await PostRequestAsync(apiRelativeUrl, query, body, method);
                T obj = JsonConvert.DeserializeObject<T>(response);
                return obj;
            } catch (Exception x) {
                Log.Add(" ошибка запроса! - " + x.Message);
                throw;
            }
        }
        //список магазинов
        public async Task GetCompains() {
            if (File.Exists(FILE_COMPAIGNS) &&
                Class365API.LastScanTime < File.GetLastWriteTime(FILE_COMPAIGNS).AddDays(7)) {
                if (_campaigns == null || _campaigns.campaigns?.Count == 0) {
                    _campaigns = JsonConvert.DeserializeObject<MarketCampaigns>(
                         File.ReadAllText(FILE_COMPAIGNS));
                }
            } else {
                _campaigns = await PostRequestAsync<MarketCampaigns>("/campaigns");
                var s = JsonConvert.SerializeObject(_campaigns);
                File.WriteAllText(FILE_COMPAIGNS, s);
            }
        }
        //резервирование
        public async Task MakeReserve() {
            try {
                if (!await DB.GetParamBoolAsync("yandex.syncEnable")) {
                    Log.Add($"{L}MakeReserve: синхронизация отключена!");
                    return;
                }
                //обновляю список магазинов
                await GetCompains();
                //для каждого магазина получаю список заказов
                foreach (var campaign in _campaigns.campaigns) {
                    var campaignId = campaign.id;
                    var s = await PostRequestAsync($"/campaigns/{campaignId}/orders", new Dictionary<string, string> {
                        { "fromDate", DateTime.Now.AddDays(-2).Date.ToString("dd-MM-yyyy") }
                    });
                    var orders = JsonConvert.DeserializeObject<MarketOrders>(s);
                    Log.Add(L + "MakeReserve: для магазина " + campaign.domain + " получено  заказов: " + orders.orders.Count);
                    //загружаем список заказов, для которых уже делали резервирование
                    var reserveList = new List<string>();
                    if (File.Exists(FILE_RESERVES)) {
                        var r = File.ReadAllText(FILE_RESERVES);
                        reserveList = JsonConvert.DeserializeObject<List<string>>(r);
                    }
                    //для каждого заказа сделать заказ с резервом в бизнес.ру
                    foreach (var order in orders.orders) {
                        //проверяем наличие резерва
                        if (reserveList.Contains(order.id))
                            continue;
                        //готовим список товаров (id, amount)
                        var goodsDict = new Dictionary<string, int>();
                        order.items.ForEach(i => goodsDict.Add(i.offerId, i.count));
                        var isResMaked = await Class365API.MakeReserve(
                            Class365API.Source("Yandex.Market"), 
                            $"Yandex.Market order {order.id}",
                            goodsDict, order.creationDate);
                        if (isResMaked) {
                            reserveList.Add(order.id);
                            if (reserveList.Count > 1000) {
                                reserveList.RemoveAt(0);
                            }
                            var rl = JsonConvert.SerializeObject(reserveList);
                            File.WriteAllText(FILE_RESERVES, rl);
                        }
                    }
                }
            } catch (Exception x) {
                Log.Add(L + "MakeReserve - " + x.Message);
                if (DB.GetParamBool("alertSound"))
                    new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
            }
        }
        //обновление акций
        public async Task UpdateActions() {
            try {
                //проверяем акции только раз в час, в начале часа
                if (Class365API.SyncStartTime.Minute > Class365API._checkIntervalMinutes)
                    return;
                await GetCompains();
                //запрашиваем список акций
                var businessId = _campaigns.campaigns[0].business.id;
                var path = $"businesses/{businessId}/promos";
                var body = new {
                    participation = "PARTICIPATING_NOW"
                };
                var actions = await PostRequestAsync<MarketActions>(path, body: body, method: "POST");
                //проверяем список товаров в каждой акции
                var limit = 450;
                foreach (var promo in actions.result.promos) {
                    path = $"businesses/{businessId}/promos/offers";
                    var query = new Dictionary<string, string> {
                        {"limit", limit.ToString()}
                    };
                    var promoId = promo.id;
                    string page_token = null;
                    List<MarketActionOffer> offers = new List<MarketActionOffer>();
                    do {
                        if (page_token != null)
                            query["page_token"] = page_token;
                        var body2 = new {
                            promoId = promoId
                        };
                        var requestOffers = await PostRequestAsync<MarketActionOffers>(path, query: query, body: body2, method: "POST");
                        offers.AddRange(requestOffers.result.offers);
                        page_token = requestOffers.result.paging.nextPageToken;
                    } while (page_token != null);
                    Log.Add($"{L}UpdateActions: [{promo.id}] {promo.name} - получено товаров " +
                            $"{promo.assortmentInfo.potentialOffers}, активно товаров {promo.assortmentInfo.activeOffers}");
                    //списки товаров на добавление и удаление из акций
                    List<MarketActionOffer> toAdd = new List<MarketActionOffer>();
                    List<MarketActionOffer> toDelete = new List<MarketActionOffer>();

                    //проверим товары в данной акции
                    foreach (var offer in offers) {
                        //проверим скидки
                        var oldPrice = offer.@params.discountParams.price;              //зачеркнутая цена в акции (до скидки)
                        var promoPrice = offer.@params.discountParams.promoPrice;       //цена по акции
                        var maxPromoPrice = offer.@params.discountParams.maxPromoPrice; //максимальная цена по акции >= цена по акции

                        var b = Class365API.GoodById(offer.offerId);                    //карточка в бизнес.ру
                        var catalogPrice = GetPrice2(b);                                //наша цена в каталоге

                        //считаем дисконт, как отношение максимальной цены по акции к нашей цене в каталоге
                        var discount = 100 * (catalogPrice - maxPromoPrice) / (float) catalogPrice;
                        
                        //новая цена для акции
                        var promoPriceNew = catalogPrice > maxPromoPrice ? maxPromoPrice : catalogPrice;
                        //новая зачеркнутая цена для акции
                        var oldPriceNew = (long) ((promoPriceNew / (1 - _oldPriceProcent/100))/10)*10;

                        //если скидка больше допустимой - убираем из акции
                        if (discount > _maxDiscount) {
                            if (offer.status != "NOT_PARTICIPATING" && toDelete.Count < 100) {
                                toDelete.Add(offer);
                                Log.Add($"{L}UpdateActions: [{offer.offerId}] {b.name} " +
                                    $"- удаляем из акции, скидка {discount} > {_maxDiscount}, oldPrice = {oldPriceNew}, promoPrice = {promoPriceNew}");
                            }
                        }
                        //если скидка в пределах допустимой - добавляем в акцию
                        if (discount <= _maxDiscount) {
                            //если статус = не участвует, либо цена до скидки не равна расчетной, либо цена со скидкой не равна максимальной
                            if (offer.status != "AUTO" && (offer.status == "NOT_PARTICIPATING" || oldPriceNew != oldPrice || promoPriceNew != promoPrice) && toAdd.Count < 100) {
                                offer.@params.discountParams.price = oldPriceNew;
                                offer.@params.discountParams.promoPrice = promoPriceNew;
                                toAdd.Add(offer);
                                Log.Add($"{L}UpdateActions: [{offer.offerId}] {b.name} " +
                                    $"- добавляем в акцию, скидка {discount} <= {_maxDiscount}, oldPrice = {oldPriceNew}, promoPrice = {promoPriceNew}");
                            }
                        }
                    }

                    //удаляем товары из акции списком
                    if (toDelete.Count > 0) {
                        path = $"businesses/{businessId}/promos/offers/delete";
                        var body3 = new {
                            promoId = promoId,
                            offerIds = toDelete.Select(s => s.offerId).ToArray()
                        };
                        var requestResult = await PostRequestAsync(path, body: body3, method: "POST");
                        Log.Add($"{L}UpdateActions: результат удаления - {requestResult}");
                    }
                    //добавляем товары в акцию списком
                    if (toAdd.Count > 0) {
                        path = $"businesses/{businessId}/promos/offers/update";
                        var body4 = new {
                            promoId = promoId,
                            offers = toAdd.Select(s => new {
                                offerId = s.offerId,
                                @params = new {
                                    discountParams = new {
                                        promoPrice = s.@params.discountParams.promoPrice,
                                        price = s.@params.discountParams.price
                                    }
                                }
                            }).ToArray()
                        };
                        var requestResult = await PostRequestAsync(path, body: body4, method: "POST");
                        Log.Add($"{L}UpdateActions: результат добавления - {requestResult}");
                    }
                }
            } catch (Exception x) {
                Log.Add($"{L}UpdateActions: ошибка {x.Message}");
            }
        }
    }
    /////////////////////////////////////
    /// классы для работы с запросами ///
    /////////////////////////////////////
    public class MarketActionOffers {
        public MarketActionOffersResult result { get; set; }        //Список товаров, которые участвуют или могут участвовать в акции
        public string status { get; set; }                          //Тип ответа: OK, ERROR
    }
    public class MarketActionOffersResult {
        public MarketActionOffer[] offers { get; set; }             //Товары, которые участвуют или могут участвовать в акции
        public MarketForwardPager paging { get; set; }              //Ссылка на следующую страницу
    }
    public class MarketActionOffer {
        public string offerId { get; set; }                         //Ваш SKU — идентификатор товара в вашей системе
        public MarketOfferParams @params {  get; set; }             //Параметры товара в акции
        public string status { get; set; }                          //Статус товара в акции
        public MarketAutoParticipationDetails autoParticipatingDetails { get; set; } //Информация об автоматическом добавлении товара в акцию
    }
    public class MarketOfferParams { 
        public MarketOfferDiscountParams discountParams { get; set; } //Параметры товара в акции с типом DIRECT_DISCOUNT или BLUE_FLASH
        public MarketPromocodeParams promocodeParams { get; set; }  //Параметры товара в акции с типом MARKET_PROMOCODE
    }
    public class MarketForwardPager {
        public string nextPageToken { get; set; }                   //Идентификатор следующей страницы результатов
    }
    public class MarketAutoParticipationDetails {
        public int[] campaignIds { get; set; }                      //Магазины, в которых товар добавлен в акцию автоматически
    }
    public class MarketOfferDiscountParams {
        public long maxPromoPrice { get; set; }                     //Максимально возможная цена для участия в акции
        public long price { get; set; }                             //Зачеркнутая цена — та, по которой товар продавался до акции
        public long promoPrice { get; set; }                        //Цена по акции — та, по которой вы хотите продавать товар
    }
    public class MarketPromocodeParams {
        public long maxPrice { get; set; }                          //Максимально возможная цена для участия в акции до применения промокода
    }
    public class MarketActions {
        public MarketActionsResult result { get; set; }             //Информация об акциях Маркета
        public string status { get; set; }                          //Тип ответа: OK, ERROR
    }
    public class MarketActionsResult {
        public MarketPromos[] promos { get; set; }
    }
    public class MarketPromos {
        public MarketAssortmentInfo assortmentInfo {  get; set; }   //Информация о товарах в акции
        public MarketBestsellerInfo bestsellerInfo { get; set; }    //Информация об акции «Бестселлеры Маркета».
        public string id { get; set; }                              //Идентификатор акции
        public string name { get; set; }                            //Название акции
        public MarketMechanicsInfo mechanicsInfo { get; set; }      //Информация о типе акции
        public bool participating { get; set; }                     //Участвует или участвовал ли продавец в этой акции

        public MarketPromoPeriod period { get; set; }               //Время проведения акции
    
        public string[] channels { get; set; }           //Список каналов продвижения товаров
        public MarketPromoConstrains constraints { get; set; }      //Ограничения в акции
    }
    public class MarketAssortmentInfo {
        public int activeOffers { get; set; }                       //Количество товаров, которые участвуют или участвовали в акции
        public int potentialOffers { get; set; }                    //Количество доступных товаров в акции
        public bool processing {  get; set; }                       //Есть ли изменения в ассортименте, которые еще не применились
    }
    public class MarketBestsellerInfo {
        public bool bestseller {  get; set; }                       //Является ли акция «Бестселлером Маркета»
        public string entryDeadline { get; set; }                   //До какой даты можно добавить товар в акцию «Бестселлеры Маркета»
        public bool renewalEnabled { get; set; }                    //Включен ли автоматический перенос ассортимента между акциями «Бестселлеры Маркета».
    }
    public class MarketMechanicsInfo {
        public string type { get; set; }                            //Тип акции: DIRECT_DISCOUNT, BLUE_FLASH, MARKET_PROMOCODE
        public MarketPromocodeInfo promocodeInfo { get; set; }      //Информация для типа MARKET_PROMOCODE. Параметр заполняется только для этого типа акции
    }
    public class MarketPromocodeInfo {
        public int discount { get; set; }                           //Процент скидки по промокоду
        public string promocode { get; set; }                       //Промокод
    }
    public class MarketPromoPeriod {
        public string dateTimeFrom { get; set; }                    //Дата и время начала акции
        public string dateTimeTo { get; set; }                      //Дата и время окончания акции
    }
    public class MarketPromoConstrains {
        public int[] warehouseIds { get; set; }                     //Идентификаторы складов, для которых действует акция
    }
    public class MarketCampaigns {
        public List<MarketCampaign> campaigns { get; set; }         //Список с информацией по каждому магазину
        public MarketPager pager { get; set; }
    }
    public class MarketCampaign {
        public string domain { get; set; }
        public string id { get; set; }
        public string clientId { get; set; }
        public MarketBusiness business { get; set; }                //Информация о кабинете
    }
    public class MarketBusiness {
        public int id { get; set; }
        public string name { get; set; }
    }
    public class MarketOrders {
        public MarketPager pager { get; set; }
        public List<MarketOrder> orders { get; set; }
    }
    public class MarketOrder {
        public string id { get; set; }
        public string status { get; set; }
        public string substatus { get; set; }
        public string creationDate { get; set; }
        public List<MarketItem> items { get; set; }
    }
    public class MarketItem {
        public string id { get; set; }
        public string offerId { get; set; }
        public string offerName { get; set; }
        public float price { get; set; }
        public int count { get; set; }
    }
    public class MarketPager {
        public int total { get; set; }
        public int from { get; set; }
        public int to { get; set; }
        public int currentPage { get; set; }
        public int pagesCount { get; set; }
        public int pageSize { get; set; }
    }
}