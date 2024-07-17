using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Selen.Tools;
using Selen.Base;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using System.Web.UI.WebControls;

namespace Selen.Sites {
    public class Wildberries {
        readonly HttpClient _hc = new HttpClient();
        readonly string _baseApiUrl = "https://suppliers-api.wildberries.ru";
        readonly string _l = "wb: ";                                                    //префикс для лога
        List<GoodObject> _bus;
        int _nameLimit = 200;                                                           //ограничение длины названия
        readonly string _url;                                                           //номер поля в карточке
        readonly int _updateFreq;                                                       //частота обновления списка (часов)
        readonly string _baseBusUrl = "https://www.wildberries.ru/catalog/00000/detail.aspx";
        List<WbCard> _productCards = new List<WbCard>();                                //список карточек вб
        readonly string _productListFile = @"..\data\wildberries\wb_productList.json";  //ссылка на файл для кеширования списка карточек вб
        static bool _isProductListCheckNeeds;
        string _jsonResponse;

        //    //readonly float _oldPriceProcent;
        //    //readonly string _reserveListFile = @"..\data\wildberries\wb_reserveList.json";
        //    //readonly string _warehouseList = @"..\data\wildberries\wb_warehouseList.json";
        //    bool _hasNext = false;                        //для запросов
        //    List<AttributeValue> _brends;                 //список брендов озон

        List<WbCategory> _categories;       //список всех категорий товаров
        List<WbObject> _objects;            //список всех подкатегорий товаров
        JArray _objectsJA;                  //список всех подкатегорий товаров в формате JsonArray
        List<WbColor> _colors;              //цвета
        List<WbCountry> _countries;         //страны
        List<WbTnvd> _tnvd;                 //ТНВЭД код

        //    static readonly string _rulesFile = @"..\data\wildberries\wb_categories.xml";
        //    static XDocument _catsXml;
        //    static IEnumerable<XElement> _rules;


        //    //производители, для которых не выгружаем номера и артикулы
        //    readonly string[] _exceptManufactures = { "general motors","chery", "nissan" };

        public Wildberries() {
            _hc.BaseAddress = new Uri(_baseApiUrl);
            var apiKey = DB.GetParamStr("wb.apiKey");
            _hc.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            _url = DB.GetParamStr("wb.url");
            //_oldPriceProcent = DB.GetParamFloat("ozon.oldPriceProcent");
            _updateFreq = DB.GetParamInt("wb.updateFreq");

        }
        //главный метод синхронизации
        public async Task SyncAsync() {
            _bus = Class365API._bus;
            //        _catsXml = XDocument.Load(_rulesFile);
            //        _rules = _catsXml.Descendants("Rule");
            if (_categories == null)
                await GetCategoriesAsync();
            if (_objects == null)
                _objects = await GetObjectsAsync();
            if (_objectsJA == null) {
                var str = JsonConvert.SerializeObject(_objects);
                _objectsJA = JArray.Parse(str);
            }
            if (_colors == null)
                await GetColorsAsync();
            if (_countries == null)
                await GetCountriesAsync();

            //tests
            //if (_tnvd == null)
            //await GetTnvdAsync("7985");
            //await GetCharcsAsync("7985");

            //        await UpdateProductsAsync();
            await GetProductListAsync();
            //        //if (Class365API.SyncStartTime.Minute >= 55) {
            await AddProductsAsync();
            //        //}
            //        await CheckProductLinksAsync(checkAll: true);
        }
        //    public async Task MakeReserve() {
        //        try {
        //            //запросить список заказов со следующими статусами
        //            var statuses = new List<string> {
        //                "awaiting_packaging",
        //                "awaiting_deliver"
        //            };  
        //            Postings postings = new Postings();
        //            foreach (var status in statuses) {
        //                var data = new {
        //                    filter = new {
        //                        cutoff_from = DateTime.Now.AddDays(-1).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'"),       //"2024-02-24T14:15:22Z",
        //                        cutoff_to = DateTime.Now.AddDays(18).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'"),
        //                        warehouse_id = new int[] { },
        //                        status = status
        //                    },
        //                    limit = 100,
        //                    offset = 0,
        //                    with = new {
        //                        analytics_data = false,
        //                        barcodes = false,
        //                        financial_data = false,
        //                        translit = false
        //                    }
        //                };
        //                var result = await PostRequestAsync(data, "/v3/posting/fbs/unfulfilled/list");
        //                var res = JsonConvert.DeserializeObject<Postings>(result);
        //                postings.postings.AddRange(res.postings);
        //                Log.Add($"{_l} получено {res.postings.Count} заказов "+ status);
        //                postings.count += res.count;
        //            }
        //            //загружаем список заказов, для которых уже делали резервирование
        //            var reserveList = new List<string>();
        //            if (File.Exists(_reserveListFile)) {
        //                var s = File.ReadAllText(_reserveListFile);
        //                var l = JsonConvert.DeserializeObject<List<string>>(s);
        //                reserveList.AddRange(l);
        //            }
        //            //для каждого заказа сделать заказ с резервом в бизнес.ру
        //            foreach (var order in postings.postings) {
        //                //проверяем наличие резерва
        //                if (reserveList.Contains(order.posting_number))
        //                    continue;
        //                //готовим список товаров (id, amount)
        //                var goodsDict = new Dictionary<string, int>();
        //                order.products.ForEach(s => goodsDict.Add(s.offer_id, s.quantity));
        //                var isResMaked = await Class365API.MakeReserve(Selen.Source.Ozon, $"Ozon order {order.posting_number}", 
        //                                                               goodsDict, order.in_process_at.AddHours(3).ToString());
        //                if (isResMaked) {
        //                    reserveList.Add(order.posting_number);
        //                    if (reserveList.Count > 1000) {
        //                        reserveList.RemoveAt(0);
        //                    }
        //                    var s = JsonConvert.SerializeObject(reserveList);
        //                    File.WriteAllText(_reserveListFile, s);
        //                }
        //            }
        //        } catch (Exception x) {
        //            Log.Add(_l + "MakeReserve - " + x.Message);
        //        }
        //    }
        //    //проверка всех карточек в бизнесе, которые изменились и имеют ссылку на озон
        //    private async Task UpdateProductsAsync() {
        //        try {
        //            foreach (var bus in _bus.Where(w => w.IsTimeUpDated()
        //                                             && !string.IsNullOrEmpty(w.ozon)
        //                                             && w.ozon.Contains("http"))) {
        //                await UpdateProductAsync(bus);
        //            }
        //        } catch (Exception x) {
        //            Log.Add(_l + "UpdateProductsAsync - " + x.Message);
        //        }
        //    }
        //    //расширенная информация о товаре
        //    private async Task<ProductInfo> GetProductInfoAsync(GoodObject bus) {
        //        try {
        //            var data = new { offer_id = bus.id };
        //            var s = await PostRequestAsync(data, "/v2/product/info");
        //            return JsonConvert.DeserializeObject<ProductInfo>(s);
        //        } catch (Exception x) {
        //            throw new Exception($"GetProductInfoAsync ошибка! name:{bus.name} message:{x.Message}");
        //        }
        //    }

