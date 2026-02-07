using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;
using Selen.Base;
using Selen.Tools;

namespace Selen.Sites {
    public class MegaMarket {

        readonly string L = "megamarket: ";
        readonly string FILE_PRIMARY_XML = @"..\data\megamarket\megamarket.xml";
        //readonly string FILE_COMPAIGNS = @"..\data\megamarket\compaigns.json";          //список магазинов
        readonly string FILE_RESERVES = @"..\data\megamarket\reserves.json";       //список сделанных резервов
                                                                                   //readonly string ACCESS_TOKEN = "Bearer y0_AgAAAAAQNtIKAAt1AQAAAAD-gMliAAAepMeJyz9OFY-kuMFylVX5_cYtQQ";
                                                                                   //public static string BasePath = "https://api.partner.market.yandex.ru";
                                                                                   //HttpClient _hc = new HttpClient();
                                                                                   //MarketCampaigns _campaigns;
        public bool IsSyncActive { get; set; }
        public MegaMarket() {
            //_hc.BaseAddress = new Uri(BasePath);
            IsSyncActive = false;
        }
        //генерация xml
        public async Task Sync() {
            if (!await DB.GetParamBoolAsync("megamarket.syncEnable")) {
                Log.Add($"{L} StartAsync: синхронизация отключена!");
                return;
            }
            IsSyncActive = true;
            while (Class365API.Status == SyncStatus.NeedUpdate)
                await Task.Delay(30000);
            var gen = Task.Factory.StartNew(() => {
                //доп. описание
                List<string> _addDesc = JsonConvert.DeserializeObject<List<string>>(
                    DB.GetParamStr("megamarket.addDescription"));
                //конвертирую время в необходимый формат 2022-12-11T17:26:06.6560855+03:00
                var timeNow = DateTime.Now.ToString("yyyy'-'MM'-'dd' 'HH':'mm':'ss");
                //var timeNow = XmlConvert.ToString(DateTime.Now, XmlDateTimeSerializationMode.Local);
                //старая цена из настроек
                var oldPriceProcent = DB.GetParamFloat("megamarket.oldPriceProcent");
                //обновляю вес товара по умолчанию
                GoodObject.UpdateDefaultWeight();
                //обновляю объем товара по умолчанию
                GoodObject.UpdateDefaultVolume();
                //обновляю срок годности товара по умолчанию
                GoodObject.UpdateDefaultValidity();
                //получаю список карточек - новые, с положительным остатком и ценой, у которых есть фотографии
                //отсортированный по цене вниз
                var bus = Class365API._bus.Where(w => w.New && w.Price > 0 &&
                                                 w.images.Count > 0 &&
                                                 (w.Amount > 0 ||
                                                 DateTime.Parse(w.updated).AddDays(5) > Class365API.LastScanTime ||
                                                 DateTime.Parse(w.updated_remains_prices).AddDays(5) > Class365API.LastScanTime))
                              .OrderByDescending(o => o.Price)
                              .Take(DB.GetParamInt("megamarket.uploadLimit")).ToList();
                Log.Add($"{L} найдено {bus.Count} потенциальных объявлений");
                var offers = new XElement("offers");
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
                            n.Contains("телевизор") || n.Contains("переделка") ||
                            b.GroupName.ToLowerInvariant() == "масла"
                            )
                            continue;
                        var offer = new XElement("offer", new XAttribute("id", b.id));
                        offer.Add(new XElement("name", b.NameLimit(510)));
                        var price = GetPrice(b);
                        offer.Add(new XElement("price", price));
                        //цена до скидки +10%
                        offer.Add(new XElement("oldprice", (Math.Ceiling((price * (1 + oldPriceProcent / 100)) / 100) * 100).ToString("F0")));
                        offer.Add(new XElement("categoryId", b.group_id));
                        foreach (var photo in b.images.Take(15))
                            offer.Add(new XElement("picture", photo.url));
                        offer.Add(new XElement("vat", "6"));//VAT_0, 5=NO_VAT

                        //<shipment - options >
                        //< option days = "1" order - before = "15" />
                        //</ shipment - options >

                        var vendor = b.GetManufacture();
                        if (vendor != null)
                            offer.Add(new XElement("vendor", vendor));
                        if (!string.IsNullOrEmpty(b.Part))  //артикул
                            offer.Add(new XElement("vendorCode", b.Part));

                        var description = b.DescriptionList(2990, _addDesc);
                        offer.Add(new XElement("description", description.Aggregate((a1, a2) => a1 + "\r\n" + a2)));
                        //offer.Add(new XElement("description", new XCData(description.Aggregate((a1, a2) => a1 + "\r\n" + a2))));

                        //количество 
                        var outlet = new XElement("outlet", new XAttribute("id", "AvtoTehnoshik40"), new XAttribute("instock", b.Amount));
                        var outlets = new XElement("outlets", outlet);
                        offer.Add(outlets);

                        offer.Add(new XElement("param", new XAttribute("name", "Вес"), new XAttribute("unit", "кг"), b.WeightString));
                        offer.Add(new XElement("param", new XAttribute("name", "Габариты"), new XAttribute("unit", "см"), $"{b.length} x {b.width} x {b.height}"));
                        if (b.GetManufactureCountry() != null)
                            offer.Add(new XElement("param", new XAttribute("name", "Страна изготовитель"), b.GetManufactureCountry()));
                        if (b.GetMaterial() != null)
                            offer.Add(new XElement("param", new XAttribute("name", "Материал"), b.GetMaterial()));


                        //если надо снять
                        //offer.Add(new XElement("disabled", b.Amount <= 0 ? "true" : "false"));
                        //добавляю оффер к офферам
                        offers.Add(offer);
                    } catch (Exception x) {
                        Log.Add(L + b.name + " - ОШИБКА ВЫГРУЗКИ! - " + x.Message);
                        if (DB.GetParamBool("alertSound"))
                            new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
                    }
                }
                Log.Add(L + "выгружено " + offers.Descendants("offer").Count());
                //создаю необходимый элемент <shop>
                var shop = new XElement("shop");
                shop.Add(new XElement("name", "АвтоТехноШик"));
                shop.Add(new XElement("company", "АвтоТехноШик"));
                shop.Add(new XElement("url", "http://автотехношик.рф"));
                //shop.Add(new XElement("platform", "Satom.ru"));
                //список групп, в которых нашлись товары
                var groupsIds = bus.Select(s => s.group_id).Distinct();
                //создаю необходимый элемент <categories>
                var categories = new XElement("categories");
                //заполняю категории
                foreach (var groupId in groupsIds) {
                    //исключение
                    //if (groupId == "2281135")// Инструменты (аренда)
                    //  continue;
                    categories.Add(new XElement("category", GoodObject.GetGroupName(groupId), new XAttribute("id", groupId)));
                }
                shop.Add(categories);
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
                xml.Save(FILE_PRIMARY_XML);
                return true;
            });
            //если файл сгенерирован и его размер ок
            if (await gen) { // && new FileInfo(FILE_PRIMARY_XML).Length > await DB.GetParamIntAsync("megamarket.xmlMinSize")) {
                //отправляю файл на сервер
                await SftpClient.FtpUploadAsync(FILE_PRIMARY_XML);
            } else {
                Log.Add(L + "файл не отправлен - ОШИБКА РАЗМЕРА ФАЙЛА!");
                if (DB.GetParamBool("alertSound"))
                    new System.Media.SoundPlayer(@"..\data\alarm.wav").Play();
            }
            IsSyncActive = false;
        }
        //цена продажи
        int GetPrice(GoodObject b) {
            var weight = b.Weight;
            var length = b.SizeSM("length", 5) + b.SizeSM("width", 5) + b.SizeSM("height", 5);
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
    }
}