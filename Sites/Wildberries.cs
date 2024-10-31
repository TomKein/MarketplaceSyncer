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

namespace Selen.Sites {
    public class Wildberries {
        readonly HttpClient _hc = new HttpClient();
        //readonly string _baseApiUrl = "https://suppliers-api.wildberries.ru";
        readonly string _l = "wb: ";                                                    //префикс для лога
        List<GoodObject> _bus;
        int _nameLimit = 200;                                                           //ограничение длины названия
        readonly string _url;                                                           //номер поля в карточке
        readonly int _updateFreq;                                                       //частота обновления списка (часов)
        int _maxDiscont;                                                                //максимальная скидка %
        readonly string _baseBusUrl = "https://www.wildberries.ru/catalog/00000/detail.aspx";
        List<WbCard> _cardsList = new List<WbCard>();                                //список карточек вб
        readonly string _cardsListFile = @"..\data\wildberries\wb_productList.json";  //ссылка на файл для кеширования списка карточек вб
        List<WbPrice> _cardsPrices = new List<WbPrice>();
        readonly string _cardsPricesFile = @"..\data\wildberries\wb_priceList.json";  //ссылка на файл для кеширования цен вб
        List<WbStock> _cardsStocks = new List<WbStock>();
        readonly string _cardsStocksFile = @"..\data\wildberries\wb_stocksList.json";  //ссылка на файл для кеширования остатков вб

        List<WbWareHouse> _wareHouses;      //список складов

        static bool _isCardsListCheckNeeds = true;
        static bool _isCardsPricesCheckNeeds = true;
        static bool _isCardsStocksCheckNeeds = true;

        string _jsonResponse;

        List<WbCategory> _categories;       //список всех категорий товаров
        List<WbObject> _objects;            //список всех подкатегорий товаров
        JArray _objectsJA;                  //список всех подкатегорий товаров в формате JsonArray
        List<WbColor> _colors;              //цвета
        List<WbCountry> _countries;         //страны
        List<WbTnvd> _tnvd;                 //ТНВЭД код

        readonly string _reserveListFile = @"..\data\wildberries\wb_reserveList.json";
        readonly string _stickerListFile = @"..\data\wildberries\wb_stickerList.json";
        readonly string _priorityFile = @"..\data\wildberries\priority.txt";

        readonly string _wareHouseListFile = @"..\data\wildberries\wb_warehouseList.json";  //склады

        static object _locker = new object();
        static bool _flag = false;

        readonly string _rulesFile = @"..\data\wildberries\wb_categories.xml";
        XDocument _catsXml;
        IEnumerable<XElement> _rules;

        //списки исключений
        List<string> _exceptionGoods = new List<string> {
                    "фиксаторы грм",
                    "фиксаторы распредвалов",
                    "набор фиксаторов",
                    "бампер ",
                    "капот ",
                    "телевизор "
                };
        List<string> _exceptionGroups = new List<string> {
                    "черновик"
                };