        //    //проверка и обновление товара
        //    async Task UpdateProductAsync(GoodObject bus, ProductInfo productInfo = null) {
        //        try {
        //            if (productInfo == null)
        //                productInfo = await GetProductInfoAsync(bus);
        //            await UpdateProductStocks(bus, productInfo);
        //            await UpdateProductPriceAsync(bus, productInfo);
        //            await UpdateProduct(bus, productInfo);
        //        } catch (Exception x) {
        //            Log.Add($"{_l} UpdateProductAsync ошибка! name:{bus.name} message:{x.Message}");
        //        }
        //    }
        //    //обновление остатков товара на озон
        //    private async Task UpdateProductStocks(GoodObject bus, ProductInfo productInfo) {
        //        try {
        //            var amount = bus.Amount;
        //            if (!bus.New || amount < 0)
        //                amount = 0;
        //            if (amount == productInfo.GetStocks())
        //                return;
        //            //объект для запроса
        //            var data = new {
        //                stocks = new[] {
        //                    new {
        //                        product_id = productInfo.id,
        //                        stock = amount.ToString("F0"),
        //                        warehouse_id = 1020000901600000
        //                    }
        //                }
        //            };
        //            var s = await PostRequestAsync(data, "/v2/products/stocks");
        //            var res = JsonConvert.DeserializeObject<List<UpdateResult>>(s);
        //            if (res.First().updated) 
        //                Log.Add(_l + bus.name + " остаток обновлен! (" + amount + ")");
        //            else 
        //                throw new Exception(s);

        //        } catch (Exception x) {
        //            Log.Add($"{_l} UpdateProductStocks: ошибка обновления остатка {bus.name} - {x.Message}");
        //        }
        //    }
        //    //расчет цен с учетом наценки
        //    private int GetNewPrice(GoodObject b) {
        //        var weight = b.Weight;
        //        var d = b.GetDimentions();
        //        var length = d[0] + d[1] + d[2];
        //        //наценка 30% на всё
        //        int overPrice = (int) (b.Price * 0.30);
        //        //если наценка меньше 200 р - округляю
        //        if (overPrice < 200)
        //            overPrice = 200;
        //        //вес от 10 кг или размер от 100 -- наценка 1500 р
        //        if (overPrice < 1500 && (weight >= 10 || length >= 100))
        //            overPrice = 1500;
        //        //вес от 30 кг или размер от 150 -- наценка 2000 р
        //        if (overPrice < 2000 && (weight >= 30 || length >= 150))
        //            overPrice = 2000;
        //        //вес от 50 кг или размер более 200 -- наценка 3000 р

