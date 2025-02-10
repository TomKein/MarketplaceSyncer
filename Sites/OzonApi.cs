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
using OfficeOpenXml;

namespace Selen.Sites {
    public class OzonApi {
        readonly HttpClient _hc = new HttpClient();
        readonly string _clientID;                    // 1331176
        readonly string _apiKey;                      // 1d0edd61-1b2d-4ac5-afa0-cf5488665d35
        readonly string _baseApiUrl = "https://api-seller.ozon.ru";
        readonly string _baseBusUrl = "https://www.ozon.ru/context/detail/id/";
        readonly string _url;                         //номер поля в карточке
        readonly string _l = "ozon: ";                //префикс для лога
        readonly float _oldPriceProcent;
        public static float _minPriceProcent;
        List<ProductListItem> _productList = new List<ProductListItem>();   //список товаров, получаемый из /v2/product/list
        readonly string _productListFile = @"..\data\ozon\ozon_productList.json";
        readonly string _reserveListFile = @"..\data\ozon\ozon_reserveList.json";
        readonly string _catsFile = @"..\data\ozon\ozon_categories.json";
        readonly string _warehouseList = @"..\data\ozon\ozon_warehouseList.json";
        readonly int _updateFreq;                //частота обновления списка (часов)
        static bool _isProductListCheckNeeds;
        bool _hasNext = false;                        //для запросов
        List<GoodObject> _bus;
        List<AttributeValue> _brends;                 //список брендов озон
        int _nameLimit = 200;                         //ограничение длины названия по умолчанию
        Random _rnd = new Random();
        List<GoodObject> _busToUpdate;        //список товаров для обновления
        readonly string _busToUpdateFile = @"..\data\ozon\toupdate.json";
        int _newPrice;

        //List<Description_category> _categories;         //список всех категорий товаров на озон
        JArray _categoriesJO;         //список всех категорий товаров на озон JObject

        static readonly string _rulesFile = @"..\data\ozon\ozon_categories.xml";
        static XDocument _rulesXML;
        static IEnumerable<XElement> _rules;

        //списки исключений
        const string EXCEPTION_GOODS_FILE = @"..\data\ozon\exceptionGoodsList.json";
        const string EXCEPTION_GROUPS_FILE = @"..\data\ozon\exceptionGroupsList.json";
        const string EXCEPTION_BRANDS_FILE = @"..\data\ozon\exceptionBrandsList.json";
        List<string> _exceptionGoods;
        List<string> _exceptionGroups;
        List<string> _exceptionBrands;