        public Wildberries() {
            var apiKey = DB.GetParamStr("wb.apiKey");
            _hc.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            _url = DB.GetParamStr("wb.url");
            _updateFreq = DB.GetParamInt("wb.updateFreq");
        }
        //главный метод синхронизации
        public async Task SyncAsync() {
            try {
                _bus = Class365API._bus;
                _catsXml = XDocument.Load(_rulesFile);
                _rules = _catsXml.Descendants("Rule");
                if (_categories == null)
                    await GetCategoriesAsync();
                if (_objects == null)
                    await GetObjectsAsync(new[] { "760", "6240", "8555", "1162", "479", "6237", "3", "2050", "6119" });
                if (_objectsJA == null) {
                    var str = JsonConvert.SerializeObject(_objects);
                    _objectsJA = JArray.Parse(str);
                }
                if (_colors == null)
                    await GetColorsAsync();
                if (_countries == null)
                    await GetCountriesAsync();

                await GetWarehouseList();
                //tests
                //if (_tnvd == null)
                //await GetTnvdAsync("7985");
                //await GetCharcsAsync("7985");
                
                await GetCardsListAsync();
                await GetPricesAsync();
                await GetStocksAsync();
                await UpdateCardsAsync();
                await AddProductsAsync();
                CheckSizes();
            } catch (Exception ex) {
                Log.Add($"{_l} SyncAsync: ошибка! - " + ex.Message);
            }
            //}
        }
        public async Task MakeReserve() {
            try {
                var data = new Dictionary<string, string>();
                data["limit"] = "1000";
                data["next"] = "0";
                var unixTimeStamp = (int) DateTime.UtcNow.AddDays(-1).Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                data["dateFrom"] = unixTimeStamp.ToString();
                var res = await GetRequestAsync(data, "https://suppliers-api.wildberries.ru/api/v3/orders");            //https://suppliers-api.wildberries.ru/api/v3/orders/new
                var wb = JsonConvert.DeserializeObject<WbOrders>(_jsonResponse);
                Log.Add($"{_l} получено {wb.orders.Count} заказов:" 
                    //+$"\n{wb.orders.Select(s => s.id).Aggregate((x1, x2) => x1 + "\n" + x2)}"
                    );

                //загружаем список заказов, для которых уже делали резервирование
                var reserveList = new List<string>();
                if (File.Exists(_reserveListFile)) {
                    var s = File.ReadAllText(_reserveListFile);
                    var l = JsonConvert.DeserializeObject<List<string>>(s);
                    reserveList.AddRange(l);
                }
                //загружаем список заказов, для которых готовы стикеры
                var stickerList = new List<string>();
                if (File.Exists(_stickerListFile)) {
                    var s = File.ReadAllText(_stickerListFile);
                    var l = JsonConvert.DeserializeObject<List<string>>(s);
                    stickerList.AddRange(l);
                }
                //для каждого заказа сделать заказ с резервом в бизнес.ру
                foreach (var order in wb.orders) {
                    //если заказа нет в списке резервов
                    if (!reserveList.Contains(order.id)) {
                        //готовим список товаров (id, amount)
                        var goodsDict = new Dictionary<string, int>();
                        var amount = Class365API._bus.Find(g=>g.id == order.article).GetQuantOfSell();
                        goodsDict.Add(order.article, amount);
                        if (await Class365API.MakeReserve(Selen.Source.Wildberries,
                                                          $"WB order {order.id}",
                                                          goodsDict, 
                                                          order.createdAt.ToString())){
                            //если резерв создан - добавляем в список и сохраняем
                            reserveList.Add(order.id);
                            if (reserveList.Count > 1000) {
                                reserveList.RemoveAt(0);
                            }
                            var s = JsonConvert.SerializeObject(reserveList);
                            File.WriteAllText(_reserveListFile, s);
                        }
                    }
                    //если стикера нет в списке
                    if (!stickerList.Contains(order.id)) {
                        //делаем запрос стикера на вб
                        var sticker = await GetOrderSticker(order.id.ToString());
                        if (sticker == null)
                            continue;
                        if (await Class365API.AddStickerCodeToReserve(Selen.Source.Wildberries,
                                                             $"WB order {order.id}",
                                                             $", code {sticker}")) {
                            //если стикер найден - добавляем в список и сохраняем
                            stickerList.Add(order.id);
                            if (stickerList.Count > 1000) {
                                stickerList.RemoveAt(0);
                            }
                            var s = JsonConvert.SerializeObject(stickerList);
                            File.WriteAllText(_stickerListFile, s);
                        }
                    }
                }
            } catch (Exception x) {
                Log.Add(_l + "MakeReserve - " + x.Message);
            }
        }
        //GET запросы к api wb
        public async Task<string> GetRequestAsync(Dictionary<string, string> request, string apiRelativeUrl) {
            //try {
            if (request != null) {
                var qstr = QueryStringBuilder.BuildQueryString(request);
                apiRelativeUrl += "?" + qstr;
            }
            HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod("GET"), apiRelativeUrl);
            var response = await _hc.SendAsync(requestMessage);
            _jsonResponse = await response.Content.ReadAsStringAsync();
            await Task.Delay(500);
            if (response.StatusCode == HttpStatusCode.OK) {
                if (_jsonResponse.Contains("\"data\"")) {
                    var responseObject = JsonConvert.DeserializeObject<WbResponse>(_jsonResponse);
                    if (responseObject.error)
                        Log.Add($"{_l} GetRequestAsync: ошибка - {responseObject.errorText}; {responseObject.additionalErrors}");
                    return JsonConvert.SerializeObject(responseObject.data);
                }
                return _jsonResponse;
            } else
                throw new Exception($"{response.StatusCode} {response.ReasonPhrase} {response.Content} // {_jsonResponse}");
            //} catch (Exception x) {
            //    Log.Add($"{_l} PostRequestAsync ошибка запроса! apiRelativeUrl:{apiRelativeUrl} request:{request} message:{x.Message}");
            //}
            //return null;
        }
        //POST запросы к api wb
        public async Task<bool> PostRequestAsync(object request, string apiRelativeUrl, bool put = false) {
            try {
                if (request == null)
                    return false;
                var method = put ? "PUT" : "POST";
                HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod(method), apiRelativeUrl);
                var httpContent = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                requestMessage.Content = httpContent;
                var response = await _hc.SendAsync(requestMessage);
                _jsonResponse = await response.Content.ReadAsStringAsync();
                await Task.Delay(500);
                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent) {
                    if (_jsonResponse.Contains("\"error\":true"))
                        return false;
                    return true;
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
                var s = await GetRequestAsync(data, "https://suppliers-api.wildberries.ru/content/v2/object/parent/all");
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
        public async Task GetObjectsAsync(string[] parentIDs, string name = null) {
            _objects = new List<WbObject>();
            var objectsFile = $@"..\data\wildberries\wb_objects.json";
            if (File.GetLastWriteTime(objectsFile) < DateTime.Now.AddDays(-_updateFreq)) {
                var limit = 1000;
                foreach (var id in parentIDs) {
                    var data = new Dictionary<string, string>();
                    data["limit"] = limit.ToString();
                    data["locale"] = "ru";
                    if (name != null)
                        data["name"] = name;
                    else
                        data["parentID"] = id;
                    for (int last = 0; ; last += limit) {
                        data["offset"] = last.ToString();
                        var s = await GetRequestAsync(data, "https://suppliers-api.wildberries.ru/content/v2/object/all");
                        var resultList = JsonConvert.DeserializeObject<List<WbObject>>(s);
                        Log.Add($"{_l} GetObjectsAsync: {name ?? id} - получено подкатегорий: {resultList.Count}");
                        _objects.AddRange(resultList);
                        if (resultList.Count < limit)
                            break;
                    }
                }
                var serializedWbObject = JsonConvert.SerializeObject(_objects);
                File.WriteAllText(objectsFile, serializedWbObject);
            } else {
                var serializedWbObject = File.ReadAllText(objectsFile);
                try {
                    _objects.AddRange(JsonConvert.DeserializeObject<List<WbObject>>(serializedWbObject));
                } catch (Exception x) {
                    Log.Add($"{_l} GetObjectsAsync: ошибка загрузки категорий - {x.Message}");
                }
            }
            Log.Add($"{_l} GetObjectsAsync: загружено подкатегорий: {_objects.Count}");
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
                var s = await GetRequestAsync(data, $"https://suppliers-api.wildberries.ru/content/v2/object/charcs/{subjectId}");
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
                var s = await GetRequestAsync(data, "https://suppliers-api.wildberries.ru/content/v2/directory/colors");
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
                var s = await GetRequestAsync(data, "https://suppliers-api.wildberries.ru/content/v2/directory/countries");
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
                var s = await GetRequestAsync(data, "https://suppliers-api.wildberries.ru/content/v2/directory/tnved");
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
        //добавление новых товаров на wb
        async Task AddProductsAsync() {
            var count = await DB.GetParamIntAsync("wb.countToAdd");
            if (count == 0)
                return;
            if (_isCardsListCheckNeeds)
                await GetCardsListAsync();
            //список карточек которые еще не добавлены на wb
            var goods = _bus.Where(w => w.Amount > 0
                                     && w.Price > 0
                                     && w.Part != null
                                     && w.images.Count > 0
                                     && w.length != null
                                     && w.height != null
                                     && w.width != null
                                     && w.SumDimentions <= 200
                                     && w.MaxDimention <= 120
                                     && w.Weight <= 25
                                     && w.New
                                     && w.wb.Length == 0
                                     //&& !_productList.Any(_ => w.id == _.offer_id)
                                     && !_exceptionGoods.Any(e => w.name.ToLowerInvariant().Contains(e))
                                     && !_exceptionGroups.Any(e => w.GroupName().ToLowerInvariant().Contains(e)))
                            .OrderByDescending(o => o.Price).ToList();  //сначала подороже
            SaveToFile(goods, @"..\data\wildberries\wb_listForAdding.csv");
            Log.Add($"{_l} карточек для добавления: {goods.Count}");

            //учитываем данные по приоритетам
            var priorityList = File.ReadLines(_priorityFile)?.ToList();
            if (priorityList.Any()) {
                var priorGoods = new List<GoodObject>();
                foreach (var name in new List<string>(priorityList)) {
                    var busGood = _bus.Find(f => f.name == name);
                    if (busGood != null) {
                        if (goods.Contains(busGood))
                            priorGoods.Add(busGood);
                        else {
                            priorityList.Remove(name);
                            File.WriteAllLines(_priorityFile, priorityList);
                        }
                    }
                }
                priorGoods.AddRange(goods);
                goods = priorGoods.Distinct().ToList();
            }

            foreach (var good in goods) {
                try {
                    var attributes = await GetAttributesAsync(good);
                    if (attributes.Count == 0) {
                        Log.Add($"{_l} AddProductsAsync: {good.name} - категория не определена!");
                        if (priorityList.Contains(good.name)) {
                            Log.Add($"{_l} AddProductsAsync: {good.name} - приоритетный товар, ждем описание категории!");
                            --count;
                        }
                        continue;
                    }
                    var length = int.Parse(good.length);
                    if (length < 5) 
                        length = 5;
                    var width = int.Parse(good.width) * good.GetQuantOfSell();
                    if (width < 5)
                        width = 5;
                    var height = int.Parse(good.height);
                    if (height < 5)
                        height = 5;
                    //формирую объект запроса
                    var data = new[] {
                        new {
                            subjectID = attributes.First().subjectID,
                            variants = new[] {
                                new {
                                    title = good.NameLimit(_nameLimit),
                                    description = GetDescription(good),
                                    vendorCode = good.id,
                                    brand=good.GetManufacture(),
                                    dimensions = new {
                                          length = length,
                                          width = width,
                                          height = height
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

                    var res = await PostRequestAsync(data, "https://suppliers-api.wildberries.ru/content/v2/cards/upload");

                    if (res) {
                        Log.Add(_l + good.name + " - товар отправлен на wildberries!");
                        await SaveUrlAsync(good);
                        _isCardsListCheckNeeds = true;
                    } else {
                        Log.Add(_l + good.name + " ошибка отправки товара на wildberries!");
                    }
                } catch (Exception x) {
                    Log.Add(_l + good.name + " - " + x.Message + x.InnerException?.Message);
                }
                if (--count <= 0)
                    break;
            }
        }
        void GetTypeName(List<WbCharc> a, XElement rule) {
            try {
                var parent = rule.Parent;
                if (parent != null) {
                    foreach (XAttribute attribute in parent.Attributes()) {
                        if (attribute.Name == "subjectID") {
                            var subId = int.Parse(attribute.Value);
                            a.Add(new WbCharc { subjectID = subId });
                        } else if (attribute.Name == "nameLimit") {
                            var nameLimit = int.Parse(attribute.Value);
                            _nameLimit = nameLimit;
                        }
                    }
                }
            } catch (Exception x) {
                Log.Add($"{_l} GetTypeName: ошибка определения категории - {x.Message}");
            }
        }
        //получить атрибуты и категорию товара 
        async Task<List<WbCharc>> GetAttributesAsync(GoodObject bus) {
            try {
                var n = bus.name.ToLowerInvariant();
                var a = new List<WbCharc>();
                _nameLimit = 200;
                foreach (var rule in _rules) {
                    var conditions = rule.Elements();
                    var eq = true;
                    foreach (var condition in conditions) {
                        if (!eq)
                            break;
                        if (condition.Name == "Starts" && !n.StartsWith(condition.Value))
                            eq = false;
                        else if (condition.Name == "Contains" && !n.Contains(condition.Value))
                            eq = false;
                    }
                    if (eq) {
                        GetTypeName(a, rule);
                        break;
                    }
                }
                if (a.Count == 0)
                    return a;

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
            try {
                var value = good.GetManufactureCountry();
                if (value == null || !_countries.Any(c => c.name != value))
                    return;
                if (value.Contains("Корея"))
                    value = "Республика Корея";
                else if (value.Contains("Китай (Тайвань)"))
                    value = "Тайвань";
                else if (value.Contains("Китай (Гонконг)"))
                    value = "Гонконг";
                a.Add(new WbCharc {
                    charcID = 14177451,
                    value = value,
                    charcType = "1"
                });
            } catch (Exception x) {
                throw new Exception($"GetManufactureCountry: {x.Message}");
            }
        }
        //Атрибут Вес с упаковкой  (кг)
        async Task GetWeight(GoodObject good, List<WbCharc> a) {
            try {
                a.Add(new WbCharc {
                    charcID = 88953,
                    value = good.Weight,                //todo дробное число валидно?
                                                        //value = (int) good.Weight,
                    charcType = "4"
                });
            } catch (Exception x) {
                throw new Exception($"GetWeight: {x.Message}");
            }
        }
        //Атрибут Артикул производителя
        async Task GetPart(GoodObject good, List<WbCharc> a) {
            try {
                a.Add(new WbCharc {
                    charcID = 5522881,
                    value = good.Part,
                    charcType = "1"
                });
            } catch (Exception x) {
                throw new Exception($"GetPart: {x.Message}");
            }
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


        //обновление ссылки в карточке бизнес.ру
        async Task SaveUrlAsync(GoodObject bus, int nmID = 0) {
            try {
                string newUrl = _baseBusUrl;
                //проверяем ссылку, если она есть, то значит товар был отправлен на вб
                if (nmID > 0)
                    newUrl = _baseBusUrl.Replace("/00000/", "/" + nmID + "/");
                if (nmID == -1)
                    newUrl = "";
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
        public async Task GetCardsListAsync() {
            try {
                //если файл свежий и обновление списка не требуется
                if (DateTime.Now < File.GetLastWriteTime(_cardsListFile).AddDays(_updateFreq)
                     && !_isCardsListCheckNeeds) {
                    //загружаем с диска, если список пустой
                    if (_cardsList.Count == 0) {
                        _cardsList = JsonConvert.DeserializeObject<List<WbCard>>(
                            File.ReadAllText(_cardsListFile));
                    }
                } else {
                    _cardsList.Clear();

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
                        var isSuc = await PostRequestAsync(data, "https://suppliers-api.wildberries.ru/content/v2/get/cards/list");
                        if (!isSuc)
                            throw new Exception("запрос товаров вернул ошибку!");
                        var productList = JsonConvert.DeserializeObject<WbProductList>(_jsonResponse);
                        _cardsList.AddRange(productList.cards);
                        Log.Add($"{_l} получено {_cardsList.Count} товаров");
                        //добавляем пагинацию в объект запроса
                        data.settings.cursor = new {
                            limit = limit,
                            updatedAt = productList.cursor.updatedAt,
                            nmID = productList.cursor.nmID
                        };
                        total = productList.cursor.total;
                    } while (total == limit);
                    Log.Add(_l + "ProductList - успешно загружено " + _cardsList.Count + " товаров");
                    File.WriteAllText(_cardsListFile, JsonConvert.SerializeObject(_cardsList));
                    Log.Add(_l + _cardsListFile + " - сохранено");
                    _isCardsListCheckNeeds = false; //сбрасываю флаг
                }
                await CheckCardsAsync();
            } catch (Exception x) {
                Log.Add(_l + "ProductList - ошибка загрузки товаров - " + x.Message);
            }
        }
        //список цен товаров
        public async Task GetPricesAsync() {
            try {
                _maxDiscont = await DB.GetParamIntAsync("wb.maxDiscont");
                //если файл свежий и обновление списка не требуется
                if (DateTime.Now < File.GetLastWriteTime(_cardsPricesFile).AddDays(_updateFreq)
                    && !_isCardsPricesCheckNeeds) {
                    //загружаем с диска, если список пустой
                    if (_cardsPrices.Count == 0) {
                        _cardsPrices = JsonConvert.DeserializeObject<List<WbPrice>>(
                            File.ReadAllText(_cardsPricesFile));
                    }
                } else {
                    _cardsPrices.Clear();
                    var limit = _cardsList.Count > 1000 ? 1000 : _cardsList.Count;
                    var offset = 0;
                    var data = new Dictionary<string, string>();
                    data["limit"] = limit.ToString();
                    data["offset"] = offset.ToString();
                    ListGoods list = new ListGoods();
                    for (int i = 0; ;) {
                        try {
                            var res = await GetRequestAsync(data, "https://discounts-prices-api.wildberries.ru/api/v2/list/goods/filter");
                            list = JsonConvert.DeserializeObject<ListGoods>(res);
                            _cardsPrices.AddRange(list.listGoods);
                            Log.Add($"{_l} получено {_cardsPrices.Count} цен товаров");
                            //добавляем пагинацию в объект запроса
                            if (list.listGoods.Count != limit || _cardsPrices.Count == _cardsList.Count)
                                break;
                            offset += limit;
                            data["offset"] = offset.ToString();
                        } catch (Exception x) {
                            limit /= 2;
                            data["limit"] = limit.ToString();
                            Log.Add($"{_l} GetPricesAsync: ошибка запроса цен товаров {i++}, limit: {limit} - " + x.Message);
                            if (limit == 0)
                                break;
                            //throw new Exception("превышено количество попыток запроса цен!");
                            await Task.Delay(10000);
                        }
                    }
                    if (_cardsPrices.Count > 0 && _cardsList.Count - _cardsPrices.Count < 100)
                        File.WriteAllText(_cardsPricesFile, JsonConvert.SerializeObject(_cardsPrices));
                    else {
                        _cardsPrices = JsonConvert.DeserializeObject<List<WbPrice>>(
                            File.ReadAllText(_cardsPricesFile));
                        Log.Add($"{_l} загружено с диска {_cardsPrices.Count} цен товаров");
                    }
                    Log.Add(_l + _cardsPricesFile + " - сохранено");
                    _isCardsPricesCheckNeeds = false;
                }
            } catch (Exception x) {
                Log.Add($"{_l} GetPricesAsync: ошибка загрузки цен товаров!! - " + x.Message);
            }
        }
        //обновление цены товара
        public async Task UpdatePrice(GoodObject good, WbCard card) {
            try {
                if (_cardsPrices.Count == 0) {
                    _isCardsPricesCheckNeeds = true;
                    throw new Exception("_cardsPrices.Count == 0");
                }
                if (card == null)
                    card = _cardsList.Find(f => f.vendorCode == good.id);
                if (card == null)
                    throw new Exception("карточка не найдена!");
                //проверяем цену на вб
                var wbPrice = _cardsPrices.Find(f => f.vendorCode == good.id)?.sizes[0].price;
                var goodNewPrice = GetNewPrice(good);
                var discount = _cardsPrices.Find(c => c.vendorCode == good.id)?.discount ?? 0;
                if (wbPrice == goodNewPrice && discount<=_maxDiscont) {
                    //wbDiscountedPrice == goodNewPrice) 
                    //Log.Add($"{_l} UpdatePrice: {good.name} - цены не нуждаются в обновлении");
                    return;
                }
                var newDiscount = discount > _maxDiscont ? _maxDiscont : discount;
                var data = new {
                    data = new[] {
                        new {
                            nmID = card.nmID,
                            price = goodNewPrice,
                            discount = newDiscount
                        }
                    }
                };
                var res = await PostRequestAsync(data, "https://discounts-prices-api.wildberries.ru/api/v2/upload/task");
                if (!res)
                    throw new Exception("ошибка запроса изменения цены");
                Log.Add($"{_l} UpdatePrice: {good.name} - цены обновлены, {wbPrice} => {goodNewPrice}," +
                        $" дисконт {discount} => {newDiscount}");
                //обновить список карточек вб
            } catch (Exception x) {
                Log.Add($"{_l} GetPricesAsync: {good.name} - ошибка обновления цен товара - {x.Message}");
            }
            _isCardsPricesCheckNeeds = true;
        }
        //расчет цен WB с учетом наценки
        private int GetNewPrice(GoodObject good) {
            var weight = good.Weight * good.GetQuantOfSell();
            var d = good.GetDimentions();
            var length = d[0] + d[1] + d[2] * good.GetQuantOfSell();
            int overPrice;
            //вес от 50 кг или размер более 200 -- наценка 30% + 3500 р
            if (weight >= 50 || length >= 200)
                overPrice = (int) (good.Price * 0.30) + 3500;
            //вес от 30 кг или размер от 150 -- наценка 30% + 2000 р
            else if (weight >= 30 || length >= 150)
                overPrice = (int) (good.Price * 0.30) + 2000;
            //вес от 10 кг или размер от 100 -- наценка 1500 р
            else if (weight >= 10 || length >= 100)
                overPrice = (int) (good.Price * 0.30) + 1500;
            //для маленьких и легких наценка 40% на всё
            else
                overPrice = (int) (good.Price * 0.40);
            //если наценка меньше 200 р - округляю
            if (overPrice < 200)
                overPrice = 200;

            return good.Price * good.GetQuantOfSell() + overPrice;
        }
        //цена до скидки (старая)
        //private int GetOldPrice(int newPrice) {
        //    return (int) (Math.Ceiling((newPrice * (1 + _oldPriceProcent / 100)) / 100) * 100);
        //}

        //проверяем привязку товаров в карточки бизнес.ру
        private async Task CheckCardsAsync(bool checkAll = false) {
            foreach (var card in _cardsList) {
                try {
                    //карточка в бизнес.ру с id = vendorCode товара на вб
                    var b = _bus.FindIndex(_ => _.id == card.vendorCode);
                    if (b == -1)
                        throw new Exception($"карточка бизнес.ру с id = {card.vendorCode} не найдена!");
                    if (!_bus[b].wb.Contains("http") ||                       //если карточка найдена,но товар не привязан к бизнес.ру
                        _bus[b].wb.Contains("/00000/") ||                     //либо ссылка есть, но неверный sku
                        checkAll) {                                           //либо передали флаг проверять всё
                        await SaveUrlAsync(_bus[b], card.nmID);
                        //await UpdateCardAsync(_bus[b], card);
                    }
                } catch (Exception x) {
                    Log.Add($"{_l} CheckGoodsAsync ошибка! checkAll:{checkAll} offer_id:{card.vendorCode} message:{x.Message}");
                    _isCardsListCheckNeeds = true;
                }
            }
        }
        //проверка и обновление товара
        async Task UpdateCardAsync(GoodObject good, WbCard card = null) {
            try {
                //if (card == null)
                //card = await GetProductInfoAsync(good);
                await UpdateMedia(good, card);
                await UpdateStocks(good, card);
                await UpdatePrice(good, card);
                await UpdateCard(good, card);
            } catch (Exception x) {
                Log.Add($"{_l} UpdateProductAsync ошибка! name:{good.name} message:{x.Message}");
            }
        }
        //обновление фотографий
        async Task UpdateMedia(GoodObject good, WbCard card) {
            try {
                //если количество фотографий в карточках совпадает - пропускаем (потом возможно сделаю получше)
                if (card == null)
                    card = _cardsList.Find(f => f.vendorCode == good.id);
                if (card == null)
                    throw new Exception("карточка не найдена!");
                if (good.images.Count == card.photos.Count)
                    return;
                while (_flag) { await Task.Delay(5000); }
                lock (_locker) {
                    _flag = true;
                }
                var urls = await SendImagesToFtpAsync(good);
                var data = new {
                    nmId = card.nmID,
                    data = urls
                };
                var isSuc = await PostRequestAsync(data, "https://suppliers-api.wildberries.ru/content/v3/media/save");
                if (!isSuc)
                    throw new Exception("запрос на обновление фото вернул ошибку!");
                Log.Add($"{_l} UpdateMediaAsync: {good.name} -  фотографии успешно обновлены! ({urls.Count})");
                card.photos = good.images.Select(i => new WbPhoto { big = i.url }).ToList();
            } catch (Exception x) {
                Log.Add($"{_l} UpdateMediaAsync ошибка! name:{good.name} message:{x.Message}");
            }
            _flag = false;
        }
        async Task<List<string>> SendImagesToFtpAsync(GoodObject good) {
            List<string> urlList = new List<string>();
            WebClient cl = new WebClient();
            cl.Encoding = Encoding.UTF8;
            var num = good.images.Count > 20 ? 20 : good.images.Count;
            for (int u = 0; u < num; u++) {
                try {
                    byte[] bts = cl.DownloadData(good.images[u].url);
                    var fileName = $"wb_{u}";
                    File.WriteAllBytes($@"..\data\wildberries\{fileName}.jpg", bts);
                    await Task.Delay(500);
                    System.Drawing.Image image = System.Drawing.Image.FromFile($@"..\data\wildberries\{fileName}.jpg");
                    if (image.Height <= 900 || image.Width <= 700)
                        using (var resized = ImageUtilities.ResizeImage(image, image.Width * 2, image.Height * 2)) {
                            fileName += "_";
                            ImageUtilities.SaveJpeg($@"..\data\wildberries\{fileName}.jpg", resized, 90);
                        }
                    image.Dispose();
                    await Task.Delay(500);
                    await SftpClient.FtpUploadAsync($@"..\data\wildberries\{fileName}.jpg");
                    await Task.Delay(1000);
                    urlList.Add($@"https://avtotehnoshik.ru/{fileName}.jpg");
                } catch (Exception x) {
                    throw new Exception($"{_l} SendImagesToFtpAsync: ошибка! - {x.Message}");
                }
            }
            cl.Dispose();
            return urlList;
        }
        //проверка всех карточек в бизнесе, которые изменились или имеют расхождения на вб
        //а также выборочная проверка карточек
        private async Task UpdateCardsAsync() {
            try {
                var checkProductCount = await DB.GetParamIntAsync("wb.checkProductCount");
                var checkProductIndex = await DB.GetParamIntAsync("wb.checkProductIndex");
                if (checkProductIndex >= _cardsList.Count)
                    checkProductIndex = 0;
                await DB.SetParamAsync("wb.checkProductIndex", (checkProductIndex + checkProductCount).ToString());
                //для каждой карточки, у которой есть ссылка на WB, но не временная, с 00000
                foreach (var good in _bus.Where(w => !string.IsNullOrEmpty(w.wb) &&
                                                w.wb.Contains("http") &&
                                                !w.wb.Contains("/00000/"))) {
                    try {
                        //находим карточку на вб
                        var card = _cardsList.Find(f => f.vendorCode == good.id);
                        if (card == null) {
                            //await SaveUrlAsync(good, -1);
                            _isCardsListCheckNeeds = true;
                            _isCardsPricesCheckNeeds = true;
                            _isCardsStocksCheckNeeds = true;
                            Log.Add($"{good.name} - карточка не найдена на ВБ!");
                        } else {
                            //проверяем остаток на вб
                            var wbStock = _cardsStocks.Find(f => f.sku == card.sizes[0].skus[0])?.amount;
                            //проверяем цену на вб
                            var wbPrice = _cardsPrices.Find(f => f.vendorCode == good.id)?.sizes[0].price;
                            var wbDiscount = _cardsPrices.Find(f => f.vendorCode == good.id)?.discount;
                            var goodNewPrice = GetNewPrice(good);
                            var goodNewAmount = (int) good.Amount/good.GetQuantOfSell();
                            //если отличается остаток, цена, или карточка была обновлена в бизнес.ру
                            if (wbStock != goodNewAmount ||
                                wbPrice != goodNewPrice ||
                                wbDiscount > _maxDiscont||
                                card.photos.Count != good.images.Count ||
                                good.IsTimeUpDated() ||
                                --checkProductIndex < 0 && checkProductCount + checkProductIndex >= 0
                                )
                                //обновляем карточку на вб
                                await UpdateCardAsync(good, card);
                        }
                    } catch (Exception x) {
                        Log.Add(_l + "UpdateCardsAsync - " + x.Message);
                    }
                }
            } catch (Exception x) {
                Log.Add($"UpdateCardsAsync: ошибка - {x.Message}");
            }
        }
        //запорос остатков товара
        async Task GetStocksAsync() {
            try {
                //если файл свежий и обновление списка не требуется
                if (DateTime.Now < File.GetLastWriteTime(_cardsStocksFile).AddDays(_updateFreq)
                    && !_isCardsStocksCheckNeeds) {
                    //загружаем с диска, если список пустой
                    if (_cardsStocks.Count == 0) {
                        _cardsStocks = JsonConvert.DeserializeObject<List<WbStock>>(
                            File.ReadAllText(_cardsStocksFile));
                    }
                } else {
                    _cardsStocks.Clear();
                    var limit = 1000;
                    var index = 0;

                    do {
                        var skus = _cardsList.Skip(index).Take(limit).Select(s => s.sizes[0].skus[0]).ToList();
                        var data = new {
                            skus = skus
                        };
                        var res = await PostRequestAsync(data, $"https://suppliers-api.wildberries.ru/api/v3/stocks/{_wareHouses[0].id}");
                        if (!res)
                            throw new Exception("ошибка запроса остатков");
                        var list = JsonConvert.DeserializeObject<WbStocks>(_jsonResponse);
                        _cardsStocks.AddRange(list.stocks);
                        index += limit;
                    } while (index < _cardsList.Count - 1);
                    Log.Add($"{_l} получено {_cardsStocks.Count} остатков товаров");
                    File.WriteAllText(_cardsStocksFile, JsonConvert.SerializeObject(_cardsStocks));
                    Log.Add(_l + _cardsStocksFile + " - сохранено");
                    _isCardsStocksCheckNeeds = false;
                }
            } catch (Exception x) {
                Log.Add(_l + "GetPricesAsync - ошибка загрузки цен товаров - " + x.Message);
            }
        }
        //обновление остатков товара
        private async Task UpdateStocks(GoodObject good, WbCard card) {
            try {
                if (card == null)
                    card = _cardsList.Find(f => f.vendorCode == good.id);
                if (card == null)
                    throw new Exception("карточка не найдена!");
                var amount =(int) good.Amount / good.GetQuantOfSell(); //делим количество шт на квант продажи
                //если вдруг товар в карточке стал Б/У - зануляем остаток, чтобы снять с продажи на ВБ
                if (!good.New || amount < 0)
                    amount = 0;
                //проверяем остаток на вб
                var wbStock = _cardsStocks.Find(f => f.sku == card.sizes[0].skus[0])?.amount;
                if (wbStock != null && wbStock == amount) {
                    //Log.Add($"{_l} UpdateStocks: {good.name} - остаток не нуждается в обновлении");
                    return;
                }
                var data = new {
                    stocks = new[] {
                            new {
                                sku = card.sizes[0].skus[0],
                                amount = (int)amount
                            }
                        }
                };
                var res = await PostRequestAsync(data, $"https://suppliers-api.wildberries.ru/api/v3/stocks/{_wareHouses[0].id}", true);
                if (!res)
                    throw new Exception("ошибка запроса изменения цены");
                Log.Add($"{_l} UpdateStocks: {good.name} - остаток обновлен {wbStock} => {amount}");
                //запросить новые остатки
                _isCardsStocksCheckNeeds = true;
            } catch (Exception x) {
                Log.Add($"{_l} UpdateProductStocks: ошибка обновления остатка {good.name} - {x.Message}");
            }
        }
        //обновление описаний товаров
        private async Task UpdateCard(GoodObject good, WbCard card) {
            try {
                //проверяем статус товара
                if (!good.New)
                    throw new Exception("товар стал Б/У!!");
                //проверяем категорию
                var attributes = await GetAttributesAsync(good);
                if (attributes.Count == 0) {
                    throw new Exception("категория не определена!!");
                }
                if (card == null) {
                    card = _cardsList.Find(f => f.vendorCode == good.id);
                }
                if (card == null)
                    throw new Exception("карточка не найдена!!");
                var length = int.Parse(good.length);
                if (length < 5)
                    length = 5;
                var width = int.Parse(good.width) * good.GetQuantOfSell();
                if (width < 5)
                    width = 5;
                var height = int.Parse(good.height);
                if (height < 5)
                    height = 5;
                //формирую объект запроса
                var data = new[] {
                    new {
                        nmID = card.nmID,
                        vendorCode = good.id,
                        brand = good.GetManufacture(),
                        title = good.NameLimit(_nameLimit),
                        description = GetDescription(good),
                        dimensions = new {
                                length = length,
                                width = width,
                                height = height
                        },
                        characteristics = new List<object>(),
                        sizes = new WbSizes[] {
                            new WbSizes{
                                chrtID = card.sizes[0].chrtID,
                                skus = card.sizes[0].skus,
                            }
                        }
                    }
                };
                foreach (var a in attributes.Skip(1)) {
                    if (a.charcType == "1")
                        data[0].characteristics.Add(new { id = a.charcID, value = a.value.ToString() });
                    else if (a.charcType == "2")
                        data[0].characteristics.Add(new { id = a.charcID, value = a.value as string[] });
                    else if (a.charcType == "4")
                        data[0].characteristics.Add(new { id = a.charcID, value = (float) a.value });  //todo валидно??
                                                                                                       //data[0].characteristics.Add(new { id = a.charcID, value = (int) a.value });
                }
                //сравниваем основные поля и характеристики
                if (card.title == data[0].title &&
                    card.description == data[0].description &&
                    card.brand == data[0].brand &&
                    card.dimensions.length == data[0].dimensions.length &&
                    card.dimensions.width == data[0].dimensions.width &&
                    card.dimensions.height == data[0].dimensions.height &&
                    IsCharacteristicsEqual(card, data[0].characteristics)) {
                    //Log.Add($"{_l} UpdateCard: {card.title} - карточка не нуждается в обновлении");
                    return;
                }

                var res = await PostRequestAsync(data, "https://suppliers-api.wildberries.ru/content/v2/cards/update");
                if (res)
                    Log.Add($"{_l} UpdateCard: {good.name} - карточка обновлена!");
                else
                    throw new Exception("ошибка обновления карточки!");
            } catch (Exception x) {
                Log.Add($"{_l} UpdateProduct: ошибка обновления описания {good.id} {good.name} - {x.Message}");
            }
        }
        string GetDescription(GoodObject good) {
            return good.DescriptionList(5000).Aggregate((a, b) => a + "\n" + b)
                       .Replace("цена за штуку", "$$$")
                       .Replace("ЦЕНА ЗА ШТУКУ", "$$$")
                       .Replace("Цена за штуку", "$$$")
                       .Replace("Цена за шт", "$$$")
                       .Replace("ЦЕНА ЗА ШТ", "$$$")
                       .Replace("цена за шт", "$$$")
                       .Replace("$$$", $"Цена за {good.GetQuantOfSell()} шт");
        }
        bool IsCharacteristicsEqual(WbCard card, List<Object> charcs) {
            if (charcs == null || card == null)
                return false;
            if (charcs.Count != card.characteristics.Count)
                return false;
            foreach (dynamic charc in charcs) {
                var isContainsCharc = card.characteristics.Any(c => c.id == charc.id);
                if (isContainsCharc) {
                    var cardValue = card.characteristics.Find(f => f.id == charc.id)?.value.ToString()
                        .Replace("[", "").Replace("]", "").Replace("\r\n", "").Replace("\"", "").Trim();
                    if (cardValue != null && cardValue == charc.value.ToString())
                        continue;
                }
                return false;
            }
            return true;
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
        //список складов
        async Task GetWarehouseList() {
            try {
                //если файл свежий и обновление списка не требуется
                if (DateTime.Now < File.GetLastWriteTime(_wareHouseListFile).AddDays(_updateFreq * 10)) {
                    //загружаем с диска, если список пустой
                    if (_wareHouses == null) {
                        _wareHouses = JsonConvert.DeserializeObject<List<WbWareHouse>>(
                            File.ReadAllText(_wareHouseListFile));
                    }
                } else {
                    var data = new Dictionary<string, string>();
                    var res = await GetRequestAsync(data, "https://suppliers-api.wildberries.ru/api/v3/warehouses");
                    if (res != null) {
                        _wareHouses = JsonConvert.DeserializeObject<List<WbWareHouse>>(res);
                        File.WriteAllText(_wareHouseListFile, res);
                    } else
                        throw new Exception("ошибка запроса складов!");
                }
            } catch (Exception x) {
                Log.Add($"{_l} GetWarehouseList: ошибка! - {x.Message}");
            }
        }
        //Получить этикетки для сборочных заданий
        public async Task<string> GetOrderSticker(string orderId) {
            try {
                Stickers stickerList;
                var data = new {
                    orders = new long[] { long.Parse(orderId) }
                };
                await PostRequestAsync(data, "https://marketplace-api.wildberries.ru/api/v3/orders/stickers?type=svg&width=58&height=40");
                stickerList = JsonConvert.DeserializeObject<Stickers>(_jsonResponse);
                if (stickerList.stickers.Count > 0) {
                    var sticker = stickerList.stickers[0].partA + " " + stickerList.stickers[0].partB;
                    Log.Add($"GetOrderSticker: получен стикер {sticker}");
                    return sticker;
                } else {
                    throw new Exception("стикер не получен!");
                }
            } catch (Exception x) {
                Log.Add($"{_l} GetOrderSticker: ошибка! - order {orderId} - {x.Message}");
            }
            return null;
        }
        //проверить габариты 
        public void CheckSizes() {
            Log.Add($"CheckSize: проверяем габариты...");

            foreach(var good in Class365API._bus.Where(g=>g.wb != null && 
                                                          g.wb.Contains("http") &&
                                                         (g.MaxDimention > 120 ||
                                                          g.SumDimentions > 200 ||
                                                          g.Weight > 25))) 
                Log.Add($"CheckSize: id:{good.id} name:{good.name} wb:{good.wb} - ошибка! габариты за пределами!!");
            
            Log.Add($"CheckSize: проверка завершена.");
        }


        ///////////////////////////////////////
        ///// классы для работы с запросами ///
        ///////////////////////////////////////
        public class WbProductList {
            public List<WbCard> cards = new List<WbCard>(); //Список КТ
            public WbCursor cursor { get; set; } //Пагинатор
        }
        public class WbCard {
            public int nmID { get; set; } //Артикул WB
            public int imtID { get; set; } //Идентификатор КТ
            public string nmUUID { get; set; } //Внутренний технический идентификатор товара
            public string subjectID { get; set; } //Идентификатор предмета
            public string vendorCode { get; set; } //Артикул продавца
            public string subjectName { get; set; } //Название предмета
            public string brand { get; set; } //Бренд
            public string title { get; set; } //Наименование товара
            public string description { get; set; } //Описание товара

            public List<WbPhoto> photos = new List<WbPhoto>(); //Массив фотографий
            public string video { get; set; } //URL видео

            public Dimensions dimensions; //Габариты упаковки товара, см

            public List<WbCharacteristic> characteristics = new List<WbCharacteristic>();

            public List<WbSizes> sizes = new List<WbSizes>();
            public string createdAt { get; set; } //Дата создания
            public string updatedAt { get; set; } //Дата изменения
        }
        public class WbSizes {
            public int chrtID { get; set; }
            public string techSize { get; set; }
            public string[] skus { get; set; }
        }
        public class WbCharacteristic {
            public int id { get; set; } //Идентификатор характеристики
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
            public Object filter { get; set; }
            public Object cursor { get; set; }
        }
        public class ListGoods {
            public List<WbPrice> listGoods = new List<WbPrice>();
        }
        public class WbPrice {
            public int nmID { get; set; }
            public string vendorCode { get; set; }

            public List<WbSizePrices> sizes = new List<WbSizePrices>();
            public string currencyIsoCode4217 { get; set; }
            public int discount { get; set; }
            public bool editableSizePrice { get; set; }
        }
        public class WbSizePrices {
            public long sizeID { get; set; }
            public decimal price { get; set; }
            public decimal discountedPrice { get; set; }
            public string techSizeName { get; set; }
        }
        public class WbWareHouse {
            public ulong id { get; set; }
            public int officeId { get; set; }
            public string name { get; set; }
            public int cargoType { get; set; }
            public int deliveryType { get; set; }
        }
        public class WbStock {
            public string sku { get; set; }
            public int amount { get; set; }
        }
        public class WbStocks {
            public List<WbStock> stocks = new List<WbStock>();
        }
        public class WbOrders {
            public List<WbOrder> orders = new List<WbOrder>();
        }
        public class WbOrder {
            public DateTime createdAt { get; set; }
            public string article { get; set; }
            public string nmId { get; set; }
            public string id { get; set; }
            public string warehouseId { get; set; }
        }

        public class Stickers {
            public List<Sticker> stickers = new List<Sticker>();
        }

        public class Sticker {
            public ulong orderId { get; set; }
            public string partA { get; set; }
            public string partB { get; set; }
            public string barcode { get; set; }
            public string file { get; set; }
        }
    }
}
