using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;
using Selen.Base;
using Selen.Tools;

namespace Selen.Sites {
    public class MegaMarket {

        readonly string _l = "megamarket: ";
        readonly string FILE_PRIMARY_XML = @"..\data\megamarket\megamarket.xml";
        //readonly string FILE_COMPAIGNS = @"..\data\megamarket\compaigns.json";          //список магазинов
        readonly string FILE_RESERVES = @"..\data\megamarket\reserves.json";       //список сделанных резервов
        //readonly string ACCESS_TOKEN = "Bearer y0_AgAAAAAQNtIKAAt1AQAAAAD-gMliAAAepMeJyz9OFY-kuMFylVX5_cYtQQ";
        //public static string BasePath = "https://api.partner.market.yandex.ru";
        //HttpClient _hc = new HttpClient();
        //MarketCampaigns _campaigns;
        public MegaMarket() {
            //_hc.BaseAddress = new Uri(BasePath);
        }
        //генерация xml
        public async Task GenerateXML() {
            while (Class365API.IsBusinessNeedRescan || Class365API._bus.Count == 0)
                await Task.Delay(30000);
            var gen = Task.Factory.StartNew(() => {
                //доп. описание
                string[] _addDesc = JsonConvert.DeserializeObject<string[]>(
                    DB.GetParamStr("megamarket.addDescription"));
                //конвертирую время в необходимый формат 2022-12-11T17:26:06.6560855+03:00
                var timeNow = DateTime.Now.ToString("yyyy'-'MM'-'dd' 'HH':'mm':'ss");
                //var timeNow = XmlConvert.ToString(DateTime.Now, XmlDateTimeSerializationMode.Local);
                //старая цена из настроек
                var oldPriceProcent = DB.GetParamFloat("megamarket.oldPriceProcent");
                //обновляю вес товара по умолчанию
                GoodObject.UpdateDefaultWeight();
                //обновляю объем товара по умолчанию
                GoodObject.UpdateDefaultVolume();
                //обновляю срок годности товара по умолчанию
                GoodObject.UpdateDefaultValidity();
                //получаю список карточек - новые, с положительным остатком и ценой, у которых есть фотографии
                //отсортированный по цене вниз
                var bus = Class365API._bus.Where(w => w.New && w.Price > 0 && 
                                                 w.images.Count > 0 && 
                                                 (w.Amount > 0 || 
                                                 DateTime.Parse(w.updated).AddDays(5) > Class365API.LastScanTime || 
                                                 DateTime.Parse(w.updated_remains_prices).AddDays(5) > Class365API.LastScanTime))
                              .OrderByDescending(o => o.Price)
                              .Take(DB.GetParamInt("megamarket.uploadLimit")).ToList();
                Log.Add($"{_l} найдено {bus.Count} потенциальных объявлений");
                var offers = new XElement("offers");
                //для каждой карточки
                foreach (var b in bus) {
                    try {
                        //исключения
                        var n = b.name.ToLowerInvariant();
                        if (b.group_id == "2281135" || n.Contains("пепельница") || //// Инструменты (аренда), пепельницы
                            n.Contains("стекло заднее") || n.Contains("стекло переднее") ||
                            n.StartsWith("крыша ") || n.Contains("задняя часть кузова") ||  //габаритные зч
                            n.Contains("крыло заднее") || n.Contains("четверть ") ||
                            n.Contains("передняя часть кузова") || n.Contains("лонжерон") ||
                            n.Contains("панель передняя") || n.Contains("морда") ||
                            n.Contains("телевизор") || n.Contains("переделка")
                            )
                            continue;
                        var offer = new XElement("offer", new XAttribute("id", b.id));
                        offer.Add(new XElement("name", b.NameLimit(510)));
                        var price = GetPrice(b);
                        offer.Add(new XElement("price", price));
                        //цена до скидки +10%
                        offer.Add(new XElement("oldprice", (Math.Ceiling((price * (1 + oldPriceProcent / 100)) / 100) * 100).ToString("F0")));
                        offer.Add(new XElement("categoryId", b.group_id));
                        foreach (var photo in b.images.Take(15))
                            offer.Add(new XElement("picture", photo.url));
                        offer.Add(new XElement("vat", "6"));//VAT_0, 5=NO_VAT

                        //<shipment - options >
                        //< option days = "1" order - before = "15" />
                        //</ shipment - options >

                        var vendor = b.GetManufacture();
                        if (vendor != null)
                            offer.Add(new XElement("vendor", vendor));
                        if (!string.IsNullOrEmpty(b.Part))  //артикул
                            offer.Add(new XElement("vendorCode", b.Part));

                        var description = b.DescriptionList(2990, _addDesc);
                        offer.Add(new XElement("description", description.Aggregate((a1, a2) => a1 + "\r\n" + a2)));
                        //offer.Add(new XElement("description", new XCData(description.Aggregate((a1, a2) => a1 + "\r\n" + a2))));

                        //количество 
                        var outlet = new XElement("outlet", new XAttribute("id", "AvtoTehnoshik40"), new XAttribute("instock", b.Amount));
                        var outlets = new XElement("outlets",outlet);
                        offer.Add(outlets);

                        offer.Add(new XElement("param", new XAttribute("name", "Вес"), new XAttribute("unit","кг"), b.WeightString));
                        offer.Add(new XElement("param", new XAttribute("name", "Габариты"), new XAttribute("unit", "см"), $"{b.length} x {b.width} x {b.height}"));
                        if (b.GetManufactureCountry() != null)
                            offer.Add(new XElement("param", new XAttribute("name", "Страна изготовитель"), b.GetManufactureCountry()));
                        if (b.GetMaterial() != null)
                            offer.Add(new XElement("param", new XAttribute("name", "Материал"), b.GetMaterial()));


                        //если надо снять
                        //offer.Add(new XElement("disabled", b.Amount <= 0 ? "true" : "false"));
                        //добавляю оффер к офферам
                        offers.Add(offer);
                    } catch (Exception x) {
                        Log.Add(_l + b.name + " - ОШИБКА ВЫГРУЗКИ! - " + x.Message);
                    }
                }
                Log.Add(_l + "выгружено " + offers.Descendants("offer").Count());
                //создаю необходимый элемент <shop>
                var shop = new XElement("shop");
                shop.Add(new XElement("name", "АвтоТехноШик"));
                shop.Add(new XElement("company", "АвтоТехноШик"));
                shop.Add(new XElement("url", "http://автотехношик.рф"));
                //shop.Add(new XElement("platform", "Satom.ru"));
                //список групп, в которых нашлись товары
                var groupsIds = bus.Select(s => s.group_id).Distinct();
                //создаю необходимый элемент <categories>
                var categories = new XElement("categories");
                //заполняю категории
                foreach (var groupId in groupsIds) {
                    //исключение
                    //if (groupId == "2281135")// Инструменты (аренда)
                      //  continue;
                    categories.Add(new XElement("category", GoodObject.GroupName(groupId), new XAttribute("id", groupId)));
                }
                shop.Add(categories);
                shop.Add(offers);
                //создаю корневой элемент 
                var root = new XElement("yml_catalog", new XAttribute("date", timeNow));
                //добавляю shop в root 
                root.Add(shop);
                //создаю новый xml
                var xml = new XDocument();
                //добавляю root в документ
                xml.Add(root);
                //сохраняю файл
                xml.Save(FILE_PRIMARY_XML);
                return true;
            });
            //если файл сгенерирован и его размер ок
            if (await gen) { // && new FileInfo(FILE_PRIMARY_XML).Length > await DB.GetParamIntAsync("megamarket.xmlMinSize")) {
                //отправляю файл на сервер
                await SftpClient.FtpUploadAsync(FILE_PRIMARY_XML);
            } else
                Log.Add(_l + "файл не отправлен - ОШИБКА РАЗМЕРА ФАЙЛА!");
        }
        //цена продажи (как на озоне)
        int GetPrice(GoodObject b) { 
            var weight = b.Weight;
            var d = b.GetDimentions();
            var length = d[0] + d[1] + d[2];
            //наценка 30% на всё
            int overPrice = (int) (b.Price * 0.30);
            //если наценка меньше 200 р - округляю
            if (overPrice < 200)
                overPrice = 200;
            //вес от 10 кг или размер от 100 -- наценка 1500 р
            if (overPrice < 1500 && (weight >= 10 || length >= 100))
                overPrice = 1500;
            //вес от 30 кг или размер от 150 -- наценка 2000 р
            if (overPrice < 2000 && (weight >= 30 || length >= 150))
                overPrice = 2000;
            //вес от 50 кг или размер более 200 -- наценка 3000 р
            if (overPrice < 3000 && (weight >= 50 || length >= 200))
                overPrice = 3000;
            return b.Price + overPrice;
        }
        //public int GetPrice2(GoodObject b) {
        //    // общая наценка для всех товаров на Яндексе + 25% (X)
        //    float newPrice = (float) (b.Price * 1.25);
        //    // доп. наценка до ПВЗ = Х + если(до 24.9 кг и сумма сторон до 199см) то + 6 % (не менее 50р.и не больше 400Р
        //    // иначе + 450р.
        //    float pvzPrice;
        //    if (b.Weight < 25 && b.SumDimentions < 200) {
        //        pvzPrice = (float) (newPrice * 0.06);
        //        if (pvzPrice < 50)
        //            pvzPrice = 50;
        //        else if (pvzPrice > 400)
        //            pvzPrice = 400;
        //    } else
        //        pvzPrice = 450;
        //    // добавляю наценку к новой цене
        //    newPrice += pvzPrice;
        //    // доп. наценка за доставку до городов - зависит от объемного веса (см3/5000)
        //    var vw = b.VolumeWeight;
        //    if (vw >= 150)
        //        newPrice += 3500;
        //    else if (vw >= 50)
        //        newPrice += 1600;
        //    else if (vw >= 35)
        //        newPrice += 1400;
        //    else if (vw >= 30)
        //        newPrice += 1200;
        //    else if (vw >= 25)
        //        newPrice += 1000;
        //    else if (vw >= 20)
        //        newPrice += 800;
        //    else if (vw >= 15)
        //        newPrice += 600;
        //    else if (vw >= 12)
        //        newPrice += 500;
        //    else if (vw >= 10)
        //        newPrice += 400;
        //    else if (vw >= 8)
        //        newPrice += 300;
        //    else if (vw >= 6)
        //        newPrice += 250;
        //    else if (vw >= 4)
        //        newPrice += 180;
        //    else if (vw >= 2)
        //        newPrice += 100;
        //    else if (vw >= 1)
        //        newPrice += 70;
        //    else if (vw >= 0.5)
        //        newPrice += 65;
        //    else if (vw >= 0.2)
        //        newPrice += 60;
        //    else 
        //        newPrice += 55;
        //    return (int) (newPrice/10) * 10; //округление цен до 10 р
        //}
        //работа с api
        //public async Task<string> PostRequestAsync(string apiRelativeUrl, Dictionary<string, string> request = null, string method = "GET") {
        //    try {
        //        if (request != null) {
        //            var qstr = QueryStringBuilder.BuildQueryString(request);
        //            apiRelativeUrl += "?" + qstr;
        //        }
        //        HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod(method), apiRelativeUrl);
        //        requestMessage.Headers.Add("Authorization", ACCESS_TOKEN);
        //        var response = await _hc.SendAsync(requestMessage);
        //        await Task.Delay(500);
        //        if (response.StatusCode == HttpStatusCode.OK) {
        //            var json = await response.Content.ReadAsStringAsync();
        //            return json;
        //        } else
        //            throw new Exception(response.StatusCode + " " + response.ReasonPhrase + " " + response.Content);
        //    } catch (Exception x) {
        //        Log.Add(" ошибка запроса! - " + x.Message);
        //        throw;
        //    }
        //}
        //public async Task<T> PostRequestAsync<T>(string apiRelativeUrl, Dictionary<string, string> request = null, string method = "GET") {
        //    try {
        //        var response = await PostRequestAsync(apiRelativeUrl, request, method);
        //        T obj = JsonConvert.DeserializeObject<T>(response);
        //        return obj;
        //    } catch (Exception x) {
        //        Log.Add(" ошибка запроса! - " + x.Message);
        //        throw;
        //    }
        //}
        //список магазинов
        //public async Task GetCompains() {
        //    if (File.Exists(FILE_COMPAIGNS) &&
        //        Class365API.ScanTime < File.GetLastWriteTime(FILE_COMPAIGNS).AddDays(7)) {
        //        if (_campaigns == null || _campaigns.campaigns?.Count == 0) {
        //            _campaigns = JsonConvert.DeserializeObject<MarketCampaigns>(
        //                 File.ReadAllText(FILE_COMPAIGNS));
        //        }
        //    } else {
        //        _campaigns = await PostRequestAsync<MarketCampaigns>("/campaigns");
        //        var s = JsonConvert.SerializeObject(_campaigns);
        //        File.WriteAllText(FILE_COMPAIGNS, s);
        //    }
        //}
        ////резервирование
        //public async Task MakeReserve() {
        //    try {
        //        //обновляю список магазинов
        //        await GetCompains();
        //        //для каждого магазина получаю список заказов
        //        foreach (var campaign in _campaigns.campaigns) {
        //            var campaignId = campaign.id;
        //            var s = await PostRequestAsync($"/campaigns/{campaignId}/orders",new Dictionary<string, string> {
        //                { "fromDate", DateTime.Now.AddDays(-2).Date.ToString("dd-MM-yyyy") }
        //            });
        //            var orders = JsonConvert.DeserializeObject<MarketOrders>(s);
        //            Log.Add(_l + "MakeReserve: для магазина " + campaign.domain + " получено  заказов: " + orders.orders.Count);
        //            //загружаем список заказов, для которых уже делали резервирование
        //            var reserveList = new List<string>();
        //            if (File.Exists(FILE_RESERVES)) {
        //                var r = File.ReadAllText(FILE_RESERVES);
        //                reserveList = JsonConvert.DeserializeObject<List<string>>(r);
        //            }
        //            //для каждого заказа сделать заказ с резервом в бизнес.ру
        //            foreach (var order in orders.orders) {
        //                //проверяем наличие резерва
        //                if (reserveList.Contains(order.id))
        //                    continue;
        //                //готовим список товаров (id, amount)
        //                var goodsDict = new Dictionary<string, int>();
        //                order.items.ForEach(i => goodsDict.Add(i.offerId, i.count));
        //                var isResMaked = await Class365API.MakeReserve(Selen.Source.YandexMarket, $"Yandex.Market order {order.id}",
        //                                                               goodsDict, order.creationDate);
        //                if (isResMaked) {
        //                    reserveList.Add(order.id);
        //                    if (reserveList.Count > 1000) {
        //                        reserveList.RemoveAt(0);
        //                    }
        //                    var rl = JsonConvert.SerializeObject(reserveList);
        //                    File.WriteAllText(FILE_RESERVES, rl);
        //                }
        //            }
        //        }
        //    } catch (Exception x) {
        //        Log.Add(_l + "MakeReserve - " + x.Message);
        //    }
        //}
    }
    /////////////////////////////////////
    /// классы для работы с запросами ///
    /////////////////////////////////////
    //public class MarketCampaigns {
    //    public List<MarketCampaign> campaigns { get; set; }
    //    public Pager pager { get; set; }
    //}
    //public class MarketCampaign {
    //    public string domain { get; set; }
    //    public string id { get; set; }
    //    public string clientId { get; set; }
    //}
    //public class MarketOrders {
    //    public Pager pager { get; set; }
    //    public List<MarketOrder> orders{ get; set; }
    //}
    //public class MarketOrder {
    //    public string id { get; set; }
    //    public string status { get; set; }
    //    public string substatus { get; set; }
    //    public string creationDate { get; set; }
    //    public List<MarketItem> items { get; set; }
    //}
    //public class MarketItem {
    //    public string id { get; set; }
    //    public string offerId { get; set; }
    //    public string offerName { get; set; }
    //    public float price { get; set; }
    //    public int count { get; set; }
    //}
    //public class Pager {
    //    public int total{ get; set; }
    //    public int from{ get; set; }
    //    public int to{ get; set; }
    //    public int currentPage{ get; set; }
    //    public int pagesCount{ get; set; }
    //    public int pageSize{ get; set; }
    //}
}