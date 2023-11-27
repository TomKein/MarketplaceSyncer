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
        ProductList _productList;                     //список товаров, получаемый из /v2/product/list
        readonly string _productListFile = @"..\ozon_productList.json";
        readonly int _updateFreq = 48;               //частота обновления списка (часов)
        List<RootObject> _bus;                        //ссылка на товары
        DB _db = DB._db;                              //база данных
        static bool _isProductListCheckNeeds = true;
        bool _hasNext = false;                        //для запросов
        List<AttributeValue> _brends;                 //список брендов озон
        List<AttributeValue> _color;                  //список цветов озон
        List<AttributeValue> _techType;               //список вид техники
        List<AttributeValue> _dangerClass;            //список класс опасности
        List<AttributeValue> _motorType;              //список Тип двигателя
        List<AttributeValue> _manufactureCoutry;      //список Страна-изготовитель
        List<AttributeValue> _material;               //список Материал
        List<AttributeValue> _placement;              //список Места установки
        List<AttributeValue> _place;                  //список Расположение детали
        List<AttributeValue> _tnved;                  //список Коды ТН ВЭД
        public OzonApi() {
            _hc.BaseAddress = new Uri(_baseApiUrl);
            _url = _db.GetParamStr("ozon.url");
            _clientID = _db.GetParamStr("ozon.id");
            _apiKey = _db.GetParamStr("ozon.apiKey");
            _oldPriceProcent = _db.GetParamFloat("ozon.oldPriceProcent");
        }
        //главный метод
        public async Task SyncAsync() {
            _bus = FormMain._bus;
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
            await CheckProductListAsync();
            await UpdateProductsAsync();
            await AddProductsAsync();
            if (RootObject.ScanTime.Month < DateTime.Now.Month)
                await CheckProductLinksAsync(checkAll: true);
        }
        //проверка всех карточек в бизнесе, которые изменились и имеют ссылку на озон
        private async Task UpdateProductsAsync() {
            foreach (var bus in _bus.Where(w => w.IsTimeUpDated()
                                             && !string.IsNullOrEmpty(w.ozon)
                                             && w.ozon.Contains("http"))) {
                await UpdateProductAsync(bus);
            }
        }
        //проверяем список товаров озон
        public async Task CheckProductListAsync() {
            try {
                var startTime = DateTime.Now;
                //если файл свежий и товары не добавляли - загружаем с диска
                var lastWriteTime = File.GetLastWriteTime(_productListFile);
                if (lastWriteTime.AddHours(_updateFreq) > startTime
                    && !_isProductListCheckNeeds) {
                    _productList = JsonConvert.DeserializeObject<ProductList>(
                        File.ReadAllText(_productListFile));
                } else {
                    //TODO добавить циклическую загрузку, чтобы получать более 1000 товаров в дальнейшем
                    var data = new {
                        //filter = new {
                        //    visibility = "ALL" //VISIBLE, INVISIBLE, EMPTY_STOCK, READY_TO_SUPPLY, STATE_FAILED
                        //},
                        last_id = "",
                        limit = 1000
                    };
                    var s = await PostRequestAsync(data, "/v2/product/list");
                    _productList = JsonConvert.DeserializeObject<ProductList>(s);
                    Log.Add(_l + "ProductList - успешно загружено " + _productList.items.Count + " товаров");
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
            foreach (var item in _productList.items) {
                try {
                    //карточка в бизнес.ру с id = артикулу товара на озон
                    var b = _bus.FindIndex(_ => _.id == item.offer_id);
                    if (b == -1)
                        throw new Exception("карточка с id = " + item.offer_id + " не найдена!");
                    //если товар не привязан к карточке, либо неверный sku в ссылке
                    if (!_bus[b].ozon.Contains("http") ||
                        _bus[b].ozon.Split('/').Last().Length < 3 ||
                        checkAll) {
                        //запросим все данные о товаре
                        var productInfo = await GetProductInfoAsync(_bus[b]);
                        await SaveUrlAsync(_bus[b], productInfo);
                        //после проверки осуществляем проверку и обновление товара
                        await UpdateProductAsync(_bus[b], productInfo);
                    }
                } catch (Exception x) {
                    Log.Add(_l + " CheckGoodsAsync ошибка - " + x.Message);
                }
            }
        }
        //расширенная информация о товаре
        private async Task<ProductInfo> GetProductInfoAsync(RootObject bus) {
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
        async Task SaveUrlAsync(RootObject bus, ProductInfo productInfo) {
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
        async Task UpdateProductAsync(RootObject bus, ProductInfo productInfo = null) {
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
        private async Task UpdateProductStocks(RootObject bus, ProductInfo productInfo) {
            try {
                if (bus.amount == productInfo.GetStocks())
                    return;
                //защита от отрицательных остатков
                if (bus.amount < 0)
                    bus.amount = 0;
                //объект для запроса
                var data = new {
                    stocks = new[] {
                        new {
                            product_id = productInfo.id,
                            stock = bus.amount.ToString("F0")
                        }
                    }
                };
                var s = await PostRequestAsync(data, "/v1/product/import/stocks");
                var res = JsonConvert.DeserializeObject<List<UpdateResult>>(s);
                if (res.First().updated) {
                    Log.Add(_l + bus.name + " остаток обновлен! (" + bus.amount + ")");
                } else {
                    Log.Add(_l + bus.name + " ошибка! остаток не обновлен! (" + bus.amount + ")" + " >>> " + s);
                }
            } catch (Exception x) {
                Log.Add(_l + " ошибка обновления остатка - " + x.Message);
            }
        }
        //расчет цен с учетом наценки
        private int GetNewPrice(RootObject b) {
            var weight = b.GetWeight();
            var d = b.GetDimentions();
            var length = d[0] + d[1] + d[2];
            //наценка 25% на всё
            int overPrice = (int) (b.price * 0.25);
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
            return b.price + overPrice;
        }
        //цена до скидки (старая)
        private int GetOldPrice(int newPrice) {
            return (int) (Math.Ceiling((newPrice * (1 + _oldPriceProcent / 100)) / 100) * 100);
        }
        //Проверка и обновление цены товара на озон
        async Task UpdateProductPriceAsync(RootObject bus, ProductInfo productInfo) {
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
                        Log.Add(_l + bus.name + " (" + bus.price + ") цены обновлены! ("
                                   + newPrice + ", " + oldPrice + ")");
                    } else {
                        Log.Add(_l + bus.name + " ошибка! цены не обновлены! (" + bus.price + ")" + " >>> " + s);
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

                await Task.Delay(1000);
                if (response.StatusCode == HttpStatusCode.OK) {
                    var js = await response.Content.ReadAsStringAsync();
                    RootResponse rr = JsonConvert.DeserializeObject<RootResponse>(js);
                    if (rr.has_next != null && rr.has_next)
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
        private async Task UpdateProduct(RootObject good, ProductInfo productInfo) {
            try {
                //проверяем группу товара
                var attributes = await GetAttributesAsync(good);
                if (attributes.typeId == 0)
                    return;
                //Запрашиваю атрибуты товара с озон
                var productFullInfo = await GetProductFullInfoAsync(productInfo);
                File.WriteAllText(@"..\ozon\product_" + productFullInfo.First().offer_id + ".json",
                    JsonConvert.SerializeObject(productFullInfo));
                //формирую объект запроса
                var data = new {
                    items = new[] {
                        new{
                            attributes = new List<Attribute>(),
                            name = good.name,
                            currency_code="RUB",
                            offer_id=good.id,
                            category_id=attributes.categoryId,
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
                    //пропускаю некоторые атрибуты
                    if (item.id != 4180 &&                                          //название
                    item.id != 9024 &&                                              //Артикул
                    item.id != 9048 &&                                              //Название модели (для объединения в одну карточку)
                    item.id != 4191 &&                                              //Аннотация
                    item.id != 22387 &&                                             //Группа товара
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
                var s = await PostRequestAsync(data, "/v2/product/import");
                var res = JsonConvert.DeserializeObject<ProductImportResult>(s);
                if (res.task_id != default(int)) {
                    Log.Add(_l + good.name + " - товар отправлен на озон!");
                } else {
                    Log.Add(_l + good.name + " ошибка отправки товара на озон!" + " ===> " + s);
                }
            } catch (Exception x) {
                Log.Add(_l + " ошибка обновления описаний - " + x.Message);
            }
        }
        //добавление новых товаров на ozon
        async Task AddProductsAsync() {
            var count = await _db.GetParamIntAsync("ozon.countToAdd");
            if (count == 0)
                return;
            if (_isProductListCheckNeeds)
                await CheckProductListAsync();
            //список карточек которые еще не добавлены на озон
            var goods = _bus.Where(w => w.amount > 0
                                     && w.price > 0
                                     && w.Part != null
                                     && w.images.Count > 0
                                     && w.height != null
                                     && w.length != null
                                     && w.width != null
                                     && w.IsNew()
                                     && !w.ozon.Contains("http")
                                     && !_productList.items.Any(_ => w.id == _.offer_id));
            Log.Add(_l + "карточек для добавления: " + goods.Count());
            SaveToFile(goods);
            var goods2 = _bus.Where(w => w.amount > 0
                                     && w.price > 0
                                     && w.images.Count > 0
                                     && w.IsNew()
                                     && !w.ozon.Contains("http")
                                     && !_productList.items.Any(_ => w.id == _.offer_id));
            SaveToFile(goods2, @"..\ozonGoodListForAdding_all.csv");
            int i = 0;
            foreach (var good in goods) {
                try {
                    //проверяем группу товара
                    var attributes = await GetAttributesAsync(good);
                    if (attributes.typeId == 0)
                        continue;
                    //формирую объект запроса
                    var data = new {
                        items = new[] {
                            new{
                                attributes = new List<Attribute>(),
                                name = good.name,
                                currency_code="RUB",
                                offer_id=good.id,
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
                    Log.Add(_l + x.Message + x.InnerException?.Message);
                }
                if (++i >= count)
                    break;
            }
        }
        //получить атрибуты и категорию товара на озон
        async Task<Attributes> GetAttributesAsync(RootObject bus) {
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
                } else if (n.Contains("генератор") &&                                   //Комплектующие генератора для авто
                    n.Contains("реле")) {
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
                } else if ((n.Contains("гофра") || n.Contains("труба гофрированная")) &&//выхлопная система
                    (n.Contains("универсальная") || n.Contains("площадка"))) {
                    a.categoryId = 33698293;//Выхлопная труба и составляющие
                    a.typeId = 98818;
                    a.typeName = "Гофра глушителя";
                } else if (n.Contains("хомут") && n.Contains("глушителя")) {
                    a.categoryId = 33698293;//Выхлопная труба и составляющие
                    a.typeId = 971043197;
                    a.typeName = "Хомут для глушителя";
                } else if (n.Contains("труба") &&
                    (n.Contains("глушителя") || n.Contains("приемная") || n.Contains("промежуточная"))) {
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
                    n.Contains("охлаждения")) {
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
                } else if (n.Contains("катушка") &&                                     //Катушки и провода зажигания
                    n.Contains("зажигания")) {
                    a.categoryId = 85835327;//Катушки и провода зажигания
                    a.typeId = 970744686;
                    a.typeName = "Катушка зажигания";
                } else if (n.Contains("замок") && n.Contains("зажиг")) {                //Замок зажигания для авто
                    a.categoryId = 85835327;//Катушки и провода зажигания
                    a.typeId = 970889769;
                    a.typeName = "Замок зажигания";
                } else if (n.Contains("группа") && n.Contains("контактная")) {          //группа контактная для авто
                    a.categoryId = 85835327;//Катушки и провода зажигания
                    a.typeId = 98812;
                    a.typeName = "Выключатель зажигания";
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
                    (n.Contains("управления дв") || n.Contains("комфорт"))) {
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
                    a.categoryId = 33698203;//Цилиндр сцепления и комплектующие
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
                } else if (n.Contains("вал ") &&                                        //Вал коробки передач для авто
                    (n.Contains("первичный") || n.Contains("вторичный"))) {
                    a.categoryId = 85817294;//КПП и составляющие
                    a.typeId = 971072319;
                    a.typeName = "Вал промежуточный";
                } else if (n.Contains("втулка") &&                                      //Сайлентблок, втулка подвески
                    n.Contains("сайлен")) {
                    a.categoryId = 85828600;//Рычаг, тяга подвески и составляющие
                    a.typeId = 970889765;
                    a.typeName = "Втулка сайлентблока";
                } else if (n.Contains("втулка ") &&                                     //Сайлентблок, втулка подвески
                    n.Contains("подвес")) {
                    a.categoryId = 85828600;//Рычаг, тяга подвески и составляющие
                    a.typeId = 970863598;
                    a.typeName = "Втулка подвески";
                } else if (n.Contains("втулка ") &&                                     //Сайлентблок, втулка подвески
                    n.Contains("стабилиз")) {
                    a.categoryId = 85828600;//Рычаг, тяга подвески и составляющие
                    a.typeId = 970840966;
                    a.typeName = "Втулка стабилизатора";
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
                } else if (n.StartsWith("замок") && n.Contains("двер")) {               //Замок двери
                    a.categoryId = 92145042;//Замки автомобильные
                    a.typeId = 970950655;
                    a.typeName = "Замок двери автомобиля";
                } else if (n.StartsWith("замок") && n.Contains("капот")) {              //Замок капота
                    a.categoryId = 92145042;//Замки автомобильные
                    a.typeId = 970892946;
                    a.typeName = "Замок капота";
                } else if (n.StartsWith("замок") && n.Contains("багаж")) {              //Замок багажника
                    a.categoryId = 92145042;//Замки автомобильные
                    a.typeId = 321057673;
                    a.typeName = "Замок для багажников";
                } else if (n.Contains("личин")) {                                       //Замок автомобильный
                    a.categoryId = 92145042;//Замки автомобильные
                    a.typeId = 971072745;
                    a.typeName = "Личинка замка";
                } else if (n.Contains("заслонка") &&                                    //Дроссельная заслонка
                    n.Contains("дроссел")) {
                    a.categoryId = 33698198;
                    a.typeId = 98826;
                    a.typeName = "Заслонка дроссельная";
                } else if (n.StartsWith("защита") &&                                    //Защита нижней части автомобиля
                    (n.Contains("двиг") || n.Contains("карт") || n.Contains("двс"))) {
                    a.categoryId = 33304846;
                    a.typeId = 970594170;
                    a.typeName = "Защита двигателя";
                } else if ((n.StartsWith("кнопка") || n.StartsWith("блок ")) &&         //Переключатель салона авто
                    n.Contains("стеклопод")) {
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
                } else if (n.Contains("набор") && n.Contains("инструмента")) {          //Набор для ремонта авто
                    a.categoryId = 27332791;
                    a.typeId = 971437067;
                    a.typeName = "Набор инструментов для автомобиля";
                } else if (n.Contains("наклад") && n.Contains("порога")) {              //Обшивка салона автомобиля
                    a.categoryId = 1000003027;
                    a.typeId = 971159265;
                    a.typeName = "Обшивка салона автомобиля";
                } else if (n.StartsWith("огнетушитель")) {                              //Огнетушитель автомобильный
                    a.categoryId = 28000060;
                    a.typeId = 95562;
                    a.typeName = "Огнетушитель автомобильный";
                } else
                    return a;
                a.additionalAttributes.AddAttribute(GetSideAttribute(n));
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
                a.additionalAttributes.AddAttribute(await GetTNVEDAttribute(bus, a));
                a.additionalAttributes.AddAttribute(GetCountInBoxAttribute(bus));
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
        //Атрибут Внутренний диаметр, см
        //Атрибут Гарантия
        //Атрибут Количество отверстий


        //Атрибут Количество в упаковке
        Attribute GetCountInBoxAttribute(RootObject good) {
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
        async Task<Attribute> GetTNVEDAttribute(RootObject good, Attributes attributes) {
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
            var file = @"..\tnved_" + categoryId + ".json";
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
        Attribute GetVolumeAttribute(RootObject good) {
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
        //Атрибут Место установки
        Attribute GetPlacementAttribute(RootObject good) {
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
        Attribute GetMotorTypeAttribute(RootObject good) {
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
        Attribute GetLengthAttribute(RootObject good) {
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
        Attribute GetHeightAttribute(RootObject good) {
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
        Attribute GetThicknessAttribute(RootObject good) {
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
        Attribute GetKeywordsAttribute(RootObject good) {
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
        Attribute GetMultiplicityAttribute(RootObject good) {
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
        Attribute GetPlaceAttribute(RootObject good) {//todo rename Side
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
        Attribute GetOEMAttribute(RootObject good) {
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
        Attribute GetManufactureCountryAttribute(RootObject good) {
            var value = good.GetManufactureCountry();
            if (value == null)
                return null;
            return new Attribute {
                complex_id = 0,
                id = 4389,
                values = new Value[] {
                    new Value{
                        value = value,
                        dictionary_value_id = _manufactureCoutry.Find(f=>f.value==value).id,
                    }
                }
            };
        }
        //Атрибут Материал
        Attribute GetMaterialAttribute(RootObject good) {
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
        Attribute GetExpirationDaysAttribute(RootObject good) {
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
        Attribute GetDangerClassAttribute(RootObject good) {
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
        Attribute GetTechTypeAttribute(RootObject good) {
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
        Attribute GetColorAttribute(RootObject good) {
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
        Attribute GetFabricBoxCountAttribute(RootObject good) {
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
        Attribute GetAlternativesAttribute(RootObject good) {
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
        Attribute GetComplectationAttribute(RootObject good) {
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
        Attribute GetSideAttribute(string n) => new Attribute {
            complex_id = 0,
            id = 22329,
            values = new Value[] {
                          new Value{
                              value = n.Contains("лев") ? "Слева"
                                                        : n.Contains("прав") ? "Справа"
                                                                             : "Универсальное"
                          }
                      }
        };
        //Атрибут Название модели (для объединения в одну карточку) (в нашем случае дублируем name карточки бизнес.ру)
        Attribute GetModelNameAttribute(RootObject good) => new Attribute {
            complex_id = 0,
            id = 9048,
            values = new Value[] {
                new Value{
                    //value = good.name
                    value = good.Part
                }
            }
        };
        //Атрибут Аннотация Описание товара
        Attribute GetDescriptionAttribute(RootObject good) => new Attribute {
            complex_id = 0,
            id = 4191,
            values = new Value[] {
                new Value{
                    value = good.DescriptionList().Aggregate((a,b)=>a+"<br>"+b)
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
        Attribute GetPackQuantityAttribute(RootObject bus) =>
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
        Attribute GetBrendAttribute(RootObject bus) {
            int id;
            string name;
            var m = bus.GetManufacture(ozon: true)?.ToLowerInvariant() ?? "";
            if (m == "vag") {
                id = 115840909;
                name = "VAG (VW/Audi/Skoda/Seat)";
            } else if (_brends.Any(a => a.value.ToLowerInvariant() == m)) {
                var attribute = _brends.Find(a => a.value.ToLowerInvariant() == m);
                id = attribute.id;
                name = attribute.value;
            } else {
                id = 0;
                name = "Нет бренда";
            }
            return new Attribute {
                complex_id = 0,
                id = 85,
                values = new Value[] {
                    new Value{
                        dictionary_value_id = id,
                        value = name
                    }
                }
            };
        }
        //Атрибут Партномер (артикул производителя) (в нашем случае артикул)
        Attribute GetPartAttribute(RootObject bus) =>
            new Attribute {
                complex_id = 0,
                id = 7236,
                values = new Value[] {
                    new Value{
                        value = bus.Part
                    }
                }
            };
        //Запрос списка атрибутов с озон (значения по умолчанию - для получения списка брендов
        public async Task<List<AttributeValue>> GetAttibuteValuesAsync(int attribute_id = 85, int category_id = 61852812) {
            var attributeValuesFile = @"..\ozon_attr_" + attribute_id + "_cat_" + category_id + ".json";
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
            var s = await PostRequestAsync(data, "/v2/category/tree");
            var res = JsonConvert.DeserializeObject<List<Category>>(s);
            File.WriteAllText(@"..\ozon_categories.json", s);
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
        void SaveToFile(IEnumerable<RootObject> goods, string fname = @"..\ozonGoodListForAdding.csv") {
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
                s.Append(good.amount);
                s.Append(splt);
                s.Append(good.price);
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
    /////////////////////////////////////////
    /// Вспомогательные классы
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
