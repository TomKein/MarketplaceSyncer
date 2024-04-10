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

namespace Selen.Sites {
    public class OzonApi {
        readonly HttpClient _hc = new HttpClient();
        readonly string _clientID;                    // 1331176
        readonly string _apiKey;                      // 1d0edd61-1b2d-4ac5-afa0-cf5488665d35
        readonly string _baseApiUrl = "https://api-seller.ozon.ru";
        readonly string _baseBusUrl = "https://www.ozon.ru/context/detail/id/";
        readonly string _url;                         //номер поля в карточке
        readonly string _l = "ozon: ";                //префикс для лога
        readonly float _oldPriceProcent = 10;
        List<ProductListItem> _productList = new List<ProductListItem>();   //список товаров, получаемый из /v2/product/list
        readonly string _productListFile = @"..\ozon\ozon_productList.json";
        readonly string _reserveListFile = @"..\ozon\ozon_reserveList.json";
        readonly int _updateFreq;                //частота обновления списка (часов)
        int _checkProductCount;              //количество проверяемых позиций за раз
        int _checkProductIndex;              //текущий индекс проверяемой позиции
        static bool _isProductListCheckNeeds;
        bool _hasNext = false;                        //для запросов
        List<GoodObject> _bus;
        List<AttributeValue> _brends;                 //список брендов озон
        List<AttributeValue> _color;                  //список цветов озон
        List<AttributeValue> _techType;               //список вид техники
        List<AttributeValue> _dangerClass;            //список класс опасности
        List<AttributeValue> _motorType;              //список Тип двигателя
        List<AttributeValue> _manufactureCoutry;      //список Страна-изготовитель
        List<AttributeValue> _material;               //список Материал
        List<AttributeValue> _placement;              //список Места установки
        List<AttributeValue> _place;                  //список Расположение детали
        List<AttributeValue> _side;                   //список Сторона установки
        List<AttributeValue> _tnved;                  //список Коды ТН ВЭД
        
        //производители, для которых не выгружаем номера и артикулы
        readonly string[] _exceptManufactures = { "general motors" };