        //        if (overPrice < 3000 && (weight >= 50 || length >= 200))
        //            overPrice = 3000;
        //        return b.Price + overPrice;
        //    }
        //    //цена до скидки (старая)
        //    private int GetOldPrice(int newPrice) {
        //        return (int) (Math.Ceiling((newPrice * (1 + _oldPriceProcent / 100)) / 100) * 100);
        //    }
        //    //Проверка и обновление цены товара на озон
        //    async Task UpdateProductPriceAsync(GoodObject bus, ProductInfo productInfo) {
        //        try {
        //            var newPrice = GetNewPrice(bus);
        //            var oldPrice = GetOldPrice(newPrice);
        //            //проверяем цены на озоне
        //            if (newPrice != productInfo.GetPrice()
        //            || oldPrice != productInfo.GetOldPrice()) {
        //                //если отличаются - создаем запрос и обновляем
        //                var data = new {
        //                    prices = new[] {
        //                        new { 
        //                            /*offer_id = bus.id, */ 
        //                            product_id = productInfo.id,
        //                            old_price = oldPrice.ToString(),
        //                            price = newPrice.ToString()
        //                        }
        //                    }
        //                };
        //                var s = await PostRequestAsync(data, "/v1/product/import/prices");
        //                var res = JsonConvert.DeserializeObject<List<UpdateResult>>(s);
        //                if (res.First().updated) {
        //                    Log.Add(_l + bus.name + " (" + bus.Price + ") цены обновлены! ("
        //                               + newPrice + ", " + oldPrice + ")");
        //                } else {
        //                    Log.Add(_l + bus.name + " ошибка! цены не обновлены! (" + bus.Price + ")" + " >>> " + s);
        //                }
        //            }
        //        } catch (Exception x) {
        //            Log.Add(_l + " ошибка обновления цены! - " + x.Message);
        //        }
        //    }
        //запросы к api wb
        public async Task<string> GetRequestAsync(Dictionary<string, string> request, string apiRelativeUrl) {
            try {
                if (request != null) {
                    var qstr = QueryStringBuilder.BuildQueryString(request);
                    apiRelativeUrl += "?" + qstr;
                }
                HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod("GET"), apiRelativeUrl);
                var response = await _hc.SendAsync(requestMessage);
                _jsonResponse = await response.Content.ReadAsStringAsync();
                await Task.Delay(500);
                if (response.StatusCode == HttpStatusCode.OK) {
                    var responseObject = JsonConvert.DeserializeObject<WbResponse>(_jsonResponse);
                    if (responseObject.error)
                        Log.Add($"{_l} GetRequestAsync: ошибка - {responseObject.errorText}; {responseObject.additionalErrors}");
                    return JsonConvert.SerializeObject(responseObject.data);
                } else
                    throw new Exception($"{response.StatusCode} {response.ReasonPhrase} {response.Content} // {_jsonResponse}");
            } catch (Exception x) {
                Log.Add($"{_l} PostRequestAsync ошибка запроса! apiRelativeUrl:{apiRelativeUrl} request:{request} message:{x.Message}");
            }
            return null;
        }
        public async Task<bool> PostRequestAsync(object request, string apiRelativeUrl) {
            try {
                if (request == null)
                    return false;
                HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod("POST"), apiRelativeUrl);
                var httpContent = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                requestMessage.Content = httpContent;
                var response = await _hc.SendAsync(requestMessage);
                _jsonResponse = await response.Content.ReadAsStringAsync();
                await Task.Delay(500);
                if (response.StatusCode == HttpStatusCode.OK) {
                    var resp = JsonConvert.DeserializeObject<WbResponse>(_jsonResponse);
                    return !resp.error;
                } else
                    throw new Exception($"{response.StatusCode} {response.ReasonPhrase} {response.Content} // {_jsonResponse}");
            } catch (Exception x) {
                Log.Add($"{_l} PostRequestAsync ошибка запроса! apiRelativeUrl:{apiRelativeUrl} request:{request} message:{x.Message}");
            }
            return false;
        }
        //Родительские категории товаров
        public async Task GetCategoriesAsync() {
            var catsFile = @"..\data\wildberries\wb_categories.json";
            if (File.GetLastWriteTime(catsFile) < DateTime.Now.AddDays(-_updateFreq)) {
                var data = new Dictionary<string, string>();
                data["locale"] = "ru";
                var s = await GetRequestAsync(data, "/content/v2/object/parent/all");
                _categories = JsonConvert.DeserializeObject<List<WbCategory>>(s);
                File.WriteAllText(catsFile, s);
                Log.Add($"{_l} GetCategoriesAsync: получено категорий {_categories.Count}");
            } else {
                var s = File.ReadAllText(catsFile);
                try {
                    _categories = JsonConvert.DeserializeObject<List<WbCategory>>(s);
                } catch (Exception x) {
                    Log.Add(x.Message);
                }
            }
        }
        //Список предметов (подкатегорий)
        public async Task<List<WbObject>> GetObjectsAsync(string name = null, string parentID = "760") {
            var listWbObject = new List<WbObject>();
            var childObjectsFile = $@"..\data\wildberries\wb_{parentID}_kids.json";
            var lastWriteTime = File.GetLastWriteTime(childObjectsFile);
            if (lastWriteTime < DateTime.Now.AddDays(-_updateFreq)) {
                var limit = 1000;
                var data = new Dictionary<string, string>();
                data["limit"] = limit.ToString();
                data["locale"] = "ru";
                if (name != null)
                    data["name"] = name;
                else
                    data["parentID"] = parentID;
                for (int last = 0; ; last += limit) {
                    data["offset"] = last.ToString();
                    var s = await GetRequestAsync(data, "/content/v2/object/all");
                    var resultList = JsonConvert.DeserializeObject<List<WbObject>>(s);
                    listWbObject.AddRange(resultList);
                    if (resultList.Count < limit)
                        break;
                }
                var serializedWbObject = JsonConvert.SerializeObject(listWbObject);
                File.WriteAllText(childObjectsFile, serializedWbObject);
                Log.Add($"{_l} GetObjectsAsync: {name ?? parentID} - получено подкатегорий: {listWbObject.Count}");
            } else {
                var serializedWbObject = File.ReadAllText(childObjectsFile);
                try {
                    listWbObject.AddRange(JsonConvert.DeserializeObject<List<WbObject>>(serializedWbObject));
                } catch (Exception x) {
                    Log.Add($"{_l} GetObjectsAsync: ошибка - {x.Message}");
                }
            }
            return listWbObject;
        }
        //Характеристики предмета (подкатегории)
        public async Task<List<WbCharc>> GetCharcsAsync(string subjectId = null) {
            if (subjectId == null)
                return null;
            var listCharcs = new List<WbCharc>();
            var childWbCharcsFile = $@"..\data\wildberries\wb_{subjectId}_charcs.json";
            var lastWriteTime = File.GetLastWriteTime(childWbCharcsFile);
            if (lastWriteTime < DateTime.Now.AddDays(-_updateFreq)) {
                var data = new Dictionary<string, string>();
                data["locale"] = "ru";
                data["subjectId"] = subjectId;
                var s = await GetRequestAsync(data, $"/content/v2/object/charcs/{subjectId}");
                var resultList = JsonConvert.DeserializeObject<List<WbCharc>>(s);
                listCharcs.AddRange(resultList);
                File.WriteAllText(childWbCharcsFile, JsonConvert.SerializeObject(listCharcs));
                Log.Add($"{_l} GetCharcsAsync: {subjectId} - получено характеристик: {listCharcs.Count}");
            } else {
                var s = File.ReadAllText(childWbCharcsFile);
                try {
                    listCharcs.AddRange(JsonConvert.DeserializeObject<List<WbCharc>>(s));
                } catch (Exception x) {
                    Log.Add($"{_l} GetCharcsAsync: ошибка - {x.Message}");
                }
            }
            return listCharcs;
        }
        //Цвет
        public async Task GetColorsAsync() {
            var fileName = @"..\data\wildberries\wb_colors.json";
            if (File.GetLastWriteTime(fileName) < DateTime.Now.AddDays(-_updateFreq)) {
                var data = new Dictionary<string, string>();
                data["locale"] = "ru";
                var s = await GetRequestAsync(data, "/content/v2/directory/colors");
                _colors = JsonConvert.DeserializeObject<List<WbColor>>(s);
                File.WriteAllText(fileName, s);
                Log.Add($"{_l} GetCategoriesAsync: получено цветов: {_colors.Count}");
            } else {
                var s = File.ReadAllText(fileName);
                try {
                    _colors = JsonConvert.DeserializeObject<List<WbColor>>(s);
                } catch (Exception x) {
                    Log.Add($"{_l} GetColorsAsync: ошибка - {x.Message}");
                }
            }
        }
        //Страна Производства
        public async Task GetCountriesAsync() {
            var fileName = @"..\data\wildberries\wb_countries.json";
            if (File.GetLastWriteTime(fileName) < DateTime.Now.AddDays(-_updateFreq)) {
                var data = new Dictionary<string, string>();
                data["locale"] = "ru";
                var s = await GetRequestAsync(data, "/content/v2/directory/countries");
                _countries = JsonConvert.DeserializeObject<List<WbCountry>>(s);
                File.WriteAllText(fileName, s);
                Log.Add($"{_l} GetCountriesAsync: получено стран: {_countries.Count}");
            } else {
                var s = File.ReadAllText(fileName);
                try {
                    _countries = JsonConvert.DeserializeObject<List<WbCountry>>(s);
                } catch (Exception x) {
                    Log.Add($"{_l} GetCountriesAsync: ошибка - {x.Message}");
                }
            }
        }
        //ТНВЭД код
        public async Task GetTnvdAsync(string subjectID = null) {
            if (subjectID == null)
                return;
            var fileName = $@"..\data\wildberries\wb_tnvd_{subjectID}.json";
            if (File.GetLastWriteTime(fileName) < DateTime.Now.AddDays(-_updateFreq)) {
                var data = new Dictionary<string, string>();
                data["locale"] = "ru";
                data["subjectID"] = subjectID;
                var s = await GetRequestAsync(data, "/content/v2/directory/tnved");
                _tnvd = JsonConvert.DeserializeObject<List<WbTnvd>>(s);
                File.WriteAllText(fileName, s);
                Log.Add($"{_l} GetTnvdAsync: {subjectID} - получено кодов ТНВЭД: {_tnvd.Count}");
            } else {
                var s = File.ReadAllText(fileName);
                try {
                    _tnvd = JsonConvert.DeserializeObject<List<WbTnvd>>(s);
                } catch (Exception x) {
                    Log.Add($"{_l} GetTnvdAsync: ошибка - {x.Message}");
                }
            }
        }
        //    //обновление описаний товаров
        //    private async Task UpdateProduct(GoodObject good, ProductInfo productInfo) {
        //        try {
        //            if (!good.New)
        //                throw new Exception("товар стал Б/У!!");
        //            //проверяем группу товара
        //            var attributes = await GetAttributesAsync(good);
        //            //Запрашиваю атрибуты товара с озон
        //            var productFullInfo = await GetProductFullInfoAsync(productInfo);
        //            File.WriteAllText(@"..\data\ozon\product_" + productFullInfo.First().offer_id + ".json",
        //                JsonConvert.SerializeObject(productFullInfo));                //формирую объект запроса
        //            if (attributes.categoryId == "111") {
        //                _isProductListCheckNeeds = true;
        //                return;
        //            }
        //            if (attributes.typeId == "0")
        //                return;
        //            var data = new {
        //                items = new[] {
        //                    new{
        //                        attributes = new List<Attribute>(),
        //                        name = good.NameLimit(_nameLimit),
        //                        currency_code="RUB",
        //                        offer_id=good.id,
        //                        description_category_id=attributes.categoryId,
        //                        //category_id=productFullInfo[0].category_id,
        //                        price = GetNewPrice(good).ToString(),
        //                        old_price = GetOldPrice(GetNewPrice(good)).ToString(),
        //                        weight = (int)(good.Weight*1000),                  //Вес с упаковкой, г
        //                        weight_unit = "g",
        //                        depth = int.Parse(good.width)*10,                       //глубина, мм
        //                        height = int.Parse(good.height)*10,                     //высота, мм
        //                        width = int.Parse(good.length)*10,                      //высота, мм
        //                        dimension_unit = "mm",
        //                        primary_image = good.images.First().url,                //главное фото
        //                        images = good.images.Skip(1).Take(14).Select(_=>_.url).ToList(),
        //                        vat="0.0"                                               //налог
        //                    }
        //                }
        //            };
        //            //переносим в объект запроса атрибуты из товара озон, которые уже есть
        //            foreach (var item in productFullInfo[0].attributes) {
        //                //Пропускаю некоторые атрибуты
        //                if (item.id != 4180 &&                                          //Название
        //                item.id != 9024 &&                                              //Артикул
        //                item.id != 9048 &&                                              //Название модели (для объединения в одну карточку)
        //                item.id != 4191 &&                                              //Аннотация
        //                item.id != 22387 &&                                             //Группа товара

        //                //item.id != 85 &&                                                //Бренд (теперь используем Без бренда,
        //                                                                                //убрать если нужно сохранять бренд, уже установленных в товарах)
        //                item.values.Length > 0) {
        //                    var values = new Value {
        //                        value = item.values[0].value,
        //                        dictionary_value_id = item.values[0].dictionary_value_id
        //                    };
        //                    //если такой атрибут уже есть - удаляю из списка и добавляю
        //                    var i = data.items[0].attributes.Find(a => a.id == item.id);
        //                    if (i != null)
        //                        data.items[0].attributes.Remove(i);
        //                    data.items[0].attributes.Add(new Attribute {
        //                        id = item.id,
        //                        complex_id = item.complex_id,
        //                        values = new Value[] { values }
        //                    });
        //                }
        //            }
        //            //теперь добавлю характеристики из карточки
        //            if (attributes.additionalAttributes != null && attributes.additionalAttributes.Count > 0)
        //                foreach (var item in attributes.additionalAttributes) {
        //                    if (item.id == 9782)                                        //Класс опасности (невозможно поменять!)
        //                        continue;
        //                    var values = new Value {
        //                        value = item.values[0].value,
        //                        dictionary_value_id = item.values[0].dictionary_value_id
        //                    };
        //                    //если такой атрибут уже есть - удаляю из списка и добавляю
        //                    var i = data.items[0].attributes.Find(a => a.id == item.id);
        //                    if (i != null) {
        //                        //проверяю бренд
        //                        if (i.id == 85 && i.values[0].value != item.values[0].value) {
        //                            Log.Add(_l + good.name + " - заменить бренд в бизнесе! - " + item.values[0].value + " на " + i.values[0].value);
        //                            //временно пропускаю карточку, чтобы не слетать на модерацию по бренду
        //                            continue;
        //                        }
        //                        data.items[0].attributes.Remove(i);
        //                    }
        //                    data.items[0].attributes.Add(new Attribute {
        //                        id = item.id,
        //                        complex_id = item.complex_id,
        //                        values = new Value[] { values }
        //                    });
        //                }
        //            var s = await PostRequestAsync(data, "/v3/product/import");
        //            var res = JsonConvert.DeserializeObject<ProductImportResult>(s);
        //            if (res.task_id != default(int)) {
        //                Log.Add($"{_l} UpdateProduct: id:{good.id} {good.name} - товар отправлен на озон!");
        //            } else {
        //                Log.Add($"{_l} UpdateProduct: id:{good.id} {good.name} - ошибка отправки товара на озон! - {s}");
        //            }
        //            ////
        //            var res2 = new ProductImportInfo();
        //            await Task.Delay(10000);
        //            var data2 = new {
        //                task_id = res.task_id.ToString()
        //            };
        //            s = await PostRequestAsync(data2, "/v1/product/import/info");
        //            res2 = JsonConvert.DeserializeObject<ProductImportInfo>(s);
        //            Log.Add(_l + good.name + " status товара - " + res2.items.First().status);
        //            if (res2.items.First().errors.Length > 0)
        //                Log.Add(_l + good.name + " ошибка - " + s);
        //            ////
        //        } catch (Exception x) {
        //            Log.Add($"{_l} UpdateProduct: ошибка обновления описания {good.id} {good.name} - {x.Message}");
        //        }
        //    }
        //добавление новых товаров на wb
        async Task AddProductsAsync() {
            var count = await DB.GetParamIntAsync("wb.countToAdd");
            if (count == 0)
                return;
            if (_isProductListCheckNeeds)
                await GetProductListAsync();
            //список исключений
            var exceptionGoods = new List<string> {
                    "фиксаторы грм",
                    "фиксаторы распредвалов",
                    "набор фиксаторов",
                    "бампер ",
                    "капот ",
                    "телевизор "
                };
            var exceptionGroups = new List<string> {
                    "черновик"
                };
            //список карточек которые еще не добавлены на wb
            var goods = _bus.Where(w => w.Amount > 0
                                     && w.Price > 0
                                     && w.Part != null
                                     && w.images.Count > 0
                                     && w.height != null
                                     && w.length != null
                                     && w.width != null
                                     && w.New
                                     && !w.wb.Contains("http")
                                     //&& !_productList.Any(_ => w.id == _.offer_id)
                                     && !exceptionGoods.Any(e => w.name.ToLowerInvariant().Contains(e))
                                     && !exceptionGroups.Any(e => w.GroupName().ToLowerInvariant().Contains(e)));
            //SaveToFile(goods);
            var goods2 = _bus.Where(w => w.Amount > 0
                                     && w.Price > 0
                                     && w.images.Count > 0
                                     && w.New
                                     && !w.wb.Contains("http")
                                     //&& !_productList.Any(_ => w.id == _.offer_id)
                                     && !exceptionGoods.Any(e => w.name.ToLowerInvariant().Contains(e))); //нет в исключениях
            SaveToFile(goods2, @"..\data\wildberries\wb_listForAdding.csv");
            Log.Add($"{_l} карточек для добавления: {goods.Count()} ({goods2.Count()})");
            foreach (var good in goods) {
                try {
                    var attributes = await GetAttributesAsync(good);
                    if (attributes.Count == 0)
                        continue;
                    //формирую объект запроса
                    var data = new[] {
                        new {
                            subjectID = attributes.First().subjectID,
                            variants = new[] {
                                new {
                                    title = good.NameLimit(_nameLimit),
                                    description = good.DescriptionList(5000).Aggregate((a,b)=>a+"\n"+b),
                                    vendorCode = good.id,
                                    brand=good.GetManufacture(),
                                    dimensions = new {
                                          length = int.Parse(good.length),
                                          width = int.Parse(good.width),
                                          height = int.Parse(good.height)
                                    },
                                    characteristics = new List<object>()
                                }
                            }
                        }
                    };
                    foreach (var a in attributes.Skip(1)) {
                        if (a.charcType == "1")
                            data[0].variants[0].characteristics.Add(new { id = a.charcID, value = a.value.ToString() });
                        else if (a.charcType == "2")
                            data[0].variants[0].characteristics.Add(new { id = a.charcID, value = a.value as string[] });
                        else if (a.charcType == "4")
                            data[0].variants[0].characteristics.Add(new { id = a.charcID, value = (float) a.value });  //todo валидно??
                            //data[0].variants[0].characteristics.Add(new { id = a.charcID, value = (int) a.value });
                    }

                    var res = await PostRequestAsync(data, "/content/v2/cards/upload");

                    if (res) { 
                        Log.Add(_l + good.name + " - товар отправлен на wildberries!");
                        await SaveUrlAsync(good);
                        _isProductListCheckNeeds = true;
                        count--;
                    } else {
                        Log.Add(_l + good.name + " ошибка отправки товара на wildberries!");
                    }

                    //s = await PostRequestAsync(data2, "/v1/product/import/info");
                    //res2 = JsonConvert.DeserializeObject<ProductImportInfo>(s);
                    //Log.Add(_l + good.name + " status товара - " + res2.items.First().status);
                    //if (res2.items.First().errors.Length > 0)
                    //    Log.Add(_l + good.name + " ошибка - " + s);
                } catch (Exception x) {
                    Log.Add(_l + good.name + " - " + x.Message + x.InnerException?.Message);
                }
                if (count == 0)
                    break;
            }
        }
        //получить атрибуты и категорию товара 
        async Task<List<WbCharc>> GetAttributesAsync(GoodObject bus) {
            try {
                var n = bus.name.ToLowerInvariant();
                var a = new List<WbCharc>();

                //foreach (var rule in _rules) {
                //    var conditions = rule.Elements();
                //    var eq = true;
                //    foreach (var condition in conditions) {
                //        if (!eq)
                //            break;
                //        if (condition.Name == "Starts" && !n.StartsWith(condition.Value))
                //            eq = false;
                //        else if (condition.Name == "Contains" && !n.Contains(condition.Value))
                //            eq = false;
                //    }
                //    if (eq)
                //        a.typeName = GetTypeName(rule);
                //}
                //if (a.typeName == null) {
                //    if ((n.Contains("генератор") || n.Contains("напряжен")) &&
                //        (n.Contains("реле") || n.Contains("регулятор"))) {
                //        a.typeName = "Регулятор напряжения генератора";
                //    } else if (n.Contains("генератор") &&
                //        n.StartsWith("болт ")) {
                //        a.typeName = "Регулятор генератора";
                //    } else

                if (n.StartsWith("стартер "))  //todo вынести определение категорий в xml
                    a.Add(new WbCharc { subjectID = 7985 });                   //"Стартеры электрические"
                else
                    return a;

                //if (!GetTypeIdAndCategoryId(a))
                //                return a;

                await GetManufactureCountry(bus, a);
                await GetWeight(bus, a);
                await GetPart(bus, a);
                await GetColor(bus, a);
                return a;
            } catch (Exception x) {
                throw new Exception($"GetAttributesAsync: {bus.name} - {x.Message}");
            }
        }
        //Атрибут Страна-изготовитель
        async Task GetManufactureCountry(GoodObject good, List<WbCharc> a) {
            var value = good.GetManufactureCountry();
            if (value == null || !_countries.Any(c => c.name != value))
                return;
            a.Add(new WbCharc {
                charcID = 14177451,
                value = value,
                charcType = "1"
            });
        }
        //Атрибут Вес с упаковкой  (кг)
        async Task GetWeight(GoodObject good, List<WbCharc> a) {
            a.Add(new WbCharc {
                charcID = 88953,
                value = good.Weight,                //todo дробное число валидно?
                //value = (int) good.Weight,
                charcType = "4"
            });
        }
        //Атрибут Артикул производителя
        async Task GetPart(GoodObject good, List<WbCharc> a) {
            a.Add(new WbCharc {
                charcID = 5522881,
                value = good.Part,
                charcType = "1"
            });
        }
        //Атрибут Цвет
        async Task GetColor(GoodObject good, List<WbCharc> a) {
            //todo !перед добавлением цвета нужно проверить, входит ли атрибут цвет в список доступных атрибутов для подкатегории, а возможно и для всех атрибутов нужно также?
            //a.Add(new WbCharc {        
            //    charcID = 5522881,
            //    value = good.Part,
            //    charcType = "1"
            //});
        }

        //    //Атрибут Код ТН ВЭД 
        //    async Task<Attribute> GetTNVEDAttribute(GoodObject good, Attributes a) {
        //        var value = good.hscode_id;
        //        if (value == null)
        //            return null;
        //        var tnved = await GetAttibuteValuesAsync(a.typeId, attribute_id: "22232");
        //        //await UpdateVedAsync(a.typeId);
        //        var code = tnved.Find(f => f.value.Contains(value));
        //        if (code == null || code.id == null || code.value == null) {
        //            Log.Add($"{_l} GetTNVEDAttribute: ошибка - ТНВЭД {good.hscode_id} не найден! наименование: {good.name}, id: {good.id}");
        //            return null;
        //        }
        //        return new Attribute {
        //            complex_id = 0,
        //            id = 22232,
        //            values = new Value[] {
        //                new Value {
        //                    value = code.value,
        //                    dictionary_value_id = code.id.ToString()
        //                }
        //            }
        //        };
        //    }
        //    //Атрибут Бренд
        //    Attribute GetBrendAttribute(GoodObject bus) {
        //        int id;
        //        string name;
        //        //var m = bus.GetManufacture(ozon: true)?.ToLowerInvariant() ?? "";
        //        //if (m == "vag") {
        //        //    id = 115840909;
        //        //    name = "VAG (VW/Audi/Skoda/Seat)";
        //        //} else if (m == "chery") {
        //        //    id = 0;
        //        //    name = "Нет бренда";
        //        //} else

        //        //if (m== "chery" && _brends.Any(a => a.value.ToLowerInvariant() == m)) {
        //        //    var attribute = _brends.Find(a => a.value.ToLowerInvariant() == m);
        //        //    id = attribute.id;
        //        //    name = attribute.value;
        //        //} else 
        //        {
        //            id = 0;
        //            name = "Нет бренда";
        //        }
        //        return new Attribute {
        //            complex_id = 0,
        //            id = 85,
        //            values = new Value[] {
        //                new Value{
        //                    //dictionary_value_id = 0,//126745801 //id,
        //                    dictionary_value_id = id.ToString(),
        //                    value = name
        //                }
        //            }
        //        };
        //    }
        //    //Атрибут Партномер (артикул производителя) (в нашем случае артикул)
        //    Attribute GetPartAttribute(GoodObject bus) {
        //        var man = bus.GetManufacture(true)?
        //                     .ToLowerInvariant();
        //        var part = _exceptManufactures.Contains(man)? bus.id
        //                                                    : bus.part;
        //        return new Attribute {
        //            complex_id = 0,
        //            id = 7236,
        //            values = new Value[] {
        //                new Value{
        //                    //value = bus.Part
        //                    value = part
        //                }
        //            }
        //        };
        //    }

        //    //Запрос списка атрибутов с озон (значения по умолчанию - для получения списка брендов
        //    public async Task<List<AttributeValue>> GetAttibuteValuesAsync(string type_id, string attribute_id = "85") {
        //        var attributeValuesFile = @"..\data\ozon\ozon_attr_" + attribute_id + "_type_id_" + type_id + ".json";
        //        var lastWriteTime = File.GetLastWriteTime(attributeValuesFile);
        //        if (lastWriteTime.AddDays(_updateFreq) > DateTime.Now) {
        //            var res = JsonConvert.DeserializeObject<List<AttributeValue>>(
        //                File.ReadAllText(attributeValuesFile));
        //            //Log.Add(_l + " загружено с диска " + res.Count + " атрибутов");
        //            return res;
        //        } else {
        //            var category_id = GetDescriptionCategoryId(type_id);
        //            var res = new List<AttributeValue>();
        //            var last = 0;
        //            do {
        //                var data = new {
        //                    attribute_id = attribute_id,
        //                    description_category_id = category_id,
        //                    language = "DEFAULT",
        //                    last_value_id = last,
        //                    limit = 1000,
        //                    type_id = type_id
        //                };
        //                var s = await PostRequestAsync(data, "/v1/description-category/attribute/values");
        //                res.AddRange(JsonConvert.DeserializeObject<List<AttributeValue>>(s));
        //                last = res.Last()?.id ?? 0;
        //            } while (_hasNext);
        //            File.WriteAllText(attributeValuesFile, JsonConvert.SerializeObject(res));
        //            //Log.Add(_l + " загружено с озон " + res.Count + " атрибутов");
        //            return res;
        //        }
        //    }



        //    string GetDescriptionCategoryId(string type_id) {
        //        var token = "..type_id";
        //        foreach (JToken type in _categoriesJO.SelectTokens(token)) {
        //            if ((string)type == type_id) {
        //                string categoryId = (string) type.Parent.Parent.Parent.Parent.Parent["description_category_id"];
        //                return categoryId;
        //            }
        //        }
        //        Log.Add(_l + "ОШИБКА - КАТЕГОРИЯ НЕ НАЙДЕНА! - type_id: " + type_id);
        //        return "";
        //    }
        //    bool GetTypeIdAndCategoryId(Attributes a) {
        //        var token = "..type_name";
        //        foreach (JToken type in _categoriesJO.SelectTokens(token)) {
        //            if ((string) type == a.typeName) {
        //                a.typeId = (string) type.Parent.Parent["type_id"];
        //                a.categoryId = (string) type.Parent.Parent.Parent.Parent.Parent["description_category_id"];
        //                return true;
        //            }
        //        }
        //        Log.Add(_l + "ОШИБКА - КАТЕГОРИЯ НЕ НАЙДЕНА! - typeName: " + a.typeName);
        //        return false;
        //    }

        //    //Запрос атрибутов существующего товара озон
        //    private async Task<List<ProductsInfoAttr>> GetProductFullInfoAsync(ProductInfo productInfo) {
        //        try {
        //            var data = new {
        //                filter = new {
        //                    product_id = new[] { productInfo.id.ToString() },
        //                    visibility = "ALL"
        //                },
        //                limit = 100
        //            };
        //            var s = await PostRequestAsync(data, "/v3/products/info/attributes");
        //            s = s.Replace("attribute_id", "id");
        //            return JsonConvert.DeserializeObject<List<ProductsInfoAttr>>(s);
        //        } catch (Exception x) {
        //            Log.Add(_l + " ошибка - " + x.Message + x.InnerException?.Message);
        //            throw;
        //        }
        //    }

        //обновление ссылки в карточке бизнес.ру
        async Task SaveUrlAsync(GoodObject bus) {
            try {
                string newUrl = _baseBusUrl;
                //проверяем ссылку, если она есть, то значит товар был отправлен на вб
                if (bus.wb.Length > 5) {
                    //doto нужен поиск nmID по объявлениям
                    //var nmId = GetNmId(bus);
                    //if (nmId.length>0){
                    //newUrl = _baseBusUrl.Replace("/00000/","/" + nmId +"/") ;
                }
                if (bus.wb != newUrl) {
                    bus.wb = newUrl;
                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                                {"id", bus.id},
                                {"name", bus.name},
                                {_url, bus.wb}
                            });
                    Log.Add(_l + bus.name + " ссылка на товар обновлена!");
                } else
                    Log.Add(_l + bus.name + " ссылка без изменений!");
            } catch (Exception x) {
                Log.Add($"{_l} SaveUrlAsync ошибка! name:{bus.name} message:{x.Message}");
            }
        }

        //запрашиваем список товаров WB
        public async Task GetProductListAsync() {
            try {                
                //если файл свежий и товары не добавляли - загружаем с диска
                if (File.Exists(_productListFile) &&
                    (DateTime.Now < File.GetLastWriteTime(_productListFile).AddDays(_updateFreq))
                    && !_isProductListCheckNeeds) {
                    if (_productCards.Count == 0) {
                        _productCards = JsonConvert.DeserializeObject<List<WbCard>>(
                            File.ReadAllText(_productListFile));
                    }
                } else {
                    _productCards.Clear();

                    var limit = 100;
                    var total = 0;
                    var data = new WbProductReqData {
                        settings = new WbProductReqSettings {
                            cursor = new {
                                limit = limit,
                            },
                            filter = new {
                                withPhoto = -1
                            }
                        }
                    };
                    do {
                        var isSuc = await PostRequestAsync(data, "/content/v2/get/cards/list");
                        if (!isSuc)
                            throw new Exception("запрос товаров вернул ошибку!");
                        var productList = JsonConvert.DeserializeObject<WbProductList>(_jsonResponse);                        
                        _productCards.AddRange(productList.cards);
                        Log.Add($"{_l} получено {_productCards.Count} товаров");
                        //добавляем пагинацию в объект запроса
                        data.settings.cursor = new {
                            limit = limit,
                            updatedAt = productList.cursor.updatedAt,
                            nmID = productList.cursor.nmID
                        };
                        total = productList.cursor.total;
                    } while (total == limit);
                    Log.Add(_l + "ProductList - успешно загружено " + _productCards.Count + " товаров");
                    File.WriteAllText(_productListFile, JsonConvert.SerializeObject(_productCards));
                    Log.Add(_l + _productListFile + " - сохранено");
                    _isProductListCheckNeeds = false; //сбрасываю флаг
                }
                await CheckProductLinksAsync();
            } catch (Exception x) {
                Log.Add(_l + "ProductList - ошибка загрузки товаров - " + x.Message);
            }
        }

        //проверяем привязку товаров в карточки бизнес.ру
        private async Task CheckProductLinksAsync(bool checkAll = false) {
            //TODO реализовать поверку ссылок на карточки вб

            //List<ProductListItem> items;
            //if (checkAll) {
            //    var _checkProductCount = await DB.GetParamIntAsync("ozon.checkProductCount");
            //    var _checkProductIndex = await DB.GetParamIntAsync("ozon.checkProductIndex");
            //    if (_checkProductIndex >= _productList.Count)
            //        _checkProductIndex = 0;
            //    await DB.SetParamAsync("ozon.checkProductIndex", (_checkProductIndex + _checkProductCount).ToString());
            //    items = _productList.Skip(_checkProductIndex).Take(_checkProductCount).ToList<ProductListItem>();
            //} else
            //    items = _productList;
            //foreach (var item in items) {
            //    try {
            //        //карточка в бизнес.ру с id = артикулу товара на озон
            //        var b = Class365API._bus.FindIndex(_ => _.id == item.offer_id);
            //        if (b == -1)
            //            throw new Exception("карточка бизнес.ру с id = " + item.offer_id + " не найдена!");
            //        if (!Class365API._bus[b].ozon.Contains("http") ||                       //если карточка найдена,но товар не привязан к бизнес.ру
            //            Class365API._bus[b].ozon.Split('/').Last().Length < 3 ||            //либо ссылка есть, но неверный sku
            //            checkAll) {                                                         //либо передали флаг проверять всё
            //            var productInfo = await GetProductInfoAsync(Class365API._bus[b]);
            //            await SaveUrlAsync(Class365API._bus[b], productInfo);
            //            await UpdateProductAsync(Class365API._bus[b], productInfo);
            //        }
            //    } catch (Exception x) {
            //        Log.Add($"{_l} CheckGoodsAsync ошибка! checkAll:{checkAll} offer_id:{item.offer_id} message:{x.Message}");
            //        _isProductListCheckNeeds = true;
            //    }
            //}
        }


        //Сохранение списка карточек, которые можно добавить на wb в виде таблицы
        void SaveToFile(IEnumerable<GoodObject> goods, string fname) {
            StringBuilder s = new StringBuilder();
            var splt = "\t";
            s.Append("id");
            s.Append(splt);
            s.Append("part");
            s.Append(splt);
            s.Append("name");
            s.Append(splt);
            s.Append("GroupName");
            s.Append(splt);
            s.Append("Amount");
            s.Append(splt);
            s.Append("price");
            s.Append(splt);
            s.Append("weight");
            s.Append(splt);
            s.Append("Width");
            s.Append(splt);
            s.Append("length");
            s.Append(splt);
            s.Append("Height");
            s.Append(splt);
            s.Append("GetManufacture");
            s.Append(splt);
            s.Append("images");
            s.Append(splt);
            s.Append("Description");
            s.AppendLine(splt);
            foreach (var good in goods) {
                s.Append(good.id);
                s.Append(splt);
                s.Append(good.Part);
                s.Append(splt);
                s.Append(good.name);
                s.Append(splt);
                s.Append(good.GroupName());
                s.Append(splt);
                s.Append(good.Amount);
                s.Append(splt);
                s.Append(good.Price);
                s.Append(splt);
                s.Append(good.weight);
                s.Append(splt);
                s.Append(good.width);
                s.Append(splt);
                s.Append(good.length);
                s.Append(splt);
                s.Append(good.height);
                s.Append(splt);
                s.Append(good.GetManufacture());
                s.Append(splt);
                s.Append(good.images.Select(g => g.url).Aggregate((a, b) => a + " " + b));
                s.Append(splt);
                s.Append(good.description);
                s.AppendLine(splt);
            }
            File.WriteAllText(fname, s.ToString(), Encoding.UTF8);
            Log.Add($"товары выгружены в {fname}");
        }
        //    //список складов
        //    async Task GetWarehouseList() {
        //        try {
        //            var data = new { };
        //            var s = await PostRequestAsync(data, "/v1/warehouse/list");
        //            File.WriteAllText(_warehouseList, s);
        //        } catch (Exception x) {
        //            Log.Add($"{_l} GetWarehouseList: ошибка запроса списка складов! - {x.Message}");
        //        }
        //    }
        //}

        //    string GetTypeName(XElement rule) {
        //        var parent = rule.Parent;
        //        if (parent != null) {
        //            return parent.Attribute("Name").Value;
        //        }
        //        return null;
        //    }

        ///////////////////////////////////////
        ///// классы для работы с запросами ///
        ///////////////////////////////////////
        public class WbProductList {
            public List<WbCard> cards = new List<WbCard>(); //Список КТ
            public WbCursor cursor { get; set; } //Пагинатор
        }

        public class WbCard {
            public string nmID { get; set; } //Артикул WB
            public string imtID { get; set; } //Идентификатор КТ
            public string nmUUID { get; set; } //Внутренний технический идентификатор товара
            public string subjectID { get; set; } //Идентификатор предмета
            public string vendorCode { get; set; } //Артикул продавца
            public string subjectName { get; set; } //Название предмета
            public string brand { get; set; } //Бренд
            public string title { get; set; } //Наименование товара
            public List<WbPhoto> photos = new List<WbPhoto>(); //Массив фото
            public string video { get; set; } //URL видео

            public Dimensions dimensions; //Габариты упаковки товара, см
            public List<WbCharacteristic> characteristics = new List<WbCharacteristic>();
            public string createdAt { get; set; } //Дата создания
            public string updatedAt { get; set; } //Дата изменения

        }
        public class WbCharacteristic {
            public string id { get; set; } //Идентификатор характеристики
            public string name { get; set; } //Название характеристики
            public object value { get; set; } //Значение характеристики. Тип значения зависит от типа характеристики

        }

        public class Dimensions {
            public int length { get; set; } //Длина, см
            public int width { get; set; } //Ширина, см
            public int height { get; set; } //Высота, см
            public bool isValid { get; set; } //корректность габаритов

        }
        public class WbPhoto {
            public string big { get; set; } //URL фото 900х1200
            public string c246x328 { get; set; } //URL фото 248х328
            public string c516x688 { get; set; } //URL фото 516x688
            public string square { get; set; } //URL фото 600х600
            public string tm { get; set; } //URL фото 75х100
        }

        public class WbCursor {
            public string updatedAt { get; set; } //Дата с которой надо запрашивать следующий список КТ
            public int nmID { get; set; } //Номер Артикула WB с которой надо запрашивать следующий список КТ
            public int total { get; set; } //Кол-во возвращенных КТ
        }

        public class WbResponse {
            public object data { get; set; }
            public bool error { get; set; }
            public string errorText { get; set; }
            public object additionalErrors { get; set; }
        }
        public class WbCharc {
            public int charcID { get; set; }
            public string subjectName { get; set; }
            public int subjectID { get; set; }
            public string name { get; set; }
            public string required { get; set; }
            public string unitNamed { get; set; }
            public string maxCount { get; set; }
            public string popular { get; set; }
            public string charcType { get; set; }
            public object value { get; set; }
        }
        public class WbColor {
            public string name { get; set; }
            public string parentName { get; set; }
        }
        public class WbCountry {
            public string name { get; set; }
            public string fullName { get; set; }
        }
        public class WbTnvd {
            public string tnved { get; set; }
            public bool isKiz { get; set; }
        }
        public class WbObject {
            public string subjectID { get; set; }
            public string parentID { get; set; }
            public string subjectName { get; set; }
            public string parentName { get; set; }
        }
        public class WbCategory {
            public string name { get; set; }
            public string id { get; set; }
            //public int id { get; set; }
            public bool isVisible { get; set; }
        }

        public class WbProductReqData {
            public WbProductReqSettings settings { get; set; }
        }
        public class WbProductReqSettings {
            public Object filter {get; set;}
            public Object cursor {get; set;} 
        }

        ///////////////////////////////////////////
        //public class ProductImportResult {
        //    public int task_id { get; set; }
        //}
        ///////////////////////////////////////////
        //public class ProductImportInfo {
        //    public Items[] items { get; set; }
        //    public int total { get; set; }

        //}
        //public class Items {
        //    public string offer_id { get; set; }
        //    public int product_id { get; set; }
        //    public string status { get; set; }
        //    public Errors[] errors { get; set; }
        //}
        //public class Errors {
        //    public string code { get; set; }
        //    public string field { get; set; }
        //    public int attribute_id { get; set; }
        //    public string state { get; set; }
        //    public string level { get; set; }
        //    public string description { get; set; }
        //    //public Optional_Description_Elements optional_description_elements { get; set; }
        //    public string attribute_name { get; set; }
        //    public string message { get; set; }
        //}
        //public class WareHouse {
        //    public string warehouse_id;
        //    public string name;
        //    public string status;
        //}
        //public class Postings {
        //    public List<Posting> postings = new List<Posting>();
        //    public int count;
        //}
        //public class Posting {
        //    public string posting_number;
        //    public decimal order_id;
        //    public string order_number;
        //    public List<Product> products = new List<Product>();
        //    public DateTime in_process_at;
        //}
        //public class Product {
        //    public string offer_id;
        //    public string name;
        //    public decimal sku;
        //    public int quantity;
        //}
        ///////////////////////////////////////////
        //    public class WbAttributes {
        //    public WbCharc charc;
        //    public string strValue;
        //    public int intValue;
        //    public string typeId;
        //    public string typeName;
        //    public List<Attribute> additionalAttributes = new List<Attribute>();
        //}
        //public static class Extentions {
        //    public static void AddAttribute(this List<Attribute> list, Attribute newAttr) {
        //        if (newAttr != null)
        //            list.Add(newAttr);
        //    }
        //}
        ///////////////////////////////////////////
        //public class ProductsInfo {
        //    public List<ProductInfo> items { get; set; }
        //}
        //public class ProductInfo {
        //    public int id { get; set; }
        //    public string name { get; set; }
        //    public string offer_id { get; set; }
        //    public string barcode { get; set; }
        //    public string buybox_price { get; set; }
        //    public int category_id { get; set; }
        //    public DateTime created_at { get; set; }
        //    public string[] images { get; set; }
        //    public string marketing_price { get; set; }
        //    public string min_ozon_price { get; set; }
        //    public string old_price { get; set; }
        //    public string premium_price { get; set; }
        //    public string price { get; set; }
        //    public string recommended_price { get; set; }
        //    public string min_price { get; set; }
        //    public Source[] sources { get; set; }
        //    public Stocks stocks { get; set; }
        //    //        public ItemErrors[] errors { get; set; }
        //    public string vat { get; set; }
        //    public bool visible { get; set; }
        //    public Visibility_Details visibility_details { get; set; }
        //    public string price_index { get; set; }
        //    //        public Commission[] commissions { get; set; }
        //    public decimal volume_weight { get; set; }
        //    public bool is_prepayment { get; set; }
        //    public bool is_prepayment_allowed { get; set; }
        //    //public object[] images360 { get; set; }
        //    public string color_image { get; set; }
        //    public string primary_image { get; set; }
        //    public Status status { get; set; }
        //    public string state { get; set; }
        //    public string service_type { get; set; }
        //    public int fbo_sku { get; set; }
        //    public int fbs_sku { get; set; }
        //    public string currency_code { get; set; }
        //    public bool is_kgt { get; set; }
        //    public Discounted_Stocks discounted_stocks { get; set; }
        //    public bool is_discounted { get; set; }
        //    public bool has_discounted_item { get; set; }
        //    //        public object[] barcodes { get; set; }
        //    public DateTime updated_at { get; set; }
        //    public Price_Indexes price_indexes { get; set; }
        //    public int sku { get; set; }
        //    public string GetSku() {
        //        return (sku != 0 ? sku : fbs_sku).ToString();
        //    }
        //    public int GetStocks() {
        //        return stocks.reserved + stocks.present;
        //    }
        //    public int GetPrice() {
        //        return int.Parse(price.Split('.').First());
        //    }
        //    public int GetOldPrice() {
        //        return int.Parse(old_price.Split('.').First());
        //    }
        //}
        //public class ItemErrors {
        //    public string code { get; set; }
        //    public string field { get; set; }
        //    public int attribute_id { get; set; }
        //    public string state { get; set; }
        //    public string level { get; set; }
        //    public string description { get; set; }
        //    public string attribute_name { get; set; }
        //}
        //public class Stocks {
        //    public int coming { get; set; }
        //    public int present { get; set; }
        //    public int reserved { get; set; }
        //}
        //public class Visibility_Details {
        //    public bool has_price { get; set; }
        //    public bool has_stock { get; set; }
        //    public bool active_product { get; set; }
        //}
        //public class Status {
        //    public string state { get; set; }
        //    public string state_failed { get; set; }
        //    public string moderate_status { get; set; }
        //    public string[] decline_reasons { get; set; }
        //    public string validation_state { get; set; }
        //    public string state_name { get; set; }
        //    public string state_description { get; set; }
        //    public bool is_failed { get; set; }
        //    public bool is_created { get; set; }
        //    public string state_tooltip { get; set; }
        //    public ItemErrors[] item_errors { get; set; }
        //    public DateTime state_updated_at { get; set; }
        //}
        //public class Discounted_Stocks {
        //    public int coming { get; set; }
        //    public int present { get; set; }
        //    public int reserved { get; set; }
        //}
        //public class Price_Indexes {
        //    public string price_index { get; set; }
        //    public External_Index_Data external_index_data { get; set; }
        //    public Ozon_Index_Data ozon_index_data { get; set; }
        //    public Self_Marketplaces_Index_Data self_marketplaces_index_data { get; set; }
        //}
        //public class External_Index_Data {
        //    public string minimal_price { get; set; }
        //    public string minimal_price_currency { get; set; }
        //    public float price_index_value { get; set; }
        //}
        //public class Ozon_Index_Data {
        //    public string minimal_price { get; set; }
        //    public string minimal_price_currency { get; set; }
        //    public float price_index_value { get; set; }
        //}
        //public class Self_Marketplaces_Index_Data {
        //    public string minimal_price { get; set; }
        //    public string minimal_price_currency { get; set; }
        //    public float price_index_value { get; set; }
        //}
        //public class Source {
        //    public bool is_enabled { get; set; }
        //    public int sku { get; set; }
        //    public string source { get; set; }
        //}
        //public class Commission {
        //    public decimal percent { get; set; }
        //    public decimal min_value { get; set; }
        //    public decimal value { get; set; }
        //    public string sale_schema { get; set; }
        //    public int delivery_amount { get; set; }
        //    public int return_amount { get; set; }
        //}
        ///////////////////////////////////////////
        //public class ProductImportItem {
        //    public Attribute[] attributes { get; set; }
        //    public string barcode { get; set; }
        //    public int category_id { get; set; }
        //    public string color_image { get; set; }
        //    public string[] complex_attributes { get; set; }
        //    public string currency_code { get; set; }
        //    public int depth { get; set; }
        //    public string dimension_unit { get; set; }
        //    public int height { get; set; }
        //    public string[] images { get; set; }
        //    public string[] images360 { get; set; }
        //    public string name { get; set; }
        //    public string offer_id { get; set; }
        //    public string old_price { get; set; }
        //    public string[] pdf_list { get; set; }
        //    public string premium_price { get; set; }
        //    public string price { get; set; }
        //    public string primary_image { get; set; }
        //    public string vat { get; set; }
        //    public int weight { get; set; }
        //    public string weight_unit { get; set; }
        //    public int width { get; set; }
        //}
        ///////////////////////////////////////////
        //public class ProductList {
        //    public List<ProductListItem> items { get; set; }
        //    public int total { get; set; }
        //    public string last_id { get; set; }
        //}
        //public class ProductListItem {
        //    public int product_id { get; set; }
        //    public string offer_id { get; set; }
        //    public bool is_fbo_visible { get; set; }
        //    public bool is_fbs_visible { get; set; }
        //    public bool archived { get; set; }
        //    public bool is_discounted { get; set; }
        //}
        ///////////////////////////////////////////
        //public class ProductInfoStocks {
        //    public Item[] items { get; set; }
        //    public int total { get; set; }
        //    public string last_id { get; set; }
        //}
        //public class Item {
        //    public int product_id { get; set; }
        //    public string offer_id { get; set; }
        //    public Stock[] stocks { get; set; }
        //}
        //public class Stock {
        //    public string type { get; set; }
        //    public int present { get; set; }
        //    public int reserved { get; set; }
        //}
        ///////////////////////////////////////////
        //public class UpdateResult {
        //    public int product_id { get; set; }
        //    public string offer_id { get; set; }
        //    public bool updated { get; set; }
        //    public UpdateResultErrors[] errors { get; set; }
        //}
        //public class UpdateResultErrors {
        //    public string code { get; set; }
        //    public string message { get; set; }
        //}
        ///////////////////////////////////////////
        //////////////////////////////////////////
        //public class ProductsInfoAttributes {
        //    public ProductsInfoAttr[] result { get; set; }
        //    public int total { get; set; }
        //    public string last_id { get; set; }
        //}
        //public class ProductsInfoAttr {
        //    public int id { get; set; }
        //    public string barcode { get; set; }
        //    public int category_id { get; set; }
        //    public string name { get; set; }
        //    public string offer_id { get; set; }
        //    public int height { get; set; }
        //    public int depth { get; set; }
        //    public int width { get; set; }
        //    public string dimension_unit { get; set; }
        //    public int weight { get; set; }
        //    public string weight_unit { get; set; }
        //    public Image[] images { get; set; }
        //    public string image_group_id { get; set; }
        //    public object[] images360 { get; set; }
        //    public object[] pdf_list { get; set; }
        //    public Attribute[] attributes { get; set; }
        //    public object[] complex_attributes { get; set; }
        //    public string color_image { get; set; }
        //    public string last_id { get; set; }
        //    public int description_category_id { get; set; }
        //}
        //public class Image {
        //    public string file_name { get; set; }
        //    public bool _default { get; set; }
        //    public int index { get; set; }
        //}
        //public class Attribute {
        //    public int id { get; set; }
        //    public int complex_id { get; set; }
        //    public Value[] values { get; set; }
        //}
        //public class Value {
        //    public string dictionary_value_id { get; set; }
        //    public string value { get; set; }
        //}
        ////=====================
        //public class Description_category {
        //    public int description_category_id { get; set; }
        //    public string category_name { get; set;}
        //    public bool disabled { get; set; } 
        //    public Description_category[] children { get; set; }
    }

}

//запрос остатков товара с озон
//async Task<int> GetInfoStocks(RootObject bus) {
//    //var sku = _bus[b].ozon.Split('/').Last();
//    var data = new { filter = new { offer_id = new[] { bus.id }, visibility = "ALL" }, limit = 100 };
//    var s = await PostRequestAsync(data, "/v3/product/info/stocks");
//    var stocks = JsonConvert.DeserializeObject<ProductInfoStocks>(s);
//    var stock = stocks.items.First().stocks.First(f => f.type == "fbs");
//    return stock.reserved + stock.present;
//}