        public OzonApi() {
            _hc.BaseAddress = new Uri(_baseApiUrl);
            _url = DB.GetParamStr("ozon.url");
            _clientID = DB.GetParamStr("ozon.id");
            _apiKey = DB.GetParamStr("ozon.apiKey");
            _oldPriceProcent = DB.GetParamFloat("ozon.oldPriceProcent");
            _minPriceProcent = DB.GetParamFloat("ozon.minPriceProcent");
            _updateFreq = DB.GetParamInt("ozon.updateFreq");
        }
        //главный метод
        public async Task SyncAsync() {
            try {
                if (!await DB.GetParamBoolAsync("ozon.syncEnable")) {
                    Log.Add($"{_l} StartAsync: синхронизация отключена!");
                    return;
                }
                while (Class365API.Status == SyncStatus.NeedUpdate)
                    await Task.Delay(30000);
                _bus = Class365API._bus;
                _rulesXML = XDocument.Load(_rulesFile);
                _rules = _rulesXML.Descendants("Rule");
                //загрузка исключений
                _exceptionGroups = JsonConvert.DeserializeObject<List<string>>(
                    File.ReadAllText(EXCEPTION_GROUPS_FILE));
                _exceptionGoods = JsonConvert.DeserializeObject<List<string>>(
                    File.ReadAllText(EXCEPTION_GOODS_FILE));
                _exceptionBrands = JsonConvert.DeserializeObject<List<string>>(
                    File.ReadAllText(EXCEPTION_BRANDS_FILE));

                //await GetWarehouseList();
                await GetCategoriesAsync();
                await UpdateProductsAsync();
                await CheckProductListAsync();
                await DeactivateActions();
                await CheckProductLinksAsync(checkAll: true);
                await Add();
                if (Class365API.SyncStartTime.Minute < Class365API._checkIntervalMinutes) {
                    await DeactivateAutoActions();
                }
            }catch(Exception x) {
                Log.Add($"{_l}SyncAsync: ошибка - {x.Message} - {x?.InnerException}");
                if (DB.GetParamBool("alertSound"))
                    new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
            }
        }
        public async Task MakeReserve() {
            try {
                if (!await DB.GetParamBoolAsync("ozon.syncEnable")) {
                    Log.Add($"{_l}MakeReserve: синхронизация отключена!");
                    return;
                }
                //запросить список заказов со следующими статусами
                var statuses = new List<string> {
                    "awaiting_packaging",
                    "awaiting_deliver"
                };
                Postings postings = new Postings();
                foreach (var status in statuses) {
                    var data = new {
                        filter = new {
                            cutoff_from = DateTime.Now.AddDays(-1).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'"),       //"2024-02-24T14:15:22Z",
                            cutoff_to = DateTime.Now.AddDays(28).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'"),
                            warehouse_id = new int[] { },
                            status = status
                        },
                        limit = 300,
                        offset = 0,
                        with = new {
                            analytics_data = false,
                            barcodes = false,
                            financial_data = false,
                            translit = false
                        }
                    };
                    var result = await PostRequestAsync(data, "/v3/posting/fbs/unfulfilled/list");
                    var res = JsonConvert.DeserializeObject<Postings>(result);
                    postings.postings.AddRange(res.postings);
                    Log.Add($"{_l} получено {res.postings.Count} заказов " + status);
                    postings.count += res.count;
                }
                //загружаем список заказов, для которых уже делали резервирование
                var reserveList = new List<string>();
                if (File.Exists(_reserveListFile)) {
                    var s = File.ReadAllText(_reserveListFile);
                    var l = JsonConvert.DeserializeObject<List<string>>(s);
                    reserveList.AddRange(l);
                }
                //для каждого заказа сделать заказ с резервом в бизнес.ру
                foreach (var order in postings.postings) {
                    //проверяем наличие резерва
                    if (reserveList.Contains(order.posting_number))
                        continue;
                    //готовим список товаров (id, amount)
                    var goodsDict = new Dictionary<string, int>();
                    order.products.ForEach(s => goodsDict.Add(s.offer_id, s.quantity));
                    var isResMaked = await Class365API.MakeReserve(
                        Class365API.Source("Ozon"),
                        $"Ozon order {order.posting_number}",
                        goodsDict, 
                        order.in_process_at.AddHours(3).ToString());
                    if (isResMaked) {
                        reserveList.Add(order.posting_number);
                        if (reserveList.Count > 1000) {
                            reserveList.RemoveAt(0);
                        }
                        var s = JsonConvert.SerializeObject(reserveList);
                        File.WriteAllText(_reserveListFile, s);
                    }
                }
            } catch (Exception x) {
                Log.Add(_l + "MakeReserve - " + x.Message);
                if (DB.GetParamBool("alertSound"))
                    new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
            }
        }
        //проверка всех карточек в бизнесе, которые изменились и имеют ссылку на озон
        private async Task UpdateProductsAsync() {
            //загружаю список на обновление
            List<GoodObject> busToUpdate;
            if (File.Exists(_busToUpdateFile)) {
                var f = File.ReadAllText(_busToUpdateFile);
                busToUpdate = JsonConvert.DeserializeObject<List<GoodObject>>(f)??new List<GoodObject>();
            } else
                busToUpdate = new List<GoodObject>();
            //список обновленных карточек со ссылкой на объявления
            _busToUpdate = Class365API._bus.Where(_ => _.ozon != null && _.ozon.Contains("http") && _.IsTimeUpDated()
                                                              || busToUpdate.Any(b => b.id == _.id)).ToList();
            if (_busToUpdate.Count > 0) {
                var bu = JsonConvert.SerializeObject(_busToUpdate);
                File.WriteAllText(_busToUpdateFile, bu);
                Log.Add($"{_l} в списке карточек для обновления {_busToUpdate.Count}");
            }
            for (int b = _busToUpdate.Count - 1; b >= 0; b--) {
                if (Class365API.IsTimeOver)
                    return;
                try {
                    if (await Update(_busToUpdate[b])) {
                        _busToUpdate.Remove(_busToUpdate[b]);
                        var bu = JsonConvert.SerializeObject(_busToUpdate);
                        File.WriteAllText(_busToUpdateFile, bu);
                    }
                } catch (Exception x) {
                    Log.Add(_l + "UpdateProductsAsync - " + x.Message);
                }
            }
        }
        //проверяем список товаров озон
        public async Task CheckProductListAsync() {
            try {
                var startTime = DateTime.Now;
                //если файл свежий и товары не добавляли - загружаем с диска
                if (File.Exists(_productListFile) &&
                    (startTime < File.GetLastWriteTime(_productListFile).AddDays(_updateFreq))
                    && !_isProductListCheckNeeds) {
                    if (_productList.Count == 0) {
                        _productList = JsonConvert.DeserializeObject<List<ProductListItem>>(
                            File.ReadAllText(_productListFile));
                    }
                } else {
                    _productList.Clear();
                    var last_id = "";
                    var total = 0;
                    do {
                        var data = new {
                            //filter = new {
                            //    visibility = "ALL" //VISIBLE, INVISIBLE, EMPTY_STOCK, READY_TO_SUPPLY, STATE_FAILED
                            //},
                            last_id = last_id,
                            limit = 1000
                        };
                        var result = await PostRequestAsync(data, "/v2/product/list");
                        var productList = JsonConvert.DeserializeObject<ProductList>(result);
                        last_id = productList.last_id;
                        total = productList.total;
                        _productList.AddRange(productList.items);
                        Log.Add($"{_l} получено {_productList.Count} товаров");
                    } while (_productList.Count < total);
                    Log.Add(_l + "ProductList - успешно загружено " + _productList.Count + " товаров");
                    File.WriteAllText(_productListFile, JsonConvert.SerializeObject(_productList));
                    Log.Add(_l + _productListFile + " - сохранено");
                    File.SetLastWriteTime(_productListFile, startTime);
                    _isProductListCheckNeeds = false; //сбрасываю флаг
                }
                await CheckProductLinksAsync();
            } catch (Exception x) {
                Log.Add(_l + "ProductList - ошибка загрузки товаров - " + x.Message);
            }
        }
        //проверяем привязку товаров в карточки бизнес.ру
        private async Task CheckProductLinksAsync(bool checkAll = false) {
            List<ProductListItem> items;
            if (checkAll) {
                var _checkProductCount = await DB.GetParamIntAsync("ozon.checkProductCount");
                var _checkProductIndex = await DB.GetParamIntAsync("ozon.checkProductIndex");
                if (_checkProductIndex >= _productList.Count)
                    _checkProductIndex = 0;
                await DB.SetParamAsync("ozon.checkProductIndex", (_checkProductIndex + _checkProductCount).ToString());
                items = _productList.Skip(_checkProductIndex).Take(_checkProductCount).ToList<ProductListItem>();
            } else
                items = _productList;
            foreach (var item in items) {
                try {
                    if (Class365API.IsTimeOver)
                        return;
                    //карточка в бизнес.ру с id = артикулу товара на озон
                    var b = Class365API._bus.FindIndex(_ => _.id == item.offer_id);
                    if (b == -1)
                        throw new Exception("карточка бизнес.ру с id = " + item.offer_id + " не найдена!");
                    if (!Class365API._bus[b].ozon.Contains("http") ||                       //если карточка найдена,но товар не привязан к бизнес.ру
                        Class365API._bus[b].ozon.Split('/').Last().Length < 3 ||            //либо ссылка есть, но неверный sku
                        checkAll) {                                                         //либо передали флаг проверять всё
                        var productInfo = await GetProductInfoAsync(Class365API._bus[b]);
                        await SaveUrlAsync(Class365API._bus[b], productInfo);
                        await Update(Class365API._bus[b], productInfo);
                    }
                } catch (Exception x) {
                    Log.Add($"{_l}CheckGoodsAsync ошибка! checkAll:{checkAll} offer_id:{item.offer_id} message:{x.Message}");
                    _isProductListCheckNeeds = true;
                }
            }
        }
        //расширенная информация о товаре
        private async Task<ProductInfo> GetProductInfoAsync(GoodObject bus) {
            var data = new { offer_id = bus.id };
            var s = await PostRequestAsync(data, "/v2/product/info");
            if (s!=null)
                return JsonConvert.DeserializeObject<ProductInfo>(s);
            Log.Add($"{_l}GetProductInfoAsync: предупреждение! [{bus.id}] {bus.name} товар не найден!!");
            return null;
        }
        //обновление ссылки в карточке бизнес.ру
        async Task SaveUrlAsync(GoodObject bus, ProductInfo productInfo) {
            try {
                var sku = productInfo.GetSku();
                if (sku == "0") {
                    Log.Add(_l + "SaveUrlAsync - ошибка! - " + bus.name + " - sku: 0");
                }
                var newUrl = _baseBusUrl + sku;
                if (bus.ozon != newUrl) {
                    bus.ozon = newUrl;
                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                            {"id", bus.id},
                            {"name", bus.name},
                            {_url, bus.ozon}
                        });
                    Log.Add(_l + bus.name + " ссылка на товар обновлена!");
                }
            } catch (Exception x) {
                Log.Add($"{_l} SaveUrlAsync ошибка! name:{bus.name} message:{x.Message}");
            }
        }
        //проверка и обновление товара
        async Task<bool> Update(GoodObject bus, ProductInfo productInfo = null) {
            try {
                if (productInfo == null)
                    productInfo = await GetProductInfoAsync(bus);
                if (productInfo == null)
                    return true;
                await UpdatePrice(bus, productInfo);
                await UpdateStocks(bus, productInfo, 1020000901600000); //основной
                await UpdateStocks(bus, productInfo, 1020002368685000); //realFbs
                //await UpdateStocks(bus, productInfo, 1020005000062736); //эконом
                await UpdateProduct(bus, productInfo);
                return true;
            } catch (Exception x) {
                Log.Add($"{_l} UpdateProductAsync ошибка! name:{bus.name} message:{x.Message}");
                return false;
            }
        }
        //обновление остатков товара на озон
        private async Task UpdateStocks(GoodObject bus, ProductInfo productInfo, long warehouseId) {
            try {
                var amount = bus.Amount;
                //обнуляем остаток, если остаток ниже нуля или товар стал б/у
                if (!bus.New || amount < 0)
                    amount = 0;
                int warehouseCount = 2;
                if (amount * warehouseCount == productInfo.GetStocks())
                    return;
                //для склада Эконом обнуляем остаток
                //if (warehouseId == 1020005000062736)
                //    amount = 0;

                //объект для запроса
                var data = new {
                    stocks = new[] {
                        new {
                            product_id = productInfo.id,
                            stock = amount.ToString("F0"),
                            warehouse_id = warehouseId
                        }
                    }
                };
                var s = await PostRequestAsync(data, "/v2/products/stocks");
                var res = JsonConvert.DeserializeObject<List<UpdateResult>>(s);
                if (res.First().updated)
                    Log.Add($"{_l}UpdateProductStocks: остаток для склада id={warehouseId} обновлен - {bus.name} ({amount})");
                else
                    throw new Exception(s);

            } catch (Exception x) {
                Log.Add($"{_l} UpdateProductStocks: ошибка обновления остатка склада id={warehouseId} - {bus.name} - {x.Message}");
                //if (DB.GetParamBool("alertSound"))
                //    new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
            }
        }
        //расчет цен с учетом наценки
        private int GetNewPrice(GoodObject b) {
            var weight = b.Weight;
            var d = b.GetDimentions();
            var length = d[0] + d[1] + d[2];
            int overPrice;
            //вес от 50 кг или размер более 200 -- наценка 30% + 3500 р
            if (weight >= 50 || length >= 200)
                overPrice = (int) (b.Price * 0.30) + 3500;
            //вес от 30 кг или размер от 150 -- наценка 30% + 2000 р
            else if (weight >= 30 || length >= 150)
                overPrice = (int) (b.Price * 0.30) + 2000;
            //вес от 10 кг или размер от 100 -- наценка 1500 р
            else if (weight >= 10 || length >= 100)
                overPrice = (int) (b.Price * 0.30) + 1500;
            //для маленьких и легких наценка 40% на всё
            else
                overPrice = (int) (b.Price * 0.40);
            //если наценка меньше 200 р - округляю
            if (overPrice < 200)
                overPrice = 200;
            return b.Price + overPrice;
        }
        //цена до скидки (старая)
        private int GetOldPrice(int newPrice) {
            return (int) (Math.Ceiling((newPrice * (1 + _oldPriceProcent / 100)) / 100) * 100);
        }
        //Проверка и обновление цены товара на озон
        async Task UpdatePrice(GoodObject bus, ProductInfo productInfo) {
            try {
                _newPrice = GetNewPrice(bus);
                var oldPrice = GetOldPrice(_newPrice);
                var min_price = (int) (_newPrice * (0.01 * (100 - _minPriceProcent)));
                //сверяем цены с озоном
                if (productInfo.GetPrice() != _newPrice
                || productInfo.GetOldPrice() != oldPrice
                || productInfo.GetMinPrice() != min_price
                //|| productInfo.GetMarketingPrice() < newPrice
                ) {
                    var data = new {
                        prices = new[] {
                            new { 
                                /*offer_id = bus.id, */ 
                                product_id = productInfo.id,
                                old_price = oldPrice.ToString(),
                                price = _newPrice.ToString(),
                                auto_action_enabled = "DISABLED",//"ENABLED",
                                price_strategy_enabled = "DISABLED",
                                min_price = min_price.ToString(),
                            }
                        }
                    };
                    var s = await PostRequestAsync(data, "/v1/product/import/prices");
                    var res = JsonConvert.DeserializeObject<List<UpdateResult>>(s);
                    if (res.First().updated) {
                        Log.Add(_l + bus.name + " (" + bus.Price + ") цены обновлены! ("
                                   + _newPrice + ", " + oldPrice + ")");
                    } else {
                        Log.Add(_l + bus.name + " ошибка! цены не обновлены! (" + bus.Price + ")" + " >>> " + s);
                    }
                }
            } catch (Exception x) {
                Log.Add(_l + " ошибка обновления цены! - " + x.Message);
            }
        }
        //запросы к api ozon
        public async Task<string> PostRequestAsync(object request, string apiRelativeUrl) {
            try {
                var httpContent = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

                HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod("POST"), apiRelativeUrl);
                requestMessage.Headers.Add("Client-Id", _clientID);
                requestMessage.Headers.Add("Api-Key", _apiKey);
                requestMessage.Content = httpContent;

                var response = await _hc.SendAsync(requestMessage);

                await Task.Delay(500);
                if (response.StatusCode == HttpStatusCode.OK) {
                    var js = await response.Content.ReadAsStringAsync();
                    RootResponse rr = JsonConvert.DeserializeObject<RootResponse>(js);
                    if (rr.has_next)
                        _hasNext = true;
                    else
                        _hasNext = false;
                    return JsonConvert.SerializeObject(rr.result);
                } else
                    throw new Exception(response.StatusCode + " " + response.ReasonPhrase + " " + response.Content);
            } catch (Exception x) {
                //throw new Exception($"{_l} PostRequestAsync ошибка запроса! apiRelativeUrl:{apiRelativeUrl} request:{request} message:{x.Message}");
                Log.Add($"{_l} PostRequestAsync ошибка запроса! apiRelativeUrl:{apiRelativeUrl} request:{request} message:{x.Message}");
            }
            return null;
        }

        //запросы к api ozon
        public async Task<string> GetRequestAsync(Dictionary<string, string> request, string apiRelativeUrl) {
            try {
                if (request != null) {
                    var qstr = QueryStringBuilder.BuildQueryString(request);
                    apiRelativeUrl += "?" + qstr;
                }
                HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod("GET"), apiRelativeUrl);
                requestMessage.Headers.Add("Client-Id", _clientID);
                requestMessage.Headers.Add("Api-Key", _apiKey);
                var response = await _hc.SendAsync(requestMessage);
                if (response.StatusCode == HttpStatusCode.OK) {
                    return await response.Content.ReadAsStringAsync();
                } else
                    throw new Exception(response.StatusCode + " " + response.ReasonPhrase + " " + response.Content);
            } catch (Exception x) {
                Log.Add($"{_l} PostRequestAsync ошибка запроса! apiRelativeUrl:{apiRelativeUrl} request:{request} message:{x.Message}");
            }
            return null;
        }
        //обновление описаний товаров
        private async Task UpdateProduct(GoodObject good, ProductInfo productInfo) {
            try {
                if (!good.New)
                    throw new Exception("товар стал Б/У!!");
                //проверяем группу товара
                var attributes = await GetAttributesAsync(good);
                //Запрашиваю атрибуты товара с озон
                var productFullInfo = await GetProductFullInfoAsync(productInfo);
                File.WriteAllText(@"..\data\ozon\product_" + productFullInfo.First().offer_id + ".json",
                    JsonConvert.SerializeObject(productFullInfo));                //формирую объект запроса
                if (attributes.categoryId == "none") {
                    _isProductListCheckNeeds = true;
                    return;
                }
                if (attributes.typeId == "0")
                    return;
                var data = new {
                    items = new[] {
                        new{
                            attributes = new List<Attribute>(),
                            name = good.NameLimit(_nameLimit),
                            currency_code="RUB",
                            offer_id=good.id,
                            description_category_id=attributes.categoryId,
                            //category_id=productFullInfo[0].category_id,
                            price = GetNewPrice(good).ToString(),
                            old_price = GetOldPrice(GetNewPrice(good)).ToString(),
                            weight = (int)(good.Weight*1000),                  //Вес с упаковкой, г
                            weight_unit = "g",
                            depth = good.SizeMM("width",30),           //мм
                            height = good.SizeMM("height",30),         //мм
                            width = good.SizeMM("length",30),          //мм
                            dimension_unit = "mm",
                            primary_image = good.images.First().url,                //главное фото
                            images = good.images.Skip(1).Take(14).Select(_=>_.url).ToList(),
                            vat="0.0"                                               //налог
                        }
                    }
                };
                //переносим в объект запроса атрибуты из товара озон, которые уже есть
                foreach (var item in productFullInfo[0].attributes) {
                    //Пропускаю некоторые атрибуты
                    if (item.id != 4180 &&                                          //Название
                    item.id != 9024 &&                                              //Артикул
                    item.id != 9048 &&                                              //Название модели (для объединения в одну карточку)
                    item.id != 4191 &&                                              //Аннотация
                    item.id != 22387 &&                                             //Группа товара

                    //item.id != 85 &&                                                //Бренд (теперь используем Без бренда,
                    //убрать если нужно сохранять бренд, уже установленных в товарах)
                    item.values.Length > 0) {
                        var values = new Value {
                            value = item.values[0].value,
                            dictionary_value_id = item.values[0].dictionary_value_id
                        };
                        //если такой атрибут уже есть - удаляю из списка и добавляю
                        var i = data.items[0].attributes.Find(a => a.id == item.id);
                        if (i != null)
                            data.items[0].attributes.Remove(i);
                        data.items[0].attributes.Add(new Attribute {
                            id = item.id,
                            complex_id = item.complex_id,
                            values = new Value[] { values }
                        });
                    }
                }
                //теперь добавлю характеристики из карточки
                if (attributes.additionalAttributes != null && attributes.additionalAttributes.Count > 0)
                    foreach (var item in attributes.additionalAttributes) {
                        if (item.id == 9782)                                        //Класс опасности (невозможно поменять!)
                            continue;
                        var values = new Value {
                            value = item.values[0].value,
                            dictionary_value_id = item.values[0].dictionary_value_id
                        };
                        //если такой атрибут уже есть - удаляю из списка и добавляю
                        var i = data.items[0].attributes.Find(a => a.id == item.id);
                        if (i != null) {
                            //проверяю бренд
                            if (i.id == 85 && i.values[0].value != item.values[0].value) {
                                Log.Add(_l + good.name + " - заменить бренд в бизнесе! - " + item.values[0].value + " на " + i.values[0].value);
                                //временно пропускаю карточку, чтобы не слетать на модерацию по бренду
                                continue;
                            }
                            data.items[0].attributes.Remove(i);
                        }
                        data.items[0].attributes.Add(new Attribute {
                            id = item.id,
                            complex_id = item.complex_id,
                            values = new Value[] { values }
                        });
                    }
                var s = await PostRequestAsync(data, "/v3/product/import");
                var res = JsonConvert.DeserializeObject<ProductImportResult>(s);
                if (res.task_id != default(int)) {
                    Log.Add($"{_l} UpdateProduct: id:{good.id} {good.name} - товар отправлен на озон!");
                } else {
                    Log.Add($"{_l} UpdateProduct: id:{good.id} {good.name} - ошибка отправки товара на озон! - {s}");
                }
                ////
                var res2 = new ProductImportInfo();
                await Task.Delay(1000);
                var data2 = new {
                    task_id = res.task_id.ToString()
                };
                s = await PostRequestAsync(data2, "/v1/product/import/info");
                res2 = JsonConvert.DeserializeObject<ProductImportInfo>(s);
                Log.Add(_l + good.name + " status товара - " + res2.items.First().status);
                if (res2.items.First().errors.Length > 0)
                    Log.Add(_l + good.name + " ошибка - " + s);
                ////
            } catch (Exception x) {
                Log.Add($"{_l} UpdateProduct: ошибка обновления описания {good.id} {good.name} - {x.Message}");
            }
        }
        //добавление новых товаров на ozon
        async Task Add() {
            var count = await DB.GetParamIntAsync("ozon.countToAdd");
            if (count == 0)
                return;
            if (_isProductListCheckNeeds)
                await CheckProductListAsync();
            //список карточек которые еще не добавлены на озон
            var goods = Class365API._bus.Where(w => w.Amount > 0
                                     && w.Price > 0
                                     && w.Part != null
                                     && w.images.Count > 0
                                     && w.height != null
                                     && w.length != null
                                     && w.width != null
                                     && w.New
                                     && w.ozon.Length == 0
                                     && !_productList.Any(_ => w.id == _.offer_id)
                                     && !_exceptionGoods.Any(e => w.name.ToLowerInvariant().Contains(e))
                                     && !_exceptionGroups.Any(e => w.GroupName.ToLowerInvariant().Contains(e))).ToList();
            SaveToFile(goods);
            var goods2 = Class365API._bus.Where(w => w.Amount > 0
                                     && w.Price > 0
                                     && w.images.Count > 0
                                     && w.New
                                     && w.ozon.Length == 0
                                     && !_productList.Any(_ => w.id == _.offer_id)
                                     && !_exceptionGoods.Any(e => w.name.ToLowerInvariant().Contains(e))).ToList(); //нет в исключениях
            SaveToFile(goods2, @"..\data\ozon\ozonGoodListForAdding_all.xlsx");
            Log.Add(_l + "карточек для добавления: " + goods.Count + " (" + goods2.Count + ")");
            int i = 0;
            foreach (var good in goods) {
                if (Class365API.IsTimeOver)
                    return;
                try {
                    //проверяем группу товара
                    var attributes = await GetAttributesAsync(good);
                    if (attributes.typeId == "0" || attributes.categoryId == "none")
                        continue;
                    //формирую объект запроса
                    var data = new {
                        items = new[] {
                            new{
                                attributes = new List<Attribute>(),
                                name = good.NameLimit(_nameLimit),
                                currency_code="RUB",
                                offer_id=good.id,
                                description_category_id=attributes.categoryId,
                                price = GetNewPrice(good).ToString(),
                                old_price = GetOldPrice(GetNewPrice(good)).ToString(),
                                weight = (int)(good.Weight*1000),                           //вес с упаковкой, г
                                weight_unit = "g",
                                depth = good.SizeMM("width",30),           //мм
                                height = good.SizeMM("height",30),         //мм
                                width = good.SizeMM("length",30),          //мм
                                dimension_unit = "mm",
                                primary_image = good.images.First().url,                    //главное фото
                                images = good.images.Skip(1).Take(14).Select(_=>_.url).ToList(),
                                vat="0.0"                                                   //налог
                            }
                        }
                    };
                    if (attributes.additionalAttributes != null && attributes.additionalAttributes.Count > 0)
                        data.items[0].attributes.AddRange(attributes.additionalAttributes);
                    var s = await PostRequestAsync(data, "/v3/product/import");
                    var res = JsonConvert.DeserializeObject<ProductImportResult>(s);
                    if (res.task_id != default(int)) {
                        Log.Add($"{_l}Add: добавлен товар - {good.name}");
                        ++i;
                    } else {
                        Log.Add($"{_l}Add: ошибка отправки товара - {good.name}");
                    }
                    var res2 = new ProductImportInfo();
                    await Task.Delay(10000);
                    var data2 = new {
                        task_id = res.task_id.ToString()
                    };
                    s = await PostRequestAsync(data2, "/v1/product/import/info");
                    res2 = JsonConvert.DeserializeObject<ProductImportInfo>(s);
                    Log.Add($"{_l}Add: статус товара {good.name} - {res2.items.First().status}");
                    if (res2.items.First().errors.Length > 0)
                        Log.Add($"{_l}Add: ошибка добавления - {good.name} - {s}");
                    _isProductListCheckNeeds = true;
                } catch (Exception x) {
                    Log.Add($"{_l}Add: ошибка добавления - {good.name} - {x.Message + x.InnerException?.Message}");
                }
                if (i >= count)
                    break;
            }
        }
        public async Task DeactivateActions() {
            try {
                //получаем список акций
                var res = await GetRequestAsync(null, "/v1/actions");
                var actions = JsonConvert.DeserializeObject<OzonActionsList>(res);
                //проверяем каждую акцию
                var limit = await DB.GetParamIntAsync("ozon.deactivateActionsLimit");
                for (int i = 0; i < actions.result.Count; i++) {
                    //проверим товары, которые можно добавить в акции
                    //задаем смещение в запросе
                    var offset = 0;
                    List<OzonActionProduct> products;
                    OzonActionProducts actionProducts;
                    if (Class365API.SyncStartTime.Minute < Class365API._checkIntervalMinutes) {
                        do {
                            if (Class365API.IsTimeOver)
                                return;
                            var data5 = new {
                                action_id = actions.result[i].id,
                                limit = limit,
                                offset = offset
                            };
                            var res5 = await PostRequestAsync(data5, "/v1/actions/candidates");
                            actionProducts = JsonConvert.DeserializeObject<OzonActionProducts>(res5);
                            //отбираем товары, у которых скидка в норме, меньше чем максимально допустимая
                            products = actionProducts.products.Where(w => w.GetActionPrice() >= w.GetMinPrice()).ToList();
                            //Log.Add($"DeactivateActions: в акции {actions.result[i].id} проверено {offset + limit} кандидатов");
                            offset += limit;
                        } while (!products.Any() && offset < actionProducts.total);
                        //добавляем в данную акцию эти товары
                        if (products.Any()) {
                            var data2 = new {
                                action_id = actions.result[i].id,
                                products = products.Select(p => new {
                                    product_id = p.id,
                                    action_price = p.action_price,
                                    //stock=p.stock
                                    stock = Class365API.FindGood(_productList.Find(f => f.product_id == p.id)?.offer_id)?.Amount ?? 1
                                }).ToArray()
                            };
                            var res4 = await PostRequestAsync(data2, "/v1/actions/products/activate");
                            Log.Add($"{_l} DeactivateActions: в акцию {actions.result[i].id} добавлены товары [{data2.products.Length}] {res4}");
                        } else
                            Log.Add($"{_l} DeactivateActions: в акцию {actions.result[i].id} проверено кандидатов {actionProducts.total}, " +
                                $"у всех скидка больше максимальной");
                    }
                    //если текущей акции нет товаров - пропускаем проверку
                    if (actions.result[i].participating_products_count == 0)
                        continue;
                    //задаем смещение в запросе
                    offset = 0;
                    //диапазон для выбора смещения
                    var range = actions.result[i].participating_products_count - limit;
                    //если он положительный - выбираем случайное смещение в рамках диапазона
                    if (range > 0)
                        offset = _rnd.Next(range);
                    var data = new {
                        action_id = actions.result[i].id,
                        limit = limit,
                        offset = offset
                    };
                    var res2 = await PostRequestAsync(data, "/v1/actions/products");
                    var action = JsonConvert.DeserializeObject<OzonActionProducts>(res2);
                    //отбираем товары, у которых скидка больше, чем максимально допустимая
                    products = action.products.Where(w => w.GetActionPrice() < w.GetMinPrice()).ToList();
                    //отменяем участие в данной акции этих товаров
                    if (!products.Any())
                        continue;
                    var data6 = new {
                        action_id = actions.result[i].id,
                        product_ids = products.Select(p => p.id).ToArray()
                    };
                    var res3 = await PostRequestAsync(data6, "/v1/actions/products/deactivate");
                    Log.Add($"{_l} DeactivateActions: из акции {actions.result[i].id} удалены товары [{data6.product_ids.Length}] {res3}");
                }
            } catch (Exception x) {
                Log.Add($"{_l} DeactivateActions: ошибка! - {x.Message}");
            }
        }
        //массовый запрос цен
        async Task<List<OzonPriceListItem>> GetPrices() {
            var last_id = "";
            var total = 0;
            var priceList = new List<OzonPriceListItem>();
            do {
                var data = new {
                    filter = new {
                        visibility = "ALL" //VISIBLE, INVISIBLE, EMPTY_STOCK, READY_TO_SUPPLY, STATE_FAILED
                    },
                    last_id = last_id,
                    limit = 1000
                };
                var result = await PostRequestAsync(data, "/v4/product/info/prices");
                var pl = JsonConvert.DeserializeObject<OzonPriceList>(result);
                last_id = pl.last_id;
                total = pl.total;
                priceList.AddRange(pl.items);
                Log.Add($"{_l} получено {priceList.Count} цен товаров");
            } while (priceList.Count < total);
            return priceList;
        }
        //проверка цен - массовый запрет автодобавления в акции
        public async Task DeactivateAutoActions() {
            try {
                if (Class365API.IsTimeOver)
                    return;
                var priceList = await GetPrices();
                var autoActionList = priceList.Where(w => w.price.auto_action_enabled).Take(200);
                foreach (var product in autoActionList) {
                    if (Class365API.IsTimeOver)
                        return;
                    var data = new {
                        prices = new[] {
                            new { 
                                /*offer_id = bus.id, */ 
                                product_id = product.product_id,
                                old_price = product.price.old_price,
                                price = product.price.price,
                                auto_action_enabled = "DISABLED",//"ENABLED",
                                price_strategy_enabled = "DISABLED",
                                //min_price = product.price.min_ozon_price
                            }
                        }
                    };
                    var s = await PostRequestAsync(data, "/v1/product/import/prices");
                    var res = JsonConvert.DeserializeObject<List<UpdateResult>>(s);
                    if (res.First().updated) {
                        Log.Add($"{_l} DeactivateAutoActions: {Class365API.FindGood(product.offer_id)?.name} - автоакция отменена!");
                    } else {
                        Log.Add($"{_l} DeactivateAutoActions: {Class365API.FindGood(product.offer_id)?.name} - автоакция не отменена!");
                    }
                }
            } catch (Exception x) {
                Log.Add($"{_l} DeactivateAutoActions: ошибка отмены автоакций! - {x.Message}");
                if (DB.GetParamBool("alertSound"))
                    new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
            }
        }
        string GetTypeName(XElement rule) {
            var parent = rule.Parent;
            if (parent != null) {
                return parent.Attribute("Name").Value;
            }
            return null;
        }
        //получить атрибуты и категорию товара на озон
        async Task<Attributes> GetAttributesAsync(GoodObject bus) {
            try {
                var n = bus.name.ToLowerInvariant();
                var a = new Attributes() {
                    typeId = "none",
                    categoryId = "none"
                };
                foreach (var rule in _rules) {
                    var conditions = rule.Elements();
                    var doSearch = true;
                    foreach (var condition in conditions) {
                        if (!doSearch)
                            break;
                        if (condition.Name == "Starts" && !n.StartsWith(condition.Value))
                            doSearch = false;
                        else if (condition.Name == "Contains" && !n.Contains(condition.Value))
                            doSearch = false;
                    }
                    if (doSearch)
                        a.typeName = GetTypeName(rule);
                }
                if (a.typeName == null) {
                    return a;
                }
                if (!GetTypeIdAndCategoryId(a))
                    return a;
                a.additionalAttributes.AddAttribute(await GetPlaceAttributeAsync(bus, a));
                a.additionalAttributes.AddAttribute(GetPackQuantityAttribute(bus));
                a.additionalAttributes.AddAttribute(GetCountAttribute());
                a.additionalAttributes.AddAttribute(GetTypeOfProductAttribute(a.typeId, a.typeName));
                a.additionalAttributes.AddAttribute(GetBrendAttribute(bus));
                a.additionalAttributes.AddAttribute(GetPartAttribute(bus));
                a.additionalAttributes.AddAttribute(GetDescriptionAttribute(bus));
                a.additionalAttributes.AddAttribute(GetModelNameAttribute(bus));
                a.additionalAttributes.AddAttribute(GetComplectationAttribute(bus));
                a.additionalAttributes.AddAttribute(GetAlternativesAttribute(bus));
                a.additionalAttributes.AddAttribute(GetFabricBoxCountAttribute(bus));
                a.additionalAttributes.AddAttribute(await GetColorAttributeAsync(bus, a));
                a.additionalAttributes.AddAttribute(await GetTechTypeAttributeAsync(bus, a));
                a.additionalAttributes.AddAttribute(await GetDangerClassAttributeAsync(bus, a));
                a.additionalAttributes.AddAttribute(GetExpirationDaysAttribute(bus));
                a.additionalAttributes.AddAttribute(await GetMaterialAttributeAsync(bus, a));
                a.additionalAttributes.AddAttribute(await GetManufactureCountryAttributeAsync(bus, a));
                a.additionalAttributes.AddAttribute(GetOEMAttribute(bus));
                a.additionalAttributes.AddAttribute(await GetSideAttributeAsync(bus, a));
                a.additionalAttributes.AddAttribute(GetMultiplicityAttribute(bus));
                a.additionalAttributes.AddAttribute(GetKeywordsAttribute(bus));
                a.additionalAttributes.AddAttribute(GetThicknessAttribute(bus));
                a.additionalAttributes.AddAttribute(GetHeightAttribute(bus));
                a.additionalAttributes.AddAttribute(GetLengthAttribute(bus));
                a.additionalAttributes.AddAttribute(await GetMotorTypeAttributeAsync(bus, a));
                a.additionalAttributes.AddAttribute(await GetPlacementAttributeAsync(bus, a));
                a.additionalAttributes.AddAttribute(GetVolumeAttribute(bus));
                a.additionalAttributes.AddAttribute(GetVolumeMLAttribute(bus));
                a.additionalAttributes.AddAttribute(await GetTNVEDAttribute(bus, a));
                a.additionalAttributes.AddAttribute(GetCountInBoxAttribute(bus));
                a.additionalAttributes.AddAttribute(GetCountOfHolesAttribute(bus));
                a.additionalAttributes.AddAttribute(GetGarantyAttribute(bus));
                a.additionalAttributes.AddAttribute(GetDiameterOutAttribute(bus));
                a.additionalAttributes.AddAttribute(GetDiameterInAttribute(bus));
                return a;

                ///для определение категорий вызываем метод Дерево категорий и типов товаров (версия 2)
                ///для корневой группы, например кузовные запчасти
                ///https://api-seller.ozon.ru/v1/description-category/tree
                ///для полученных категорий вызываем справочник характеристик
                ///https://api-seller.ozon.ru/v2/category/attribute/values



                //var t = await GetAttibuteValuesAsync(attribute_id: 8229, category_id: a.categoryId);
                //Log.Add(t.Select(s => "\nid: " + s.id + " " + s.value).Aggregate((x, y) => x + y));
                //await Task.Delay(3000);
            } catch (Exception x) {
                throw new Exception($"GetAttributesAsync: {bus.name} - {x.Message}");
            }

        }

        //Оригинальные запчасти ?? 9104  dictionary_id": 1835
        //Напряжение?? 5381 "dictionary_id": 48
        //Атрибут Применимость




        //Атрибут Внешний диаметр, см
        Attribute GetDiameterOutAttribute(GoodObject good) {
            var value = good.GetDiameterOut();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 7366,
                values = new Value[] {
                    new Value{
                        value = value
                    }
                }
            };
        }
        //Атрибут Внутренний диаметр, см
        Attribute GetDiameterInAttribute(GoodObject good) {
            var value = good.GetDiameterIn();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 7368,
                values = new Value[] {
                new Value{
                    value = value
                }
            }
            };
        }
        //Атрибут Гарантия
        Attribute GetGarantyAttribute(GoodObject good) {
            var value = good.GetGaranty();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 4385,
                values = new Value[] {
                new Value{
                    value = value
                }
            }
            };
        }
        //Атрибут Количество отверстий
        Attribute GetCountOfHolesAttribute(GoodObject good) {
            var value = good.GetCountOfHoles();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 20407,
                values = new Value[] {
                new Value{
                    value = value
                }
            }
            };
        }

        //Атрибут Количество в упаковке
        Attribute GetCountInBoxAttribute(GoodObject good) {
            var value = good.GetCountInBox();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 8513,
                values = new Value[] {
                new Value{
                    value = value
                }
            }
            };
        }

        //Атрибут Код ТН ВЭД 
        async Task<Attribute> GetTNVEDAttribute(GoodObject good, Attributes a) {
            var value = good.hscode_id;
            if (value == null)
                return null;
            var tnved = await GetAttibuteValuesAsync(a.typeId, attribute_id: "22232");
            //await UpdateVedAsync(a.typeId);
            var code = tnved.Find(f => f.value.Contains(value));
            if (code == null || code.id == null || code.value == null) {
                Log.Add($"{_l} GetTNVEDAttribute: ошибка - ТНВЭД {good.hscode_id} не найден! наименование: {good.name}, id: {good.id}");
                return null;
            }
            return new Attribute {
                complex_id = 0,
                id = 22232,
                values = new Value[] {
                    new Value {
                        value = code.value,
                        dictionary_value_id = code.id.ToString()
                    }
                }
            };
        }
        //Атрибут Объем, л
        Attribute GetVolumeAttribute(GoodObject good) {
            var value = good.GetVolume();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 7194,
                values = new Value[] {
                    new Value{
                        value = value
                    }
                }
            };
        }
        //Атрибут Объем, мл
        Attribute GetVolumeMLAttribute(GoodObject good) {
            var value = good.GetVolumeML();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 5710,
                values = new Value[] {
                    new Value{
                        value = value
                    }
                }
            };
        }
        //Атрибут Место установки
        async Task<Attribute> GetPlacementAttributeAsync(GoodObject good, Attributes a) {
            var value = good.GetPlacement();
            if (value == null)
                return null;
            var placement = await GetAttibuteValuesAsync(a.typeId, attribute_id: "7367");
            var p = placement?.Find(f => f.value == value).id.ToString();
            if (p == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 7367,
                values = new Value[] {
                    new Value{
                        value = value,
                        dictionary_value_id = p,
                    }
                }
            };
        }
        //Атрибут Тип двигателя
        async Task<Attribute> GetMotorTypeAttributeAsync(GoodObject good, Attributes a) {
            var value = good.GetMotorType();
            if (value == null)
                return null;
            var motorType = await GetAttibuteValuesAsync(a.typeId, attribute_id: "8303");
            var mt = motorType?.Find(f => f.value == value)?.id.ToString();
            if (mt == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 8303,
                values = new Value[] {
                    new Value{
                        value = value,
                        dictionary_value_id = mt,
                    }
                }
            };
        }
        //Атрибут Длина, см
        Attribute GetLengthAttribute(GoodObject good) {
            var value = good.GetLengthAttr();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 9802,
                values = new Value[] {
                    new Value{
                        value = value
                    }
                }
            };
        }
        //Атрибут Высота, см
        Attribute GetHeightAttribute(GoodObject good) {
            var value = good.GetHeight();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 6606,
                values = new Value[] {
                    new Value{
                        value = value
                    }
                }
            };
        }
        //Атрибут Толщина, см
        Attribute GetThicknessAttribute(GoodObject good) {
            var value = good.GetThickness();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 6859,
                values = new Value[] {
                    new Value{
                        value = value
                    }
                }
            };
        }
        //Атрибут Ключевые слова
        Attribute GetKeywordsAttribute(GoodObject good) {
            var value = good.GetKeywords();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 22336,
                values = new Value[] {
                    new Value{
                        value = value.Replace(",",";")
                    }
                }
            };
        }
        //Атрибут Квант продажи, шт (Кратность покупки)
        Attribute GetMultiplicityAttribute(GoodObject good) {
            var value = good.GetQuantOfSell();
            if (value == 1)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 21497,
                values = new Value[] {
                    new Value{
                        value = value.ToString()
                    }
                }
            };
        }
        //Атрибут Расположение детали
        async Task<Attribute> GetPlaceAttributeAsync(GoodObject good, Attributes a) {
            var value = good.GetPlace();
            if (value == null)
                return null;
            var place = await GetAttibuteValuesAsync(a.typeId, attribute_id: "20189");
            var p = place?.Find(f => f.value == value);
            if (p == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 20189,
                values = new Value[] {
                    new Value{
                        value = value,
                        dictionary_value_id = p.id.ToString(),
                    }
                }
            };
        }
        //Атрибут ОЕМ-номер
        Attribute GetOEMAttribute(GoodObject good) {
            var man = good.GetManufacture(true)?
                          .ToLowerInvariant();
            if (_exceptionBrands.Contains(man))
                return null;
            var value = good.GetOEM();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 7324,
                values = new Value[] {
                    new Value{
                        value = value
                    }
                }
            };
        }
        //Атрибут Страна-изготовитель
        async Task<Attribute> GetManufactureCountryAttributeAsync(GoodObject good, Attributes a) {
            var value = good.GetManufactureCountry();
            if (value == null)
                return null;
            var manufactureCoutry = await GetAttibuteValuesAsync(a.typeId, attribute_id: "4389");
            var mc = manufactureCoutry?.Find(f => f.value == value);
            if (mc == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 4389,
                values = new Value[] {
                    new Value{
                        value = value,
                        dictionary_value_id = mc.id.ToString(),
                    }
                }
            };
        }
        //Атрибут Материал
        async Task<Attribute> GetMaterialAttributeAsync(GoodObject good, Attributes a) {
            var value = good.GetMaterial();
            if (value == null)
                return null;
            var material = await GetAttibuteValuesAsync(a.typeId, attribute_id: "7199");
            var m = material?.Find(f => f.value == value)?.id.ToString();
            if (m == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 7199,
                values = new Value[] {
                    new Value{
                        value = value,
                        dictionary_value_id = m,
                    }
                }
            };
        }
        //Атрибут Срок годности, дней
        Attribute GetExpirationDaysAttribute(GoodObject good) {
            var value = good.GetExpirationDays();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 8205,
                values = new Value[] {
                    new Value{
                        value = value
                    }
                }
            };
        }
        //Атрибут Класс опасности
        async Task<Attribute> GetDangerClassAttributeAsync(GoodObject good, Attributes a) {
            var value = good.GetDangerClass();
            if (value == null)
                return null;
            var dangerClass = await GetAttibuteValuesAsync(a.typeId, attribute_id: "9782");
            var d = dangerClass?.Find(f => f.value.Contains(value));
            if (d == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 9782,
                values = new Value[] {
                    new Value{
                        dictionary_value_id = d.id.ToString(),
                        value = d.value.ToString()
                    }
                }
            };
        }
        //Атрибут Вид техники
        async Task<Attribute> GetTechTypeAttributeAsync(GoodObject good, Attributes a) {
            var value = good.GetTechType();
            if (value == null)
                return null;
            var techType = await GetAttibuteValuesAsync(a.typeId, attribute_id: "7206");
            var t = techType?.Find(f => f.value == value)?.id.ToString();
            if (t == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 7206,
                values = new Value[] {
                    new Value{
                        dictionary_value_id = t,
                        value = value
                    }
                }
            };
        }
        //Атрибут Цвет товара
        async Task<Attribute> GetColorAttributeAsync(GoodObject good, Attributes a) {
            var value = good.GetColor();
            if (value == null)
                return null;
            var color = await GetAttibuteValuesAsync(a.typeId, attribute_id: "10096");
            var col = color?.Find(f => f.value == value)?.id.ToString();
            if (col == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 10096,
                values = new Value[] {
                    new Value{
                        dictionary_value_id = col,
                        value = value
                    }
                }
            };
        }
        //Атрибут Количество заводских упаковок
        Attribute GetFabricBoxCountAttribute(GoodObject good) {
            var value = good.GetFabricBoxCount();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 11650,
                values = new Value[] {
                    new Value{
                        value = value
                    }
                }
            };
        }
        //Атрибут Альтернативные артикулы
        Attribute GetAlternativesAttribute(GoodObject good) {
            var man = good.GetManufacture(true)?
                          .ToLowerInvariant();
            if (_exceptionBrands.Contains(man))
                return null;
            var value = good.GetAlternatives();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 11031,
                values = new Value[] {
                    new Value{
                        value = value
                    }
                }
            };
        }
        //Атрибут Комплектация
        Attribute GetComplectationAttribute(GoodObject good) {
            var value = good.GetComplectation();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 4384,
                values = new Value[] {
                    new Value{
                        value = value
                    }
                }
            };
        }
        //Сторона установки (параметр)
        async Task<Attribute> GetSideAttributeAsync(GoodObject good, Attributes a) {
            var value = good.GetSide();
            if (value == null)
                return null;
            var side = await GetAttibuteValuesAsync(a.typeId, attribute_id: "22329");
            var s = side?.Find(f => f.value == value);
            if (s == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 22329,
                values = new Value[] {
                    new Value{
                        dictionary_value_id = s.id.ToString(),
                        value = value
                    }
                }
            };
        }

        //Атрибут Название модели (для объединения в одну карточку)
        //(в нашем случае дублируем id карточки бизнес.ру)
        Attribute GetModelNameAttribute(GoodObject bus) {
            return new Attribute {
                complex_id = 0,
                id = 9048,
                values = new Value[] {
                new Value{
                    value = bus.id.ToString()
                }
            }
            };
        }

        //Атрибут Аннотация Описание товара
        Attribute GetDescriptionAttribute(GoodObject good) {
            try {
                return new Attribute {
                    complex_id = 0,
                    id = 4191,
                    values = new Value[] {
                    new Value{
                        value = good.DescriptionList()
                            .Where(w=>!w.ToLower().Contains("оригинал")
                                    &&!w.ToLower().Contains("новая")
                                    &&!w.ToLower().Contains("новые")
                                    &&!w.ToLower().Contains("новое")
                                    &&!w.ToLower().Contains("новый")
                                    &&!w.ToLower().Contains("недочет")
                                    &&!w.ToLower().Contains("недочёт")
                                    &&!w.ToLower().Contains("наличии")
                                    &&!w.ToLower().Contains("упаковк")
                                    &&!w.ToLower().Contains("без короб")
                                    &&!w.ToLower().Contains("уценка")
                                    &&!w.ToLower().Contains("уценен")
                                    &&!w.ToLower().Contains("уценён")
                                    &&!w.ToLower().Contains("витринн")
                            )
                            .Aggregate((a,b)=>a+"<br>"+b)
                    }
                }
                };
            } catch (Exception x) {
                Log.Add($"{_l}GetDescriptionAttribute: ошибка описания! [{good.id}] {good.name} - {x.Message}");
                throw x;
            }
        }
        //Атрибут Тип товара
        Attribute GetTypeOfProductAttribute(string id, string name) =>
            new Attribute {
                complex_id = 0,
                id = 8229,
                values = new Value[] {
                    new Value{
                        dictionary_value_id = id,
                        value = name
                    }
                }
            };
        //Атрибут Количество в упаковке
        Attribute GetPackQuantityAttribute(GoodObject bus) =>
            new Attribute {
                complex_id = 0,
                id = 7335,
                values = new Value[]{
                    new Value{
                        value = bus.GetPackQuantity()
                    }
                }
            };
        //Атрибут Количество штук (обязательный параметр)
        Attribute GetCountAttribute(int cnt = 1) =>
            new Attribute {
                complex_id = 0,
                id = 7202,
                values = new Value[] {
                    new Value{
                        value = cnt.ToString()
                    }
                }
            };
        //Атрибут Бренд
        Attribute GetBrendAttribute(GoodObject bus) {
            int id;
            string name;
            //var m = bus.GetManufacture(ozon: true)?.ToLowerInvariant() ?? "";
            //if (m == "vag") {
            //    id = 115840909;
            //    name = "VAG (VW/Audi/Skoda/Seat)";
            //} else if (m == "chery") {
            //    id = 0;
            //    name = "Нет бренда";
            //} else

            //if (m== "chery" && _brends.Any(a => a.value.ToLowerInvariant() == m)) {
            //    var attribute = _brends.Find(a => a.value.ToLowerInvariant() == m);
            //    id = attribute.id;
            //    name = attribute.value;
            //} else 
            {
                id = 0;
                name = "Нет бренда";
            }
            return new Attribute {
                complex_id = 0,
                id = 85,
                values = new Value[] {
                    new Value{
                        //dictionary_value_id = 0,//126745801 //id,
                        dictionary_value_id = id.ToString(),
                        value = name
                    }
                }
            };
        }
        //Атрибут Партномер (артикул производителя) (в нашем случае артикул)
        Attribute GetPartAttribute(GoodObject bus) {
            var man = bus.GetManufacture(true)?
                         .ToLowerInvariant();
            var part = _exceptionBrands.Contains(man) ? bus.id
                                                      : bus.part;
            return new Attribute {
                complex_id = 0,
                id = 7236,
                values = new Value[] {
                    new Value{
                        //value = bus.Part
                        value = part
                    }
                }
            };
        }
        //Запрос списка атрибутов с озон (значения по умолчанию - для получения списка брендов
        public async Task<List<AttributeValue>> GetAttibuteValuesAsync(string type_id, string attribute_id = "85") {
            var attributeValuesFile = @"..\data\ozon\ozon_attr_" + attribute_id + "_type_id_" + type_id + ".json";
            var lastWriteTime = File.GetLastWriteTime(attributeValuesFile);
            if (lastWriteTime.AddDays(_updateFreq) > DateTime.Now) {
                var res = JsonConvert.DeserializeObject<List<AttributeValue>>(
                    File.ReadAllText(attributeValuesFile));
                //Log.Add(_l + " загружено с диска " + res.Count + " атрибутов");
                return res;
            } else {
                var category_id = GetDescriptionCategoryId(type_id);
                var res = new List<AttributeValue>();
                var last = 0;
                do {
                    var data = new {
                        attribute_id = attribute_id,
                        description_category_id = category_id,
                        language = "DEFAULT",
                        last_value_id = last,
                        limit = 1000,
                        type_id = type_id
                    };
                    var s = await PostRequestAsync(data, "/v1/description-category/attribute/values");
                    res.AddRange(JsonConvert.DeserializeObject<List<AttributeValue>>(s));
                    last = res.Last()?.id ?? 0;
                } while (_hasNext);
                File.WriteAllText(attributeValuesFile, JsonConvert.SerializeObject(res));
                //Log.Add(_l + " загружено с озон " + res.Count + " атрибутов");
                return res;
            }
        }
        //Запрос категорий озон
        public async Task GetCategoriesAsync(int rootCategoryId = 0) {
            if (File.GetLastWriteTime(_catsFile) < DateTime.Now.AddDays(-_updateFreq)) {
                var data = new {
                    category_id = rootCategoryId,
                    language = "DEFAULT"
                };
                var s = await PostRequestAsync(data, "/v1/description-category/tree");
                //_categories = JsonConvert.DeserializeObject<List<Description_category>>(s);
                _categoriesJO = JArray.Parse(s);
                File.WriteAllText(_catsFile, s);
                //Log.Add(_l + "GetCategoriesAsync - получено категорий " + _categories.Count);
            } else {
                var s = File.ReadAllText(_catsFile);
                try {
                    //_categories = JsonConvert.DeserializeObject<List<Description_category>>(s);
                    _categoriesJO = JArray.Parse(s);
                } catch (Exception x) {
                    Log.Add(x.Message);
                }
            }
        }
        string GetDescriptionCategoryId(string type_id) {
            var token = "..type_id";
            foreach (JToken type in _categoriesJO.SelectTokens(token)) {
                if ((string) type == type_id) {
                    string categoryId = (string) type.Parent.Parent.Parent.Parent.Parent["description_category_id"];
                    return categoryId;
                }
            }
            Log.Add(_l + "ОШИБКА - КАТЕГОРИЯ НЕ НАЙДЕНА! - type_id: " + type_id);
            return "";
        }
        bool GetTypeIdAndCategoryId(Attributes a) {
            var token = "..type_name";
            foreach (JToken type in _categoriesJO.SelectTokens(token)) {
                if ((string) type == a.typeName) {
                    a.typeId = (string) type.Parent.Parent["type_id"];
                    a.categoryId = (string) type.Parent.Parent.Parent.Parent.Parent["description_category_id"];
                    return true;
                }
            }
            Log.Add(_l + "ОШИБКА - КАТЕГОРИЯ НЕ НАЙДЕНА! - typeName: " + a.typeName);
            return false;
        }
        //Запрос атрибутов существующего товара озон
        private async Task<List<ProductsInfoAttr>> GetProductFullInfoAsync(ProductInfo productInfo) {
            try {
                var data = new {
                    filter = new {
                        product_id = new[] { productInfo.id.ToString() },
                        visibility = "ALL"
                    },
                    limit = 100
                };
                var s = await PostRequestAsync(data, "/v3/products/info/attributes");
                s = s.Replace("attribute_id", "id");
                return JsonConvert.DeserializeObject<List<ProductsInfoAttr>>(s);
            } catch (Exception x) {
                Log.Add(_l + " ошибка - " + x.Message + x.InnerException?.Message);
                throw;
            }
        }
        //Сохранение списка карточек, которые можно добавить на озон в виде таблицы
        void SaveToFile(List<GoodObject> goods, string fname = @"..\data\ozon\ozonGoodListForAdding.xlsx") {
            try {
                var fileInfo = new FileInfo(fname);
                var excelPackage = new ExcelPackage(fileInfo);
                while (excelPackage.Workbook.Worksheets.Count>0) {
                    excelPackage.Workbook.Worksheets.Delete(1);
                }
                excelPackage.Workbook.Worksheets.Add("список для добавления на Озон");
                var sheet = excelPackage.Workbook.Worksheets[1];
                sheet.Cells[1, 1].Value = "Id";
                sheet.Cells[1, 2].Value = "Part";
                sheet.Cells[1, 3].Value = "Name";
                sheet.Cells[1, 4].Value = "Amount";
                sheet.Cells[1, 5].Value = "Price";
                sheet.Cells[1, 6].Value = "Weight";
                sheet.Cells[1, 7].Value = "Width";
                sheet.Cells[1, 8].Value = "Length";
                sheet.Cells[1, 9].Value = "Height";
                sheet.Cells[1, 10].Value = "Manufacture";
                sheet.Cells[1, 11].Value = "Images";
                goods = goods.OrderBy(o => o.name).ToList();
                for (int i = 0; i < goods.Count; i++) {
                    sheet.Cells[i + 2, 1].Value = goods[i].id;
                    sheet.Cells[i + 2, 2].Value = goods[i].Part;
                    sheet.Cells[i + 2, 3].Value = goods[i].name;
                    sheet.Cells[i + 2, 4].Value = goods[i].Amount;
                    sheet.Cells[i + 2, 5].Value = goods[i].Price;
                    sheet.Cells[i + 2, 6].Value = goods[i].weight?.ToString("F1") ?? "0";
                    sheet.Cells[i + 2, 7].Value = goods[i].width;
                    sheet.Cells[i + 2, 8].Value = goods[i].length;
                    sheet.Cells[i + 2, 9].Value = goods[i].height;
                    sheet.Cells[i + 2, 10].Value = goods[i].GetManufacture();
                    sheet.Cells[i + 2, 11].Value = goods[i].images.Count;
                }
                excelPackage.Save();
                Log.Add($"{_l}SaveToFile: список карточек выгружен в {fname}");
            } catch (Exception x) {
                Log.Add($"{_l}SaveToFile: ошибка сохранения списка - {x.Message}");
            }
        }
        //список складов
        async Task GetWarehouseList() {
            try {
                var data = new { };
                var s = await PostRequestAsync(data, "/v1/warehouse/list");
                File.WriteAllText(_warehouseList, s);
            } catch (Exception x) {
                Log.Add($"{_l} GetWarehouseList: ошибка запроса списка складов! - {x.Message}");
            }
        }
    }
    /////////////////////////////////////
    /// классы для работы с запросами ///
    /////////////////////////////////////

    public class OzonPriceList {
        public List<OzonPriceListItem> items { get; set; }
        public int total { get; set; }
        public string last_id { get; set; }
    }
    public class OzonPriceListItem {
        public int product_id { get; set; }
        public string offer_id { get; set; }
        public OzonPrice price { get; set; }
        public object marketing_actions { get; set; }
        public string volume_weight { get; set; }
    }
    public class OzonPrice {
        public string price { get; set; }
        public string old_price { get; set; }
        public string retail_price { get; set; }
        public string vat { get; set; }
        public string min_ozon_price { get; set; }
        public string marketing_price { get; set; }
        public string marketing_seller_price { get; set; }
        public bool auto_action_enabled { get; set; }
    }


    public class OzonActionProducts {
        public List<OzonActionProduct> products = new List<OzonActionProduct>();
        public int total;
    }
    public class OzonActionProduct {
        public int id;
        public string price;
        public string action_price;
        public string max_action_price;
        //"add_mode": "MANUAL",
        public int stock;
        //"min_stock": 0
        public int GetPrice() {
            return price.Length > 0 ? int.Parse(price.Split('.').First()) : 0;
        }
        public int GetActionPrice() {
            return action_price.Length > 0 ? int.Parse(action_price.Split('.').First()) : 0;
        }
        public int GetMinPrice() {
            return (int) (GetPrice() * (0.01 * (100 - OzonApi._minPriceProcent)));
        }
    }
    public class OzonActionsList {
        public List<OzonActions> result = new List<OzonActions>();
    }
    public class OzonActions {
        public int id;
        public int participating_products_count;
    }


    public class WareHouse {
        public string warehouse_id;
        public string name;
        public string status;
    }
    public class Postings {
        public List<Posting> postings = new List<Posting>();
        public int count;
    }
    public class Posting {
        public string posting_number;
        public decimal order_id;
        public string order_number;
        public List<Product> products = new List<Product>();
        public DateTime in_process_at;
    }
    public class Product {
        public string offer_id;
        public string name;
        public decimal sku;
        public int quantity;
    }
    /////////////////////////////////////////
    public class Attributes {
        public string categoryId;
        public string brendId;
        public string brendName;
        public string typeId;
        public string typeName;
        public List<Attribute> additionalAttributes = new List<Attribute>();
    }
    public static class Extentions {
        public static void AddAttribute(this List<Attribute> list, Attribute newAttr) {
            if (newAttr != null)
                list.Add(newAttr);
        }
    }
    /////////////////////////////////////////
    public class RootResponse {
        public object result { get; set; }
        public bool has_next { get; set; }
    }
    public class ProductsInfo {
        public List<ProductInfo> items { get; set; }
    }
    public class ProductInfo {
        public int id { get; set; }
        public string name { get; set; }
        public string offer_id { get; set; }
        public string barcode { get; set; }
        public string buybox_price { get; set; }
        public int category_id { get; set; }
        public DateTime created_at { get; set; }
        public string[] images { get; set; }
        public string marketing_price { get; set; }
        public string min_ozon_price { get; set; }
        public string old_price { get; set; }
        public string premium_price { get; set; }
        public string price { get; set; }
        public string recommended_price { get; set; }
        public string min_price { get; set; }
        public Source[] sources { get; set; }
        public Stocks stocks { get; set; }
        //        public ItemErrors[] errors { get; set; }
        public string vat { get; set; }
        public bool visible { get; set; }
        public Visibility_Details visibility_details { get; set; }
        public string price_index { get; set; }
        //        public Commission[] commissions { get; set; }
        public decimal volume_weight { get; set; }
        public bool is_prepayment { get; set; }
        public bool is_prepayment_allowed { get; set; }
        //public object[] images360 { get; set; }
        public string color_image { get; set; }
        public string primary_image { get; set; }
        public Status status { get; set; }
        public string state { get; set; }
        public string service_type { get; set; }
        public int fbo_sku { get; set; }
        public int fbs_sku { get; set; }
        public string currency_code { get; set; }
        public bool is_kgt { get; set; }
        public Discounted_Stocks discounted_stocks { get; set; }
        public bool is_discounted { get; set; }
        public bool has_discounted_item { get; set; }
        //        public object[] barcodes { get; set; }
        public DateTime updated_at { get; set; }
        public Price_Indexes price_indexes { get; set; }
        public int sku { get; set; }
        public string GetSku() {
            return (sku != 0 ? sku : fbs_sku).ToString();
        }
        public int GetStocks() {
            return stocks.reserved + stocks.present;
        }
        public int GetPrice() {
            return price.Length > 0 ? int.Parse(price.Split('.').First()) : 0;
        }
        public int GetOldPrice() {
            return old_price.Length > 0 ? int.Parse(old_price.Split('.').First()) : 0;
        }
        public int GetMinPrice() {
            return min_price.Length > 0 ? int.Parse(min_price.Split('.').First()) : 0;
        }
        public int GetMarketingPrice() {
            return marketing_price.Length > 0 ? int.Parse(marketing_price.Split('.').First()) : 0;
        }
    }
    public class ItemErrors {
        public string code { get; set; }
        public string field { get; set; }
        public int attribute_id { get; set; }
        public string state { get; set; }
        public string level { get; set; }
        public string description { get; set; }
        public string attribute_name { get; set; }
    }
    public class Stocks {
        public int coming { get; set; }
        public int present { get; set; }
        public int reserved { get; set; }
    }
    public class Visibility_Details {
        public bool has_price { get; set; }
        public bool has_stock { get; set; }
        public bool active_product { get; set; }
    }
    public class Status {
        public string state { get; set; }
        public string state_failed { get; set; }
        public string moderate_status { get; set; }
        public string[] decline_reasons { get; set; }
        public string validation_state { get; set; }
        public string state_name { get; set; }
        public string state_description { get; set; }
        public bool is_failed { get; set; }
        public bool is_created { get; set; }
        public string state_tooltip { get; set; }
        public ItemErrors[] item_errors { get; set; }
        public DateTime state_updated_at { get; set; }
    }
    public class Discounted_Stocks {
        public int coming { get; set; }
        public int present { get; set; }
        public int reserved { get; set; }
    }
    public class Price_Indexes {
        public string price_index { get; set; }
        public External_Index_Data external_index_data { get; set; }
        public Ozon_Index_Data ozon_index_data { get; set; }
        public Self_Marketplaces_Index_Data self_marketplaces_index_data { get; set; }
    }
    public class External_Index_Data {
        public string minimal_price { get; set; }
        public string minimal_price_currency { get; set; }
        public float price_index_value { get; set; }
    }
    public class Ozon_Index_Data {
        public string minimal_price { get; set; }
        public string minimal_price_currency { get; set; }
        public float price_index_value { get; set; }
    }
    public class Self_Marketplaces_Index_Data {
        public string minimal_price { get; set; }
        public string minimal_price_currency { get; set; }
        public float price_index_value { get; set; }
    }
    public class Source {
        public bool is_enabled { get; set; }
        public int sku { get; set; }
        public string source { get; set; }
    }
    public class Commission {
        public decimal percent { get; set; }
        public decimal min_value { get; set; }
        public decimal value { get; set; }
        public string sale_schema { get; set; }
        public int delivery_amount { get; set; }
        public int return_amount { get; set; }
    }
    /////////////////////////////////////////
    public class ProductImportItem {
        public Attribute[] attributes { get; set; }
        public string barcode { get; set; }
        public int category_id { get; set; }
        public string color_image { get; set; }
        public string[] complex_attributes { get; set; }
        public string currency_code { get; set; }
        public int depth { get; set; }
        public string dimension_unit { get; set; }
        public int height { get; set; }
        public string[] images { get; set; }
        public string[] images360 { get; set; }
        public string name { get; set; }
        public string offer_id { get; set; }
        public string old_price { get; set; }
        public string[] pdf_list { get; set; }
        public string premium_price { get; set; }
        public string price { get; set; }
        public string primary_image { get; set; }
        public string vat { get; set; }
        public int weight { get; set; }
        public string weight_unit { get; set; }
        public int width { get; set; }
    }
    /////////////////////////////////////////
    public class ProductList {
        public List<ProductListItem> items { get; set; }
        public int total { get; set; }
        public string last_id { get; set; }
    }
    public class ProductListItem {
        public int product_id { get; set; }
        public string offer_id { get; set; }
        public bool is_fbo_visible { get; set; }
        public bool is_fbs_visible { get; set; }
        public bool archived { get; set; }
        public bool is_discounted { get; set; }
    }
    /////////////////////////////////////////
    public class ProductInfoStocks {
        public Item[] items { get; set; }
        public int total { get; set; }
        public string last_id { get; set; }
    }
    public class Item {
        public int product_id { get; set; }
        public string offer_id { get; set; }
        public Stock[] stocks { get; set; }
    }
    public class Stock {
        public string type { get; set; }
        public int present { get; set; }
        public int reserved { get; set; }
    }
    /////////////////////////////////////////
    public class UpdateResult {
        public int product_id { get; set; }
        public string offer_id { get; set; }
        public bool updated { get; set; }
        public UpdateResultErrors[] errors { get; set; }
    }
    public class UpdateResultErrors {
        public string code { get; set; }
        public string message { get; set; }
    }
    /////////////////////////////////////////
    public class AttributeValue {
        public int id { get; set; }
        public string value { get; set; }
        public string info { get; set; }
        public string picture { get; set; }
    }
    /////////////////////////////////////////
    public class ProductImportResult {
        public int task_id { get; set; }
    }
    /////////////////////////////////////////
    public class ProductImportInfo {
        public Items[] items { get; set; }
        public int total { get; set; }

    }
    public class Items {
        public string offer_id { get; set; }
        public int product_id { get; set; }
        public string status { get; set; }
        public Errors[] errors { get; set; }
    }
    public class Errors {
        public string code { get; set; }
        public string field { get; set; }
        public int attribute_id { get; set; }
        public string state { get; set; }
        public string level { get; set; }
        public string description { get; set; }
        //public Optional_Description_Elements optional_description_elements { get; set; }
        public string attribute_name { get; set; }
        public string message { get; set; }
    }
    public class Category {
        public int category_id { get; set; }
        public string title { get; set; }
        public Category[] children { get; set; }
    }
    ////////////////////////////////////////
    public class ProductsInfoAttributes {
        public ProductsInfoAttr[] result { get; set; }
        public int total { get; set; }
        public string last_id { get; set; }
    }
    public class ProductsInfoAttr {
        public int id { get; set; }
        public string barcode { get; set; }
        public int category_id { get; set; }
        public string name { get; set; }
        public string offer_id { get; set; }
        public int height { get; set; }
        public int depth { get; set; }
        public int width { get; set; }
        public string dimension_unit { get; set; }
        public int weight { get; set; }
        public string weight_unit { get; set; }
        public Image[] images { get; set; }
        public string image_group_id { get; set; }
        public object[] images360 { get; set; }
        public object[] pdf_list { get; set; }
        public Attribute[] attributes { get; set; }
        public object[] complex_attributes { get; set; }
        public string color_image { get; set; }
        public string last_id { get; set; }
        public int description_category_id { get; set; }
    }
    public class Image {
        public string file_name { get; set; }
        public bool _default { get; set; }
        public int index { get; set; }
    }
    public class Attribute {
        public int id { get; set; }
        public int complex_id { get; set; }
        public Value[] values { get; set; }
    }
    public class Value {
        public string dictionary_value_id { get; set; }
        public string value { get; set; }
    }
    //=====================
    public class Description_category {
        public int description_category_id { get; set; }
        public string category_name { get; set; }
        public bool disabled { get; set; }
        public Description_category[] children { get; set; }
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
