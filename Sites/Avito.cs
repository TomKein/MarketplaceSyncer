using Newtonsoft.Json;
using Selen.Base;
using Selen.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Selen.Sites {
    internal class Avito {
        //получение ключей https://www.avito.ru/professionals/api
        //получение токена https://developers.avito.ru/api-catalog/auth/documentation#operation/getAccessToken
        readonly string _l = "avito: ";
        readonly string _fileNameExport = @"..\data\avito.xml";
        readonly string _fileNameStockExport = @"..\data\avitostock.xml";
        readonly string _autoCatalogFile = @"..\data\autocatalog.xml";
        readonly string _genFile = @"..\data\avito\avito_generations.txt";
        static readonly string _catsFile = @"..\data\avito\avito_categories.xml";
        static XDocument _cats;
        static IEnumerable<XElement> _rules;
        readonly string _autoCatalogUrl = "http://autoload.avito.ru/format/Autocatalog.xml";
        readonly string _applicationAttribureId = "2543011";
        List<string> _addDesc;
        List<string> _addDesc2;
        //цены для рассрочки
        int _creditPriceMin;
        int _creditPriceMax;
        string _creditDescription;


        string _clientId;// = "4O7a8RcHV1qdcbp_Lr7f";
        string _clientSecret;// = "1JtB0Yi801aPfl2mKLziP_QcauNEaZaTMuG6asuB";
        string _accessToken;
        readonly string FILE_RESERVES = @"..\data\avito\reserves.json";       //список сделанных резервов
        string _basePath;// = "https://api.avito.ru";
        XDocument autoCatalogXML;
        HttpClient _hc = new HttpClient();

        List<GoodObject> _bus;
        public Avito() {
            _clientId = DB.GetParamStr("avito.clientId");
            _clientSecret = DB.GetParamStr("avito.clientSecret");
            _basePath = DB.GetParamStr("avito.basePath");
            _accessToken = DB.GetParamStr("avito.accessToken");
            _hc.BaseAddress = new Uri(_basePath);
        }
        public async Task<string> PostRequestAsync(string apiRelativeUrl, Dictionary<string, string> request = null, string method = "GET") {
            try {
                if (request != null) {
                    var qstr = QueryStringBuilder.BuildQueryString(request);
                    apiRelativeUrl += "?" + qstr;
                }
                for (int i = 1; i <= 10; i++) {
                    HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod(method), apiRelativeUrl);
                    requestMessage.Headers.Add("Authorization", "Bearer " + _accessToken);
                    var response = await _hc.SendAsync(requestMessage);
                    var json = await response.Content.ReadAsStringAsync();
                    if (response.StatusCode == HttpStatusCode.OK) {
                        await Task.Delay(500);
                        return json;
                    } else if (response.StatusCode == HttpStatusCode.InternalServerError) {
                        Log.Add($"{_l} ошибка сервиса! {response.StatusCode} ({i})");
                    } else {
                        Log.Add($"{_l} ошибка запроса! {response.StatusCode} ({i})");
                        await Task.Delay(500);
                        await GetAccessToken();
                    }
                }
                throw new Exception("превышено количество попыток!!");

            } catch (Exception x) {
                Log.Add($"{_l} ошибка запрос не отправлен! - " + x.Message);
                throw;
            }
        }
        public async Task<T> PostRequestAsync<T>(string apiRelativeUrl, Dictionary<string, string> request = null, string method = "GET") {
            try {
                var response = await PostRequestAsync(apiRelativeUrl, request, method);
                T obj = JsonConvert.DeserializeObject<T>(response);
                return obj;
            } catch (Exception x) {
                Log.Add(" ошибка запроса! - " + x.Message);
                throw;
            }
        }
        public async Task GetAccessToken() {
            Log.Add($"{_l} получаю токен для доступа!");
            var req = new Dictionary<string, string>() {
                {"grant_type","client_credentials" },
                {"client_id", _clientId },
                {"client_secret",_clientSecret },
            };
            var qstr = QueryStringBuilder.BuildQueryString(req);
            HttpContent content = new StringContent(qstr, Encoding.UTF8, "application/x-www-form-urlencoded");
            var response = await _hc.PostAsync(_basePath + "/token", content);
            await Task.Delay(200);
            if (response.StatusCode == HttpStatusCode.OK) {
                var json = await response.Content.ReadAsStringAsync();
                var r = JsonConvert.DeserializeObject<AvitoResponseToken>(json);
                _accessToken = r.access_token;
                await DB.SetParamAsync("avito.accessToken", _accessToken);
                Log.Add($"{_l} новый токен получен! {_accessToken}");
            } else
                throw new Exception(response.StatusCode + " " + response.ReasonPhrase + " " + response.Content);
        }
        //резервирование
        public async Task MakeReserve() {
            try {
                //получаю список заказов
                var orders = await PostRequestAsync<AvitoOrders>("/order-management/1/orders");
                Log.Add($"{_l} MakeReserve - получено  заказов: " + orders.orders.Count);
                //загружаем список заказов, для которых уже делали резерв
                var reserveList = new List<string>();
                if (File.Exists(FILE_RESERVES)) {
                    var r = File.ReadAllText(FILE_RESERVES);
                    reserveList = JsonConvert.DeserializeObject<List<string>>(r);
                }
                //для каждого заказа сделать заказ с резервом в бизнес.ру
                foreach (var order in orders.orders) {
                    //проверяем наличие резерва
                    if (reserveList.Contains(order.marketplaceId) ||
                        order.status == "canceled" ||
                        order.status == "delivered" ||
                        order.status == "closed" ||
                        order.status == "in_dispute" ||
                        order.status == "on_return"
                        )
                        continue;
                    //готовим список товаров (id, amount)
                    var goodsDict = new Dictionary<string, int>();
                    order.items.ForEach(i => goodsDict.Add(i.id, i.count));
                    var isResMaked = await Class365API.MakeReserve(Selen.Source.Avito, $"Avito order {order.marketplaceId}",
                                                                   goodsDict, order.createdAt.AddHours(3).ToString());
                    if (isResMaked) {
                        reserveList.Add(order.marketplaceId);
                        if (reserveList.Count > 1000) {
                            reserveList.RemoveAt(0);
                        }
                        var rl = JsonConvert.SerializeObject(reserveList);
                        File.WriteAllText(FILE_RESERVES, rl);
                    }
                }
            } catch (Exception x) {
                Log.Add($"{_l} MakeReserve - " + x.Message);
            }
        }
        //выгрузка XML
        public async Task GenerateXML() {
            while (Class365API.Status == SyncStatus.NeedUpdate)
                await Task.Delay(30000);
            _bus = Class365API._bus;
            if (Class365API.SyncStartTime.Minute % 30 < 25)
                return;
            if (!await DB.GetParamBoolAsync("avito.syncEnable")) {
                Log.Add(_l + "синхронизация отключена!");
                return;
            }
            await UpdateApplicationsAsync(_bus);
            await Task.Factory.StartNew(() => {
                _cats = XDocument.Load(_catsFile);
                _rules = _cats.Descendants("Rule");
                var offersLimit = DB.GetParamInt("avito.offersLimit");
                var priceLevel = DB.GetParamInt("avito.priceLevel");
                _addDesc = JsonConvert.DeserializeObject<List<string>>(
                    DB.GetParamStr("avito.addDescription"));
                _addDesc2 = JsonConvert.DeserializeObject<List<string>>(
                    DB.GetParamStr("avito.addDescription2"));
                //цены для рассрочки
                _creditPriceMin = DB.GetParamInt("creditPriceMin");
                _creditPriceMax = DB.GetParamInt("creditPriceMax");
                _creditDescription = DB.GetParamStr("creditDescription");
                GoodObject.UpdateDefaultVolume();
                GoodObject.UpdateDefaultWeight();
                var xml = new XDocument();
                var xmlStock = new XDocument();
                var root = new XElement("Ads", new XAttribute("formatVersion", "3"), new XAttribute("target", "Avito.ru"));
                var rootStock = new XElement("items", new XAttribute("date", DateTime.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss")),
                                                      new XAttribute("formatVersion", "1"));
                var days = DB.GetParamInt("avito.daysUpdatedToUpload");
                //список карточек с положительным остатком и ценой, у которых есть фотографии
                //отсортированный по убыванию цены
                var bus = _bus.Where(w => w.Price >= priceLevel && w.images.Count > 0 &&
                                    (w.Amount > 0 ||
                                     DateTime.Parse(w.updated).AddHours(2).AddDays(days) > Class365API.LastScanTime ||
                                     DateTime.Parse(w.updated_remains_prices).AddHours(2).AddDays(days) > Class365API.LastScanTime))
                              .OrderByDescending(o => o.Price);
                Log.Add(_l + "найдено " + bus.Count() + " товаров для выгрузки");
                int i = 0;
                foreach (var b in bus) {
                    try {
                        //заполнение остатков
                        var itemStock = new XElement("item");
                        itemStock.Add(new XElement("id", b.id));
                        itemStock.Add(new XElement("stock", b.Amount));
                        rootStock.Add(itemStock);

                        //основной xml
                        var ad = new XElement("Ad");
                        ad.Add(new XElement("Id", b.id));
                        //категория товара
                        foreach (var item in GetCategoryAvito(b)) {
                            ad.Add(new XElement(item.Key, item.Value));
                        }

                        var images = new XElement("Images");
                        foreach (var photo in b.images.Take(10)) {
                            images.Add(new XElement("Image", new XAttribute("url", photo.url)));
                        }
                        ad.Add(images);

                        if (b.Amount <= 0) {
                            ad.Add(new XElement("DateEnd", DateTime.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss") + "+03:00"));
                        }
                        //else {
                        //    ad.Add(new XElement("DateBegin", DateTime.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss")+"+03:00"));
                        //}
                        ad.Add(new XElement("Address", "Россия, Калуга, Московская улица, 331"));
                        ad.Add(new XElement("Price", b.Price));
                        ad.Add(new XElement("ContactPhone", "8 920 899-45-45"));
                        ad.Add(new XElement("Title", b.NameLimit(100)));
                        ad.Add(new XElement("Description", new XCData(GetDescription(b))));
                        ad.Add(new XElement("ManagerName", "Менеджер"));
                        ad.Add(new XElement("AdType", "Товар приобретен на продажу"));
                        ad.Add(new XElement("Condition", b.New ? "Новое" : "Б/у"));
                        ad.Add(new XElement("Availability", "В наличии"));
                        ad.Add(new XElement("Originality", b.Origin ? "Оригинал" : "Аналог"));
                        //номер запчасти
                        if (!string.IsNullOrEmpty(b.Part))
                            ad.Add(new XElement("OEM", b.Part));
                        //производитель
                        var brand = b.GetManufacture();
                        if (!string.IsNullOrEmpty(brand))
                            ad.Add(new XElement("Brand", b.GetManufacture()));
                        // если запчасть б/у и нет номера или бренда,
                        // то нужно добавить Для чего подходит (требование авито)
                        if (!b.New/* && (string.IsNullOrEmpty(b.part) || string.IsNullOrEmpty(brand))*/) {
                            var app = GetApplication(b);
                            if (app != null) {
                                var list = app.Split(',');
                                ad.Add(new XElement("Make", list[0]));
                                if (list.Length > 2) {
                                    ad.Add(new XElement("Model", list[1].Trim()));
                                    ad.Add(new XElement("Generation", list[2].Trim()));
                                }
                            }
                        }
                        root.Add(ad);
                        //считаем объявления с положительным остатком
                        if (b.Amount > 0)
                            i++;
                        if (i >= offersLimit)
                            break;
                    } catch (Exception x) {
                        Log.Add(_l + i + " - " + b.name + " - " + x.Message);
                    }
                }
                Log.Add(_l + "выгружено " + i);
                xml.Add(root);
                xmlStock.Add(rootStock);
                xml.Save(_fileNameExport);
                xmlStock.Save(_fileNameStockExport);
            });
            if (new FileInfo(_fileNameExport).Length > await DB.GetParamIntAsync("avito.xmlMinSize")) {
                await SftpClient.FtpUploadAsync(_fileNameExport);
                await SftpClient.FtpUploadAsync(_fileNameStockExport);
            } else
                Log.Add($"{_l} ошибка - файл не отправлен, т.к. меньше минимального размера!");
        }
        string GetDescription(GoodObject b) {
            var d = b.DescriptionList(2990, _addDesc);
            if (b.GroupName() != "АВТОХИМИЯ" &&
                b.GroupName() != "МАСЛА" &&
                b.GroupName() != "УСЛУГИ" &&
                b.GroupName() != "Кузов (новое)" &&
                b.GroupName() != "Аксессуары" &&
                b.GroupName() != "Кузовные запчасти" &&
                b.GroupName() != "Инструменты (новые)" &&
                b.GroupName() != "Инструменты (аренда)" &&
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
                d.AddRange(_addDesc2);
            if (b.Price >= _creditPriceMin && b.Price <= _creditPriceMax)
                d.Insert(0, _creditDescription);
            var descStr = d.Aggregate((a1, a2) => a1 + "\r\n" + a2);
            //if (DateTime.Now.Day != Class365API.LastScanTime.Day) //еще вариант обновления
            if (DateTime.Now.Hour <= 9)
                descStr += ".";
            return descStr;
        }
        public Task GetAutoCatalogXmlAsync() => Task.Factory.StartNew(() => {
            var period = DB.GetParamInt("avito.autoCatalogPeriod");
            if (!File.Exists(_autoCatalogFile) || File.GetLastWriteTime(_autoCatalogFile).AddHours(period) < DateTime.Now) {
                try {
                    Log.Add(_l + "запрашиваю новый автокаталог xml с avito...");
                    autoCatalogXML = XDocument.Load(_autoCatalogUrl);
                    if (autoCatalogXML.Element("Catalog").Elements("Make").Count() > 200)
                        autoCatalogXML.Save(_autoCatalogFile);
                    else
                        throw new Exception("мало элементов в автокаталоге");
                    Log.Add(_l + "автокаталог обновлен!");
                    return;
                } catch (Exception x) {
                    Log.Add(_l + "ошибка запроса xml - " + x.Message);
                }
            }
            autoCatalogXML = XDocument.Load(_autoCatalogFile);
            Log.Add(_l + "автокаталог загружен!");
        });
        private string GetApplication(GoodObject b) {
            //проверяю заполнение атрибута
            if (b.attributes != null && b.attributes.Any(a => a.Attribute.id == _applicationAttribureId)) {
                return b.attributes.Find(f => f.Attribute.id == _applicationAttribureId).Value.name.ToString();
            }
            //если атрибут не заполнен, пытаюсь определить из названия и описания
            var desc = (b.name + " " + b.HtmlDecodedDescription())
                       .ToLowerInvariant()
                       .Replace("vw ", "volkswagen ")
                       .Replace("vag ", "volkswagen ")
                       .Replace("psa ", "peugeot ");
            //новый словарь для учета совпадений
            var dict = new Dictionary<string, int>();
            // получаем список производителей
            var makes = autoCatalogXML.Element("Catalog").Elements("Make");
            //проверяю похожесть на каждый элемент списка
            foreach (var make in makes) {
                dict.Add(make.Attribute("name").Value, 0);
                if (desc.Contains(make.Attribute("name").Value.ToLowerInvariant()))
                    dict[make.Attribute("name").Value] += 11;
                foreach (var model in make.Elements("Model")) {
                    if (desc.Contains(model.Attribute("name").Value.ToLowerInvariant()))
                        dict[make.Attribute("name").Value] += model.Attribute("name").Value.Length;
                }
            }
            //определяю лучшие совпадения
            var best = dict.OrderByDescending(o => o.Value).Where(w => w.Value >= 3).ToList();
            //если они есть
            if (best.Count > 0) {
                return best[0].Key;
            } else
                Log.Add("GetMake: " + b.name + " пропущен - не удалось определить автопроизводителя");
            return null;
        }
        public async Task UpdateApplicationsAsync(List<GoodObject> bus) {
            //обновляю автокаталог
            await GetAutoCatalogXmlAsync();
            var list = new List<string>();
            foreach (var make in autoCatalogXML.Element("Catalog").Elements("Make"))
                foreach (var model in make.Elements("Model"))
                    foreach (var generation in model.Elements("Generation")) {
                        var name = new StringBuilder();
                        name.Append(make.Attribute("name").Value);
                        name.Append(", ");
                        name.Append(model.Attribute("name").Value);
                        name.Append(", ");
                        name.Append(generation.Attribute("name").Value);
                        list.Add(name.ToString());
                    }
            File.WriteAllText(_genFile, list.Aggregate((a, b) => a + "\n" + b));
            //await Applications.UpdateApplicationListAsync(list);
        }
        static void GetParams(XElement rule, Dictionary<string, string> d) {
            var parent = rule.Parent;
            if (parent != null) {
                GetParams(parent, d);
                d.Add(parent.Name.ToString(), parent.Attribute("Name").Value);
            }
        }


        //категории авито
        public static Dictionary<string, string> GetCategoryAvito(GoodObject b) {
            var name = b.name.ToLowerInvariant()
                             .Replace(@"б\у", "").Replace("б/у", "").Replace("б.у.", "").Replace("б.у", "").Trim();
            var d = new Dictionary<string, string>();


            foreach (var rule in _rules) {
                var conditions = rule.Elements();
                var eq = true;
                foreach (var condition in conditions) {
                    if (!eq)
                        break;
                    if (condition.Name == "Starts" && !name.StartsWith(condition.Value))
                        eq = false;
                    else if (condition.Name == "Contains" && !name.Contains(condition.Value))
                        eq = false;
                    else if (condition.Name == "MaxWeight" && b.Weight > float.Parse(condition.Value.Replace(".", ",")))
                        eq = false;
                    else if (condition.Name == "MinWeight" && b.Weight < float.Parse(condition.Value.Replace(".", ",")))
                        eq = false;
                    else if (condition.Name == "MaxLength" && b.GetLength() > float.Parse(condition.Value.Replace(".", ",")))
                        eq = false;
                    else if (condition.Name == "MinLength" && b.GetLength() < float.Parse(condition.Value.Replace(".", ",")))
                        eq = false;
                }
                if (eq) {
                    GetParams(rule, d);
                    if (d.Any())
                        break;
                }
            }
            if (d.ContainsKey("ProductType")) {
                if (d["ProductType"] == "Колёса") {
                    d.Add("RimDiameter", b.GetDiskSize());              //Диаметр диска
                    d.Add("RimOffset", "0");                            //TODO Вылет, пока ставим 0, добавить определение из описания
                    d.Add("RimBolts", b.GetNumberOfHoles());            //количество отверстий
                    d.Add("RimWidth", "5");                             //TODO ширина диска, пока 5, добавить определ. из описания
                    d.Add("RimBoltsDiameter", b.GetDiameterOfHoles());  //диаметр расп. отверстий                                               
                    d.Add("RimType", "Штампованные");                   //тип
                    d.Add("TireType", "Всесезонные");                   //шины
                    d.Add("TireSectionWidth", "115");                   //ширина шины
                    d.Add("TireAspectRatio", "55");                     //соотношение
                    d.Add("RimDIA", b.GetDiaStup());                    //диаметр ступицы
                    d.Add("ResidualTread", "10");
                } else if (d["ProductType"] == "Диски") {
                    d.Add("RimDiameter", b.GetDiskSize());              //Диаметр диска
                    d.Add("RimOffset", "0");                            //TODO Вылет, пока ставим 0, добавить определение из описания
                    d.Add("RimBolts", b.GetNumberOfHoles());            //количество отверстий
                    d.Add("RimWidth", "5");                             //TODO ширина диска, пока 5, добавить определ. из описания
                    d.Add("RimBoltsDiameter", b.GetDiameterOfHoles());  //диаметр расп. отверстий                                               
                    d.Add("RimType", b.DiskType());                     //тип
                    d.Add("RimDIA", b.GetDiaStup());                    //диаметр ступицы
                }
            }
            // else if (d["ProductType"]== "Колпаки") {}
            //else if (name.StartsWith("шины ") ||
            //           name.StartsWith("шина ") ||
            //           name.StartsWith("резина")) {}
            if (!d.Any())
                throw new Exception("категория не определена!");
            return d;
        }
    }
    //////////////////////////////////
    ///типы для работы с запросами ///
    //////////////////////////////////
    public class AvitoResponseToken {
        public string access_token { get; set; }
        public int expires_in { get; set; }
        public string token_type { get; set; }
    }
    public class AvitoOrders {
        public bool hasMore { get; set; }
        public List<AvitoOrder> orders { get; set; }
    }
    public class AvitoOrder {
        public DateTime createdAt { set; get; } //"2024-04-03T13:51:37Z",
        public string id { set; get; }
        public string marketplaceId { set; get; }
        public List<AvitoItem> items { get; set; }
        public AvitoPrices prices { get; set; }
        public string status { set; get; }
        public DateTime updatedAt { get; set; }  //": "2024-04-03T13:59:52Z"
    }
    public class AvitoItem {
        public string avitoId { set; get; }
        public string chatId { set; get; }
        public int count { set; get; }
        public string id { get; set; }
        public string location { set; get; }
        public AvitoPrices prices { set; get; }
        public string title { set; get; }
    }
    public class AvitoPrices {
        public string commission { set; get; }
        public string price { set; get; }
        public string total { set; get; }
    }
}