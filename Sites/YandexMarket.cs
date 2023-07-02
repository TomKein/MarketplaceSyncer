using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;
using Selen.Base;
using Selen.Tools;

namespace Selen.Sites {
    internal class YandexMarket {

        readonly string _l = "yandex: ";
        readonly string filename = @"..\yandex.xml";
        readonly string satomUrl = "https://xn--80aejmkqfc6ab8a1b.xn--p1ai/yml-export/889dec0b799fb1c3efb2eb1ca4d7e41e/?full=1&save";
        readonly string satomFile = @"..\satom_import.xml";
        XDocument satomYML;

        public void GetSatomXml() {
            //загружаю xml с satom: если файлу больше 6 часов - пытаюсь запросить новый, иначе загружаю с диска
            var period = DB._db.GetParamInt("satomRequestPeriod");
            if (File.Exists(satomFile) && File.GetLastWriteTime(satomFile).AddHours(period) < DateTime.Now) {
                try {
                    Log.Add(_l + "запрашиваю новый каталог xml с satom...");
                    satomYML = XDocument.Load(satomUrl);
                    if (satomYML.Descendants("offer").Count() > 10000)
                        satomYML.Save(satomFile);
                    else
                        throw new Exception("мало элементов");
                    Log.Add(_l + "каталог обновлен!");
                    return;
                } catch (Exception x) {
                    Log.Add(_l + "ошибка запроса xml с satom.ru - " + x.Message);
                }
            }
            satomYML = XDocument.Load(satomFile);
            Log.Add(_l + "каталог загружен!");
        }
        //генерация xml
        public async Task GenerateXML(List<RootObject> _bus) {
            var gen = Task.Factory.StartNew(() => {
                //интервал проверки
                var uploadInterval = DB._db.GetParamInt("yandex.uploadInterval");
                if (uploadInterval == 0 || DateTime.Now.Hour == 0 || DateTime.Now.Hour % uploadInterval != 0)
                    return false;
                //загружаю xml с satom
                GetSatomXml();
                //доп. описание
                string[] _addDesc = JsonConvert.DeserializeObject<string[]>(
                    DB._db.GetParamStr("yandex.addDescription"));
                //конвертирую время в необходимый формат 2022-12-11T17:26:06.6560855+03:00
                var timeNow = XmlConvert.ToString(DateTime.Now, XmlDateTimeSerializationMode.Local);
                //обновляю вес товара по умолчанию
                RootObject.UpdateDefaultWeight();
                //обновляю объем товара по умолчанию
                RootObject.UpdateDefaultVolume();
                //обновляю срок годности товара по умолчанию
                RootObject.UpdateDefaultValidity();
                //создаю необходимый элемент <shop>
                var shop = new XElement("shop");
                shop.Add(new XElement("name", "АвтоТехноШик"));
                shop.Add(new XElement("company", "АвтоТехноШик"));
                shop.Add(new XElement("url", "https://автотехношик.рф"));
                shop.Add(new XElement("platform", "Satom.ru"));
                //получаю список карточек с положительным остатком и ценой, у которых есть фотографии
                //отсортированный по цене вниз
                var bus = _bus.Where(w => w.price > 0
                                       && w.images.Count > 0
                                       && (w.amount > 0 || DateTime.Parse(w.updated).AddDays(5) > DateTime.Now))
                              .OrderByDescending(o => o.price);
                //список групп, в которых нашлись товары
                var groupsIds = bus.Select(s => s.group_id).Distinct();
                //создаю необходимый элемент <categories>
                var categories = new XElement("categories");
                //заполняю категории
                foreach (var groupId in groupsIds) {
                    //исключение
                    if (groupId == "2281135")// Инструменты (аренда)
                        continue;
                    categories.Add(new XElement("category", RootObject.GroupName(groupId), new XAttribute("id", groupId)));
                }
                shop.Add(categories);
                //создаю необходимый элемент <offers>
                var offers = new XElement("offers");
                Log.Add(_l + "найдено " + bus.Count() + " потенциальных объявлений");
                ////наценка минимальная (дефолтная) из настроек
                //var overPriceMin = DB._db.GetParamInt("yandex.overPriceMin");
                ////наценка повышенная из настроек
                //var overPriceMax = DB._db.GetParamInt("yandex.overPriceMax");
                ////пороговый вес для максимальной наценки из настроек
                //var overPriceThresWeight = DB._db.GetParamInt("yandex.overPriceThresWeight");
                ////пороговая длина для максимальной наценки из настроек
                //var overPriceThresLength = DB._db.GetParamInt("yandex.overPriceThresWeight");
                //старая цена из настроек
                var oldPriceProcent = DB._db.GetParamFloat("yandex.oldPriceProcent");
                //для каждой карточки
                foreach (var b in bus) {
                    try {
                        //исключение
                        var n = b.name.ToLowerInvariant();
                        if (b.group_id == "2281135" || n.Contains("пепельница") || //// Инструменты (аренда), пепельницы
                            n.Contains("стекло заднее") || n.Contains("стекло переднее")) 
                            continue;
                        //создаю новый элемент <offer> с атрубутом id
                        var offer = new XElement("offer", new XAttribute("id", b.id));
                        //добавляю категорию товара
                        offer.Add(new XElement("categoryId", b.group_id));
                        //наименование (до 150 символов)
                        offer.Add(new XElement("name", b.NameLimit(150)));
                        //цена
                        var price = GetPrice(b);
                        offer.Add(new XElement("price", price));
                        //цена до скидки +10%
                        offer.Add(new XElement("oldprice", (Math.Ceiling((price * (1 + oldPriceProcent/100))/100)*100).ToString("F0")));
                        //валюта 
                        offer.Add(new XElement("currencyId", "RUR"));
                        //изображения (до 20 шт)
                        foreach (var photo in b.images.Take(20)) 
                            offer.Add(new XElement("picture", photo.url));                        
                        //если надо снять
                        if (b.amount <= 0) 
                            offer.Add(new XElement("disabled", "true"));
                        else
                            offer.Add(new XElement("disabled", "false"));
                        //описание
                        var description = b.DescriptionList(2990, _addDesc);
                        offer.Add(new XElement("description", new XCData(description.Aggregate((a1, a2) => a1 + "\r\n" + a2))));
                        //производитель
                        var vendor = b.GetManufacture();
                        if (vendor != null)
                            offer.Add(new XElement("vendor", vendor));
                        //артикул
                        if (!string.IsNullOrEmpty(b.part))
                            offer.Add(new XElement("vendorCode", b.part));
                        //количество 
                        offer.Add(new XElement("count", b.amount));
                        //вес
                        offer.Add(new XElement("weight", b.GetWeightString()));
                        //размеры
                        offer.Add(new XElement("dimensions", b.GetDimentionsString()));
                        //блок ресейл
                        if (!b.IsNew()) {
                            //состояние - бывший в употреблении
                            var condition = new XElement("condition", new XAttribute("type", "preowned"));
                            //внешний вид - хороший, есть следы использования
                            condition.Add(new XElement("quality", "good"));
                            //описание состояния - ищу строку в описании
                            var reason = (b.DescriptionList(3000).Where(w => !RootObject.IsNew(w))?.First())
                                         ?? "Б/у оригинал, в хорошем состоянии!";
                            condition.Add(new XElement("reason", reason));
                            //сохраняю в оффер
                            offer.Add(condition);
                        }
                        //срок годности 
                        if (!b.IsGroupSolidParts())  //если группа масла, автохимия, аксессуары
                            offer.Add(new XElement("period-of-validity-days", b.GetValidity()));
                        //квант продажи
                        var quant = b.GetQuantOfSell();
                        if (!string.IsNullOrEmpty(quant)) {
                            offer.Add(new XElement("min-quantity", quant));
                            offer.Add(new XElement("step-quantity", quant));
                        }
                        //добавляю оффер к офферам
                        offers.Add(offer);
                    } catch (Exception x) {
                        Log.Add(_l + b.name + " - " + x.Message);
                    }
                }
                Log.Add(_l + "выгружено " + offers.Descendants("offer").Count());
                //добавляю offers в shop
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
                xml.Save(filename);
                return true;
            });
            //если файл сгенерирован и его размер ок
            if (await gen && new FileInfo(filename).Length > await DB._db.GetParamIntAsync("yandex.xmlMinSize"))
                //отправляю файл на сервер
                await SftpClient.FtpUploadAsync(filename);
        }

        private int GetPrice(RootObject b) {
            var weight = b.GetWeight();
            var d = b.GetDimentions();
            var length = d[0] + d[1] + d[2];
            //наценка 20% на всё
            int overPrice = (int)(b.price * 0.2);
            //если наценка меньше 100 р - округляю
            if (overPrice < 100)
                overPrice = 100;
            //вес от 10 кг или размер от 100 -- минимальная наценка 1000 р
            if (overPrice < 1000 && (weight >= 10 || length >= 100))
                overPrice = 1000;
            //вес от 30 кг -- минимальная наценка 2000 р
            if (overPrice < 2000 && weight >= 30)
                overPrice = 2000;
            return b.price + overPrice;
        }
    }
}
