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
using DocumentFormat.OpenXml.Drawing;
using JetBrains.Annotations;
using OfficeOpenXml;

namespace Selen.Sites {
    public class Wildberries {
        const string L = "wb: ";
        readonly HttpClient _httpClient = new HttpClient();
        List<GoodObject> _bus;
        int _nameLimit = 200;                                                               //огрничение длины заголовков
        readonly string _url;                                                               //номер поля в карточке
        readonly int _updateFreq;                                                           //частота обновления списка (часов)
        int _maxDiscont;                                                                    //максимальная скидка %
        const string BASE_BUS_URL = "https://www.wildberries.ru/catalog/00000/detail.aspx"; //временная ссылка для бизнес.ру
        const string CARDS_LIST_FILE = @"..\data\wildberries\wb_productList.json";          //товары
        List<WbCard> _cardsList = new List<WbCard>();
        const string CARDS_PRICES_FILE = @"..\data\wildberries\wb_priceList.json";          //цены
        List<WbPrice> _cardsPrices = new List<WbPrice>();
        const string CARDS_STOCKS_FILE = @"..\data\wildberries\wb_stocksList.json";         //остатки
        List<WbStock> _cardsStocks = new List<WbStock>();
        const string COLORS_FILE = @"..\data\wildberries\wb_colors.json";                   //цвета
        List<WbColor> _colors;
        const string CATEGORIES_FILE = @"..\data\wildberries\wb_categories.json";           //категории товаров
        List<WbCategory> _categories;
        const string SUBJECTS_CHARCS_FILE = @"..\data\wildberries\wb_subjects_charcs.json"; //характеристики подкатегорий
        List<WbCharc> _subjectsCharcs;

        List<WbWareHouse> _wareHouses;                                                      //склады

        static bool _isCardsListCheckNeeds = true;
        static bool _isCardsPricesCheckNeeds = true;
        static bool _isCardsStocksCheckNeeds = true;

        List<WbObject> _objects;            //подкатегории товаров
        JArray _objectsJA;                  //подкатегории товаров в формате JsonArray
        List<WbCountry> _countries;         //страны
        List<WbTnvd> _tnvd;                 //коды ТНВЭД

        const string RESERVE_LIST_FILE = @"..\data\wildberries\wb_reserveList.json";
        const string STICKER_LIST_FILE = @"..\data\wildberries\wb_stickerList.json";
        const string WARE_HOUSE_LIST_FILE = @"..\data\wildberries\wb_warehouseList.json";  //склады
        const string API_KEY_FILE = @"..\data\wildberries\api.key";  //ключ API

        List<GoodObject> _busToUpdate;        //список товаров для обновления
        const string BUS_TO_UPDATE_FILE = @"..\data\wildberries\toupdate.json";

        static object _locker = new object();
        static bool _flag = false;

        const string RULES_FILE = @"..\data\wildberries\wb_categories.xml";
        XDocument _catsXml;
        IEnumerable<XElement> _rules;

        //списки исключений
        const string EXCEPTION_GOODS_FILE = @"..\data\wildberries\exceptionGoodsList.json";
        const string EXCEPTION_GROUPS_FILE = @"..\data\wildberries\exceptionGroupsList.json";
        const string EXCEPTION_BRANDS_FILE = @"..\data\wildberries\exceptionBrandsList.json";
        List<string> _exceptionGoods;
        Dictionary<string, string> _exceptionBrands;
        List<string> _exceptionGroups;

        //наценки
        int _addPriceLevel1;
        int _addPriceLevel2;
        int _addPriceLevel3;
        int _basePriceProcent;
        int _kgtPriceProcent;
        int _minOverPrice;

        //статус
        public bool IsSyncActive { get; set; }

