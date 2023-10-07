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
        readonly int _updateFreq = 24;                //частота обновления списка (часов)
        List<RootObject> _bus;                        //ссылка на товары
        DB _db = DB._db;                              //база данных
        static bool _isProductListCheckNeeds = true;
        bool _hasNext = false;                        //для запросов
        List<AttributeValue> _brends;                 //список брендов озон
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
            await CheckProductListAsync();
            await UpdateProductsAsync();
            await AddProductsAsync();
            if (RootObject.ScanTime.Day < DateTime.Now.Day)
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
                //TODO добавить проверку описаний await UpdateProductDecription()
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
                    Log.Add(_l + bus.name + " ошибка! остаток не обновлен! (" + bus.amount + ")");
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
                        Log.Add(_l + bus.name + " ошибка! цены не обновлены! (" + bus.price + ")");
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
        //добавление новых товаров на ozon
        async Task AddProductsAsync() {
            var count = await _db.GetParamIntAsync("ozon.countToAdd");
            if (count == 0)
                return;
            if (_isProductListCheckNeeds)
                await CheckProductListAsync();
            if (_brends == null)
                _brends = await GetAttibuteValuesAsync();
            //список карточек которые еще не добавлены на озон
            var goods = _bus.Where(w => w.amount > 0
                                     && w.price > 0
                                     && w.part != null
                                     && w.images.Count > 0
                                     && w.height != null
                                     && w.length != null
                                     && w.width != null
                                     && w.IsNew()
                                     && !w.ozon.Contains("http")
                                     && !_productList.items.Any(_ => w.id == _.offer_id));
            Log.Add(_l + "карточек для добавления: " + goods.Count());
            SaveToFile(goods);
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
                                attributes = new List<object> {
                                    new {                        //Бренд
                                        complex_id = 0,
                                        id = 85,
                                        values = new[] {
                                            new {
                                                dictionary_value_id = attributes.brendId,
                                                value = attributes.brendName
                                            }
                                        }
                                    },
                                    new {                        //Партномер (артикул производителя)
                                        complex_id = 0,          //в нашем случае артикул
                                        id = 7236,
                                        values = new[] {
                                            new {
                                                value = good.part
                                            }
                                        }
                                    },
                                    new {                        //Тип товара
                                        complex_id = 0,
                                        id = 8229,
                                        values = new[] {
                                            new {
                                                dictionary_value_id = attributes.typeId,
                                                value = attributes.typeName
                                            }
                                        }
                                    },
                                    new {                        //Название модели (для объединения в одну карточку)
                                        complex_id = 0,          //в нашем случае дублируем name карточки бизнес.ру
                                        id = 9048,
                                        values = new[] {
                                            new {
                                                value = good.name
                                            }
                                        }
                                    },
                                   //new {                        //Аннотация Описание товара
                                    //    complex_id = 0,
                                    //    id = 4191,
                                    //    values = new[] {
                                    //        new {
                                    //            value = good.DescriptionList().Aggregate((a,b)=>a+"<br>"+b)
                                    //        }
                                    //    }
                                    //}
                                },
                                name = good.name,
                                currency_code="RUB",
                                offer_id=good.id,
                                category_id=attributes.categoryId,
                                price = GetNewPrice(good).ToString(),
                                old_price = GetOldPrice(GetNewPrice(good)).ToString(),
                                weight = (int)(good.GetWeight()*1000),         //Вес с упаковкой, г
                                weight_unit = "g",
                                depth = int.Parse(good.width)*10,                      //глубина, мм
                                height = int.Parse(good.height)*10,                    //высота, мм
                                width = int.Parse(good.length)*10,                    //высота, мм
                                dimension_unit = "mm",
                                primary_image = good.images.First().url,                //главное фото
                                images = good.images.Skip(1).Take(14).Select(_=>_.url).ToList(),
                                vat="0.0"

                            }
                        }
                    };
                    if (attributes.additionalAttributes != null && attributes.additionalAttributes.Count > 0)
                        data.items[0].attributes.AddRange(attributes.additionalAttributes);
                    //var testJson = JsonConvert.SerializeObject(data);
                    //File.WriteAllText(@"..\test.json", testJson);
                    var s = await PostRequestAsync(data, "/v2/product/import");
                    var res = JsonConvert.DeserializeObject<ProductImportResult>(s);
                    if (res.task_id != default(int)) {
                        Log.Add(_l + good.name + " - товар отправлен на озон!");
                    } else {
                        Log.Add(_l + good.name + " ошибка отправки товара на озон!");
                    }
                    var res2 = new ProductImportInfo();
                    await Task.Delay(5000);
                    var data2 = new {
                        task_id = res.task_id.ToString()
                    };
                    s = await PostRequestAsync(data2, "/v1/product/import/info");
                    res2 = JsonConvert.DeserializeObject<ProductImportInfo>(s);
                    Log.Add(_l + good.name + " status товара - " + res2.items.First().status);
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
            var n = bus.name.ToLowerInvariant();
            var a = new Attributes();
            if (n.StartsWith("генератор ")) {
                a.categoryId = 61852812;
                a.typeId = 970707037;
                a.typeName = "Генератор в сборе";
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.additionalAttributes = GetCountAttribute();
            } else if (n.StartsWith("стартер ")) {
                a.categoryId = 61852812;
                a.typeId = 98941;
                a.typeName = "Стартер в сборе";
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.additionalAttributes = GetCountAttribute();
            } else if ((n.Contains("гофра") || n.Contains("труба гофрированная")) &&    //выхлопная система
                (n.Contains("универсальная") || n.Contains("площадка"))) {
                a.categoryId = 33698291;
                a.typeId = 98818;
                a.typeName = "Гофра глушителя";
                GetBrend(ref a.brendId, ref a.brendName, bus);
            } else if (n.Contains("хомут") && n.Contains("глушителя")) {
                a.categoryId = 33698291;
                a.typeId = 971043197;
                a.typeName = "Хомут для глушителя";
                GetBrend(ref a.brendId, ref a.brendName, bus);
            } else if (n.Contains("труба") &&
                (n.Contains("глушителя") || n.Contains("приемная") || n.Contains("промежуточная"))) {
                a.categoryId = 33698291;
                a.typeId = 98954;
                a.typeName = "Труба глушителя";
                GetBrend(ref a.brendId, ref a.brendName, bus);
            } else if (n.StartsWith("резонатор ") ||
                n.StartsWith("пламегаситель ") ||
                n.Contains("стронгер")) {
                a.categoryId = 33698292;
                a.typeId = 98906;
                a.typeName = "Резонатор глушителя";
                GetBrend(ref a.brendId, ref a.brendName, bus);
            } else if (n.Contains("скобы приёмной трубы") ||
                n.Contains("глушител") &&
                (n.Contains("подвеск") || n.Contains("кронштейн") ||
                n.Contains("крепление") || n.Contains("держател"))) {
                a.categoryId = 33698296;
                a.typeId = 970984895;
                a.typeName = "Крепление глушителя";
                GetBrend(ref a.brendId, ref a.brendName, bus);
            } else if (n.Contains("комплект фланцев с трубой") ||
                n.Contains("глушител") &&
                (n.Contains("ремкомплект") || n.Contains("фланец"))) {
                a.categoryId = 33698291;
                a.typeId = 971100632;
                a.typeName = "Ремкомплект глушителя";
                GetBrend(ref a.brendId, ref a.brendName, bus);
            } else if (n.StartsWith("глушитель ") &&
                (bus.GroupName().Contains("ыхлопная") || bus.GroupName().Contains("лушител"))) {
                a.categoryId = 33698292;
                a.typeId = 971906701;
                a.typeName = "Глушитель";
                GetBrend(ref a.brendId, ref a.brendName, bus);
            } else if (n.StartsWith("суппорт ") &&                                      //тормозная система
                (bus.GroupName().Contains("тормоз") || n.Contains("тормоз"))) {
                a.categoryId = 85842995;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 970725296;
                a.typeName = "Суппорты тормозные";
                a.additionalAttributes = GetCountAttribute();
            } else if (n.Contains("цилиндр") && 
                n.Contains("главный") &&
                (bus.GroupName().Contains("тормоз") || n.Contains("тормоз"))) {
                a.categoryId = 85842992;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 98965;
                a.typeName = "Цилиндр тормозной главный";
                a.additionalAttributes = GetCountAttribute();
            } else if (n.Contains("цилиндр") &&
                (bus.GroupName().Contains("тормоз") || n.Contains("тормоз"))) {
                a.categoryId = 85842992;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 98966;
                a.typeName = "Цилиндр тормозной рабочий";
                a.additionalAttributes = GetCountAttribute();
            } else if (n.Contains("барабан") &&
                n.Contains("тормоз")) {
                a.categoryId = 33698167;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 98799;
                a.typeName = "Барабан тормозной";
                a.additionalAttributes = GetCountAttribute();
            } else if (n.Contains("диск") &&
                n.Contains("тормоз")) {
                a.categoryId = 33698167;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 98825;
                a.typeName = "Диск тормозной";
                a.additionalAttributes = GetCountAttribute();
            } else if (n.Contains("колодки") &&
                n.Contains("тормоз")) {
                a.categoryId = 33698187;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 96167;
                a.typeName = "Колодки тормозные";
                a.additionalAttributes = GetCountAttribute();
            } else if (n.Contains("ручка") &&                                           //ручки
                (n.Contains("двери") || n.Contains("наруж") || n.Contains("внутр"))) {
                a.categoryId = 99606705;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 970939934;
                a.typeName = "Ручка дверная автомобильная";
            } else if (n.Contains("ручка") &&
                n.Contains("стеклоподъемника")) {
                a.categoryId = 1000002306;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 970945542;
                a.typeName = "Ручка стеклоподъемника";
                a.additionalAttributes = GetCountAttribute();
            } else if (n.Contains("радиатор") &&                                        //охлаждение
                n.Contains("охлаждения")) {
                a.categoryId = 39655599;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 970782911;
                a.typeName = "Радиатор охлаждения";
                a.additionalAttributes = GetCountAttribute();
            } else if (n.Contains("радиатор") &&
                (n.Contains("отопителя") || n.Contains("печки"))) {
                a.categoryId = 86296320;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 970781727;
                a.typeName = "Радиатор отопителя салона";
                a.additionalAttributes = GetCountAttribute();
            } else if (n.Contains("радиатор") &&
                n.Contains("кондиционера")) {
                a.categoryId = 85833530;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 970781671;
                a.typeName = "Радиатор кондиционера";
                a.additionalAttributes = GetCountAttribute();
            } else if (n.Contains("мотор") &&
                (n.Contains("печки") || n.Contains("отопителя"))) {
                a.categoryId = 78305548;
                a.typeId = 970782175;
                a.typeName = "Электровентилятор отопления";
                a.additionalAttributes = GetCountAttribute();
            } else if (n.Contains("катушка") &&                                         //зажигание
                n.Contains("зажигания")) {
                a.categoryId = 85835327;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 970744686;
                a.typeName = "Катушка зажигания";
                a.additionalAttributes = GetCountAttribute();
            } else if ((n.StartsWith("фара") || n.StartsWith("фары")) &&                 //световые приборы
                n.Contains("птф")|| n.Contains("противотуман")) {
                a.categoryId = 85797378;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 367249975;
                a.typeName = "Фары противотуманные (ПТФ)";
                a.additionalAttributes = GetCountAttribute();
            } else if (n.StartsWith("фара") ||                                         
                n.StartsWith("фары")) {
                a.categoryId = 33697184;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 970687095;
                a.typeName = "Фара автомобильная";
                a.additionalAttributes = GetCountAttribute();
            } else if (n.StartsWith("фонарь") || 
                n.StartsWith("фонари")) {
                a.categoryId = 85797441;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 970687094;
                a.typeName = "Задний фонарь автомобильный";
                a.additionalAttributes = GetCountAttribute();
            } else if (n.StartsWith("насос гур") ||                                    //рулевое управление
                n.StartsWith("гидроусилитель")||
                n.StartsWith("насос гидроусилителя")) {
                a.categoryId = 85832653;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 98858;
                a.typeName = "Насос ГУР";
                a.additionalAttributes = GetCountAttribute();
            } else if (n.StartsWith("насос топливный") ||                             //топливная система
                n.StartsWith("топливный насос")) {
                a.categoryId = 85843113;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 98860;
                a.typeName = "Насос топливный";
                a.additionalAttributes = GetCountAttribute();
            } else if (n.StartsWith("зеркало") &&                                    //зеркала
                (n.Contains("прав")|| n.Contains("лев"))) {
                a.categoryId = 99426212;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 970695250;
                a.typeName = "Зеркало боковое";
                a.additionalAttributes = GetCountAttribute();
                a.additionalAttributes.Add(GetSideAttribute(n));
            } else if (n.StartsWith("зеркало")) {
                a.categoryId = 28305306;
                GetBrend(ref a.brendId, ref a.brendName, bus);
                a.typeId = 93362;
                a.typeName = "Зеркало заднего вида";
                a.additionalAttributes = GetCountAttribute();
                a.additionalAttributes.Add(GetSideAttribute(n));
            }


            return a;

                //var t = await GetAttibuteValuesAsync(attribute_id: 8229, category_id: a.categoryId);
                //Log.Add(t.Select(s => "\nid: " + s.id + " " + s.value).Aggregate((x, y) => x + y));
                //await Task.Delay(3000);

        }
        //Сторона установки (параметр)
        object GetSideAttribute(string n) => new {
                      complex_id = 0,
                      id = 22329,
                      values = new[] {
                          new {
                              value = n.Contains("лев") ? "Слева"
                                                        : n.Contains("прав") ? "Справа"
                                                                             : "Универсальное"
                          }
                      }
            };

        //Количество штук (обязательный параметр)
        List<object> GetCountAttribute(int cnt = 1) =>
            new List<object> {
                 new {
                      complex_id = 0,
                      id = 7202,
                      values = new[] {
                          new {
                              value = cnt.ToString()
                          }
                      }
                 },
            };
        //бренд
        void GetBrend(ref int id, ref string name, RootObject bus) {
            var m = bus.GetManufacture().ToLowerInvariant() ?? "";
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
        }
        //сохранение в файл позиций, которые нужно добавить на озон
        void SaveToFile(IEnumerable<RootObject> goods) {
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
                s.Append(good.part);
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
            File.WriteAllText(@"..\ozonGoodListForAdding.csv", s.ToString(), Encoding.UTF8);
            Log.Add("товары выгружены в ozonGoodListForAdding.csv");
        }
        //получаем список атрибутов с озон (значения по умолчанию - для получения списка брендов
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
        //запрос категорий озон
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

    }
    public class Attributes {
        public int categoryId;
        public int brendId;
        public string brendName;
        public int typeId;
        public string typeName;
        public List<object> additionalAttributes;
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
    public class Attribute {
        public int complex_id { get; set; }
        public int id { get; set; }
        public Value[] values { get; set; }
    }
    public class Value {
        public int dictionary_value_id { get; set; }
        public string value { get; set; }
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
