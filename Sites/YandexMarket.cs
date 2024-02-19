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

        readonly string LP = "yandex: ";
        readonly string FILE_NAME_PRIMARY = @"..\yandex.xml";
        readonly string FILE_NAME_EXPRESS = @"..\yandex_express.xml";
        readonly int EXPRESS_MAX_LENGTH = 90;
        readonly int EXPRESS_MAX_WIDTH = 54;
        readonly int EXPRESS_MAX_HEIGHT = 43;
        readonly int EXPRESS_MAX_WEIGHT = 30;

        //генерация xml
        public async Task GenerateXML(List<RootObject> _bus) {
            var gen = Task.Factory.StartNew(() => {
                //интервал проверки
                var uploadInterval = DB.GetParamInt("yandex.uploadInterval");
                if (uploadInterval == 0 || DateTime.Now.Hour == 0 || DateTime.Now.Hour % uploadInterval != 0)
                    return false;
                //доп. описание
                string[] _addDesc = JsonConvert.DeserializeObject<string[]>(
                    DB.GetParamStr("yandex.addDescription"));
                //конвертирую время в необходимый формат 2022-12-11T17:26:06.6560855+03:00
                var timeNow = XmlConvert.ToString(DateTime.Now, XmlDateTimeSerializationMode.Local);
                //старая цена из настроек
                var oldPriceProcent = DB.GetParamFloat("yandex.oldPriceProcent");
                //обновляю вес товара по умолчанию
                RootObject.UpdateDefaultWeight();
                //обновляю объем товара по умолчанию
                RootObject.UpdateDefaultVolume();
                //обновляю срок годности товара по умолчанию
                RootObject.UpdateDefaultValidity();
                //получаю список карточек с положительным остатком и ценой, у которых есть фотографии
                //отсортированный по цене вниз
                var bus = _bus.Where(w => w.price > 0
                                       && w.images.Count > 0
                                       && (w.amount > 0 || DateTime.Parse(w.updated).AddDays(5) > Class365API.LastScanTime))
                              .OrderByDescending(o => o.price);
                Log.Add(LP + "найдено " + bus.Count() + " потенциальных объявлений");
                var offers = new XElement("offers");
                var offersExpress = new XElement("offers");
                //для каждой карточки
                foreach (var b in bus) {
                    try {
                        //исключения
                        var n = b.name.ToLowerInvariant();
                        if (b.group_id == "2281135" || n.Contains("пепельница") || //// Инструменты (аренда), пепельницы
                            n.Contains("стекло заднее") || n.Contains("стекло переднее") ||
                            n.StartsWith("крыша ") || n.Contains("задняя часть кузова") ||  //габаритные зч
                            n.Contains("крыло заднее") || n.Contains("четверть ") ||
                            n.Contains("передняя часть кузова") || n.Contains("лонжерон") ||
                            n.Contains("панель передняя") || n.Contains("морда") || 
                            n.Contains("телевизор")
                            ) 
                            continue;
                        var offer = new XElement("offer", new XAttribute("id", b.id));
                        offer.Add(new XElement("categoryId", b.group_id));
                        offer.Add(new XElement("name", b.NameLimit(150)));
                        var price = GetPrice(b);
                        offer.Add(new XElement("price", price));
                        //цена до скидки +10%
                        offer.Add(new XElement("oldprice", (Math.Ceiling((price * (1 + oldPriceProcent/100))/100)*100).ToString("F0")));
                        offer.Add(new XElement("currencyId", "RUR"));
                        foreach (var photo in b.images.Take(20)) 
                            offer.Add(new XElement("picture", photo.url));                        
                        var description = b.DescriptionList(2990, _addDesc);
                        offer.Add(new XElement("description", new XCData(description.Aggregate((a1, a2) => a1 + "\r\n" + a2))));
                        var vendor = b.GetManufacture();
                        if (vendor != null)
                            offer.Add(new XElement("vendor", vendor));
                        //артикул
                        if (!string.IsNullOrEmpty(b.Part))
                            offer.Add(new XElement("vendorCode", b.Part));
                        offer.Add(new XElement("weight", b.GetWeightString()));
                        offer.Add(new XElement("dimensions", b.GetDimentionsString()));
                        //блок ресейл
                        if (!b.IsNew()) {
                            //состояние - бывший в употреблении
                            var condition = new XElement("condition", new XAttribute("type", "preowned"));
                            //внешний вид - хороший, есть следы использования
                            condition.Add(new XElement("quality", "good"));
                            description = b.DescriptionList(390, _addDesc);
                            condition.Add(new XElement("reason", description.Aggregate((a1, a2) => a1 + "\r\n" + a2)));
                            //сохраняю в оффер
                            offer.Add(condition);
                        }
                        //срок годности (если группа масла, автохимия, аксессуары)
                        if (!b.IsGroupSolidParts())  
                            offer.Add(new XElement("period-of-validity-days", b.GetValidity().Split(' ').First()));
                        //квант продажи
                        var quant = b.GetQuantOfSell();
                        if (!string.IsNullOrEmpty(quant)) {
                            offer.Add(new XElement("min-quantity", quant));
                            offer.Add(new XElement("step-quantity", quant));
                        }

                        //копия оффера для склада Экспресс
                        var offerExpress = new XElement(offer);

                        //количество 
                        offer.Add(new XElement("count", b.amount));

                        var amountExpress = GetAmountExpress(b);
                        offerExpress.Add(new XElement("count", amountExpress));

                        //если надо снять
                        offer.Add(new XElement("disabled", b.amount <= 0 ? "true":"false"));
                        offerExpress.Add(new XElement("disabled", amountExpress <= 0 ? "true" : "false"));
                        //добавляю оффер к офферам
                        offers.Add(offer);
                        offersExpress.Add(offerExpress);
                    } catch (Exception x) {
                        Log.Add(LP + b.name + " - ОШИБКА ВЫГРУЗКИ! - " + x.Message);
                    }
                }
                Log.Add(LP + "выгружено " + offers.Descendants("offer").Count());
                //создаю необходимый элемент <shop>
                var shop = new XElement("shop");
                shop.Add(new XElement("name", "АвтоТехноШик"));
                shop.Add(new XElement("company", "АвтоТехноШик"));
                shop.Add(new XElement("url", "https://автотехношик.рф"));
                shop.Add(new XElement("platform", "Satom.ru"));
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
                //копия магазина для выгрузки Экспресс
                var shopExpress = new XElement(shop);

                shop.Add(offers);
                shopExpress.Add(offersExpress);
                //создаю корневой элемент 
                var root = new XElement("yml_catalog", new XAttribute("date", timeNow));
                var rootExpress = new XElement(root);
                //добавляю shop в root 
                root.Add(shop);
                rootExpress.Add(shopExpress);
                //создаю новый xml
                var xml = new XDocument();
                var xmlExpress = new XDocument();
                //добавляю root в документ
                xml.Add(root);
                xmlExpress.Add(rootExpress);
                //сохраняю файл
                xml.Save(FILE_NAME_PRIMARY);
                xmlExpress.Save(FILE_NAME_EXPRESS);
                return true;
            });
            //если файл сгенерирован и его размер ок
            if (await gen && new FileInfo(FILE_NAME_PRIMARY).Length > await DB.GetParamIntAsync("yandex.xmlMinSize")) {
                //отправляю файл на сервер
                await SftpClient.FtpUploadAsync(FILE_NAME_PRIMARY);
                await SftpClient.FtpUploadAsync(FILE_NAME_EXPRESS);
            } else
                Log.Add(LP + "файл не отправлен - ОШИБКА РАЗМЕРА ФАЙЛА!");
        }
        private float GetAmountExpress(RootObject b) { 
            var size = b.GetDimentions();
            if (size[0] > EXPRESS_MAX_LENGTH ||
                size[1] > EXPRESS_MAX_WIDTH ||
                size[2] > EXPRESS_MAX_HEIGHT ||
                b.GetWeight() > EXPRESS_MAX_WEIGHT) 
                return 0;
            return b.amount;
        }
        private int GetPrice(RootObject b) {
            var weight = b.GetWeight();
            var d = b.GetDimentions();
            var length = d[0] + d[1] + d[2];
            //наценка 25% на всё
            int overPrice = (int)(b.price * 0.25);
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
            //скидка на всё 3% и округление рублей до десятков в меньшую сторону
            //таким образом, скидка будет 3% или чуть более
            var newPrice = (int)((0.97*(b.price + overPrice)) / 10);
            return 10 * newPrice; 
        }
    }
}