        public Wildberries() {
            var apiKey = File.ReadAllText(API_KEY_FILE);
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            _url = DB.GetParamStr("wb.url");
            _updateFreq = DB.GetParamInt("wb.updateFreq");
            IsSyncActive = false;
        }
        //главный метод синхронизации
        public async Task SyncAsync() {
            try {
                if (!await DB.GetParamBoolAsync("wb.syncEnable")) {
                    Log.Add($"{L}Sync: синхронизация отключена!");
                    return;
                }
                IsSyncActive = true;
                _bus = Class365API._bus;
                _catsXml = XDocument.Load(RULES_FILE);
                _rules = _catsXml.Descendants("Rule");
                _exceptionGroups = JsonConvert.DeserializeObject<List<string>>(
                    File.ReadAllText(EXCEPTION_GROUPS_FILE));
                _exceptionGoods = JsonConvert.DeserializeObject<List<string>>(
                    File.ReadAllText(EXCEPTION_GOODS_FILE));
                _exceptionBrands = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                    File.ReadAllText(EXCEPTION_BRANDS_FILE));
                if (_categories == null)
                    await GetCategoriesAsync();
                if (_objects == null)
                    await GetObjectsAsync(new[] { "8891", "760", "6240", "8555", "1162", "479", "6237", "3", "2050", "6119" });
                if (_objectsJA == null) {
                    var str = JsonConvert.SerializeObject(_objects);
                    _objectsJA = JArray.Parse(str);
                }
                if (_colors == null)
                    await GetColorsAsync();
                if (_countries == null)
                    await GetCountriesAsync();
                await GetSubjectsCharcs();

                await GetWarehouseList();

                _addPriceLevel1 = await DB.GetParamIntAsync("wb.addPriceLevel1");
                _addPriceLevel2 = await DB.GetParamIntAsync("wb.addPriceLevel2");
                _addPriceLevel3 = await DB.GetParamIntAsync("wb.addPriceLevel3");
                _basePriceProcent = await DB.GetParamIntAsync("wb.basePriceProcent");
                _kgtPriceProcent = await DB.GetParamIntAsync("wb.kgtPriceProcent");
                _minOverPrice = await DB.GetParamIntAsync("wb.minOverPrice");

                await GetCardsListAsync();
                await GetPricesAsync();
                await GetStocksAsync();
                await DeactivateMaslaProductsAsync();
                await UpdateAll();
                await UpdateRandom();
                await Add();
                CheckSizes();
            } catch (Exception ex) {
                if (DB.GetParamBool("alertSound"))
                    new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
                Log.Add($"{L}Sync: ошибка - " + ex.Message);
                new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
            }
            IsSyncActive = false;
        }
        public async Task MakeReserve() {
            try {
                if (!await DB.GetParamBoolAsync("wb.syncEnable")) {
                    Log.Add($"{L}MakeReserve: синхронизация отключена!");
                    return;
                }
                IsSyncActive = true;
                var data = new Dictionary<string, string>();
                data["limit"] = "1000";
                data["next"] = "0";
                var unixTimeStamp = (int) DateTime.UtcNow.AddDays(-4).Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                data["dateFrom"] = unixTimeStamp.ToString();
                var res = await GetRequestAsync(data, "https://marketplace-api.wildberries.ru/api/v3/orders");            //https://suppliers-api.wildberries.ru/api/v3/orders/new
                var wb = JsonConvert.DeserializeObject<WbOrders>(res);
                Log.Add($"{L}MakeReserve: получено заказов: {wb.orders.Count}");

                //загружаем список заказов, для которых уже делали резервирование
                var reserveList = new List<string>();
                if (File.Exists(RESERVE_LIST_FILE)) {
                    var s = File.ReadAllText(RESERVE_LIST_FILE);
                    var l = JsonConvert.DeserializeObject<List<string>>(s);
                    reserveList.AddRange(l);
                }
                //загружаем список заказов, для которых готовы стикеры
                var stickerList = new List<string>();
                if (File.Exists(STICKER_LIST_FILE)) {
                    var s = File.ReadAllText(STICKER_LIST_FILE);
                    var l = JsonConvert.DeserializeObject<List<string>>(s);
                    stickerList.AddRange(l);
                }
                //для каждого заказа сделать заказ с резервом в бизнес.ру
                foreach (var order in wb.orders) {
                    //если заказа нет в списке резервов
                    if (!reserveList.Contains(order.id)) {
                        //готовим список товаров (id, amount)
                        var goodsDict = new Dictionary<string, int>();
                        var amount = Class365API._bus.Find(g => g.id == order.article).GetQuantOfSell();
                        goodsDict.Add(order.article, amount);
                        if (await Class365API.MakeReserve(
                            Class365API.Source("Wildberries"),
                            $"WB order {order.id}",
                            goodsDict,
                            order.createdAt.ToString())) {
                            //если резерв создан - добавляем в список и сохраняем
                            reserveList.Add(order.id);
                            if (reserveList.Count > 1000) {
                                reserveList.RemoveAt(0);
                            }
                            var s = JsonConvert.SerializeObject(reserveList);
                            File.WriteAllText(RESERVE_LIST_FILE, s);
                        }
                    }
                    //если стикера нет в списке
                    if (!stickerList.Contains(order.id)) {
                        //делаем запрос стикера на вб
                        var sticker = await GetOrderSticker(order.id.ToString());
                        if (sticker == null)
                            continue;
                        if (await Class365API.AddStickerCodeToReserve(
                            Class365API.Source("Wildberries"),
                            $"WB order {order.id}",
                            $", code {sticker}")) {
                            //если стикер найден - добавляем в список и сохраняем
                            stickerList.Add(order.id);
                            if (stickerList.Count > 1000) {
                                stickerList.RemoveAt(0);
                            }
                            var s = JsonConvert.SerializeObject(stickerList);
                            File.WriteAllText(STICKER_LIST_FILE, s);
                        }
                    }
                }
            } catch (Exception x) {
                if (DB.GetParamBool("alertSound"))
                    new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
                Log.Add($"{L}MakeReserve: ошибка - " + x.Message);
            }
            IsSyncActive = false;
        }
        //GET запросы к api wb
        public async Task<string> GetRequestAsync(Dictionary<string, string> request, string apiRelativeUrl) {
            try {
                if (request != null) {
                    var qstr = QueryStringBuilder.BuildQueryString(request);
                    apiRelativeUrl += "?" + qstr;
                }
                HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod("GET"), apiRelativeUrl);

                HttpResponseMessage response = null;
                for (int i = 0; i < 5; i++) {
                    response = await _httpClient.SendAsync(requestMessage);
                    var res = await response.Content.ReadAsStringAsync();
                    await Task.Delay(1500);

                    if (response.StatusCode == HttpStatusCode.OK) {
                        if (res.Contains("\"data\"")) {
                            var responseObject = JsonConvert.DeserializeObject<WbResponse>(res);
                            if (responseObject.error)
                                Log.Add($"{L}GetRequest: ошибка - {responseObject.errorText}; {responseObject.additionalErrors}");
                            else
                                return JsonConvert.SerializeObject(responseObject.data);
                        } else
                            return res;
                    }
                }
                throw new Exception($"{response?.StatusCode} {response?.ReasonPhrase} {response?.Content}");
            } catch (Exception x) {
                Log.Add($"{L}GetRequest: ошибка - apiRelativeUrl:{apiRelativeUrl}, request:{request}, message:{x.Message}");
                throw x;
            }
            //return null;
        }
        //POST запросы к api wb
        [ItemCanBeNull]
        public async Task<string> PostRequestAsync(object request, string apiRelativeUrl, bool put = false) {
            try {
                if (request == null)
                    return string.Empty;
                var method = put ? HttpMethod.Put : HttpMethod.Post;
                HttpRequestMessage requestMessage = new HttpRequestMessage(method, apiRelativeUrl);
                var httpContent = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                requestMessage.Content = httpContent;
                var response = await _httpClient.SendAsync(requestMessage);
                var respContent = await response.Content.ReadAsStringAsync();
                await Task.Delay(1000);
                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent) {
                    if (respContent.Contains("\"error\":true"))
                        return string.Empty;
                    return respContent;
                } else
                    throw new Exception($"{response.StatusCode} {response.ReasonPhrase} {response.Content} // {respContent}");
            } catch (Exception x) {
                Log.Add($"{L} PostRequestAsync ошибка запроса! apiRelativeUrl:{apiRelativeUrl} request:{request} message:{x.Message}");
            }
            return string.Empty;
        }

        //todo: новый метод для запросов, пока не используется. на замену PostRequestAsync и GetRequestAsync
        //public async Task<string> RequestAsync(string apiRelativeUrl, Dictionary<string, string> query = null, object body = null, string method = "GET") {
        //    try {
        //        if (query != null) {
        //            var qstr = QueryStringBuilder.BuildQueryString(query);
        //            apiRelativeUrl += "?" + qstr;
        //        }
        //        HttpRequestMessage requestMessage = new HttpRequestMessage(new HttpMethod(method), apiRelativeUrl);
        //        if (body != null) {
        //            var httpContent = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
        //            requestMessage.Content = httpContent;
        //        }
        //        var response = await _hc.SendAsync(requestMessage);
        //        await Task.Delay(2000);
        //        if (response.StatusCode == HttpStatusCode.OK) {
        //            var json = await response.Content.ReadAsStringAsync();
        //            return json;
        //        } else
        //            throw new Exception(response.StatusCode + " " + response.ReasonPhrase + " " + await response.Content.ReadAsStringAsync());
        //    } catch (Exception x) {
        //        Log.Add(" ошибка запроса! - " + x.Message);
        //        throw;
        //    }
        //}

        //Родительские категории товаров
        public async Task GetCategoriesAsync() {
            if (File.GetLastWriteTime(CATEGORIES_FILE) < DateTime.Now.AddDays(-_updateFreq)) {
                var data = new Dictionary<string, string>();
                data["locale"] = "ru";
                var s = await GetRequestAsync(data, "https://content-api.wildberries.ru/content/v2/object/parent/all");
                _categories = JsonConvert.DeserializeObject<List<WbCategory>>(s);
                File.WriteAllText(CATEGORIES_FILE, s);
                Log.Add($"{L} GetCategoriesAsync: получено категорий {_categories.Count}");
            } else {
                var s = File.ReadAllText(CATEGORIES_FILE);
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
                        var s = await GetRequestAsync(data, "https://content-api.wildberries.ru/content/v2/object/all");
                        var resultList = JsonConvert.DeserializeObject<List<WbObject>>(s);
                        Log.Add($"{L} GetObjectsAsync: {name ?? id} - получено подкатегорий: {resultList.Count}");
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
                    Log.Add($"{L} GetObjectsAsync: ошибка загрузки категорий - {x.Message}");
                }
            }
            Log.Add($"{L} GetObjectsAsync: загружено подкатегорий: {_objects.Count}");
        }
        //характеристики подкатегорий
        public async Task GetSubjectsCharcs() {
            if (_subjectsCharcs != null)
                return;
            if (File.GetLastWriteTime(SUBJECTS_CHARCS_FILE) < DateTime.Now.AddDays(-_updateFreq)) {
                _subjectsCharcs = new List<WbCharc>();
                Log.Add($"{L} GetSubjectsCharcs: файл характеристик подкатегорий устарел, они будут запрошены заново");
            } else {
                try {
                    var s = File.ReadAllText(SUBJECTS_CHARCS_FILE);
                    _subjectsCharcs = JsonConvert.DeserializeObject<List<WbCharc>>(s);
                    Log.Add($"{L} GetSubjectsCharcs: загружены характеристики подкатегорий: {_subjectsCharcs.Count}");
                } catch (Exception x) {
                    Log.Add($"{L} GetSubjectsCharcs: ошибка загрузки характеристик категорйи - {x.Message}");
                }
            }
        }
        //Характеристики предмета (подкатегории)
        public async Task<List<WbCharc>> GetCharcsAsync(int subjectId) {
            //если наш список характеристик уже содержит характеристики для требуемого id - используем
            try {
                if (_subjectsCharcs.Any(a => a.subjectID == subjectId)) {
                    return _subjectsCharcs.Where(w => w.subjectID == subjectId).ToList();
                }
                //иначе запрашиваем характеристики с вб
                var data = new Dictionary<string, string>();
                data["locale"] = "ru";
                data["subjectId"] = subjectId.ToString();
                var s = await GetRequestAsync(data, $"https://content-api.wildberries.ru/content/v2/object/charcs/{subjectId}");
                var resultList = JsonConvert.DeserializeObject<List<WbCharc>>(s);
                if (resultList.Count > 0) {
                    Log.Add($"{L}GetCharcs: [{subjectId}] {resultList[0].subjectName} - получено характеристик: {resultList.Count}");
                    _subjectsCharcs.AddRange(resultList);
                    File.WriteAllText(SUBJECTS_CHARCS_FILE, JsonConvert.SerializeObject(_subjectsCharcs));
                    Log.Add($"{L}GetCharcs: список характеристик сохранен: {_subjectsCharcs.Count}");
                    return resultList;
                } else {
                    Log.Add($"{L}GetCharcs: предупреждение - характеристики {subjectId} не получены!");
                }
            } catch (Exception x) {
                Log.Add($"{L}GetCharcs: ошибка - {x.Message}");
            }
            return null;
        }
        //Цвет
        public async Task GetColorsAsync() {
            if (File.GetLastWriteTime(COLORS_FILE) < DateTime.Now.AddDays(-_updateFreq)) {
                var data = new Dictionary<string, string>();
                data["locale"] = "ru";
                var s = await GetRequestAsync(data, "https://content-api.wildberries.ru/content/v2/directory/colors");
                _colors = JsonConvert.DeserializeObject<List<WbColor>>(s);
                File.WriteAllText(COLORS_FILE, s);
                Log.Add($"{L} GetCategoriesAsync: получено цветов: {_colors.Count}");
            } else {
                var s = File.ReadAllText(COLORS_FILE);
                try {
                    _colors = JsonConvert.DeserializeObject<List<WbColor>>(s);
                } catch (Exception x) {
                    Log.Add($"{L} GetColorsAsync: ошибка - {x.Message}");
                }
            }
        }
        //Страна Производства
        public async Task GetCountriesAsync() {
            var fileName = @"..\data\wildberries\wb_countries.json";
            if (File.GetLastWriteTime(fileName) < DateTime.Now.AddDays(-_updateFreq)) {
                var data = new Dictionary<string, string>();
                data["locale"] = "ru";
                var s = await GetRequestAsync(data, "https://content-api.wildberries.ru/content/v2/directory/countries");
                _countries = JsonConvert.DeserializeObject<List<WbCountry>>(s);
                File.WriteAllText(fileName, s);
                Log.Add($"{L} GetCountriesAsync: получено стран: {_countries.Count}");
            } else {
                var s = File.ReadAllText(fileName);
                try {
                    _countries = JsonConvert.DeserializeObject<List<WbCountry>>(s);
                } catch (Exception x) {
                    Log.Add($"{L} GetCountriesAsync: ошибка - {x.Message}");
                }
            }
        }
        //ТНВЭД код
        public async Task GetTnvdAsync(GoodObject g, List<WbCharc> a) {
            try {
                //тнвэд в картоке товара
                var goodTNVED = g.hscode_id;
                //id подгруппы
                var subjectID = a[0].subjectID;
                //характеристики подгруппы
                var objectCharcs = await GetCharcsAsync(subjectID);
                //если среди характеристик подгруппы вб нет тнвэд, то пропуск
                if (!objectCharcs.Any(c => c.charcID == 15000001)) // как вариант "name": "ТНВЭД"
                    return;

                var fileName = $@"..\data\wildberries\wb_tnvd_{subjectID}.json";
                if (File.GetLastWriteTime(fileName) < DateTime.Now.AddDays(-_updateFreq * 3)) {
                    var data = new Dictionary<string, string>();
                    data["locale"] = "ru";
                    data["subjectID"] = subjectID.ToString();
                    var s = await GetRequestAsync(data, "https://content-api.wildberries.ru/content/v2/directory/tnved");
                    _tnvd = JsonConvert.DeserializeObject<List<WbTnvd>>(s);
                    File.WriteAllText(fileName, s);
                    Log.Add($"{L}GetTnvdAsync: {subjectID} - получено кодов ТНВЭД: {_tnvd.Count}");
                } else {
                    var s = File.ReadAllText(fileName);
                    _tnvd = JsonConvert.DeserializeObject<List<WbTnvd>>(s);
                }
                //если не содержится в списке - выдаем предупреждение
                if (!_tnvd.Any(t => t.tnved == goodTNVED))
                    Log.Add($"{L}GetTnvdAsync: {goodTNVED} - не найден в списке кодов ТНВЭД на WB: " +
                        $"{_tnvd.Select(s => s.tnved).Aggregate((a1, a2) => a1 + "; " + a2).ToString()}");
                //добавляем характеристику
                if (goodTNVED != null)
                    a.Add(new WbCharc {
                        charcID = 15000001,
                        value = goodTNVED,
                        charcType = "1"
                    });
            } catch (Exception x) {
                Log.Add($"{L}GetTnvdAsync: ошибка - {x.Message}");
            }
        }
        //добавление новых товаров на wb
        async Task Add() {
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
                                     && w.WB.Length == 0
                                     //&& !_productList.Any(_ => w.id == _.offer_id)
                                     && !_exceptionGoods.Any(e => w.name.ToLowerInvariant().Contains(e))
                                     && !_exceptionGroups.Any(e => w.GroupName.ToLowerInvariant().Contains(e))
                                     && w.GroupName.ToLowerInvariant() != "масла")
                            .OrderByDescending(o => o.Price).ToList();  //сначала подороже
            SaveToFile(goods);
            Log.Add($"{L}Add: карточек для добавления: {goods.Count}");
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
                                    title = good.NameLimit(_nameLimit, "WB_TITLE"),
                                    description = GetDescription(good),
                                    vendorCode = good.id,
                                    brand = GetBrand(good, attributes),
                                    dimensions = new {
                                          length = good.SizeSM("length",5),
                                          width = good.SizeSM("width",5),
                                          height = good.SizeSM("height",5),
                                          weightBrutto = good.Weight
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
                            data[0].variants[0].characteristics.Add(new { id = a.charcID, value = Math.Round((decimal) (float) a.value, 2) });
                    }
                    var res = await PostRequestAsync(data, "https://content-api.wildberries.ru/content/v2/cards/upload");
                    if (!string.IsNullOrEmpty(res)) {
                        Log.Add($"{L}Add: {good.name} - товар отправлен на wildberries!");
                        await SaveUrlAsync(good);
                        _isCardsListCheckNeeds = true;
                    } else {
                        Log.Add($"{L}Add: {good.name} - ошибка отправки товара на wildberries!");
                    }
                } catch (Exception x) {
                    Log.Add($"{L}Add: ошибка - {good.name} - {x.Message}; {x.InnerException?.Message}");
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
                Log.Add($"{L} GetTypeName: ошибка определения категории - {x.Message}");
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
                await GetTnvdAsync(bus, a);
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
                else if (value.Contains("ОАЭ"))
                    value = "Объединенные Арабские Эмираты";
                else if (value.Contains("Соединенные Штаты"))
                    value = "США";
                a.Add(new WbCharc {
                    charcID = 14177451,
                    value = value,
                    charcType = "1"
                });
            } catch (Exception x) {
                throw new Exception($"GetManufactureCountry: {x.Message}");
            }
        }
        //Атрибут Вес 
        async Task GetWeight(GoodObject good, List<WbCharc> a) {
            try {
                var charcs = await GetCharcsAsync(a[0].subjectID);
                //if (charcs.Any(_ => _.charcID == 88952))         //Вес товара с упаковкой (г)
                //    a.Add(new WbCharc {
                //        charcID = 88952,
                //        value = good.Weight*1000, //кг => г
                //        charcType = "4"
                //    });
                if (charcs.Any(_ => _.charcID == 89008))         //Вес товара без упаковки (г)
                    a.Add(new WbCharc {
                        charcID = 89008,
                        value = good.WeightNet * 1000, //кг => г
                        charcType = "4"
                    });
                //if (charcs.Any(_=>_.charcID == 88953))          //Вес с упаковкой (кг)
                //    a.Add(new WbCharc {
                //    charcID = 88953,
                //    value = good.Weight,
                //    charcType = "4"
                //});
            } catch (Exception x) {
                throw new Exception($"{L}GetWeight: ошибка - {x.Message}");
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
                string newUrl = BASE_BUS_URL;
                //проверяем ссылку, если она есть, то значит товар был отправлен на вб
                if (nmID > 0)
                    newUrl = BASE_BUS_URL.Replace("/00000/", "/" + nmID + "/");
                if (nmID == -1)
                    newUrl = "";
                if (bus.WB != newUrl) {
                    bus.WB = newUrl;
                } else
                    Log.Add(L + bus.name + " ссылка без изменений!");
            } catch (Exception x) {
                Log.Add($"{L} SaveUrlAsync ошибка! name:{bus.name} message:{x.Message}");
            }
        }

        //запрашиваем список товаров WB
        public async Task GetCardsListAsync() {
            try {
                //если файл свежий и обновление списка не требуется
                if (DateTime.Now < File.GetLastWriteTime(CARDS_LIST_FILE).AddHours(_updateFreq)
                     && !_isCardsListCheckNeeds) {
                    //загружаем с диска, если список пустой
                    if (_cardsList.Count == 0) {
                        _cardsList = JsonConvert.DeserializeObject<List<WbCard>>(
                            File.ReadAllText(CARDS_LIST_FILE));
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
                        var res = await PostRequestAsync(data, "https://content-api.wildberries.ru/content/v2/get/cards/list");
                        if (string.IsNullOrEmpty(res))
                            throw new Exception("запрос товаров вернул ошибку!");
                        var productList = JsonConvert.DeserializeObject<WbProductList>(res);
                        _cardsList.AddRange(productList.cards);
                        Log.Add($"{L} получено {_cardsList.Count} товаров");
                        //добавляем пагинацию в объект запроса
                        data.settings.cursor = new {
                            limit = limit,
                            updatedAt = productList.cursor.updatedAt,
                            nmID = productList.cursor.nmID
                        };
                        total = productList.cursor.total;
                    } while (total == limit);
                    Log.Add(L + "ProductList - успешно загружено " + _cardsList.Count + " товаров");
                    File.WriteAllText(CARDS_LIST_FILE, JsonConvert.SerializeObject(_cardsList));
                    Log.Add(L + CARDS_LIST_FILE + " - сохранено");
                    _isCardsListCheckNeeds = false; //сбрасываю флаг
                }
                await CheckCardsAsync();
            } catch (Exception x) {
                Log.Add(L + "ProductList - ошибка загрузки товаров - " + x.Message);
                _cardsList = JsonConvert.DeserializeObject<List<WbCard>>(
                    File.ReadAllText(CARDS_LIST_FILE));
            }
        }
        //список цен товаров
        public async Task GetPricesAsync() {
            try {
                _maxDiscont = await DB.GetParamIntAsync("wb.maxDiscount");
                //если файл свежий и обновление списка не требуется
                if (DateTime.Now < File.GetLastWriteTime(CARDS_PRICES_FILE).AddHours(1)
                    && !_isCardsPricesCheckNeeds) {
                    //загружаем с диска, если список пустой
                    if (_cardsPrices.Count == 0) {
                        _cardsPrices = JsonConvert.DeserializeObject<List<WbPrice>>(
                            File.ReadAllText(CARDS_PRICES_FILE));
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
                            Log.Add($"{L} получено {_cardsPrices.Count} цен товаров");
                            //добавляем пагинацию в объект запроса
                            if (list.listGoods.Count != limit || _cardsPrices.Count == _cardsList.Count)
                                break;
                            offset += limit;
                            data["offset"] = offset.ToString();
                        } catch (Exception x) {
                            limit /= 2;
                            data["limit"] = limit.ToString();
                            Log.Add($"{L} GetPricesAsync: ошибка запроса цен товаров {i++}, limit: {limit} - " + x.Message);
                            if (limit == 0)
                                break;
                            //throw new Exception("превышено количество попыток запроса цен!");
                            await Task.Delay(10000);
                        }
                    }
                    if (_cardsPrices.Count > 0 && _cardsList.Count - _cardsPrices.Count < 100)
                        File.WriteAllText(CARDS_PRICES_FILE, JsonConvert.SerializeObject(_cardsPrices));
                    else {
                        _cardsPrices = JsonConvert.DeserializeObject<List<WbPrice>>(
                            File.ReadAllText(CARDS_PRICES_FILE));
                        Log.Add($"{L} загружено с диска {_cardsPrices.Count} цен товаров");
                    }
                    Log.Add(L + CARDS_PRICES_FILE + " - сохранено");
                    _isCardsPricesCheckNeeds = false;
                }
            } catch (Exception x) {
                Log.Add($"{L} GetPricesAsync: ошибка загрузки цен товаров!! - " + x.Message);
                _cardsPrices = JsonConvert.DeserializeObject<List<WbPrice>>(
                    File.ReadAllText(CARDS_PRICES_FILE));
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
                if (wbPrice == goodNewPrice && discount <= _maxDiscont) {
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
                if (string.IsNullOrEmpty(res))
                    throw new Exception("ошибка запроса изменения цены");
                Log.Add($"{L} UpdatePrice: {good.name} - цены обновлены, {wbPrice} => {goodNewPrice}," +
                        $" дисконт {discount} => {newDiscount}");
                //обновить список карточек вб
            } catch (Exception x) {
                Log.Add($"{L} GetPricesAsync: {good.name} - ошибка обновления цен товара - {x.Message}");
            }
            _isCardsPricesCheckNeeds = true;
        }
        //расчет цен WB с учетом наценки
        private int GetNewPrice(GoodObject g) {
            var weight = g.Weight * g.GetQuantOfSell();
            var l = g.SizeSM("length", 5);
            var w = g.SizeSM("width", 5);
            var h = g.SizeSM("height", 5);
            var length = (l + w + h) * g.GetQuantOfSell();
            var volume = l * w * h / 1000; // объем в литрах
            var volumeOverprice = volume switch
            {
                <= 3 => 150,
                <= 190 => volume * 30,
                _ => 4500
            };
            int overPrice;
            //вес от 50 кг или размер более 200 -- наценка 40% + 4500 р
            if (weight >= 50 || length >= 200)
                overPrice = (int) (g.Price * 0.01 * _kgtPriceProcent) + Math.Max(_addPriceLevel3, volumeOverprice);
            //вес от 30 кг или размер от 150 -- наценка 40% + 3000 р
            else if (weight >= 30 || length >= 150)
                overPrice = (int) (g.Price * 0.01 * _kgtPriceProcent) + Math.Max(_addPriceLevel2, volumeOverprice);
            //вес от 10 кг или размер от 100 -- наценка 1500 р
            else if (weight >= 10 || length >= 100)
                overPrice = (int) (g.Price * 0.01 * _kgtPriceProcent) + Math.Max(_addPriceLevel1, volumeOverprice);
            //для маленьких и легких наценка 40% на всё
            else
                overPrice = (int) (g.Price * 0.01 * _basePriceProcent);
            //если наценка меньше 300 р - округляю
            if (overPrice < _minOverPrice)
                overPrice = _minOverPrice;
            return (g.Price * g.GetQuantOfSell() + overPrice).Round(50);
        }
        //проверяем привязку товаров в карточки бизнес.ру
        private async Task CheckCardsAsync(bool checkAll = false) {
            foreach (var card in _cardsList) {
                try {
                    //карточка в бизнес.ру с id = vendorCode товара на вб
                    var b = _bus.FindIndex(_ => _.id == card.vendorCode);
                    if (b == -1)
                        Log.Add($"{L}CheckCards: предупреждение - карточка бизнес.ру с id = {card.vendorCode} не найдена!");
                    else if (!_bus[b].WB.Contains("http") ||                  //если карточка найдена,но товар не привязан к бизнес.ру
                        _bus[b].WB.Contains("/00000/") ||                     //либо ссылка есть, но неверный sku
                        checkAll) {                                           //либо передали флаг проверять всё
                        await SaveUrlAsync(_bus[b], card.nmID);
                        //await UpdateCardAsync(_bus[b], card);
                    }
                } catch (Exception x) {
                    Log.Add($"{L} CheckGoodsAsync ошибка - checkAll:{checkAll} offer_id:{card.vendorCode} message:{x.Message}");
                    //_isCardsListCheckNeeds = true;
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
                if (DB.GetParamBool("alertSound"))
                    new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
                Log.Add($"{L} UpdateProductAsync ошибка - name:{good.name} message:{x.Message}");
                throw x;
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
                var res = await PostRequestAsync(data, "https://content-api.wildberries.ru/content/v3/media/save");
                await Task.Delay(1000);
                if (string.IsNullOrEmpty(res))
                    throw new Exception("запрос на обновление фото вернул ошибку!");
                Log.Add($"{L}UpdateMedia: {good.name} -  фотографии успешно обновлены! ({urls.Count})");
                card.photos = good.images.Select(i => new WbPhoto { big = i.url }).ToList();
            } catch (Exception x) {
                Log.Add($"{L}UpdateMedia: ошибка - name:{good.name} message:{x.Message}");
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
                    var fileName = $"wb_{Class365API.ORGANIZATION_ID}_{u}";
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
                    throw new Exception($"{L}SendImagesToFtp: ошибка! - {x.Message}");
                }
            }
            cl.Dispose();
            return urlList;
        }
        async Task UpdateAll() {
            try {
                await GetGoodListToUpdate();
                for (int b = _busToUpdate.Count - 1; b >= 0; b--) {
                    if (Class365API.IsTimeOver)
                        return;
                    try {
                        if (await CheckCardAsync(_busToUpdate[b])) {
                            _busToUpdate.Remove(_busToUpdate[b]);
                            var bu = JsonConvert.SerializeObject(_busToUpdate);
                            File.WriteAllText(BUS_TO_UPDATE_FILE, bu);
                            await Task.Delay(100);
                        }
                    } catch (Exception x) {
                        Log.Add(L + "UpdateProductsAsync - " + x.Message);
                    }
                }
            } catch (Exception x) {
                Log.Add($"{L}UpdateAll: ошибка обновления! {x.Message}");
            }
        }
        //готовлю список на обновление
        async Task GetGoodListToUpdate() => await Task.Factory.StartNew(() => {
            List<GoodObject> busToUpdate;
            if (File.Exists(BUS_TO_UPDATE_FILE)) {
                var f = File.ReadAllText(BUS_TO_UPDATE_FILE);
                busToUpdate = JsonConvert.DeserializeObject<List<GoodObject>>(f) ?? new List<GoodObject>();
            } else
                busToUpdate = new List<GoodObject>();
            //список обновленных карточек со ссылкой на объявления
            _busToUpdate = Class365API._bus
                                        .Where(_ => _.WB != null
                                                && _.WB.Contains("http")
                                                && !_.WB.Contains("/00000/")
                                                && _.IsTimeUpDated()
                                                || busToUpdate.Any(b => b.id == _.id)

                                                || _cardsPrices.Find(f => f.vendorCode == _.id)?.discount > _maxDiscont

                                                )
                                        .OrderBy(_ => _cardsPrices.Find(f => f.vendorCode == _.id)?.discount)
                                        .ToList();
            if (_busToUpdate.Count > 0) {
                var bu = JsonConvert.SerializeObject(_busToUpdate);
                File.WriteAllText(BUS_TO_UPDATE_FILE, bu);
                Log.Add($"{L}карточек в списке для обновления: {_busToUpdate.Count}");
            }
        });
        //выборочная проверка и обновление
        async Task UpdateRandom() {
            try {
                var checkProductCount = await DB.GetParamIntAsync("wb.checkProductCount");
                var checkProductIndex = await DB.GetParamIntAsync("wb.checkProductIndex");
                if (checkProductIndex >= _cardsList.Count)
                    checkProductIndex = 0;
                List<GoodObject> busToUpdate = await Task<List<GoodObject>>.Factory.StartNew(() => {
                    return Class365API._bus.Where(_ => _.WB != null &&
                                                                _.WB.Contains("http") &&
                                                               !_.WB.Contains("/00000/"))
                                                    .Skip(checkProductIndex)
                                                    .Take(checkProductCount)
                                                    .ToList();
                });
                for (int b = 0; b < busToUpdate.Count; b++) {
                    if (Class365API.IsTimeOver)
                        break;
                    try {
                        var card = _cardsList.Find(f => f.vendorCode == busToUpdate[b].id);
                        if (card == null) {
                            Log.Add($"предупреждение: {busToUpdate[b].name} - карточка не найдена на ВБ!");
                        } else {
                            await UpdateCardAsync(busToUpdate[b], card);
                            await Task.Delay(200);
                            checkProductIndex++;
                        }
                    } catch (Exception x) {
                        Log.Add($"{L}UpdateRandom: ошибка - " + x.Message);
                    }
                }
                await DB.SetParamAsync("wb.checkProductIndex", checkProductIndex.ToString());
            } catch (Exception x) {
                Log.Add($"{L}UpdateRandom: ошибка выборочной проверки - {x.Message}");
            }
        }

        //проверка всех карточек в бизнесе, которые изменились или имеют расхождения на вб
        //а также выборочная проверка карточек
        public async Task<bool> CheckCardAsync(GoodObject good) {
            try {
                //находим карточку на вб
                var card = _cardsList.Find(f => f.vendorCode == good.id);
                if (card == null) {
                    //await SaveUrlAsync(good, -1);
                    //_isCardsListCheckNeeds = true;
                    //_isCardsPricesCheckNeeds = true;
                    //_isCardsStocksCheckNeeds = true;
                    Log.Add($"предупреждение: {good.name} - карточка не найдена на ВБ!");
                } else {
                    //проверяем остаток на вб
                    var wbStock = _cardsStocks.Find(f => f.sku == card.sizes[0].skus[0])?.amount;
                    //проверяем цену на вб
                    var wbPrice = _cardsPrices.Find(f => f.vendorCode == good.id)?.sizes[0].price;
                    var wbDiscount = _cardsPrices.Find(f => f.vendorCode == good.id)?.discount;
                    var goodNewPrice = GetNewPrice(good);
                    var goodNewAmount = (int) good.Amount / good.GetQuantOfSell();
                    //если отличается остаток, цена, или карточка была обновлена в бизнес.ру
                    if (wbStock != goodNewAmount ||
                        wbPrice != goodNewPrice ||
                        wbDiscount > _maxDiscont ||
                        card.photos.Count != good.images.Count ||
                        good.IsTimeUpDated())
                        //обновляем карточку на вб
                        await UpdateCardAsync(good, card);
                }
                return true;
            } catch (Exception x) {
                Log.Add(L + "UpdateCardsAsync - " + x.Message);
                return false;
            }
        }
        //запрос остатков товара
        async Task GetStocksAsync() {
            try {
                //если файл свежий и обновление списка не требуется
                if (DateTime.Now < File.GetLastWriteTime(CARDS_STOCKS_FILE).AddHours(_updateFreq)
                    && !_isCardsStocksCheckNeeds) {
                    //загружаем с диска, если список пустой
                    if (_cardsStocks.Count == 0) {
                        _cardsStocks = JsonConvert.DeserializeObject<List<WbStock>>(
                            File.ReadAllText(CARDS_STOCKS_FILE));
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
                        var res = await PostRequestAsync(data, $"https://marketplace-api.wildberries.ru/api/v3/stocks/{_wareHouses[0].id}");
                        var res1 = await PostRequestAsync(data, $"https://marketplace-api.wildberries.ru/api/v3/stocks/{_wareHouses[1].id}");
                        if (string.IsNullOrEmpty(res))
                            throw new Exception("ошибка запроса остатков");
                        var list = JsonConvert.DeserializeObject<WbStocks>(res);
                        var list1 = JsonConvert.DeserializeObject<WbStocks>(res1);
                        var combinedStock = list.stocks
                            .Concat(list1.stocks)
                            .GroupBy(s => s.sku)
                            .Select(g => new WbStock
                            {
                                sku = g.Key,
                                amount = g.Sum(s => s.amount)
                            })
                            .ToList();
                        _cardsStocks.AddRange(combinedStock);
                        index += limit;
                    } while (index < _cardsList.Count - 1);
                    Log.Add($"{L}GetStocksAsync: получено {_cardsStocks.Count} остатков товаров");
                    File.WriteAllText(CARDS_STOCKS_FILE, JsonConvert.SerializeObject(_cardsStocks));
                    Log.Add(L + CARDS_STOCKS_FILE + " - сохранено");
                    _isCardsStocksCheckNeeds = false;
                }
            } catch (Exception x) {
                Log.Add($"{L}GetPricesAsync: - ошибка загрузки цен товаров - " + x.Message);
                _cardsStocks = JsonConvert.DeserializeObject<List<WbStock>>(
                    File.ReadAllText(CARDS_STOCKS_FILE));
            }
        }
        //обновление остатков товара
        private async Task UpdateStocks(GoodObject good, WbCard card) {
            try {
                if (card == null)
                    card = _cardsList.Find(f => f.vendorCode == good.id);
                if (card == null)
                    throw new Exception("карточка не найдена!");
                var amount = (int) good.Amount / good.GetQuantOfSell(); //делим количество шт на квант продажи
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
                                amount = amount
                            }
                        }
                };
                var res = await PostRequestAsync(data, $"https://marketplace-api.wildberries.ru/api/v3/stocks/{_wareHouses[0].id}", true);
                var res1 = await PostRequestAsync(data, $"https://marketplace-api.wildberries.ru/api/v3/stocks/{_wareHouses[1].id}", true);
                if (string.IsNullOrEmpty(res))
                    throw new Exception("ошибка запроса изменения цены");
                Log.Add($"{L}UpdateStocks: {good.name} - остаток обновлен {wbStock} => {amount}");
                //запросить новые остатки
                _isCardsStocksCheckNeeds = true;
            } catch (Exception x) {
                if (DB.GetParamBool("alertSound"))
                    new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
                Log.Add($"{L}UpdateProductStocks: ошибка обновления остатка {good.name} - {x.Message}");
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
                //формирую объект запроса
                var data = new[] {
                    new {
                        nmID = card.nmID,
                        vendorCode = good.id,
                        brand = GetBrand(good,attributes),
                        title = good.NameLimit(_nameLimit, "WB_TITLE"),
                        description = GetDescription(good),
                        dimensions = new {
                            length = good.SizeSM("length",5),
                            width = good.SizeSM("width",5),
                            height = good.SizeSM("height",5),
                            weightBrutto = good.Weight
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
                        data[0].characteristics.Add(new { id = a.charcID, value = Math.Round((decimal) (float) a.value, 2) });
                }
                //сравниваем основные поля и характеристики
                if (card.title == data[0].title &&
                    card.description == data[0].description &&
                    card.brand == data[0].brand &&
                    card.dimensions.length == data[0].dimensions.length &&
                    card.dimensions.width == data[0].dimensions.width &&
                    card.dimensions.height == data[0].dimensions.height &&
                    card.dimensions.weightBrutto == data[0].dimensions.weightBrutto &&
                    IsCharacteristicsEqual(card, data[0].characteristics)) {
                    //Log.Add($"{_l} UpdateCard: {card.title} - карточка не нуждается в обновлении");
                    return;
                }

                var res = await PostRequestAsync(data, "https://content-api.wildberries.ru/content/v2/cards/update");
                if (!string.IsNullOrEmpty(res))
                    Log.Add($"{L}UpdateCard: {good.name} - карточка обновлена!");
                else
                    throw new Exception("ошибка обновления карточки!");
            } catch (Exception x) {
                Log.Add($"{L}UpdateCard: ошибка обновления описания {good.id} {good.name} - {x.Message}");
            }
        }
        string GetDescription(GoodObject good) {
            return good.DescriptionList(2000, removePhone: true, specDesc: "WB_DESCRIPTION").Aggregate((a, b) => a + "\n" + b)
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
        void SaveToFile(List<GoodObject> goods, string fname = @"..\data\wildberries\wbGoodListForAdding.xlsx") {
            try {
                var fileInfo = new FileInfo(fname);
                var excelPackage = new ExcelPackage(fileInfo);
                while (excelPackage.Workbook.Worksheets.Count > 0) {
                    excelPackage.Workbook.Worksheets.Delete(1);
                }
                excelPackage.Workbook.Worksheets.Add("список для добавления на WB");
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
                Log.Add($"{L}SaveToFile: список карточек выгружен в {fname}");
            } catch (Exception x) {
                Log.Add($"{L}SaveToFile: ошибка сохранения списка - {x.Message}");
            }
        }
        //список складов
        async Task GetWarehouseList() {
            try {
                //если файл свежий и обновление списка не требуется
                if (DateTime.Now < File.GetLastWriteTime(WARE_HOUSE_LIST_FILE).AddHours(_updateFreq * 2)) {
                    //загружаем с диска, если список пустой
                    if (_wareHouses == null) {
                        _wareHouses = JsonConvert.DeserializeObject<List<WbWareHouse>>(
                            File.ReadAllText(WARE_HOUSE_LIST_FILE));
                    }
                } else {
                    var data = new Dictionary<string, string>();
                    var res = await GetRequestAsync(data, "https://marketplace-api.wildberries.ru/api/v3/warehouses");
                    if (res != null) {
                        _wareHouses = JsonConvert.DeserializeObject<List<WbWareHouse>>(res);
                        File.WriteAllText(WARE_HOUSE_LIST_FILE, res);
                    } else
                        throw new Exception("ошибка запроса складов!");
                }
            } catch (Exception x) {
                Log.Add($"{L}GetWarehouseList: ошибка! - {x.Message}");
            }
        }
        //Получить этикетки для сборочных заданий
        public async Task<string> GetOrderSticker(string orderId) {
            try {
                Stickers stickerList;
                var data = new {
                    orders = new long[] { long.Parse(orderId) }
                };
                var res = await PostRequestAsync(data, "https://marketplace-api.wildberries.ru/api/v3/orders/stickers?type=svg&width=58&height=40");
                stickerList = JsonConvert.DeserializeObject<Stickers>(res);
                if (stickerList.stickers.Count > 0) {
                    var sticker = stickerList.stickers[0].partA + " " + stickerList.stickers[0].partB;
                    Log.Add($"{L}GetOrderSticker: получен стикер {sticker}");
                    return sticker;
                } else {
                    Log.Add($"{L}GetOrderSticker: предупреждение! - order {orderId} - стикер не получен!");
                }
            } catch (Exception x) {
                Log.Add($"{L}GetOrderSticker: ошибка! - order {orderId} - {x.Message}");
            }
            return null;
        }
        //проверить габариты 
        public void CheckSizes() {
            Log.Add($"{L}CheckSize: проверяем габариты...");

            foreach (var good in Class365API._bus.Where(g => g.WB != null &&
                                                          g.WB.Contains("http") &&
                                                         (g.MaxDimention > 120 ||
                                                          g.SumDimentions > 200 ||
                                                          g.Weight > 25)))
                Log.Add($"{L}CheckSize: id:{good.id} name:{good.name} wb:{good.WB} - ошибка! габариты за пределами!!");

            Log.Add($"{L}CheckSize: проверка завершена.");
        }
        //проиизводитель
        string GetBrand(GoodObject b, List<WbCharc> attributes) {
            var brand = b.GetManufacture() ?? "";
            //если список иключений содержит название бренда, проверяем название категории
            if (_exceptionBrands.Keys.Contains(brand)) {
                var name = _catsXml.Descendants("Type")
                                   .First(f => f.Attribute("subjectID")
                                                .Value == attributes.First()
                                                                    .subjectID
                                                                    .ToString())
                                   .Attribute("name").Value;
                //если исключение для бренда содержит название категории - не заполняем
                if (_exceptionBrands[brand].Contains(name))
                    return "";
            }
            return brand;
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
            public float weightBrutto { get; set; } //новое свойство для передачи веса брутто!!
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

        //деактивация товаров группы Масла (установка остатка 0)
        public async Task DeactivateMaslaProductsAsync() {
            try {
                //получаем список товаров группы Масла с ссылками на WB
                var maslaGoods = Class365API._bus.Where(w => w.GroupName.ToLowerInvariant() == "масла" && !string.IsNullOrEmpty(w.WB) && w.WB.Contains("http")).ToList();
                if (maslaGoods.Count == 0) return;
                
                Log.Add($"{L}DeactivateMaslaProducts: найдено {maslaGoods.Count} товаров группы Масла для деактивации");
                
                //получаем список карточек WB
                if (_isCardsListCheckNeeds)
                    await GetCardsListAsync();
                
                foreach (var good in maslaGoods) {
                    try {
                        //находим карточку товара
                        var card = _cardsList.Find(f => f.vendorCode == good.id);
                        if (card != null) {
                            //устанавливаем остаток 0 на всех складах
                            var data = new {
                                stocks = new[] {
                                    new {
                                        sku = card.sizes[0].skus[0],
                                        amount = 0
                                    }
                                }
                            };
                            var res = await PostRequestAsync(data, $"https://marketplace-api.wildberries.ru/api/v3/stocks/{_wareHouses[0].id}", true);
                            var res1 = await PostRequestAsync(data, $"https://marketplace-api.wildberries.ru/api/v3/stocks/{_wareHouses[1].id}", true);
                            Log.Add($"{L}DeactivateMaslaProducts: деактивирован товар {good.name} (остаток установлен в 0)");
                            await Task.Delay(500);
                        }
                    } catch (Exception ex) {
                        Log.Add($"{L}DeactivateMaslaProducts: ошибка деактивации товара {good.name} - {ex.Message}");
                    }
                }
            } catch (Exception x) {
                Log.Add($"{L}DeactivateMaslaProducts: ошибка - {x.Message}");
            }
        }
    }
}
