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
using System.Xml.Linq;

namespace Selen.Sites {
    internal class Avito {
        //получение ключей https://www.avito.ru/professionals/api
        //получение токена https://developers.avito.ru/api-catalog/auth/documentation#operation/getAccessToken
        readonly string _l = "avito: ";
        readonly string _fileNameExport = @"..\data\avito.xml";
        readonly string _fileNameStockExport = @"..\data\avitostock.xml";
        readonly string _autoCatalogFile = @"..\data\autocatalog.xml";
        readonly string _autoCatalogUrl = "http://autoload.avito.ru/format/Autocatalog.xml";
        readonly string _applicationAttribureId = "2543011";
        XDocument autoCatalogXML;
        string _clientId;// = "4O7a8RcHV1qdcbp_Lr7f";
        string _clientSecret;// = "1JtB0Yi801aPfl2mKLziP_QcauNEaZaTMuG6asuB";
        string _accessToken;
        readonly string FILE_RESERVES = @"..\data\avito\reserves.json";       //список сделанных резервов
        string _basePath;// = "https://api.avito.ru";
        HttpClient _hc = new HttpClient();

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
                    } else if (response.StatusCode==HttpStatusCode.InternalServerError) {
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
        public async Task GenerateXML(List<GoodObject> _bus) {
            if (Class365API.SyncStartTime.Minute < 55)
                return;
            if (!await DB.GetParamBoolAsync("avito.syncEnable")) {
                Log.Add(_l + "синхронизация отключена!");
                return;
            }
            await UpdateApplicationsAsync(_bus);
            await Task.Factory.StartNew(() => {
                var offersLimit = DB.GetParamInt("avito.offersLimit");
                var priceLevel = DB.GetParamInt("avito.priceLevel");
                string[] _addDesc = JsonConvert.DeserializeObject<string[]>(
                    DB.GetParamStr("avito.addDescription"));
                string[] _addDesc2 = JsonConvert.DeserializeObject<string[]>(
                    DB.GetParamStr("avito.addDescription2"));
                //цены для рассрочки
                var creditPriceMin = DB.GetParamInt("creditPriceMin");
                var creditPriceMax = DB.GetParamInt("creditPriceMax");
                var creditDescription = DB.GetParamStr("creditDescription");
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
                var bus = _bus.Where(w => w.Price >= priceLevel && w.images.Count > 0
                                      && (w.Amount > 0 || DateTime.Parse(w.updated).AddHours(2).AddDays(days) > Class365API.LastScanTime))
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
                        var d = b.DescriptionList(2990, _addDesc);
                        d.AddRange(_addDesc2);
                        if (b.Price >= creditPriceMin && b.Price <= creditPriceMax)
                            d.Insert(0, creditDescription);
                        ad.Add(new XElement("Description", new XCData(d.Aggregate((a1, a2) => a1 + "\r\n" + a2))));
                        ad.Add(new XElement("ManagerName", "Менеджер"));
                        ad.Add(new XElement("AdType", "Товар приобретен на продажу"));
                        ad.Add(new XElement("Condition", b.IsNew() ? "Новое" : "Б/у"));
                        ad.Add(new XElement("Availability", "В наличии"));
                        ad.Add(new XElement("Originality", b.IsOrigin() ? "Оригинал" : "Аналог"));
                        //номер запчасти
                        if (!string.IsNullOrEmpty(b.Part))
                            ad.Add(new XElement("OEM", b.Part));
                        //производитель
                        var brand = b.GetManufacture();
                        if (!string.IsNullOrEmpty(brand))
                            ad.Add(new XElement("Brand", b.GetManufacture()));
                        // если запчасть б/у и нет номера или бренда,
                        // то нужно добавить Для чего подходит (требование авито)
                        if (!b.IsNew()/* && (string.IsNullOrEmpty(b.part) || string.IsNullOrEmpty(brand))*/) {
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
            }
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
            File.WriteAllText(@"..\avito_generations.txt", list.Aggregate((a, b) => a + "\n" + b));
            //await Applications.UpdateApplicationListAsync(list);
        }
        //категории авито
        public static Dictionary<string, string> GetCategoryAvito(GoodObject b) {
            var name = b.name.ToLowerInvariant()
                             .Replace(@"б\у", "").Replace("б/у", "").Replace("б.у.", "").Replace("б.у", "").Trim();
            var d = new Dictionary<string, string>();
            if (name.StartsWith("масло ")) {
                d.Add("TypeId", "4-942");                           //Масла и автохимия
            } else if (name.StartsWith("фиксаторы") ||
                name.StartsWith("набор ") && name.Contains("инструмент") ||
                name.StartsWith("набор ") && name.Contains("ключ") ||
                name.Contains("бандаж") && name.Contains("глушителя") ||
                name.StartsWith("набор фиксаторов") ||
                name.StartsWith("домкрат") ||
                name.StartsWith("щетка для дрели") ||
                name.StartsWith("ключ балонный") ||
                name.StartsWith("стяжка пружин") ||
                name.StartsWith("штангенциркуль") ||
                name.StartsWith("трещотка ") ||
                name.StartsWith("лопата саперная") ||
                name.StartsWith("автотестер ") ||
                name.StartsWith("ключ ") ||
                name.StartsWith("отвертка ") ||
                name.StartsWith("бокорезы ") ||
                name.StartsWith("воронка с фильтром ") ||
                name.StartsWith("щетка мет") ||
                name.StartsWith("мультиметр цифровой") ||
                name.StartsWith("лампа переносная") ||
                name.StartsWith("манометр ") ||
                name.StartsWith("набор для ремонта") ||
                name.StartsWith("знак аварийной") ||
                name.StartsWith("головка") &&
                (name.Contains("торц") || name.Contains("удлин") || name.Contains("шестиг")) ||
                name.StartsWith("струна")) {
                d.Add("TypeId", "4-963");                          //Инструменты
            } else if (name.StartsWith("венец") ||
                name.Contains("коленвал") ||
                name.Contains("кольцо стопорное") ||
                name.Contains("маховик")) {
                d.Add("TypeId", "16-833");                          //Коленвал, маховик
            } else if (name.Contains("фазорегулятор") ||
                name.StartsWith("клапан газорасп") ||
                name.StartsWith("крышка дв") ||
                name.Contains("лобовая") && name.Contains("крышка") ||
                name.Contains("ось") && name.Contains("коромыс") ||
                name.Contains("коромысл") && name.Contains("клапан") ||
                name.Contains("натяж") && (name.Contains("планк") || name.Contains("башмак")) ||
                name.StartsWith("крышка лоб") ||
                name.StartsWith("натяжитель") ||
                name.StartsWith("балансир") ||
                name.StartsWith("клапан выпуск") ||
                name.StartsWith("крышка сапуна") ||
                name.StartsWith("муфта vvti") ||
                name.StartsWith("клапан впуск")) {
                d.Add("TypeId", "16-841");                          //Ремни, цепи, элементы ГРМ
            } else if ((name.StartsWith("трубк") ||
                 name.StartsWith("диск") ||
                 name.StartsWith("вилка") ||
                 name.StartsWith("крышка") ||
                 name.StartsWith("шланг") ||
                 name.Contains("комплект") ||
                 name.Contains("к-т")
                 ) && name.Contains("сцеплен") ||
                name.StartsWith("гидротрансформатор акпп") ||
                name.StartsWith("гидроаккумулятор") ||
                name.Contains("цилиндр сцепления") ||
                name.Contains("бачок сцепления") ||
                name.StartsWith("кардан ") ||
                name.StartsWith("раздатка ") ||
                name.StartsWith("кулиса ") ||
                name.StartsWith("трубка вакуумная") ||
                name.StartsWith("трубка акпп") ||
                name.StartsWith("механизм селектора") ||
                name.StartsWith("механизм кулисы") ||
                name.StartsWith("редуктор") && name.Contains("моста") ||
                name.StartsWith("корзина") ||
                name.StartsWith("гидроблок") ||
                name.StartsWith("переделка с акпп") ||
                name.StartsWith("соленоид акпп") ||
                name.StartsWith("ступица") ||

                name.Contains("подшипник") && (name.Contains("ступи") || name.Contains("выжимно") ||
                name.Contains("сепаратор") || name.Contains("кпп ") || name.Contains("акпп") ||
                name.Contains("дифференциала") || name.Contains("autostar") || name.Contains("конический") ||
                name.Contains("полуоси")) ||

                name.StartsWith("фланец") && (name.Contains("раздат") ||
                name.Contains("полуоси") || name.Contains("кардан")) ||

                name.Contains("дифферен") && (name.Contains("механизм") || name.Contains("акпп")) ||


                name.StartsWith("шарнир штока кпп") ||
                name.StartsWith("крышка разда") ||
                name.StartsWith("фильтр акпп") ||
                name.StartsWith("шрус") ||
                name.StartsWith("цапфа") ||
                name.StartsWith("пластина мкпп") ||
                name.StartsWith("фланец кпп") ||
                (name.Contains("фланец") || name.Contains("передача") ||
                 name.Contains("механизм") || name.Contains("узел") ||
                 name.Contains("корпус") || name.Contains("крышка") ||
                 name.Contains("вал ") || name.Contains("селектор ") || name.Contains("муфта"))
                                              && name.Contains("кпп ") ||
                name.Contains("комплект кулисы")) {
                d.Add("TypeId", "11-629");                           //Трансмиссия и привод
            } else if (name.StartsWith("клапанная крышка") ||
                name.StartsWith("крышка клапанная") ||
                name.StartsWith("крышка гбц")) {
                d.Add("TypeId", "16-832");                           //Клапанная крышка
            } else if (name.StartsWith("стекло ") ||
                name.StartsWith("форточ")) {
                //d.Add("TypeId", "11-626");                         //Стекла
                d.Add("TypeId", "11-625");                           //Салон
            } else if (name.StartsWith("бачок гур") ||
                name.StartsWith("комплект гур") ||
                (name.Contains("рейк") && name.Contains("рул")) ||
                name.StartsWith("бачок гидроусилителя") ||
                name.StartsWith("насос гур") ||

                (name.StartsWith("шкив") || name.Contains("трубк") || name.Contains("шланг")) &&
                (name.Contains("гур") || name.Contains("гидро") || name.Contains("эур") || name.Contains("давлен")) ||

                name.StartsWith("крышка бачка гур") ||
                name.StartsWith("карданчик рул") ||
                name.Contains("кардан") && name.Contains("рул") ||
                name.StartsWith("вал рул") ||
                name.StartsWith("колонка рулевая") ||
                name.StartsWith("рулевой вал") ||
                name.StartsWith("рулевой ред") ||
                name.StartsWith("наконечник рулевой") ||
                name.StartsWith("рулевая колонка") ||
                name.StartsWith("насос усилителя руля") ||
                name.StartsWith("трубка гуp обратка") ||
                name.StartsWith("крышка эур") ||
                name.Contains("рулевая") && name.Contains("тяга") ||
                name.Contains("рулевые") && name.Contains("тяги") ||
                name.StartsWith("руль ") ||
                name.StartsWith("радиатор гур")) {
                d.Add("TypeId", "11-624");                              //Рулевое управление
            } else if (name.StartsWith("капот") ||
                name.StartsWith("утеплитель капота") ||
                name.StartsWith("обшивка капота") ||
                name.StartsWith("шумоизоляция кап")) {
                d.Add("TypeId", "16-814");                              //Капот
            } else if (name.StartsWith("крышка баг")) {
                d.Add("TypeId", "16-818");                              //Крышка, дверь багажника
            } else if (name.StartsWith("крыло") ||
                name.StartsWith("четверть задняя")) {
                d.Add("TypeId", "16-816");                              //Крылья
            } else if (name.StartsWith("зеркало") ||
                name.StartsWith("крышка зеркала") ||
                name.StartsWith("подложка наружного зеркала") ||
                name.StartsWith("зеркальный") ||
                name.Contains("зеркала") && (name.StartsWith("крутилка") || name.Contains("джойстик")) ||
                name.StartsWith("стекло зеркала")) {
                d.Add("TypeId", "16-812");                              //Зеркала
            } else if (name.StartsWith("замок бага") ||
                name.StartsWith("замок зад") ||
                name.StartsWith("замок кап") ||
                name.StartsWith("замок крыш") ||
                name.StartsWith("замок люч") ||
                name.StartsWith("шток замка") ||
                name.Contains("часть замка") ||
                name.StartsWith("замок пер") ||
                name.StartsWith("компрессор ц.з.") ||
                name.Contains("централь") && name.Contains("замка") ||
                name.Contains("замок") && name.Contains("сиден") ||
                name.Contains("крю") && name.Contains("капота") ||
                name.StartsWith("фиксатор") && (name.Contains("капота") || name.Contains("замка")) ||
                name.StartsWith("замок стек") ||
                name.StartsWith("личинк") ||
                name.StartsWith("комплект личинок") ||
                name.StartsWith("комплект замка") ||
                name.StartsWith("ответка замка") ||
                name.StartsWith("замок") && name.Contains("двер")) {
                //d.Add("TypeId", "16-810");                              //Замки меняю на
                d.Add("TypeId", "11-625");                                //Салон (для авито доставки)
            } else if (name.StartsWith("дверь") ||
                name.StartsWith("панель двери") ||
                name.StartsWith("ручка") ||
                name.StartsWith("ручки")) {
                //d.Add("TypeId", "16-808");                              //Двери
                d.Add("TypeId", "11-625");                           //Салон
            } else if (name.StartsWith("бампер") ||
                name.StartsWith("абсорбер зад") ||
                name.StartsWith("абсорбер пер") ||
                name.StartsWith("решетка птф") ||
                (name.StartsWith("решетк") ||
                 name.StartsWith("спойлер") ||
                 name.StartsWith("поглотитель") ||
                 name.StartsWith("усилитель") ||
                 name.StartsWith("заглушка") ||
                 name.StartsWith("направляющ") ||
                 name.StartsWith("пыльник") ||
                 name.StartsWith("буфер") ||
                 name.StartsWith("боковина") ||
                 name.StartsWith("наполнитель") ||
                 name.StartsWith("клык") ||
                 name.StartsWith("абсорбер")
                ) && name.Contains("бампер")) {
                d.Add("TypeId", "16-806");                              //Бамперы
            } else if (name.StartsWith("амортизатор баг") ||
                name.StartsWith("амортизатор две") ||
                name.StartsWith("амортизатор кап") ||
                name.Contains("телевизор") ||
                name.Contains("жабо") ||
                name.Contains("водосток") ||
                name.StartsWith("решетка стеклоочист") ||
                name.StartsWith("дождевик") ||
                name.StartsWith("люк ") ||
                name.StartsWith("крышка люка") ||
                name.StartsWith("задняя часть") ||
                name.StartsWith("четверть ") ||
                name.StartsWith("локер ") ||
                name.StartsWith("траверса ") ||
                name.StartsWith("ус ") ||
                (name.StartsWith("ресничк") ||
                 name.StartsWith("планк")) && name.Contains("фар") ||
                name.StartsWith("планка") && (
                    name.Contains("под фар") ||
                    name.Contains("под решетк") ||
                    name.Contains("фонар")) ||
                name.StartsWith("подкрыл") ||
                name.StartsWith("арка крыла") ||
                name.StartsWith("корпус возд") ||
                name.StartsWith("лючок бензобака") ||
                name.StartsWith("панель передняя") ||
                name.StartsWith("колпачок ступицы") ||
                name.StartsWith("подножка внутренняя") ||
                name.StartsWith("задняя панель") ||
                name.StartsWith("рамка") && name.Contains("двери") ||
                name.StartsWith("рамка радиато") ||
                name.StartsWith("лючок топл") ||
                name.StartsWith("спойлер") ||
                name.StartsWith("бачок") && name.Contains("омывателя") ||
                name.StartsWith("распор") && name.Contains("стак") ||
                name.StartsWith("амортизатор крыш")) {
                d.Add("TypeId", "16-819");                              //Кузов по частям
            } else if (name.StartsWith("блок") && name.Contains("печк") ||
                name.StartsWith("блок управления отоп") ||
                name.StartsWith("блок управления клим") ||
                name.Contains("трубка") && name.Contains("отоп") ||
                name.StartsWith("блок клим") ||
                name.StartsWith("моторчик печки") ||
                name.StartsWith("воздухоочиститель") ||
                name.Contains("вентилятор") ||
                name.StartsWith("диффузор вентилятора") ||
                name.StartsWith("клапан заслон") ||
                name.StartsWith("клапан печк") ||
                name.StartsWith("клапан конди") ||
                name.StartsWith("осушитель кондиционера") ||
                name.StartsWith("патрубок ") ||
                name.StartsWith("компрессор кондиц") ||

                (name.StartsWith("шланг") || name.StartsWith("трубк")) &&
                    (name.Contains("кондиц") || name.Contains("отопи") || name.Contains("охлаж")) ||

                name.Contains("помпа") ||
                name.Contains("водяной") && name.Contains("насос") ||
                name.StartsWith("моторчик отоп") ||
                name.Contains("термостат") ||
                name.StartsWith("корпус") && name.Contains("отопител") ||
                name.StartsWith("испаритель конд") ||
                name.StartsWith("радиатор") ||
                name.StartsWith("моторчики засл") ||
                name.StartsWith("тросы печки") ||
                name.StartsWith("диффузор") ||
                name.StartsWith("интеркулер") ||
                name.StartsWith("бачок расширительный") ||
                name.StartsWith("привод заслонки") ||
                name.StartsWith("трубка") && name.Contains("охла") ||
                name.StartsWith("трубка конд") ||
                name.StartsWith("фланец") &&
                    (name.Contains("охлажден") ||
                     name.Contains("вентиляц")) ||
                name.StartsWith("решет") && name.Contains("моторчик") ||
                name.Contains("мотор") && name.Contains("печк") ||
                (name.StartsWith("сервопривод") || name.StartsWith("кран")) &&
                    (name.Contains("заслонк") ||
                     name.Contains("отопит") ||
                     name.Contains("печк")) ||
                name.StartsWith("мотор охлаждения") ||
                name.StartsWith("насос охлаждения") ||
                name.StartsWith("крыльчатка охлаждения") ||
                name.StartsWith("крышка расширительного бачка") ||
                name.StartsWith("тройник охлаждения") ||
                name.StartsWith("терморегулятор") ||
                name.StartsWith("шкив помпы") ||
                name.StartsWith("вентиляционный дефлектор") ||
                name.StartsWith("заслонка воздушная") ||
                name.StartsWith("тройник системы охлаждения") ||
                name.StartsWith("теплообменник") ||
                name.StartsWith("моторчик засл")) {
                d.Add("TypeId", "16-521");                              //Система охлаждения
            } else if (name.StartsWith("корпус маслянн") ||
                 name.Contains("масло") ||
                 name.StartsWith("сапун") ||
                 name.StartsWith("охладитель масл") ||
                 name.StartsWith("втулка масл") ||
                 name.StartsWith("корпус регулятора масла") ||
                 name.StartsWith("крышка заливная") ||
                 name.StartsWith("щуп акпп") ||
                 name.StartsWith("трубка щуп") ||
                 name.Contains("масля")) {
                d.Add("TypeId", "16-836");                              //Масляный насос, система смазки
            } else if (name.Contains("магнитола") ||
                name.StartsWith("динамик") ||
                name.StartsWith("камера зад") ||
                name.StartsWith("дисплей инф") ||
                name.Contains("сабвуфер") ||
                name.Contains("чейнджер") ||
                name.Contains("сплиттер") ||
                name.StartsWith("твиттер")) {
                d.Add("TypeId", "20");                              //Аудио- и видеотехника          
            } else if (name.StartsWith("диск торм") ||
                name.StartsWith("трос") && (name.Contains("тормоз") || name.Contains("ручник"))) {
                d.Add("TypeId", "11-628");                          //Тормозная система
            } else if (name.Contains("докатка") ||
                 name.StartsWith("колесо")) {
                d.Add("TypeId", "10-045");                          //Шины, диски и колёса / Колёса
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
            } else if (name.StartsWith("диск") &&
                (name.Contains("лит") || name.Contains("штам") || name.Contains(" r1"))) {
                d.Add("TypeId", "10-046");                          //Шины, диски и колёса / Диски
                d.Add("RimDiameter", b.GetDiskSize());              //Диаметр диска
                d.Add("RimOffset", "0");                            //TODO Вылет, пока ставим 0, добавить определение из описания
                d.Add("RimBolts", b.GetNumberOfHoles());            //количество отверстий
                d.Add("RimWidth", "5");                             //TODO ширина диска, пока 5, добавить определ. из описания
                d.Add("RimBoltsDiameter", b.GetDiameterOfHoles());  //диаметр расп. отверстий                                               
                d.Add("RimType", b.DiskType());                     //тип
                d.Add("RimDIA", b.GetDiaStup());                    //диаметр ступицы
            } else if (name.StartsWith("колпак")) {
                d.Add("TypeId", "10-044");                          //Шины, диски и колёса / Колпаки
            } else if (name.StartsWith("шины ") ||
                       name.StartsWith("шина ") ||
                       name.StartsWith("резина")) {
                d.Add("TypeId", "10-048");                          //Шины, диски и колёса / Шины
            } else if (name.StartsWith("фонар") ||
                name.StartsWith("плафон") ||
                name.StartsWith("корректор") ||
                name.StartsWith("патрон") ||
                name.StartsWith("фара") ||
                name.StartsWith("облицовка фары") ||
                name.StartsWith("подсветка") && name.Contains("номера") ||
                name.StartsWith("стоп доп") ||
                name.StartsWith("поворотник") ||
                name.StartsWith("повторитель") ||
                name.StartsWith("крышка фары") ||
                name.StartsWith("патрон повор") ||
                name.StartsWith("катафот") ||
                name.Contains("фонар") &&
                (name.StartsWith("плата") ||
                name.StartsWith("провод") ||
                name.StartsWith("патрон") ||
                name.StartsWith("крышк")) ||
                b.GroupName() == "Световые приборы транспорта") {
                d.Add("TypeId", "11-618");                          //Автосвет
            } else if (name.StartsWith("активатор") ||
                  name.StartsWith("актуатор") ||
                  name.StartsWith("антенн") ||
                  name.StartsWith("блок airbag") ||
                  name.StartsWith("блок bsi") ||
                  name.StartsWith("блок bsm") ||
                  name.StartsWith("блок gps") ||
                  name.StartsWith("блок srs") ||
                  name.StartsWith("блок ант") ||
                  name.StartsWith("блок звук") ||
                  name.StartsWith("блок имм") ||
                  name.StartsWith("блок кноп") ||
                  name.StartsWith("блок комф") ||
                  name.StartsWith("блок корр") ||
                  name.StartsWith("блок круиз") ||
                  name.StartsWith("блок курс") ||
                  name.StartsWith("блок мото") ||
                  name.StartsWith("блок парк") ||
                  name.StartsWith("блок подрул") ||
                  name.StartsWith("блок предох") ||
                  name.StartsWith("блок примен") ||
                  name.StartsWith("блок радио") ||
                  name.StartsWith("блок раз") ||
                  name.StartsWith("блок рег") ||
                  name.StartsWith("блок реле") ||
                  name.StartsWith("блок сигн") ||
                  name.StartsWith("блок стекло") ||
                  name.StartsWith("блок управ") ||
                  name.StartsWith("блок усил") ||
                  name.StartsWith("блок фильтр") ||
                  name.StartsWith("блок приемника") ||
                  name.StartsWith("блок центр") ||
                  name.StartsWith("блок электр") ||
                  name.StartsWith("блок энерго") ||
                  name.StartsWith("вентилятор") ||
                  name.StartsWith("выключател") ||
                  name.StartsWith("гнездо") ||
                  name.StartsWith("датчик") ||
                  name.StartsWith("джойстик") ||
                  name.StartsWith("диагностич") ||
                  name.StartsWith("дисплей") ||
                  name.StartsWith("дмрв") ||
                  name.StartsWith("замок заж") ||
                  name.StartsWith("замок рул") ||
                  name.StartsWith("зуммер") ||
                  name.StartsWith("иммобилай") ||
                  name.StartsWith("индикато") ||
                  name.StartsWith("интерфей") ||
                  name.StartsWith("клакс") ||
                  name.StartsWith("клапан") ||
                  name.StartsWith("клемм") ||
                  name.StartsWith("кнопк") ||
                  name.StartsWith("коммутат") ||
                  name.StartsWith("комплект иммоб") ||
                  name.StartsWith("комплект эбу") ||
                  name.StartsWith("корпус блока") ||
                  name.StartsWith("корпус датч") ||
                  name.StartsWith("корпус кнопок") ||
                  name.StartsWith("корпус перекл") ||
                  name.StartsWith("корпус плоск") ||
                  name.StartsWith("корпус подрул") ||
                  name.StartsWith("корпус трамб") ||
                  name.StartsWith("круиз") ||
                  name.StartsWith("лямбда") ||
                  name.StartsWith("обманка лямбд") ||
                  name.StartsWith("обманка датчика") ||
                  name.StartsWith("модуль") ||
                  name.StartsWith("мотор вент") ||
                  name.StartsWith("мотор зад") ||
                  name.StartsWith("мотор засл") ||
                  name.StartsWith("мотор отоп") ||
                  name.StartsWith("мотор пода") ||
                  name.StartsWith("мотор стекло") ||
                  name.StartsWith("моторчик") ||
                  name.StartsWith("насос бачка") ||
                  name.StartsWith("насос возд") ||
                  name.StartsWith("насос омыв") ||
                  name.StartsWith("панель приб") ||
                  name.StartsWith("переключател") ||
                  name.StartsWith("предохранит") ||
                  name.StartsWith("набор ") && name.Contains("предохранит") ||
                  name.StartsWith("привод центр") ||
                  name.StartsWith("прикуриват") ||
                  name.StartsWith("провод") ||
                  name.StartsWith("разъем") ||
                  name.StartsWith("распределитель зажиг") ||
                  name.StartsWith("переключатель") ||
                  name.StartsWith("расходомер") ||
                  name.StartsWith("регулятор") ||
                  name.StartsWith("резистор") ||
                  name.StartsWith("реле") ||
                  name.StartsWith("розетка") ||
                  name.StartsWith("сигнал") ||
                  name.StartsWith("трамблер") ||
                  name.StartsWith("усилитель am") ||
                  name.StartsWith("усилитель ан") ||
                  name.StartsWith("фишка") ||
                  name.Contains("панель") && name.Contains("прибор") ||
                  name.StartsWith("шлейф") ||
                  name.Contains("контактная") && name.Contains("группа") ||
                  name.StartsWith("расходомер") ||
                  name.StartsWith("эбу") ||
                  name.StartsWith("конденсатор") ||
                  name.StartsWith("заслонка отопителя") ||
                  name.StartsWith("вакуумный переключатель") ||
                  name.StartsWith("электропроводка печки") ||
                  name.StartsWith("пиропатрон петли") ||
                  b.GroupName() == "Электрика, зажигание") {
                d.Add("TypeId", "11-630");                          //Электрооборудование
            } else if (name.StartsWith("ящик") ||
                  name.StartsWith("обшивка") ||
                  name.Contains("бардачок") ||
                  name.Contains("крышка бардач") ||
                  name.Contains("воздуховод") ||
                  name.Contains("кожух подрул") ||
                  name.Contains("кожух рул") ||
                  name.Contains("подушка безопасности") ||
                  name.Contains("рем") && name.Contains("безопасности") ||
                  name.StartsWith("airbag") ||
                  name.StartsWith("ремень задний") ||
                  name.StartsWith("вещево") ||
                  name.StartsWith("ковер") ||
                  name.StartsWith("ручник") ||
                  name.StartsWith("рычаг") && name.Contains("кпп") ||
                  name.StartsWith("рычаг") && name.Contains("тормоз") ||
                  name.StartsWith("рычаг") && name.Contains("руля") ||
                  name.StartsWith("крышка блока предохра") ||
                  name.Contains("торпедо") ||
                  name.Contains("крышка кулис") ||
                  name.StartsWith("пепельниц") ||
                  name.Contains("консоль") ||
                  name.StartsWith("педаль") ||
                  name.StartsWith("подлокотник") ||
                  name.StartsWith("подстакан") ||
                  name.StartsWith("очечник") ||
                  name.StartsWith("полка ") ||
                  name.StartsWith("преднатяжитель ремня без") ||
                  name.StartsWith("рамка ") ||
                  name.StartsWith("чехол") ||
                  name.StartsWith("педали") ||
                  name.Contains("корпус подрулевых") ||
                  name.Contains("козырек солнц") ||
                  name.Contains("регулятор") && name.Contains("ремня") ||
                  name.StartsWith("дефлектор") ||
                  name.StartsWith("диван") ||
                  name.StartsWith("сиденье") ||
                  name.StartsWith("сиденья") ||
                  name.StartsWith("сидения") ||
                  name.StartsWith("подголовник") ||
                  name.StartsWith("треугольник задний") ||
                  name.StartsWith("лоток для домкрата") ||
                  name.StartsWith("облицовка рычага") ||
                  name.StartsWith("обшивки дверей") ||
                  name.StartsWith("ограничитель двери") ||
                  name.StartsWith("механизм регулировки ремня") ||
                  name.StartsWith("панель блока печки") ||
                  name.StartsWith("панель кожух") ||
                  name.StartsWith("решетк") && name.Contains("динамик") ||
                  name.StartsWith("ответная часть ремня") ||
                  name.StartsWith("крючок для одежды") ||
                  name.StartsWith("заглушка крепления сидения") ||
                  name.StartsWith("крышка внутренней ручки") ||
                  name.StartsWith("крышка запаски") ||
                  name.StartsWith("уплотнитель стекла") ||
                  name.StartsWith("заглушка болта запасного колеса") ||
                  name.StartsWith("кожух") && name.Contains("ремня безопасности") ||
                  name.StartsWith("уголок") && name.Contains("задн") ||
                  name.StartsWith("крышка") && (
                    name.Contains("приборной панели") ||
                    name.Contains("подушки безопасности") ||
                    name.Contains("центральной консоли")) ||
                  name.StartsWith("часы") ||
                  name.StartsWith("шторка") ||
                  name.StartsWith("пол багажника") ||
                  name.StartsWith("заглушка в руль") ||
                  name.StartsWith("заглушка переключател") ||
                  name.Contains("карман") ||
                  name.StartsWith("фиксатор") && name.Contains("стекл") ||
                  name.StartsWith("ограничит") && name.Contains("двер") ||
                  name.StartsWith("направляющая") ||
                  name.StartsWith("ролик") && name.Contains("двер") ||
                  name.StartsWith("механизм") && name.Contains("двер") ||
                  name.Contains("лыжный") && name.Contains("мешок") ||
                  name.StartsWith("пластик салона") ||
                  name.StartsWith("панель блока управления") ||
                  name.StartsWith("салазка") && name.Contains("двер")
                  ) {
                d.Add("TypeId", "11-625");                        //Салон
            } else if (name.Contains("дворник") ||
                 name.Contains("стеклоподъемник") ||
                 name.StartsWith("поводок") ||
                 name.StartsWith("стабилизатор") ||
                 name.StartsWith("порог ") ||
                 name.StartsWith("спойлер") ||
                 name.StartsWith("рейлинг") ||
                 name.StartsWith("дуги крыши") ||
                 name.StartsWith("трос ") ||
                 name.StartsWith("тросик замк") ||
                 name.StartsWith("передняя панель") ||
                 (name.Contains("мотор") || name.Contains("трапеция"))
                                         && name.Contains("стеклоочист") ||
                 name.StartsWith("фаркоп") ||
                 name.StartsWith("ниша запасного колеса") ||
                 name.StartsWith("панель верхняя")) {
                d.Add("TypeId", "16-819");                        //Кузов по частям
            } else if (name.StartsWith("генератор") ||
                  name.Contains("втягивающее") ||
                  name.StartsWith("подшипник генератора") ||
                  name.StartsWith("обгонная муфта генератора") ||
                  name.StartsWith("шкив генератора") ||
                  name.StartsWith("щетки генератора") ||
                  name.StartsWith("диодный") ||
                  name.StartsWith("бендикс") ||
                  name.StartsWith("стартер") ||
                  (name.StartsWith("крышка") || name.StartsWith("вилка") || name.StartsWith("щетки"))
                                                                          && name.Contains("стартер")) {
                d.Add("TypeId", "16-829");                          // Генераторы, стартеры
            } else if (name.StartsWith("амортизатор") ||
                  name.StartsWith("кулак") ||
                  name.StartsWith("пружин") ||
                  name.StartsWith("стойка") ||
                  name.StartsWith("рычаг") ||
                  name.StartsWith("ремкомплект рычага") ||
                  name.StartsWith("тарелка пружины") ||
                  name.Contains("опорн") && name.Contains("чашк") && name.Contains("амортизат") ||
                  name.StartsWith("тяга ") ||
                  name.Contains("опор") &&
                  (name.Contains("амортизат") ||
                   name.Contains("шаров")) ||
                  (name.StartsWith("скоба") || name.StartsWith("втулка")) && name.Contains("стабилиз") ||
                  name.StartsWith("сайлентблок") ||
                  name.StartsWith("сайленблок") ||
                  name.StartsWith("втулка сайлентблока") ||
                  name.StartsWith("привод ") ||
                  name.StartsWith("мкпп ") ||
                  name.StartsWith("акпп ")) {
                d.Add("TypeId", "11-623");                          // Подвеска
            } else if (name.StartsWith("абсорбер") ||
                   name.StartsWith("шланг") && name.Contains("абсорбер") ||
                   name.StartsWith("бак топл") ||
                   name.StartsWith("воздуховод") ||
                   name.StartsWith("воздухозаборник") ||
                   name.StartsWith("глушитель") ||
                   name.StartsWith("горловина") ||
                   name.Contains("заслонка") && name.Contains("дроссел") ||
                   name.StartsWith("корпус воздуш") ||
                   (name.StartsWith("крышка") || name.Contains("корпус ")) && name.Contains("фильтр") ||
                   name.StartsWith("инжектор") ||
                   name.Contains("катализатор") ||
                   (name.StartsWith("шланг") ||
                    name.StartsWith("крышка")) && name.Contains("топлив") ||
                   (name.Contains("промежуточная") ||
                    name.Contains("приемная") || name.Contains("приёмная")) && name.Contains("труба") ||
                   name.StartsWith("резонатор") ||
                   name.StartsWith("гофра") ||
                   name.StartsWith("патрубок воз") ||
                   name.StartsWith("патрубок интеркулера") ||
                   name.StartsWith("приёмная труба") ||
                   name.StartsWith("приемная труба") ||
                   name.StartsWith("коллектор") ||
                   name.StartsWith("впускной коллектор") ||
                   name.StartsWith("карбюратор") ||
                   name.StartsWith("бензонасос") ||
                   name.StartsWith("насос вакуум") ||
                   name.StartsWith("вакуумный аккумулятор") ||
                   name.Contains("топливный") && name.Contains("насос") ||
                   name.StartsWith("пламегаситель") ||
                   name.StartsWith("гофра") ||
                   name.StartsWith("форсунк") ||
                   name.StartsWith("труба прием") ||
                   name.StartsWith("трубка ") ||
                   name.StartsWith("трубки ") ||
                   name.StartsWith("резонатор воздуш") ||
                   name.StartsWith("ресивер воздуш") ||
                   name.StartsWith("ресивер вакуумный") ||
                   name.Contains("фильтр") && name.Contains("топлив") ||
                   name.Contains("уровень") && name.Contains("топлив") ||
                   name.StartsWith("крышка") && name.Contains("воздушного") ||
                   name.StartsWith("площадка") && name.Contains("приемной трубы") ||
                   name.StartsWith("потенциометр") && name.Contains("заслонки") ||
                   name.StartsWith("шланг") && name.Contains("картер") ||
                   name.StartsWith("бачок вакуумный") ||
                   name.StartsWith("моновпрыск") ||
                   name.StartsWith("рампа топливная") ||
                   name.StartsWith("ремонтная площадка") ||
                   name.StartsWith("насос вторичного воздуха") ||
                   name.StartsWith("потенциометр дроссельной заслонки") ||
                   name.StartsWith("труба соединительная") ||
                   name.StartsWith("насадка глушителя") ||
                   name.StartsWith("хомут глушителя") ||
                   name.StartsWith("обратный клапан") ||
                   name.StartsWith("проставка под карб") ||
                   name.StartsWith("сепаратор картерн") ||
                   name.StartsWith("фланец") &&
                   (name.Contains("карб") ||
                    name.Contains("монов") ||
                    name.Contains("глушит")) ||
                   name.StartsWith("тнвд") ||
                   name.Contains("коллектора") ||
                   name.StartsWith("бензобак") ||
                   name.StartsWith("труба прямая") ||
                   name.StartsWith("адаптер") && name.Contains("топливн") ||
                   name.StartsWith("штуцер топлив") ||
                   name.StartsWith("изгиб трубы глушителя") ||
                   name.StartsWith("накидная гайка топливного насоса")
                   ) {
                d.Add("TypeId", "11-627");                          //Топливная и выхлопная системы
            } else if (name.StartsWith("балка") ||
                    name.StartsWith("подрамник") ||
                    name.Contains("лонжерон") ||
                    name.StartsWith("усилитель подрамника")) {
                d.Add("TypeId", "16-805");                          //Балки, лонжероны
            } else if (name.StartsWith("блок цилиндров") ||
                     name.StartsWith("гбц ") ||
                     name.StartsWith("крышка масляного поддона") ||
                     name.StartsWith("масляный поддон") ||
                     name.StartsWith("передняя крышка двс") ||
                     name.StartsWith("поддон")) {
                d.Add("TypeId", "16-827");                          //Блок цилиндров, головка, картер
            } else if (name.StartsWith("двигатель")) {
                d.Add("TypeId", "16-830");                          //Двигатель в сборе
            } else if (name.StartsWith("катушка заж")) {
                d.Add("TypeId", "16-831");                          //Катушка зажигания, свечи, электрика
            } else if (name.StartsWith("молдинг") ||
                name.StartsWith("бархотк") ||
                name.StartsWith("декоративная крышка") ||
                name.StartsWith("крышка декоративная") ||
                name.StartsWith("заглушка птф") ||
                name.StartsWith("заглушка п/т фары") ||
                name.StartsWith("кожух двс") ||
                name.StartsWith("треугольник двери") ||
                name.StartsWith("крышка аккумулятора") ||
                name.StartsWith("крышка омывателя фары") ||
                name.StartsWith("надпись ") ||
                name.StartsWith("заглушка противотуманных") ||
                name.StartsWith("накладк") ||
                name.StartsWith("заглушка туманки") ||
                name.StartsWith("хром") && (name.Contains("стекл") || name.Contains("крыл") || name.Contains("двер")) ||
                name.StartsWith("эмблема")) {
                d.Add("TypeId", "16-822");                          //Молдинги, накладки
            } else if (name.StartsWith("брызговик") ||
                 name.StartsWith("накладк")) {
                d.Add("TypeId", "16-807");                          //Брызговики
            } else if (name.StartsWith("суппорт") ||
                 name.StartsWith("бачок торм") ||
                 name.StartsWith("крышка бачка торм") ||
                 name.StartsWith("пластины бараб") ||
                 name.StartsWith("бачок главного тормозного цилиндра") ||
                 name.StartsWith("разветвитель торм") ||
                 name.Contains("цилиндр") && name.Contains("тормоз") ||
                 name.StartsWith("электро ручник") ||
                 name.StartsWith("ремкомплект бараб") ||
                 name.StartsWith("трубк") && (name.Contains("тормоз") || name.Contains("вакуум")) ||
                 name.StartsWith("усилитель торм") ||
                 name.StartsWith("усилитель вакуум") ||
                 name.Contains("тормоз") && (name.Contains("барабан") || name.Contains("механизм")) ||
                 name.StartsWith("бачок гтц") ||
                 name.StartsWith("штуцер прокачки") ||
                 name.StartsWith("ремкомплект задних колодок") ||
                 name.StartsWith("колодки тормоз") ||
                 name.StartsWith("колодки бараб") ||
                 name.StartsWith("переходник тормозной") ||
                 name.StartsWith("распределитель торм") ||
                 name.Contains("abs") && (name.StartsWith("насос") || name.StartsWith("блок")) ||
                 name.Contains("планка") && name.Contains("тормоза") ||
                 name.StartsWith("скоба") && name.Contains("суппор")) {
                d.Add("TypeId", "11-628");                          //Тормозная система
            } else if (name.StartsWith("болт") ||
                  name.StartsWith("кронштейн") ||
                  name.StartsWith("гайка") ||
                  name.StartsWith("креплени") ||
                  name.StartsWith("опора") ||
                  name.StartsWith("подушка") ||
                  name.StartsWith("площадка") && name.Contains(" ак") ||
                  name.StartsWith("упор ") ||
                  name.StartsWith("гидроопора дв") ||
                  name.StartsWith("гидро опора дв") ||
                  name.StartsWith("торсион") ||
                  name.StartsWith("штырь") && name.Contains("капот") ||
                  name.StartsWith("хомут") && name.Contains("бака") ||
                  name.StartsWith("петл") ||
                  name.StartsWith("скобы приёмной трубы") ||
                  name.StartsWith("скоба лампы") ||
                  name.StartsWith("крюк буксировочный") ||
                  name.StartsWith("адаптер") && name.Contains("щет") ||
                  name.Contains("держатель")) {
                d.Add("TypeId", "16-815");                          //Крепления
            } else if (name.Contains("грм") ||
                   name.StartsWith("ролик") && (name.Contains("натяж") || name.Contains("ремн")) ||
                   name.StartsWith("вал баланс") ||
                   name.StartsWith("муфта распр") ||
                   name.StartsWith("ванос") ||
                   name.StartsWith("вал кором") ||
                   name.StartsWith("ролик обводной") ||
                   name.StartsWith("распредвал") ||
                   name.StartsWith("постель ") ||
                   name.StartsWith("крышка постел") ||
                   name.StartsWith("площадка датчика фаз") ||
                   name.StartsWith("промежуточный вал") ||
                   name.StartsWith("промвал") ||
                   name.StartsWith("планка успокоителя") ||
                   name.StartsWith("планка натяжителя") ||
                   name.StartsWith("успокоитель цепи") ||
                   name.StartsWith("вал пром") ||
                   name.StartsWith("шестерня") ||
                   name.StartsWith("шкив") ||
                   name.StartsWith("вал масл")) {
                d.Add("TypeId", "16-841");                          //Ремни, цепи, элементы ГРМ
            } else if (name.StartsWith("решетка радиатора") ||
                name.StartsWith("решётка радиатора") ||
                name.StartsWith("хром решетки радиа")
                ) {
                d.Add("TypeId", "16-825");                          //Решетка радиатора
            } else if (name.StartsWith("защита") ||
                name.Contains("экран") && name.Contains("тепл") ||
                name.StartsWith("щиток ") ||
                name.StartsWith("шумоизоляция") ||
                name.Contains("шуба") ||
                name.StartsWith("тормозной щит") ||
                name.StartsWith("уплотнитель") ||
                name.StartsWith("пыльник")) {
                d.Add("TypeId", "16-811");                          //Защита
            } else if (name.StartsWith("поршен") ||
                name.StartsWith("кольца пор") ||
                name.Contains("шатун")) {
                d.Add("TypeId", "16-838");                          //Поршни, шатуны, кольца
            } else if (name.StartsWith("крыша ")) {
                d.Add("TypeId", "16-817");                          //Крыша
            } else if (
                name.Contains("вал карданный")) {
                d.Add("TypeId", "11-629");                           //Трансмиссия и привод
            } else if (name.StartsWith("турбина ") ||
                name.StartsWith("трубк") && name.Contains("турб")
                ) {
                d.Add("TypeId", "16-842");                           //Турбины, компрессоры
            } else if (b.group_id == "289732" ||                      //  "Автохимия"
                name.StartsWith("огнетушитель") ||
                name.StartsWith("салфетка ")) {
                d.Add("TypeId", "4-942");                           //Автокосметика и автохимия
            } else if (name.StartsWith("прокладка") ||
                name.StartsWith("свеча зажиг") ||
                name.StartsWith("комплект прокладок") ||
                name.StartsWith("хомут ") ||
                name.StartsWith("сальник привода") ||
                name.StartsWith("кольцо уплотнительное") ||
                name.StartsWith("сальник промежуточного вала") ||
                name.StartsWith("соединитель патрубк") ||
                name.StartsWith("сальник №")) {
                d.Add("TypeId", "11-621");                          //Запчасти для ТО
            }

            if (d.Count > 0)
                d.Add("Category", "Запчасти и аксессуары"); //главная категория
            else
                throw new Exception("категория не определена!");

            //корректировка категорий для авито доставки
            if (d["TypeId"] == "11-629" //Трансмиссия и привод
                && b.GetWeight() < 15
                && b.GetLength() < 120) {
                d["TypeId"] = "11-623"; // меняем категорию на Подвеска
            }
            if (d["TypeId"] == "16-819" //Кузов по частям
                && b.GetWeight() < 15
                && b.GetLength() < 120) {
                d["TypeId"] = "11-625"; // меняем категорию на Салон
            }

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