        public OzonApi() {
            _hc.BaseAddress = new Uri(_baseApiUrl);
            _url = DB.GetParamStr("ozon.url");
            _clientID = DB.GetParamStr("ozon.id");
            _apiKey = DB.GetParamStr("ozon.apiKey");
            _oldPriceProcent = DB.GetParamFloat("ozon.oldPriceProcent");
            _updateFreq = DB.GetParamInt("ozon.updateFreq");
        }
        //главный метод
        public async Task SyncAsync() {
            _bus = Class365API._bus;
            if (_brends == null) {
                _brends = await GetAttibuteValuesAsync(category_id: 92120918);
                _brends.AddRange(await GetAttibuteValuesAsync(category_id: 17027495));
                _brends.AddRange(await GetAttibuteValuesAsync(category_id: 61852812));
            }
            if (_color == null) {
                _color = await GetAttibuteValuesAsync(attribute_id: 10096);
            }
            if (_techType == null) {
                _techType = await GetAttibuteValuesAsync(attribute_id: 7206);
            }
            if (_dangerClass == null) {
                _dangerClass = await GetAttibuteValuesAsync(attribute_id: 9782);
            }
            if (_manufactureCoutry == null) {
                _manufactureCoutry = await GetAttibuteValuesAsync(attribute_id: 4389);
            }
            if (_material == null) {
                _material = await GetAttibuteValuesAsync(attribute_id: 7199);
            }
            if (_place == null) {
                _place = await GetAttibuteValuesAsync(attribute_id: 20189);
            }
            if (_placement == null) {
                _placement = await GetAttibuteValuesAsync(attribute_id: 7367);
            }
            if (_motorType == null) {
                _motorType = await GetAttibuteValuesAsync(attribute_id: 8303);
            }
            if (_side == null) {
                _side = await GetAttibuteValuesAsync(attribute_id: 22329);
            }
            await UpdateProductsAsync();
            await MakeReserve();
            await CheckProductListAsync();
            if (Class365API.SyncStartTime.Minute >= 55) {
                await AddProductsAsync();
            }
            await CheckProductLinksAsync(checkAll: true);
        }
        private async Task MakeReserve() {
            try {
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
                            cutoff_to = DateTime.Now.AddDays(5).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'"),
                            warehouse_id = new int[] { },
                            status = status
                        },
                        limit = 100,
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
                    Log.Add($"{_l} получено {res.postings.Count} заказов "+ status);
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
                    var isResMaked = await Class365API.MakeReserve(Selen.Source.Ozon, $"Ozon order {order.posting_number}", 
                                                                   goodsDict, order.in_process_at.AddHours(3).ToString());
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
            }
        }
        //проверка всех карточек в бизнесе, которые изменились и имеют ссылку на озон
        private async Task UpdateProductsAsync() {
            try {
                foreach (var bus in _bus.Where(w => w.IsTimeUpDated()
                                                 && !string.IsNullOrEmpty(w.ozon)
                                                 && w.ozon.Contains("http"))) {
                    await UpdateProductAsync(bus);
                }
            } catch (Exception x) {
                Log.Add(_l + "UpdateProductsAsync - " + x.Message);
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
                _checkProductCount = await DB.GetParamIntAsync("ozon.checkProductCount");
                _checkProductIndex = await DB.GetParamIntAsync("ozon.checkProductIndex");
                if (_checkProductIndex >= _productList.Count) 
                    _checkProductIndex = 0;
                await DB.SetParamAsync("ozon.checkProductIndex", (_checkProductIndex + _checkProductCount).ToString());
                items = _productList.Skip(_checkProductIndex).Take(_checkProductCount).ToList<ProductListItem>();
            } else 
                items = _productList;
            foreach (var item in items) {
                try {
                    //карточка в бизнес.ру с id = артикулу товара на озон
                    var b = Class365API._bus.FindIndex(_ => _.id == item.offer_id);
                    if (b == -1)
                        throw new Exception("карточка бизнес.ру с id = " + item.offer_id + " не найдена!");
                    if (!Class365API._bus[b].ozon.Contains("http") ||                       //если карточка найдена,но товар не привязан к бизнес.ру
                        Class365API._bus[b].ozon.Split('/').Last().Length < 3 ||            //либо ссылка есть, но неверный sku
                        checkAll) {                                             //либо передали флаг проверять всё
                        var productInfo = await GetProductInfoAsync(Class365API._bus[b]);
                        await SaveUrlAsync(Class365API._bus[b], productInfo);
                        await UpdateProductAsync(Class365API._bus[b], productInfo);
                    }
                } catch (Exception x) {
                    Log.Add(_l + " CheckGoodsAsync ошибка - " + x.Message);
                }
            }
        }
        //расширенная информация о товаре
        private async Task<ProductInfo> GetProductInfoAsync(GoodObject bus) {
            try {
                var data = new { offer_id = bus.id };
                var s = await PostRequestAsync(data, "/v2/product/info");
                return JsonConvert.DeserializeObject<ProductInfo>(s);
            } catch (Exception x) {
                Log.Add(_l + " ошибка - " + x.Message + x.InnerException?.Message);
                throw;
            }
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
                } else
                    Log.Add(_l + bus.name + " ссылка без изменений!");
            } catch (Exception x) {
                Log.Add(_l + " SaveUrlAsync - ошибка! - " + x.Message + x.InnerException?.Message);
            }
        }
        //проверка и обновление товара
        async Task UpdateProductAsync(GoodObject bus, ProductInfo productInfo = null) {
            try {
                if (productInfo == null)
                    productInfo = await GetProductInfoAsync(bus);
                await UpdateProductStocks(bus, productInfo);
                await UpdateProductPriceAsync(bus, productInfo);
                await UpdateProduct(bus, productInfo);
                //TODO добавить проверку и обновление фотографий await UpdateProductImages()
            } catch (Exception x) {
                Log.Add(_l + " ошибка - " + x.Message + x.InnerException?.Message);
            }
        }
        //обновление остатков товара на озон
        private async Task UpdateProductStocks(GoodObject bus, ProductInfo productInfo) {
            try {
                if (bus.Amount == productInfo.GetStocks())
                    return;
                //защита от отрицательных остатков
                if (bus.Amount < 0)
                    bus.Amount = 0;
                //объект для запроса
                var data = new {
                    stocks = new[] {
                        new {
                            product_id = productInfo.id,
                            stock = bus.Amount.ToString("F0")
                        }
                    }
                };
                var s = await PostRequestAsync(data, "/v1/product/import/stocks");
                var res = JsonConvert.DeserializeObject<List<UpdateResult>>(s);
                if (res.First().updated) {
                    Log.Add(_l + bus.name + " остаток обновлен! (" + bus.Amount + ")");
                } else {
                    Log.Add(_l + bus.name + " ошибка! остаток не обновлен! (" + bus.Amount + ")" + " >>> " + s);
                }
            } catch (Exception x) {
                Log.Add(_l + " ошибка обновления остатка - " + x.Message);
            }
        }
        //расчет цен с учетом наценки
        private int GetNewPrice(GoodObject b) {
            var weight = b.GetWeight();
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
        //цена до скидки (старая)
        private int GetOldPrice(int newPrice) {
            return (int) (Math.Ceiling((newPrice * (1 + _oldPriceProcent / 100)) / 100) * 100);
        }
        //Проверка и обновление цены товара на озон
        async Task UpdateProductPriceAsync(GoodObject bus, ProductInfo productInfo) {
            try {
                var newPrice = GetNewPrice(bus);
                var oldPrice = GetOldPrice(newPrice);
                //проверяем цены на озоне
                if (newPrice != productInfo.GetPrice()
                || oldPrice != productInfo.GetOldPrice()) {
                    //если отличаются - создаем запрос и обновляем
                    var data = new {
                        prices = new[] {
                            new { 
                                /*offer_id = bus.id, */ 
                                product_id = productInfo.id,
                                old_price = oldPrice.ToString(),
                                price = newPrice.ToString()
                            }
                        }
                    };
                    var s = await PostRequestAsync(data, "/v1/product/import/prices");
                    var res = JsonConvert.DeserializeObject<List<UpdateResult>>(s);
                    if (res.First().updated) {
                        Log.Add(_l + bus.name + " (" + bus.Price + ") цены обновлены! ("
                                   + newPrice + ", " + oldPrice + ")");
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
                //requestMessage.Headers.Add("Host", "api-seller.ozon.ru");
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
                Log.Add(_l + " ошибка запроса! - " + x.Message);
                throw;
            }
        }
        //обновление описаний товаров
        private async Task UpdateProduct(GoodObject good, ProductInfo productInfo) {
            try {
                //проверяем группу товара
                var attributes = await GetAttributesAsync(good);
                if (attributes.typeId == 0)
                    return;
                //Запрашиваю атрибуты товара с озон
                var productFullInfo = await GetProductFullInfoAsync(productInfo);
                File.WriteAllText(@"..\ozon\product_" + productFullInfo.First().offer_id + ".json",
                    JsonConvert.SerializeObject(productFullInfo));                //формирую объект запроса
                if (attributes.categoryId == 111) {
                    _isProductListCheckNeeds = true;
                    return;
                }
                var data = new {
                    items = new[] {
                        new{
                            attributes = new List<Attribute>(),
                            name = good.name,
                            currency_code="RUB",
                            offer_id=good.id,
                            description_category_id=attributes.categoryId,
                            //category_id=productFullInfo[0].category_id,
                            price = GetNewPrice(good).ToString(),
                            old_price = GetOldPrice(GetNewPrice(good)).ToString(),
                            weight = (int)(good.GetWeight()*1000),                  //Вес с упаковкой, г
                            weight_unit = "g",
                            depth = int.Parse(good.width)*10,                       //глубина, мм
                            height = int.Parse(good.height)*10,                     //высота, мм
                            width = int.Parse(good.length)*10,                      //высота, мм
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

                    item.id != 85 &&                                                //Бренд (теперь используем Без бренда,
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
                    Log.Add(_l + good.name + " - товар отправлен на озон!");
                } else {
                    Log.Add(_l + good.name + " ошибка отправки товара на озон!" + " ===> " + s);
                }

                ////
                var res2 = new ProductImportInfo();
                await Task.Delay(10000);
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
                Log.Add(_l + " ошибка обновления описаний - " + x.Message);
            }
        }
        //добавление новых товаров на ozon
        async Task AddProductsAsync() {
            var count = await DB.GetParamIntAsync("ozon.countToAdd");
            if (count == 0)
                return;
            if (_isProductListCheckNeeds)
                await CheckProductListAsync();
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
            //список карточек которые еще не добавлены на озон
            var goods = Class365API._bus.Where(w => w.Amount > 0
                                     && w.Price > 0
                                     && w.Part != null
                                     && w.images.Count > 0
                                     && w.height != null
                                     && w.length != null
                                     && w.width != null
                                     && w.IsNew()
                                     && !w.ozon.Contains("http")
                                     && !_productList.Any(_ => w.id == _.offer_id)
                                     && !exceptionGoods.Any(e => w.name.ToLowerInvariant().Contains(e))
                                     && !exceptionGroups.Any(e => w.GroupName().ToLowerInvariant().Contains(e)));
            SaveToFile(goods);
            var goods2 = Class365API._bus.Where(w => w.Amount > 0
                                     && w.Price > 0
                                     && w.images.Count > 0
                                     && w.IsNew()
                                     && !w.ozon.Contains("http")
                                     && !_productList.Any(_ => w.id == _.offer_id)
                                     && !exceptionGoods.Any(e => w.name.ToLowerInvariant().Contains(e))); //нет в исключениях
            SaveToFile(goods2, @"..\ozon\ozonGoodListForAdding_all.csv");
            Log.Add(_l + "карточек для добавления: " + goods.Count() + " (" + goods2.Count() + ")");
            int i = 0;
            foreach (var good in goods) {
                try {
                    //проверяем группу товара
                    var attributes = await GetAttributesAsync(good);
                    if (attributes.typeId == 0 || attributes.categoryId == 111)
                        continue;
                    //формирую объект запроса
                    var data = new {
                        items = new[] {
                            new{
                                attributes = new List<Attribute>(),
                                name = good.name,
                                currency_code="RUB",
                                offer_id=good.id,
                                //description_category_id=attributes.categoryId,
                                category_id=attributes.categoryId,
                                price = GetNewPrice(good).ToString(),
                                old_price = GetOldPrice(GetNewPrice(good)).ToString(),
                                weight = (int)(good.GetWeight()*1000),                      //Вес с упаковкой, г
                                weight_unit = "g",
                                depth = int.Parse(good.width)*10,                           //глубина, мм
                                height = int.Parse(good.height)*10,                         //высота, мм
                                width = int.Parse(good.length)*10,                          //высота, мм
                                dimension_unit = "mm",
                                primary_image = good.images.First().url,                    //главное фото
                                images = good.images.Skip(1).Take(14).Select(_=>_.url).ToList(),
                                vat="0.0"                                                   //налог
                            }
                        }
                    };
                    if (attributes.additionalAttributes != null && attributes.additionalAttributes.Count > 0)
                        data.items[0].attributes.AddRange(attributes.additionalAttributes);
                    var s = await PostRequestAsync(data, "/v2/product/import");
                    var res = JsonConvert.DeserializeObject<ProductImportResult>(s);
                    if (res.task_id != default(int)) {
                        Log.Add(_l + good.name + " - товар отправлен на озон!");
                    } else {
                        Log.Add(_l + good.name + " ошибка отправки товара на озон!");
                    }
                    var res2 = new ProductImportInfo();
                    await Task.Delay(10000);
                    var data2 = new {
                        task_id = res.task_id.ToString()
                    };
                    s = await PostRequestAsync(data2, "/v1/product/import/info");
                    res2 = JsonConvert.DeserializeObject<ProductImportInfo>(s);
                    Log.Add(_l + good.name + " status товара - " + res2.items.First().status);
                    if (res2.items.First().errors.Length > 0)
                        Log.Add(_l + good.name + " ошибка - " + s);
                    _isProductListCheckNeeds = true;
                } catch (Exception x) {
                    Log.Add(_l + good.name + " - " + x.Message + x.InnerException?.Message);
                }
                if (++i >= count)
                    break;
            }
        }
        //получить атрибуты и категорию товара на озон
        async Task<Attributes> GetAttributesAsync(GoodObject bus) {
            try {

                var n = bus.name.ToLowerInvariant();
                var a = new Attributes();
                if (n.StartsWith("генератор ")) {
                    a.categoryId = 61852812;//Генератор автомобильный и комплектующие
                    a.typeId = 970707037;
                    a.typeName = "Генератор в сборе";
                } else if (n.Contains("генератор") &&                                   //Комплектующие генератора для авто
                     n.Contains("щетк")) {
                    a.categoryId = 61852812;//Генератор автомобильный и комплектующие
                    a.typeId = 970892942;
                    a.typeName = "Щетки генератора";
                } else if ((n.Contains("генератор") || n.Contains("напряжен")) &&                                   //Комплектующие генератора для авто
                    (n.Contains("реле") || n.Contains("регулятор"))) {
                    a.categoryId = 61852812;//Генератор автомобильный и комплектующие
                    a.typeId = 970863594;
                    a.typeName = "Регулятор напряжения генератора";
                } else if (n.Contains("генератор") &&                                   //Комплектующие генератора для авто
                    n.StartsWith("болт ")) {
                    a.categoryId = 61852812;//Генератор автомобильный и комплектующие
                    a.typeId = 970876396;
                    a.typeName = "Регулятор генератора";
                } else if (n.StartsWith("стартер ")) {                                  //стартер
                    a.categoryId = 85844628;//Стартер автомобильный и составляющие
                    a.typeId = 98941;
                    a.typeName = "Стартер в сборе";
                } else if (n.StartsWith("бендикс") && n.Contains("стартер")) {
                    a.categoryId = 85844628;//Стартер автомобильный и составляющие
                    a.typeId = 970863600;
                    a.typeName = "Бендикс стартера";
                } else if (n.StartsWith("вилк") && n.Contains("стартер")) {
                    a.categoryId = 85844628;//Стартер автомобильный и составляющие
                    a.typeId = 971072669;
                    a.typeName = "Вилка стартера";
                } else if (n.Contains("втягивающее") &&
                    (n.Contains("реле") || n.Contains("стартер"))) {
                    a.categoryId = 85844628;//Стартер автомобильный и составляющие
                    a.typeId = 98910;
                    a.typeName = "Реле втягивающее стартера";
                } else if ((n.Contains("гофра") || n.Contains("труба гофрированная")) &&//выхлопная система
                    (n.Contains("универсальная") || n.Contains("площадка") || n.Contains("глушителя"))) {
                    a.categoryId = 33698293;//Выхлопная труба и составляющие
                    a.typeId = 98818;
                    a.typeName = "Гофра глушителя";
                } else if (n.Contains("хомут") && n.Contains("глушителя")) {
                    a.categoryId = 33698293;//Выхлопная труба и составляющие
                    a.typeId = 971043197;
                    a.typeName = "Хомут для глушителя";
                } else if (n.Contains("труба") &&
                    (n.Contains("глушителя") || n.Contains("приемная") || n.Contains("промежуточная")) ||
                    n.StartsWith("изгиб трубы глушителя") ||
                    n.StartsWith("труба прямая")
                    ) {
                    a.categoryId = 33698293;//Выхлопная труба и составляющие
                    a.typeId = 98954;
                    a.typeName = "Труба глушителя";
                } else if (n.StartsWith("резонатор ") ||
                    n.StartsWith("пламегаситель ") ||
                    n.Contains("стронгер")) {
                    a.categoryId = 33698293;//Выхлопная труба и составляющие
                    a.typeId = 98906;
                    a.typeName = "Резонатор глушителя";
                } else if (n.Contains("скобы приёмной трубы") ||
                    n.Contains("глушител") &&
                    (n.Contains("подвеск") || n.Contains("кронштейн") ||
                    n.Contains("крепление") || n.Contains("держател"))) {
                    a.categoryId = 33698293;//Выхлопная труба и составляющие
                    a.typeId = 970964738;
                    a.typeName = "Крепление глушителя";
                } else if (n.Contains("комплект фланцев с трубой") ||
                    n.Contains("глушител") &&
                    (n.Contains("ремкомплект") || n.Contains("фланец"))) {
                    a.categoryId = 33698293;//Выхлопная труба и составляющие
                    a.typeId = 971100632;
                    a.typeName = "Ремкомплект глушителя";
                } else if (n.StartsWith("глушитель ") &&
                    (bus.GroupName().Contains("ыхлопная") || bus.GroupName().Contains("лушител"))) {
                    a.categoryId = 33698293;//Выхлопная труба и составляющие
                    a.typeId = 971906701;
                    a.typeName = "Глушитель";
                } else if (n.StartsWith("суппорт ") &&                                  //тормозная система
                    (bus.GroupName().Contains("тормоз") || n.Contains("тормоз"))) {
                    a.categoryId = 85842995;//Тормозной суппорт автомобильный
                    a.typeId = 970725296;
                    a.typeName = "Суппорты тормозные";
                } else if (n.Contains("цилиндр") &&
                    n.Contains("главный") &&
                    (bus.GroupName().Contains("тормоз") || n.Contains("тормоз"))) {
                    a.categoryId = 85842992;//Тормозной цилиндр и составляющие
                    a.typeId = 98965;
                    a.typeName = "Цилиндр тормозной главный";
                } else if (n.Contains("цилиндр") &&
                    (bus.GroupName().Contains("тормоз") || n.Contains("тормоз"))) {
                    a.categoryId = 85842992;//Тормозной цилиндр и составляющие
                    a.typeId = 98966;
                    a.typeName = "Цилиндр тормозной рабочий";
                } else if (n.Contains("барабан") &&
                    n.Contains("тормоз")) {
                    a.categoryId = 33698183;//Тормозные диски и барабаны
                    a.typeId = 98799;
                    a.typeName = "Барабан тормозной";
                } else if (n.Contains("диск") &&
                    n.Contains("тормоз")) {
                    a.categoryId = 33698183;//Тормозные диски и барабаны
                    a.typeId = 98825;
                    a.typeName = "Диск тормозной";
                } else if (n.Contains("колодки") &&
                    n.Contains("тормоз")) {
                    a.categoryId = 33698187;//Колодки тормозные
                    a.typeId = 96167;
                    a.typeName = "Колодки тормозные";
                } else if (n.Contains("ручка") &&                                       //ручки
                    (n.Contains("двери") || n.Contains("наруж") || n.Contains("внутр"))) {
                    a.categoryId = 99606705;
                    a.typeId = 970939934;
                    a.typeName = "Ручка дверная автомобильная";
                } else if (n.Contains("ручка") &&
                    n.Contains("стеклоподъемника")) {
                    a.categoryId = 99606705;
                    a.typeId = 970945542;
                    a.typeName = "Ручка стеклоподъемника";
                } else if ((n.Contains("радиатор") || n.StartsWith("диффузор")) &&      //Радиатор охлаждения для авто
                    (n.Contains("охлаждения") || n.Contains("вентилятор"))) {
                    a.categoryId = 85833530; //Радиатор автомобильный и составляющие
                    a.typeId = 970782911;
                    a.typeName = "Радиатор охлаждения";
                } else if (n.Contains("радиатор") &&
                    (n.Contains("отопителя") || n.Contains("печки"))) {
                    a.categoryId = 85833530; //Радиатор автомобильный и составляющие
                    a.typeId = 970781727;
                    a.typeName = "Радиатор отопителя салона";
                } else if (n.Contains("радиатор") &&
                    n.Contains("кондиционера")) {
                    a.categoryId = 85833530; //Радиатор автомобильный и составляющие
                    a.typeId = 970781671;
                    a.typeName = "Радиатор кондиционера";
                } else if (n.Contains("бачок ") &&                                      //Расширительный бачок для авто
                    n.Contains("расширит")) {
                    a.categoryId = 85833530; //Радиатор автомобильный и составляющие
                    a.typeId = 970885027;
                    a.typeName = "Бачок расширительный для автомобиля";
                } else if ((n.Contains("мотор") || n.StartsWith("вентилятор")) &&       //вентилятор охлаждения
                    (n.Contains("охлаждения") || n.Contains("двс"))) {
                    a.categoryId = 85833530;//Радиатор автомобильный и составляющие
                    a.typeId = 970854831;
                    a.typeName = "Вентилятор радиатора";
                } else if ((n.Contains("мотор") || n.StartsWith("вентилятор")) &&
                    (n.Contains("печки") || n.Contains("отопителя"))) {
                    a.categoryId = 78305548;
                    a.typeId = 970782175;
                    a.typeName = "Электровентилятор отопления";
                } else if (n.Contains("катушка") &&
                    n.Contains("зажигания")) {
                    a.categoryId = 85835327;//Катушки и провода зажигания
                    a.typeId = 970744686;
                    a.typeName = "Катушка зажигания";
                } else if (n.Contains("замок") && n.Contains("зажиг")) {
                    a.categoryId = 85835327;//Катушки и провода зажигания
                    a.typeId = 970889769;
                    a.typeName = "Замок зажигания";
                } else if (n.Contains("группа") && n.Contains("контактная")) {
                    a.categoryId = 85835327;//Катушки и провода зажигания
                    a.typeId = 98812;
                    a.typeName = "Выключатель зажигания";
                } else if (n.StartsWith("трамблер") ||
                           n.StartsWith("распределитель зажигания")) {
                    a.categoryId = 85835327;//Катушки и провода зажигания
                    a.typeId = 971072773;
                    a.typeName = "Распределитель зажигания";
                } else if (n.StartsWith("провод") &&
                    (n.Contains("высоков") || n.Contains(" в/в") || n.Contains("зажиг"))) {                     //провода зажигания
                    a.categoryId = 85835327;//Катушки и провода зажигания
                    a.typeId = 98893;
                    a.typeName = "Комплект высоковольтных проводов";
                } else if (n.StartsWith("датчик") ||
                    n.StartsWith("обманка датчика")) {                                  //Датчик для авто
                    a.categoryId = 85843109;
                    a.typeId = 971006606;
                    a.typeName = "Датчик для автомобиля";
                } else if (n.StartsWith("поворотник")) {                                //Световые приборы
                    a.categoryId = 33697184;//Фары, фонари и составляющие
                    a.typeId = 970854830;
                    a.typeName = "Указатель поворота";
                } else if ((n.StartsWith("фара") || n.StartsWith("фары")) &&
                    n.Contains("птф") || n.Contains("противотуман")) {
                    a.categoryId = 33697184;//Фары, фонари и составляющие
                    a.typeId = 367249975;
                    a.typeName = "Фары противотуманные (ПТФ)";
                } else if (n.StartsWith("катафот")) {
                    a.categoryId = 92265227;
                    a.typeId = 92198;
                    a.typeName = "Светоотражатель";
                } else if ((n.StartsWith("заглушка") &&                                   //Пластик кузова, молдинги, подкрылки
                    (n.Contains("бампер") || n.Contains("туман")))) {
                    a.categoryId = 27332774;//Защита внешних частей автомобиля
                    a.typeId = 970954154;
                    a.typeName = "Заглушка бампера автомобиля";
                } else if (n.StartsWith("фара") ||
                    n.StartsWith("фары")) {
                    a.categoryId = 33697184;//Фары, фонари и составляющие
                    a.typeId = 970687095;
                    a.typeName = "Фара автомобильная";
                } else if (n.StartsWith("фонарь") ||
                    n.StartsWith("фонари") || n.StartsWith("стоп дополнительный")) {
                    a.categoryId = 33697184;//Фары, фонари и составляющие
                    a.typeId = 970687094;
                    a.typeName = "Задний фонарь автомобильный";
                } else if (n.StartsWith("насос гур") ||                                 //рулевое управление
                    n.StartsWith("гидроусилитель") ||
                    n.StartsWith("насос гидроусилителя")) {
                    a.categoryId = 86296436;//Насос ГУР и составляющие
                    a.typeId = 98858;
                    a.typeName = "Насос ГУР";
                } else if (n.StartsWith("насос топливный") ||                           //топливная система
                    n.StartsWith("топливный насос")) {
                    a.categoryId = 85843113;
                    a.typeId = 98860;
                    a.typeName = "Насос топливный";
                } else if (n.StartsWith("зеркало") &&                                   //зеркала
                    (n.Contains("прав") || n.Contains("лев"))) {
                    a.categoryId = 99426212;
                    a.typeId = 970695250;
                    a.typeName = "Зеркало боковое";
                } else if (n.StartsWith("зеркало")) {
                    a.categoryId = 28305306;
                    a.typeId = 93362;
                    a.typeName = "Зеркало заднего вида";
                } else if (n.StartsWith("амортизатор") &&
                    (n.Contains("багажн") || n.Contains("капот"))) {                    //амортизатор багажника или капота
                    a.categoryId = 33304844;
                    a.typeId = 970852535;
                    a.typeName = "Упор багажника";
                } else if (n.StartsWith("амортизатор") &&                               //Амортизатор подвески
                    (n.Contains("перед") || n.Contains("задн"))) {
                    a.categoryId = 36201235;
                    a.typeId = 970744063;
                    a.typeName = "Амортизатор подвески";
                } else if (n.StartsWith("бачок") &&                                     //Бачок ГУР
                    (n.Contains("гур") || n.Contains("гидроусил"))) {
                    a.categoryId = 86296436;
                    a.typeId = 970984894;
                    a.typeName = "Бачок ГУР";
                } else if (n.Contains("бачок ") &&                                      //Расширительный бачок для авто
                    n.Contains("стекло")) {
                    a.categoryId = 85817600;
                    a.typeId = 970707039;
                    a.typeName = "Бачок стеклоомывателя";
                } else if (n.StartsWith("блок управ") &&
                    (n.Contains("отопител") || n.Contains("печк"))) {                   //Блок управления для авто
                    a.categoryId = 85843091;
                    a.typeId = 970885026;
                    a.typeName = "Блок управления отопителем";
                } else if (n.StartsWith("блок ") &&                                     //Блок управления для авто
                    (n.Contains("управления эур") || n.Contains("управления дв") ||
                     n.Contains("комфорт") || n.Contains("электрон") || n.Contains("bsi"))) {
                    a.categoryId = 85843091;
                    a.typeId = 971005681;
                    a.typeName = "Блок управления";
                } else if (n.StartsWith("блок ") && n.Contains("abs")) {                //Блок управления abs
                    a.categoryId = 85843091;
                    a.typeId = 970882084;
                    a.typeName = "Блок ABS";
                } else if (n.Contains("вилка ") &&                                      //Вилка сцепления
                    n.Contains("сцеплени")) {
                    a.categoryId = 33698203;//Цилиндр сцепления и комплектующие
                    a.typeId = 970978797;
                    a.typeName = "Вилка сцепления";
                } else if (n.Contains("диск ") &&                                       //Диск сцепления
                    n.Contains("сцеплени")) {
                    a.categoryId = 85835774;//Сцепление автомобильное и комплектующие
                    a.typeId = 98823;
                    a.typeName = "Диск сцепления";
                } else if (n.Contains("цилиндр ") &&                                    //Цилиндр сцепления
                    n.Contains("сцеплени") && n.Contains("главный")) {
                    a.categoryId = 33698203;//Цилиндр сцепления и комплектующие
                    a.typeId = 98963;
                    a.typeName = "Цилиндр сцепления главный";
                } else if (n.Contains("цилиндр ") &&                                    //Цилиндр сцепления
                    n.Contains("сцеплени") && n.Contains("рабоч")) {
                    a.categoryId = 33698203;//Цилиндр сцепления и комплектующие
                    a.typeId = 98964;
                    a.typeName = "Цилиндр сцепления рабочий";
                } else if (n.StartsWith("корзина") &&                                       //Корзина сцепления
                    n.Contains("сцеплени")) {
                    a.categoryId = 85835774;//Сцепление автомобильное и комплектующие
                    a.typeId = 970854836;
                    a.typeName = "Корзина сцепления";
                } else if (n.StartsWith("комплект") &&                                       //Комплект сцепления
                    n.Contains("сцеплени")) {
                    a.categoryId = 85835774;//Сцепление автомобильное и комплектующие
                    a.typeId = 98840;
                    a.typeName = "Комплект сцепления";
                } else if (n.Contains("вал ") &&                                        //Вал коробки передач для авто
                    (n.Contains("первичный") || n.Contains("вторичный"))) {
                    a.categoryId = 85817294;//КПП и составляющие
                    a.typeId = 971072319;
                    a.typeName = "Вал промежуточный";
                } else if (n.Contains("втулка") &&                                      //Втулка сайлентблока
                    n.Contains("сайлен")) {
                    a.categoryId = 85828600;//Рычаг, тяга подвески и составляющие
                    a.typeId = 970889765;
                    a.typeName = "Втулка сайлентблока";
                } else if (n.Contains("втулка ") &&                                     //Втулка подвески
                    n.Contains("подвес")) {
                    a.categoryId = 85828600;//Рычаг, тяга подвески и составляющие
                    a.typeId = 970863598;
                    a.typeName = "Втулка подвески";
                } else if (n.Contains("втулка ") &&                                     //Втулка стабилизатора
                    n.Contains("стабилиз")) {
                    a.categoryId = 85828600;//Рычаг, тяга подвески и составляющие
                    a.typeId = 970840966;
                    a.typeName = "Втулка стабилизатора";
                } else if (n.StartsWith("сайлентблок")) {                                //Сайлентблок
                    a.categoryId = 85828600;//Рычаг, тяга подвески и составляющие
                    a.typeId = 98928;
                    a.typeName = "Сайлентблок";
                } else if (n.StartsWith("гайка  ")) {                                   //Гайка, шайба
                    a.categoryId = 87716822;//74190355 Автокрепеж
                    a.typeId = 94544;
                    a.typeName = "Гайка";
                } else if (n.StartsWith("герметик ")) {                                 //Автохимия - Герметик, клей
                    a.categoryId = 33717355;
                    a.typeId = 369952585;
                    a.typeName = "Герметик автомобильный";
                } else if (n.StartsWith("жидкий ключ")) {                               //Автохимия - Смазка
                    a.categoryId = 33717369;
                    a.typeId = 92227;
                    a.typeName = "Ключ жидкий";
                } else if (n.StartsWith("присадк") && n.Contains("антигель")) {         //Автохимия - Присадки
                    a.categoryId = 79268071;
                    a.typeId = 92261;
                    a.typeName = "Антигель";
                } else if ((n.StartsWith("присадк") || n.StartsWith("очистител")) &&
                    (n.Contains("топл") || n.Contains("инж") || n.Contains("карбюр") ||
                    n.Contains("форс"))) {                                  //очиститель топливной системы
                    a.categoryId = 98327483;
                    a.typeId = 92243;
                    a.typeName = "Очиститель топливной системы";
                } else if (n.StartsWith("триботехническ") ||
                    (n.Contains("присад") && n.Contains("робот"))) {
                    a.categoryId = 79268071;
                    a.typeId = 92258;
                    a.typeName = "Присадка в масло";
                } else if (n.StartsWith("присадк")) {
                    a.categoryId = 79268071;
                    a.typeId = 92259;
                    a.typeName = "Присадка в топливо";
                } else if (n.StartsWith("очиститель") && n.Contains("конд")) {
                    a.categoryId = 98327483;
                    a.typeId = 92246;
                    a.typeName = "Очиститель кондиционера";
                } else if (n.Contains("очист") && n.Contains("охл")) {
                    a.categoryId = 79268071;
                    a.typeId = 970952444;
                    a.typeName = "Очиститель системы охлаждения";
                } else if ((n.Contains("раскоксовка") || n.Contains("промывка")) && n.Contains("двигат")) {
                    a.categoryId = 98327483;
                    a.typeId = 970892544;
                    a.typeName = "Раскоксовка двигателя";
                } else if (n.StartsWith("антифриз")) {                                  //Автохимия - Антифриз, тосол
                    a.categoryId = 33717357;
                    a.typeId = 92224;
                    a.typeName = "Антифриз";
                } else if (n.StartsWith("замок") && n.Contains("двер")) {               //Замок двери
                    a.categoryId = 92145042;//Замки автомобильные
                    a.typeId = 970950655;
                    a.typeName = "Замок двери автомобиля";
                } else if (n.StartsWith("замок") && n.Contains("капот") ||
                    n.StartsWith("комплект замка капота")) {                            //Замок капота
                    a.categoryId = 92145042;//Замки автомобильные
                    a.typeId = 970892946;
                    a.typeName = "Замок капота";
                } else if (n.StartsWith("замок") && n.Contains("багаж")) {              //Замок багажника
                    a.categoryId = 92145042;//Замки автомобильные
                    a.typeId = 321057673;
                    a.typeName = "Замок для багажников";
                } else if (n.StartsWith("трос") &&
                    n.Contains("замка") && n.Contains("двери")) {                       //Трос замка двери
                    a.categoryId = 92145042;//Замки автомобильные
                    a.typeId = 971072793;
                    a.typeName = "Трос замка двери";
                } else if (n.StartsWith("трос") &&
                    n.Contains("лючка") && n.Contains("бак")) {                       //Трос открывания
                    a.categoryId = 92145042;//Замки автомобильные
                    a.typeId = 971049579;
                    a.typeName = "Трос открывания";
                } else if (n.Contains("личин")) {                                       //Личинка замка
                    a.categoryId = 92145042;//Замки автомобильные
                    a.typeId = 971072745;
                    a.typeName = "Личинка замка";
                } else if (n.Contains("заслонка") &&                                    //Дроссельная заслонка
                    n.Contains("дроссел")) {
                    a.categoryId = 33698198;
                    a.typeId = 98826;
                    a.typeName = "Заслонка дроссельная";
                } else if ((n.StartsWith("защита") || n.StartsWith("пыльник")) &&         //Защита нижней части автомобиля
                    (n.Contains("двиг") || n.Contains("карт") || n.Contains("двс"))) {
                    a.categoryId = 33304846;
                    a.typeId = 970594170;
                    a.typeName = "Защита двигателя";
                } else if (((n.StartsWith("кнопка") || n.StartsWith("блок ")) &&         //Переключатель салона авто
                    n.Contains("стеклопод")) ||
                    n.StartsWith("переключатель света")) {
                    a.categoryId = 92145050;
                    a.typeId = 971032531;
                    a.typeName = "Переключатель салона автомобиля";
                } else if ((n.StartsWith("колонка") || n.StartsWith("вал")) &&          //Вал рулевой
                    n.Contains("рулев")) {
                    a.categoryId = 85833342;//Рулевая рейка и составляющие
                    a.typeId = 970984870;
                    a.typeName = "Вал рулевой";
                } else if (n.Contains("кулис")) {                                       //Кулиса и составляющие для авто
                    a.categoryId = 85817294;//КПП и составляющие
                    a.typeId = 971072743;
                    a.typeName = "Кулиса КПП";
                } else if (n.Contains("компрессор кондиционера")) {                     //Компрессор климатической установки для авто
                    a.categoryId = 85833494;//Компрессор климатической установки для авто
                    a.typeId = 970782176;
                    a.typeName = "Компрессор кондиционера";
                } else if (n.StartsWith("кордщетка") && n.Contains("дрел")) {           //Принадлежности для шлифовки, полировки
                    a.categoryId = 32451153;
                    a.typeId = 94949;
                    a.typeName = "Чашка шлифовальная";
                } else if ((n.StartsWith("кронштейн") || n.Contains("направляющая"))
                    && n.Contains("бампер")) {                                          //Кронштейн крепления бампера для авто
                    a.categoryId = 86292839;//Кронштейн крепления бампера для авто
                    a.typeId = 970863593;
                    a.typeName = "Кронштейн крепления для автомобиля";
                } else if (n.StartsWith("крыло")) {                                     //Крыло автомобильное
                    a.categoryId = 101407402;//арка колеса, 48159484 -? кузовные запчасти
                    a.typeId = 970967838;
                    a.typeName = "Крыло для автомобиля";
                } else if (n.Contains("масло") && n.Contains("моторное")) {             //Автохимия - Масло моторное
                    a.categoryId = 33717370;//Автохимия - Масло моторное
                    a.typeId = 96161;
                    a.typeName = "Масло моторное";
                } else if (n.Contains("масло") && n.Contains("трансмис")) {             //Автохимия - Трансмиссионное, гидравлическое масла
                    a.categoryId = 81105347;
                    a.typeId = 970637220;
                    a.typeName = "Масло индустриальное";
                } else if ((n.StartsWith("жидкость") || n.Contains("масло")) &&
                    (n.Contains("гур") || n.Contains("гидравлическое"))) {             //Автохимия - Трансмиссионное, гидравлическое масла
                    a.categoryId = 81105347;
                    a.typeId = 92229;
                    a.typeName = "Жидкость для гидроусилителя";
                } else if (n.Contains("набор") && n.Contains("инструмента")) {          //Набор для ремонта авто
                    a.categoryId = 27332791;
                    a.typeId = 971437067;
                    a.typeName = "Набор инструментов для автомобиля";
                } else if (n.Contains("наклад") && n.Contains("порога")) {              //Обшивки салона
                    a.categoryId = 1000003027;
                    a.typeId = 971159265;
                    a.typeName = "Обшивка салона автомобиля";
                } else if (n.StartsWith("огнетушитель")) {
                    a.categoryId = 28000060;//Огнетушитель автомобильный
                    a.typeId = 95562;
                    a.typeName = "Огнетушитель автомобильный";
                } else if (n.StartsWith("стеклоподъемник")) {
                    a.categoryId = 86472353; //Стеклоподъемник автомобильный
                    a.typeId = 970744186;
                    a.typeName = "Стеклоподъемник";
                } else if (n.StartsWith("брызговик")) {
                    a.categoryId = 27332785; //Брызговики
                    a.typeId = 94655;
                    a.typeName = "Брызговики";
                } else if (n.StartsWith("вкладыши шатуна")) {
                    a.categoryId = 85812214;
                    a.typeId = 970892948;
                    a.typeName = "Вкладыш шатунный";
                } else if (n.StartsWith("прокладк") &&
                    (n.Contains("поддон") || n.Contains("топлив")||n.Contains("крышки дв"))) {     //Прокладка двигателя
                    a.categoryId = 85810218;
                    a.typeId = 98894;
                    a.typeName = "Прокладка двигателя";
                } else if (n.StartsWith("прокладк") &&
                    (n.Contains("гбц") || n.Contains("клапан"))) {
                    a.categoryId = 85810218;
                    a.typeId = 970782913;
                    a.typeName = "Прокладка ГБЦ";
                } else if (n.StartsWith("прокладк") &&
                    (n.Contains("трубы") || n.Contains("катализ") || n.Contains("глушит"))) {
                    a.categoryId = 85810218;
                    a.typeId = 971072768;
                    a.typeName = "Прокладка глушителя";
                } else if (n.StartsWith("прокладк") &&
                    n.Contains("впуск") && n.Contains("коллект")) {
                    a.categoryId = 85810218;
                    a.typeId = 971738348;
                    a.typeName = "Прокладка впускного коллектора";
                } else if (n.StartsWith("прокладк") &&
                    (n.Contains("радиатор") || n.Contains("масл"))) {
                    a.categoryId = 85810218;
                    a.typeId = 971749438;
                    a.typeName = "Прокладка для системы охлаждения автомобиля";
                } else if (n.StartsWith("кольцо") &&
                    n.Contains("форсун")) {
                    a.categoryId = 85810218;
                    a.typeId = 98839;
                    a.typeName = "Кольцо, прокладка форсунки";
                } else if (n.StartsWith("камера") &&                                        //Камера заднего вида
                    n.Contains("задн") && n.Contains("вид")) {
                    a.categoryId = 81050058;
                    a.typeId = 94746;
                    a.typeName = "Камера заднего вида";
                } else if (n.StartsWith("клапан") &&                                        //Клапан впускной
                    n.Contains("впуск")) {
                    a.categoryId = 85812214;
                    a.typeId = 98828;
                    a.typeName = "Клапан впускной";
                } else if (n.StartsWith("колпач") &&                                        //Колпачки маслосъёмные
                    n.Contains("маслос")) {
                    a.categoryId = 85810218;
                    a.typeId = 98838;
                    a.typeName = "Колпачок маслосъемный";
                } else if (n.StartsWith("комплект") &&                                        //Комплект ГРМ
                    n.Contains("грм")) {
                    a.categoryId = 85814516;
                    a.typeId = 970782921;
                    a.typeName = "Ремкомплект ремня ГРМ";
                } else if ((n.StartsWith("молдинг") ||                                        //Молдинг (ресничка) фары 
                    n.StartsWith("ресничка")) &&
                    n.Contains("фар")) {
                    a.categoryId = 27332774;
                    a.typeId = 970682241;
                    a.typeName = "Накладка на фары";
                } else if (n.StartsWith("крышка") &&                                      //Крышка бачка 
                    n.Contains("бачка") &&
                    (n.Contains("расшир") || n.Contains("сцепл"))) {
                    a.categoryId = 85833530;
                    a.typeId = 971072740;
                    a.typeName = "Крышка бачка расширительного";
                } else if (n.StartsWith("моторчик") &&                                    //Моторчик заднего дворника 
                    n.Contains("зад") &&
                    (n.Contains("дворник") || n.Contains("стеклооч"))) {
                    a.categoryId = 86470408;
                    a.typeId = 970892937;
                    a.typeName = "Мотор стеклоочистителя";
                } else if (n.StartsWith("трапеция") &&                                    //Трапеция, рычаг стеклоочистителя 
                    (n.Contains("дворник") || n.Contains("стеклооч"))) {
                    a.categoryId = 86470408;
                    a.typeId = 970885007;
                    a.typeName = "Трапеция стеклоочистителя";
                } else if ((n.StartsWith("подшипник") ||                         //Ступица, подшипник колеса 
                    n.StartsWith("обойма подшипника"))
                    &&
                    (n.Contains("ступи") || n.Contains("колес") || 
                    n.Contains("полуоси") ||n.Contains("конический"))) {
                    a.categoryId = 36201238;
                    a.typeId = 98883;
                    a.typeName = "Подшипник ступицы";
                } else if (n.StartsWith("гайка под лямбда-зонд")) {                        //Гайка под лямбда-зонд 
                    a.categoryId = 33485121;
                    a.typeId = 98803;
                    a.typeName = "Болты, гайки, хомуты, стяжки";
                } else if (n.StartsWith("диск штампованный")) {                            //Диск штампованный 
                    a.categoryId = 27332798;
                    a.typeId = 94832;
                    a.typeName = "Колесный диск";
                } else if (n.StartsWith("полукольцо коленвала")) {                         //Полукольцо коленвала 
                    a.categoryId = 85812214;
                    a.typeId = 971437282;
                    a.typeName = "Полукольцо коленвала";
                } else if (n.StartsWith("корпус плоского разъема")) {                         //Корпус плоского разъема 
                    a.categoryId = 85843110;
                    a.typeId = 970863583;
                    a.typeName = "Соединитель проводки";
                } else if (n.StartsWith("накладка двери")) {                         //Накладка двери 
                    a.categoryId = 27332774;
                    a.typeId = 970719027;
                    a.typeName = "Молдинг для автомобиля";
                } else if (n.StartsWith("обманка лямбд")) {                         //Обманка лямбды 
                    a.categoryId = 33698293;
                    a.typeId = 970896619;
                    a.typeName = "Миникатализатор";
                } else if (n.StartsWith("опора амортизатора")) {                         //Опора амортизатора 
                    a.categoryId = 36201237;
                    a.typeId = 98863;
                    a.typeName = "Опора амортизатора";
                } else if ((n.StartsWith("опора") || n.StartsWith("подуш")) &&
                    (n.Contains("двс") || n.Contains("двигател"))) {                         //Опора двигателя 
                    a.categoryId = 33696914;
                    a.typeId = 970782919;
                    a.typeName = "Опора двигателя";
                } else if (n.StartsWith("паста") && n.Contains("очистки")
                    || n.StartsWith("очиститель рук")) {                               //Паста для очистки рук 
                    a.categoryId = 98327483;
                    a.typeId = 92264;
                    a.typeName = "Средство для очистки рук";
                } else if (n.StartsWith("патрубок") && n.Contains("бака")) {             //Патрубок бака 
                    a.categoryId = 86292454;
                    a.typeId = 970584071;
                    a.typeName = "Шланг топливный";
                } else if (n.StartsWith("патрубок") &&                                  //Патрубок охлаждения 
                    (n.Contains("охлаждения") || n.Contains("радиатора"))) {
                    a.categoryId = 86292454;
                    a.typeId = 98873;
                    a.typeName = "Патрубок охлаждения";
                } else if (n.StartsWith("переключатель") &&                              //Переключатель подрулевой 
                      (n.Contains("стеклоочистител") || n.Contains("поворот"))) {
                    a.categoryId = 85833342;
                    a.typeId = 970889789;
                    a.typeName = "Переключатель подрулевой";
                } else if (n.StartsWith("петл") && n.Contains("капота")) {              //Петля капота 
                    a.categoryId = 93446801;
                    a.typeId = 971109197;
                    a.typeName = "Петля капота";
                } else if (n.StartsWith("петл") && n.Contains("лючка")) {              //Петля лючка бензобака 
                    a.categoryId = 33698195;
                    a.typeId = 971049580;
                    a.typeName = "Ремкомплект лючка бензобака";
                } else if (n.Contains("глушител") &&                                     //Подвес глушителя 
                    (n.Contains("подвес") || n.Contains("подушка") || n.Contains("крепление"))) {
                    a.categoryId = 33698293;//Выхлопная труба и составляющие
                    a.typeId = 970964738;
                    a.typeName = "Подушка крепления глушителя";
                } else if (n.Contains("поддон") &&                                     //Поддон двсгателя 
                    (n.Contains("двс") || n.Contains("двсгателя") || n.Contains("картера"))) {
                    a.categoryId = 85806235;
                    a.typeId = 970891893;
                    a.typeName = "Поддон картера двигателя";
                } else if (n.StartsWith("подкрыл") &&                                     //подкрылки 
                    (n.Contains("перед") || n.Contains("зад") || n.Contains("лев") || n.Contains("прав"))) {
                    a.categoryId = 33304847;
                    a.typeId = 94663;
                    a.typeName = "Подкрылки";
                } else if (n.StartsWith("прокладка") && n.Contains("зеркал") &&          //Прокладка наружного зеркала
                    (n.Contains("нар") || n.Contains("прав") || n.Contains("лев"))) {
                    a.categoryId = 99426212;
                    a.typeId = 970829668;
                    a.typeName = "Запчасть бокового зеркала";
                } else if (n.StartsWith("подшипник") &&          //Подшипник выжимной
                    n.Contains("выжимной")) {
                    a.categoryId = 85835774;
                    a.typeId = 98878;
                    a.typeName = "Подшипник выжимной";
                } else if (n.Contains("подшипник") &&          //Подшипник опоры амортизатора
                    (n.Contains("стойк") || n.Contains("аморт") || n.Contains("опор"))) {
                    a.categoryId = 36201237;
                    a.typeId = 98877;
                    a.typeName = "Подшипник амортизатора";
                } else if (n.StartsWith("полироль ")){           //Полироль 
                    a.categoryId = 33717350;
                    a.typeId = 92256;
                    a.typeName = "Полироль автомобильный";
                } else if ((n.StartsWith("помпа") || n.StartsWith("насос ")) &&           //Помпа водяная
                  (n.Contains("водян") || n.Contains("охлажд"))) {
                    a.categoryId = 39653253;
                    a.typeId = 98857;
                    a.typeName = "Водяной насос (помпа)";
                } else if (n.StartsWith("поршень") || n.StartsWith("поршни")){          //Поршень
                    a.categoryId = 85812214;
                    a.typeId = 970782910;
                    a.typeName = "Поршень";
                } else if ((n.StartsWith("постель") && n.Contains("распредвал")) ||      //Постель с распредвалом
                  n.StartsWith("распредвал")) {
                    a.categoryId = 85812214;
                    a.typeId = 98905;
                    a.typeName = "Распредвал";
                } else if (n.StartsWith("преднатяжитель") && n.Contains("безопасност")){   //Преднатяжитель ремня безопасности 
                    a.categoryId = 85806235;
                    a.typeId = 970799804;
                    a.typeName = "Запчасти автомобильные";
                } else if (n.StartsWith("преобразователь") && n.Contains("ржавчины")){      //Преобразователь ржавчины 
                    a.categoryId = 33717356;
                    a.typeId = 92257;
                    a.typeName = "Преобразователь ржавчины";
                } else if ((n.Contains("привод") || n.Contains("полуось")) &&
                    (n.Contains("лев") || n.Contains("прав"))){                             //Привод 
                    a.categoryId = 85817289;
                    a.typeId = 98888;
                    a.typeName = "Привод в сборе";
                } else if (n.Contains("раскоксовыв") &&
                    (n.Contains("двигателя") || n.Contains("двс"))){                      //Раскоксовывание двигателя 
                    a.categoryId = 98327483;
                    a.typeId = 970892544;
                    a.typeName = "Раскоксовка двигателя";
                } else if (n.StartsWith("расходомер") ||
                    n.StartsWith("дмрв") || n.StartsWith("датчик расхода")){              //Расходомер воздуха 
                    a.categoryId = 85843109;
                    a.typeId = 970740203;
                    a.typeName = "Датчик массового расхода воздуха";
                } else if (n.StartsWith("реле ") &&
                    n.Contains("поворот")){              //Реле поворотов
                    a.categoryId = 39653253;
                    a.typeId = 971072777;
                    a.typeName = "Реле указателей поворота";
                } else if (n.StartsWith("реле ") &&
                    n.Contains("бензо")){              //Реле бензонасоса
                    a.categoryId = 39653253;
                    a.typeId = 971047416;
                    a.typeName = "Реле бензонасоса";
                } else if (n.StartsWith("реле ")){              //Реле универсальное
                    a.categoryId = 39653253;
                    a.typeId = 971047417;
                    a.typeName = "Реле универсальное для автомобиля";
                } else if (n.StartsWith("резистор") &&
                    (n.Contains("вентилятор") || n.Contains("отопител"))){              //Резистор вентилятора 
                    a.categoryId = 85833530;
                    a.typeId = 971072776;
                    a.typeName = "Резистор вентилятора";
                } else if (n.Contains("рейка") &&
                    n.Contains("рулевая")){              //Рейка рулевая 
                    a.categoryId = 85833342;
                    a.typeId = 970784043;
                    a.typeName = "Рейка рулевая";
                } else if (n.StartsWith("ремкомплект") &&
                    n.Contains("суппорт")){              //Ремкомплект тормозного суппорта 
                    a.categoryId = 85842995;
                    a.typeId = 98917;
                    a.typeName = "Ремкомплект суппорта";
                } else if (n.StartsWith("решетка") &&
                    (n.Contains("бампер") || n.Contains("радиатор"))){    //решетка бампера радиатора
                    a.categoryId = 100186418;
                    a.typeId = 97666;
                    a.typeName = "Решетка радиатора";
                } else if (n.StartsWith("ролик ") &&
                    n.Contains("ремня")){              //Ролик натяжной ремня 
                    a.categoryId = 85814516;
                    a.typeId = 970889786;
                    a.typeName = "Ролик натяжителя";
                } else if (n.StartsWith("ручник ")){    //Ручник 
                    a.categoryId = 86292728;
                    a.typeId = 971032883;
                    a.typeName = "Рычаг тормоза";
                } else if (n.StartsWith("рычаг") &&          //Рычаг подвески
                    (n.Contains("подвеск") || n.Contains("зад") ||
                    n.Contains("лев") || n.Contains("прав"))) {
                    a.categoryId = 85828600;
                    a.typeId = 970849745;
                    a.typeName = "Рычаг подвески";
                } else if (n.StartsWith("свеч") &&
                    n.Contains("зажигания")) {             //Свеча зажигания 
                    a.categoryId = 33698210;
                    a.typeId = 95765;
                    a.typeName = "Свеча зажигания";
                } else if (n.StartsWith("смазка ") ||                //Смазка 
                    n.StartsWith("защита") && n.Contains("клемм")){
                    a.categoryId = 33717369;
                    a.typeId = 92263;
                    a.typeName = "Смазка";
                } else if (n.StartsWith("спойлер") &&
                    n.Contains("бампера")){              //Спойлер бампера 
                    a.categoryId = 27332774;
                    a.typeId = 970895069;
                    a.typeName = "Спойлер автомобиля";
                } else if (n.StartsWith("стекло ") &&
                    n.Contains("двери")){              //стекло двери 
                    a.categoryId = 93446800;
                    a.typeId = 970977226;
                    a.typeName = "Автостекло";
                } else if ((n.StartsWith("стекло ") && n.Contains("зеркала")) ||              //Стекло зеркала 
                   (n.Contains("зеркальный") && n.Contains("элемент"))) {
                    a.categoryId = 28305306;
                    a.typeId = 971092521;
                    a.typeName = "Элемент зеркальный";
                } else if (n.StartsWith("струна ") && n.Contains("срезания")) {              //Струна для срезания стекла 
                    a.categoryId = 45393031;
                    a.typeId = 97469;
                    a.typeName = "Специнструмент для авто";
                } else if (n.StartsWith("ступица ")) {              //Ступица 
                    a.categoryId = 36201238;
                    a.typeId = 98945;
                    a.typeName = "Ступица";
                } else if (n.StartsWith("трубка ") && n.Contains("турб")) {              //Трубка турбокомпрессора 
                    a.categoryId = 86292454;
                    a.typeId = 971047420;
                    a.typeName = "Патрубок турбокомпрессора";
                } else if (n.StartsWith("успокоитель цепи") && n.Contains("грм")) {              //Успокоитель цепи ГРМ 
                    a.categoryId = 85814516;
                    a.typeId = 98956;
                    a.typeName = "Успокоитель цепи ГРМ";
                } else if (n.StartsWith("фильтр ") && n.Contains("топливный")) {              //Фильтр топливный 
                    a.categoryId = 33698213;
                    a.typeId = 96179;
                    a.typeName = "Фильтр топливный";
                } else if (n.StartsWith("фланец ") &&
                    (n.Contains("карбюратор") || n.Contains("моновпрыск"))) {              //Фланец карбюратора 
                    a.categoryId = 33698197;
                    a.typeId = 971061543;
                    a.typeName = "Ремкомплект карбюратора";
                } else if (n.StartsWith("форсунка ") &&
                    (n.Contains("омывателя") || n.Contains("фар"))) {              //Форсунка омывателя 
                    a.categoryId = 85817600;
                    a.typeId = 970863584;
                    a.typeName = "Форсунка омывателя";
                } else if (n.StartsWith("шарнир ") &&
                    (n.Contains("штока") || n.Contains("кпп"))) {              //Шарнир штока КПП 
                    a.categoryId = 85817294;
                    a.typeId = 98898;
                    a.typeName = "Запчасти для коробки передач";
                } else if (n.StartsWith("шатун ")) {              //шатун 
                    a.categoryId = 85812214;
                    a.typeId = 971362794;
                    a.typeName = "Шатун двигателя";
                } else if (n.StartsWith("штатная подсветка дверей")) {              //Штатная подсветка 
                    a.categoryId = 33697187;
                    a.typeId = 970681436;
                    a.typeName = "Проекция логотипа автомобиля";
                } else if (n.StartsWith("штуцер") &&
                    (n.Contains("прокачки") || n.Contains("суппорт"))) {              //Штуцер прокачки тормозного суппорта 
                    a.categoryId = 85842995;
                    a.typeId = 98917;
                    a.typeName = "Ремкомплект суппорта";
                } else if (n.StartsWith("щетка ") &&
                    (n.Contains("дрели") || n.Contains("чашка"))) {              //Щетка для дрели 
                    a.categoryId = 32451142;
                    a.typeId = 94913;
                    a.typeName = "Корщетка";
                } else if (n.StartsWith("щиток ") &&
                    n.Contains("тормозной")) {              //Щиток тормозной 
                    a.categoryId = 85842996;
                    a.typeId = 98918;
                    a.typeName = "Ремкомплект тормозного механизма";
                } else if (n.StartsWith("эмблема ")) {              //Эмблема  
                    a.categoryId = 74204537;
                    a.typeId = 96755;
                    a.typeName = "Эмблема для автомобиля";
                } else if ((n.StartsWith("airbag ") || n.StartsWith("подушка ")) &&
                    (n.Contains("рул") || n.Contains("безопасност"))) {              //AIRBAG Подушка безопасности
                    a.categoryId = 99615343;
                    a.typeId = 971026652;
                    a.typeName = "Подушка безопасности";
                } else if (n.StartsWith("эмаль ")) {                             //эмали
                    a.categoryId = 32451132;
                    a.typeId = 96664;
                    a.typeName = "Эмаль";
                } else if (n.StartsWith("хомут ")) {                           //хомуты
                    a.categoryId = 87717033;
                    a.typeId = 94561;
                    a.typeName = "Хомут";
                } else if (n.StartsWith("ключ комбинированный")||
                    n.StartsWith("набор") && n.Contains("ключ")) {             //Ключи
                    a.categoryId = 45393031;
                    a.typeId = 92082;
                    a.typeName = "Ключ";
                } else if (n.StartsWith("отвертк")) {            //отвертки
                    a.categoryId = 45393034;
                    a.typeId = 92108;
                    a.typeName = "Отвертка";
                } else if (n.StartsWith("головка") && 
                    (n.Contains("удлин")||n.Contains("торц")||n.Contains("шести"))) {            //головки
                    a.categoryId = 32451092;
                    a.typeId = 92145;
                    a.typeName = "Торцевая головка";
                } else if (n.StartsWith("шрус") && n.Contains("наружн")) {            //шрус наружный
                    a.categoryId = 85817289;
                    a.typeId = 98968;
                    a.typeName = "ШРУС наружный";
                } else if (n.StartsWith("шрус") && n.Contains("внутр")) {            //шрус внутренний
                    a.categoryId = 85817289;
                    a.typeId = 98969;
                    a.typeName = "ШРУС внутренний";
                } else if (n.StartsWith("адаптер") && n.Contains("щет")) {            //Адаптер щетки стеклоочистителя
                    a.categoryId = 33698214;
                    a.typeId = 95588;
                    a.typeName = "Адаптер щетки стеклоочистителя";
                } else if (n.StartsWith("электропроводка") && n.Contains("печ")) {            //Комплект автопроводки
                    a.categoryId = 85843110;
                    a.typeId = 94674;
                    a.typeName = "Комплект автопроводки";
                } else if (n.StartsWith("очиститель") && n.Contains("нержав")) {            //очиститель нержавейки
                    a.categoryId = 17033691;
                    a.typeId = 92701;
                    a.typeName = "Специальное чистящее средство";
                } else if (n.Contains("насос") && n.Contains("масляный")||
                    n.Contains("колесо масляного насоса")) {                      //Насос масляный
                    a.categoryId = 86292577;
                    a.typeId = 98859;
                    a.typeName = "Насос масляный";
                } else if (n.Contains("тяга") && n.Contains("рулевая")) {            //Тяга рулевая
                    a.categoryId = 85833342;
                    a.typeId = 98955;
                    a.typeName = "Тяга рулевая";
                } else if (n.Contains("пыльник") && n.Contains("шрус")) {            //Пыльник ШРУСа
                    a.categoryId = 85817289;
                    a.typeId = 98903;
                    a.typeName = "Пыльник ШРУСа";
                } else if (n.Contains("соединитель") && n.Contains("отоп")) {            // соединители патрубки отопления
                    a.categoryId = 86292454;
                    a.typeId = 970740221;
                    a.typeName = "Патрубки отопления";
                } else if (n.Contains("воронка") && n.Contains("носик")) {            // Воронка техническая
                    a.categoryId = 86473322;
                    a.typeId = 92168;
                    a.typeName = "Воронка техническая";
                } else if (n.Contains("щетка") && n.Contains("металлическая")) {            // щетка металическая
                    a.categoryId = 1000003987;
                    a.typeId = 92157;
                    a.typeName = "Щетка строительная";
                } else if (n.StartsWith("изолента")) {            // изолента
                    a.categoryId = 43132640;
                    a.typeId = 94549;
                    a.typeName = "Изолента";
                } else if (n.Contains("холодная") && n.Contains("сварка")) {            // Холодная сварка
                    a.categoryId = 33717355;
                    a.typeId = 92272;
                    a.typeName = "Холодная сварка";
                } else if (n.Contains("трос") && n.Contains("буксировочный")) {            // Трос буксировочный
                    a.categoryId = 27332707;
                    a.typeId = 92207;
                    a.typeName = "Трос буксировочный";
                } else if (n.Contains("кольцо") && n.Contains("уплотнительное")) {            // Кольцо уплотнительное приемной трубы
                    a.categoryId = 33698197;
                    a.typeId = 970863596;
                    a.typeName = "Кольцо уплотнительное для автомобиля";
                } else if (n.StartsWith("салфетка")) {            // Салфетка микрофибра
                    a.categoryId = 74172611;
                    a.typeId = 269166265;
                    a.typeName = "Салфетка автомобильная";
                } else if (n.Contains("фиксатор") && n.Contains("резьбы")) {       // Фиксатор резьбы
                    a.categoryId = 33717355;
                    a.typeId = 970682158;
                    a.typeName = "Фиксатор резьбы";
                } else if (n.Contains("быстрый") && n.Contains("старт")) {       // Быстрый старт для двигателя
                    a.categoryId = 98327483;
                    a.typeId = 92228;
                    a.typeName = "Жидкость для быстрого запуска";
                } else if (n.StartsWith("трос") && n.Contains("спидометра")) {       // Трос спидометра
                    a.categoryId = 101292869;
                    a.typeId = 970889772;
                    a.typeName = "Трос спидометра";
                } else if (n.StartsWith("трос") && (
                    n.Contains("ручника") ||
                    n.Contains("тормоз") && 
                    (n.Contains("ручн") || n.Contains("стоян")))) {       // Трос ручника
                    a.categoryId = 101292869;
                    a.typeId = 98952;
                    a.typeName = "Трос ручного тормоза";
                } else if (n.StartsWith("размораживатель") && n.Contains("замков")) {       // Размораживатель замков 
                    a.categoryId = 33717339;
                    a.typeId = 970742711;
                    a.typeName = "Размораживатель замков";
                } else if (n.Contains("лампа") && (n.Contains("галоген")||n.Contains("ксенон"))) {  // Автолампы 
                    a.categoryId = 33697187;
                    a.typeId = 367249974;
                    a.typeName = "Лампа автомобильная";
                    //todo atributes lapm type, connection
                } else if (n.StartsWith("опора") && n.Contains("кпп") ||
                    n.StartsWith("подушка коробки")) {                                          // Опоры кпп, акпп
                    a.categoryId = 33696914;
                    a.typeId = 98868;
                    a.typeName = "Опора КПП";
                } else if (n.Contains("автотестер")) {                                         // Автотестер
                    a.categoryId = 81070531;
                    a.typeId = 92206;
                    a.typeName = "Тестер автомобильный";
                } else if (n.StartsWith("предохранител")||
                    n.StartsWith("набор предохранителей")) {                                  // Предохранитель 
                    a.categoryId = 85843120;
                    a.typeId = 92190;
                    a.typeName = "Предохранители для автомобиля";
                    //todo atribute current (A), count in pkg
                } else if ((n.Contains("стяжки") || n.Contains("хомуты")) && n.Contains("кабел")) {       // Хомуты (стяжки)
                    a.categoryId = 33485121;
                    a.typeId = 98803;
                    a.typeName = "Болты, гайки, хомуты, стяжки";
                } else if (n.StartsWith("рамка") && n.Contains("номер")) {                      // Рамка под номерной знак
                    a.categoryId = 74169478;
                    a.typeId = 94664;
                    a.typeName = "Рамка госномера";
                } else if (n.StartsWith("ключ") && n.Contains("свеч")) {                    // Ключ свечной
                    a.categoryId = 45393031;
                    a.typeId = 504866196;
                    a.typeName = "Ключ свечной";
                    //todo atribute length
                } else if (n.StartsWith("держатель") && 
                    (n.Contains("телеф") || n.Contains("навиг"))) {       // Держатель для телефона / навигатора
                    a.categoryId = 27332753;
                    a.typeId = 115950474;
                    a.typeName = "Держатель автомобильный";
                    //todo atribute placement 
                } else if (n.StartsWith("съемник") && n.Contains("фильтр")) {       // Съемник масляного фильтра
                    a.categoryId = 27332790;
                    a.typeId = 92140;
                    a.typeName = "Съемник";
                } else if (n.StartsWith("баллон") && n.Contains("газовый")) {
                    a.categoryId = 1000003745;
                    a.typeId = 93535;
                    a.typeName = "Баллон с газом туристический";
                } else if (n.StartsWith("бокорезы")) {
                    a.categoryId = 91933861;
                    a.typeId = 92054;
                    a.typeName = "Бокорезы";
                    //todo atribute length
                } else if (n.Contains("бандаж") && n.Contains("глушителя")) {
                    a.categoryId = 86473322;
                    a.typeId = 971061974;
                    a.typeName = "Клейкая лента автомобильная";
                } else if (n.StartsWith("грунт ")) {                            // Грунтовка
                    a.categoryId = 86474732;
                    a.typeId = 92214;
                    a.typeName = "Автогрунтовка";
                } else if (n.Contains("жилет") && n.Contains("светоотраж")) {
                    a.categoryId = 87138242;
                    a.typeId = 94222;
                    a.typeName = "Светоотражающий жилет";
                    //todo atrib Целевая аудитория: Взрослая
                } else if (n.StartsWith("зажим-крокодил") ||
                    n.StartsWith("клемма-зажим")) {
                    a.categoryId = 88940265;
                    a.typeId = 970882088;
                    a.typeName = "Зажим Крокодил";
                    //todo atr ед. в одном товаре
                } else if (n.StartsWith("кабель для телефона")) {
                    a.categoryId = 17034034;
                    a.typeId = 971081965;
                    a.typeName = "Кабель для мобильных устройств";
                } else if (n.Contains("клей") && n.Contains("эпоксидный")) {
                    a.categoryId = 87181058;
                    a.typeId = 970576927;
                    a.typeName = "Клей эпоксидный";
                } else if (n.StartsWith("подшипник") && n.Contains("генератора")) {
                    a.categoryId = 61852812;
                    a.typeId = 98879;
                    a.typeName = "Подшипник генератора";
                } else if (n.StartsWith("подшипник") && n.Contains("дифференциала")) {
                    a.categoryId = 85817294;
                    a.typeId = 971475668;
                    a.typeName = "Подшипник редуктора";
                } else if (n.StartsWith("подшипник") && n.Contains("кпп") ||
                    n.Contains("сепаратор") && n.Contains("подшипник")) {
                    a.categoryId = 85817294;
                    a.typeId = 970889785;
                    a.typeName = "Подшипник КПП";
                } else if (n.StartsWith("провода прикуривания")) {
                    a.categoryId = 36502156;
                    a.typeId = 92192;
                    a.typeName = "Провода для прикуривания";
                } else if (n.StartsWith("штангенциркуль")) {
                    a.categoryId = 90730184;
                    a.typeId = 91715;
                    a.typeName = "Штангенциркуль";
                } else if (n.StartsWith("отбойник амортизатора")) {
                    a.categoryId = 36201237;
                    a.typeId = 98872;
                    a.typeName = "Отбойник амортизатора";
                } else if (n.StartsWith("натяжитель приводного ремня")) {
                    a.categoryId = 85814516;
                    a.typeId = 970863590;
                    a.typeName = "Натяжитель ремня";
                } else if (n.Contains("наконечник") && n.Contains("рулевой")) {
                    a.categoryId = 85833342;
                    a.typeId = 98854;
                    a.typeName = "Наконечник рулевой";
                } else if (n.Contains("опора") && n.Contains("шаровая")) {
                    a.categoryId = 85828600;
                    a.typeId = 98870;
                    a.typeName = "Опора шаровая";
                } else if ((n.Contains("стойка") || n.Contains("тяга")) && n.Contains("стабилизатора")) {
                    a.categoryId = 85828600;
                    a.typeId = 98943;
                    a.typeName = "Стойка стабилизатора";
                } else if (n.Contains("скоба") && n.Contains("стабилизатора")) {
                    a.categoryId = 85828600;
                    a.typeId = 98939;
                    a.typeName = "Стабилизатор поперечной устойчивости";
                } else if (n.Contains("кольц") && n.Contains("поршн")) {
                    a.categoryId =  85812214;
                    a.typeId =  971058287;
                    a.typeName = "Кольцо поршневое";
                } else if (n.Contains("пыльник") && n.Contains("шаров")) {
                    a.categoryId =  85828600;
                    a.typeId =  971123162;
                    a.typeName = "Пыльник шаровой опоры";
                } else if (n.Contains("сальник") && n.Contains("коленвал")) {
                    a.categoryId = 85812214;
                    a.typeId = 98929;
                    a.typeName = "Сальник вала";
                } else if (n.Contains("ремкомплект") && n.Contains("рычаг")) {
                    a.categoryId =  85828600;
                    a.typeId =  970984563;
                    a.typeName = "Ремкомплект рычага подвески";
                } else
                    return a;
                a.additionalAttributes.AddAttribute(GetSideAttribute(bus));
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
                a.additionalAttributes.AddAttribute(GetColorAttribute(bus));
                a.additionalAttributes.AddAttribute(GetTechTypeAttribute(bus));
                a.additionalAttributes.AddAttribute(GetDangerClassAttribute(bus));
                a.additionalAttributes.AddAttribute(GetExpirationDaysAttribute(bus));
                a.additionalAttributes.AddAttribute(GetMaterialAttribute(bus));
                a.additionalAttributes.AddAttribute(GetManufactureCountryAttribute(bus));
                a.additionalAttributes.AddAttribute(GetOEMAttribute(bus));
                a.additionalAttributes.AddAttribute(GetPlaceAttribute(bus));
                a.additionalAttributes.AddAttribute(GetMultiplicityAttribute(bus));
                a.additionalAttributes.AddAttribute(GetKeywordsAttribute(bus));
                a.additionalAttributes.AddAttribute(GetThicknessAttribute(bus));
                a.additionalAttributes.AddAttribute(GetHeightAttribute(bus));
                a.additionalAttributes.AddAttribute(GetLengthAttribute(bus));
                a.additionalAttributes.AddAttribute(GetMotorTypeAttribute(bus));
                a.additionalAttributes.AddAttribute(GetPlacementAttribute(bus));
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
                Log.Add("GetAttributesAsync: " + x.Message + x.InnerException?.Message);
                throw;
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
        async Task<Attribute> GetTNVEDAttribute(GoodObject good, Attributes attributes) {
            var value = good.hscode_id;
            if (value == null)
                return null;
            await UpdateVedAsync(attributes.categoryId);
            var t = new Value {
                value = _tnved.Find(f => f.value.Contains(value))?.value,
                dictionary_value_id = _tnved.Find(f => f.value.Contains(value))?.id ?? 0
            };
            if (t.dictionary_value_id == 0 || t.value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 22232,
                values = new Value[] {
                    t
                }
            };
        }
        //обновление тнвэд для каждой категории
        private async Task UpdateVedAsync(int categoryId) {
            var file = @"..\ozon\tnved_" + categoryId + ".json";
            //если файл свежий - загружаем с диска
            var lastWriteTime = File.GetLastWriteTime(file);
            if (lastWriteTime.AddHours(_updateFreq) > DateTime.Now) {
                _tnved = JsonConvert.DeserializeObject<List<AttributeValue>>(
                    File.ReadAllText(file));
            } else {
                _tnved = await GetAttibuteValuesAsync(category_id: categoryId, attribute_id: 22232);
                File.WriteAllText(file, JsonConvert.SerializeObject(_tnved));
            }
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
        Attribute GetPlacementAttribute(GoodObject good) {
            var value = good.GetPlacement();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 7367,
                values = new Value[] {
                    new Value{
                        value = value,
                        dictionary_value_id = _placement.Find(f=>f.value==value).id,
                    }
                }
            };
        }
        //Атрибут Тип двигателя
        Attribute GetMotorTypeAttribute(GoodObject good) {
            var value = good.GetMotorType();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 8303,
                values = new Value[] {
                    new Value{
                        value = value,
                        dictionary_value_id = _motorType.Find(f=>f.value==value).id,
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
            var value = good.GetMultiplicity();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 21497,
                values = new Value[] {
                    new Value{
                        value = value
                    }
                }
            };
        }
        //Атрибут Расположение детали
        Attribute GetPlaceAttribute(GoodObject good) {//todo rename Side
            var value = good.GetPlace();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 20189,
                values = new Value[] {
                    new Value{
                        value = value,
                        dictionary_value_id = _place.Find(f=>f.value==value).id,
                    }
                }
            };
        }
        //Атрибут ОЕМ-номер
        Attribute GetOEMAttribute(GoodObject good) {
            var man = good.GetManufacture(true)?
                          .ToLowerInvariant();
            if (_exceptManufactures.Contains(man))
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
        Attribute GetManufactureCountryAttribute(GoodObject good) {
            var value = good.GetManufactureCountry();
            if (value == null)
                return null;
            var dictionaryId = _manufactureCoutry.Find(f => f.value == value)?.id;
            if (dictionaryId == null) return null;
            return new Attribute {
                complex_id = 0,
                id = 4389,
                values = new Value[] {
                    new Value{
                        value = value,
                        dictionary_value_id = (int)dictionaryId,
                    }
                }
            };
        }
        //Атрибут Материал
        Attribute GetMaterialAttribute(GoodObject good) {
            var value = good.GetMaterial();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 7199,
                values = new Value[] {
                    new Value{
                        value = value,
                        dictionary_value_id = _material.Find(f=>f.value==value).id,
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
        Attribute GetDangerClassAttribute(GoodObject good) {
            var value = good.GetDangerClass();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 9782,
                values = new Value[] {
                    new Value{
                        dictionary_value_id = _dangerClass.Find(f=>f.value.Contains(value)).id,
                        value = _dangerClass.Find(f=>f.value.Contains(value)).value
                    }
                }
            };
        }
        //Атрибут Вид техники
        Attribute GetTechTypeAttribute(GoodObject good) {
            var value = good.GetTechType();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 7206,
                values = new Value[] {
                    new Value{
                        dictionary_value_id = _techType.Find(f=>f.value==value).id,
                        value = value
                    }
                }
            };
        }
        //Атрибут Цвет товара
        Attribute GetColorAttribute(GoodObject good) {
            var value = good.GetColor();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 10096,
                values = new Value[] {
                    new Value{
                        dictionary_value_id = _color.Find(f=>f.value==value).id,
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
            if (_exceptManufactures.Contains(man))
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
        Attribute GetSideAttribute(GoodObject good) {
            var value = good.GetSide();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 22329,
                values = new Value[] {
                    new Value{
                        dictionary_value_id = _side.Find(f=>f.value==value).id,
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
                    value = bus.id
                }
            }
            };
        }

        //Атрибут Аннотация Описание товара
        Attribute GetDescriptionAttribute(GoodObject good) => new Attribute {
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
        //Атрибут Тип товара
        Attribute GetTypeOfProductAttribute(int id, string name) =>
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
                        dictionary_value_id = id,
                        value = name
                    }
                }
            };
        }
        //Атрибут Партномер (артикул производителя) (в нашем случае артикул)
        Attribute GetPartAttribute(GoodObject bus) {
            var man = bus.GetManufacture(true)?
                         .ToLowerInvariant();
            var part = _exceptManufactures.Contains(man)? bus.id
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
        public async Task<List<AttributeValue>> GetAttibuteValuesAsync(int attribute_id = 85, int category_id = 61852812) {
            var attributeValuesFile = @"..\ozon\ozon_attr_" + attribute_id + "_cat_" + category_id + ".json";
            var lastWriteTime = File.GetLastWriteTime(attributeValuesFile);
            if (lastWriteTime.AddHours(_updateFreq) > DateTime.Now) {
                var res = JsonConvert.DeserializeObject<List<AttributeValue>>(
                    File.ReadAllText(attributeValuesFile));
                Log.Add(_l + " загружено с диска" + res.Count + " атрибутов");
                return res;
            } else {
                var res = new List<AttributeValue>();
                var last = 0;
                do {
                    var data = new {
                        attribute_id = attribute_id,
                        category_id = category_id,
                        language = "DEFAULT",
                        last_value_id = last,
                        limit = 1000
                    };
                    var s = await PostRequestAsync(data, "/v2/category/attribute/values");
                    res.AddRange(JsonConvert.DeserializeObject<List<AttributeValue>>(s));
                    last = res.Last()?.id ?? 0;
                } while (_hasNext);
                File.WriteAllText(attributeValuesFile, JsonConvert.SerializeObject(res));
                Log.Add(_l + " загружено с озон " + res.Count + " атрибутов");
                return res;
            }
        }
        //Запрос категорий озон
        public async Task GetCategoriesAsync(int rootCategoryId = 0) {
            var data = new {
                category_id = rootCategoryId,
                language = "DEFAULT"
            };
            //var s = await PostRequestAsync(data, "/v2/category/tree");
            var s = await PostRequestAsync(data, "/v1/description-category/tree");
            var res = JsonConvert.DeserializeObject<List<Description_category>>(s);
            File.WriteAllText(@"..\ozon\ozon_categories.json", s);
            Log.Add(_l + "GetCategoriesAsync - получено категорий " + res.Count);
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
        void SaveToFile(IEnumerable<GoodObject> goods, string fname = @"..\ozon\ozonGoodListForAdding.csv") {
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
            Log.Add("товары выгружены в ozonGoodListForAdding.csv");
        }
    }
    /////////////////////////////////////
    /// классы для работы с запросами ///
    /////////////////////////////////////
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
        public int categoryId;
        public int brendId;
        public string brendName;
        public int typeId;
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
            return int.Parse(price.Split('.').First());
        }
        public int GetOldPrice() {
            return int.Parse(old_price.Split('.').First());
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
        public int dictionary_value_id { get; set; }
        public string value { get; set; }
    }
    //=====================
    public class Description_category {
        public int description_category_id { get; set; }
        public string category_name { get; set;}
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
