using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;
using Selen.Base;
using Selen.Tools;

namespace Selen.Sites {
    class YoulaXml {
        string filename = @"..\youla.xml";

        string satomUrl = "https://xn--80aejmkqfc6ab8a1b.xn--p1ai/yml-export/889dec0b799fb1c3efb2eb1ca4d7e41e/?full=1";
        string satomFile = @"..\satom_import.xml";
        XDocument satomYML;

        public YoulaXml() {
            //загружаю xml с satom: если файлу больше 6 часов - пытаюсь запросить новый, иначе загружаю с диска
            //if (File.Exists(satomFile) && File.GetLastWriteTime(satomFile).AddHours(6) < DateTime.Now) {
            //try {
            //satomYML = XDocument.Load(satomUrl);
            //satomYML.Save(satomFile);
            //return;
            //} catch (Exception x) {
            //  Log.Add("YoulaXml: ошибка запроса xml с satom.ru - " + x.Message);
            //}
            //}
            satomYML = XDocument.Load(satomFile);
        }
        //получаю прямые ссылки на фото из каталога сатом
        List<string> GetSatomPhotos(RootObject b) {
            var list = new List<string>();
            //проверка каталога xml
            if (satomYML.Root.Name != "yml_catalog")
                throw new Exception("GetSatomPhotos: ошибка чтения каталога satom! - корневой элемент не найден");
            //ищем товар в каталоге
            var offer = satomYML.Descendants("offer")?
                .First(w => w.Element("description").Value.Split(':').Last()
                             .Split('<').First().Trim() == b.id);
            //получаем фото
            if (offer == null)
                throw new Exception("GetSatomPhotos: оффер не найден в каталоге satom");
            list = offer.Elements("picture").Select(s => s.Value).ToList();
            //проверка наличия фото
            if (list.Count == 0)
                throw new Exception("GetSatomPhotos: фото не найдены в каталоге satom");
            return list;
        }


        //генерация xml
        public async Task GenerateXML(List<RootObject> _bus) {
            //количество объявлений в тарифе
            var offersLimit = await DB._db.GetParamIntAsync("youla.offersLimit");
            //доп. описание
            string[] _addDesc = JsonConvert.DeserializeObject<string[]>(
                    await DB._db.GetParamStrAsync("youla.addDescription"));
            //время в нужном формате
            var dt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            //создаю новый xml
            var xml = new XDocument();
            //корневой элемент yml_catalog
            var root = new XElement("yml_catalog", new XAttribute("date", dt));
            //создаю главный контейнер shop
            var shop = new XElement("shop");
            //создаю контейнер offers
            var offers = new XElement("offers");
            //список карточек с положительным остатком и ценой, у которых есть ссылка на юлу и фотографии
            //var bus = _bus.Where(w => w.amount > 0 && w.price > 0 && w.youla.Contains("http") && w.images.Count > 0);
            var bus = _bus.Where(w => w.amount > 0 && w.price > 1000 && !w.youla.Contains("http") && w.images.Count > 0);
            //для каждой карточки
            //foreach (var b in bus.Take(offersLimit)) {
            foreach (var b in bus.Take(10)) {
                try {
                    //id объявления на юле из ссылки
                    //var youlaId = b.youla.Split('-').Last();
                    //элемент offer
                    //var offer = new XElement("offer", new XAttribute("id", youlaId));
                    var offer = new XElement("offer", new XAttribute("id", b.id));
                    //категория товара
                    foreach (var item in GetCategory(b)) {
                        offer.Add(new XElement(item.Key, item.Value));
                    }
                    //адрес магазина
                    offer.Add(new XElement("adress", "Россия, Калуга, Московская улица, 331"));
                    //цена
                    offer.Add(new XElement("price", b.price));
                    //телефон
                    offer.Add(new XElement("phone", "8 920 899-45-45"));
                    //наименование
                    offer.Add(new XElement("name", b.NameLength(100)));
                    //изображения
                    foreach (var photo in GetSatomPhotos(b)) {
                        offer.Add(new XElement("picture", photo));
                    }
                    //описание
                    var d = b.DescriptionList(2990, _addDesc).Aggregate((a1, a2) => a1 + "\r\n" + a2);
                    offer.Add(new XElement("description", new XCData(d)));
                    //имя менеджера
                    offer.Add(new XElement("managerName", "Менеджер 1"));
                    //добавляю элемент offer в контейнер offers
                    offers.Add(offer);
                } catch (Exception x) {
                    Log.Add("GenerateXML: " + b.name + " - " + x.Message);
                }
            }
            //добавяю время генерации
            offers.Add(new XElement("generation-date", dt));
            //добавляю контейнер offers в shop
            shop.Add(offers);
            //добавляю shop в корневой элемент yml_catalog
            root.Add(shop);
            //добавляю root в документ
            xml.Add(root);
            //сохраняю файл
            xml.Save(filename);
        }
        public async Task GenerateXML_avito(List<RootObject> _bus) {
            await Task.Factory.StartNew(() =>{
                //количество объявлений в тарифе
                var offersLimit = DB._db.GetParamInt("youla.offersLimit");
                //доп. описание
                string[] _addDesc = JsonConvert.DeserializeObject<string[]>(
                        DB._db.GetParamStr("youla.addDescription"));
                //создаю новый xml
                var xml = new XDocument();
                //корневой элемент yml_catalog
                var root = new XElement("Ads", new XAttribute("formatVersion", "3"), new XAttribute("target", "Avito.ru"));
                //список карточек с положительным остатком и ценой, у которых есть ссылка на юлу и фотографии
                //var bus = _bus.Where(w => w.amount > 0 && w.price > 0 && w.youla.Contains("http") && w.images.Count > 0);
                var bus = _bus.Where(w => w.amount > 0 && w.price > 1000 && !w.youla.Contains("http") && w.images.Count > 0);
                //для каждой карточки
                foreach (var b in bus) {
                    try {
                        //id объявления на юле из ссылки
                        //var youlaId = b.youla.Split('-').Last();
                        //элемент offer
                        //var offer = new XElement("offer", new XAttribute("id", youlaId));
                        var ad = new XElement("Ad");
                        //id
                        ad.Add(new XElement("Id", b.id));
                        //категория товара
                        foreach (var item in GetCategoryAvito(b)) {
                            ad.Add(new XElement(item.Key, item.Value));
                        }
                        //адрес магазина
                        ad.Add(new XElement("Address", "Россия, Калуга, Московская улица, 331"));
                        //цена
                        ad.Add(new XElement("Price", b.price));
                        //телефон
                        ad.Add(new XElement("ContactPhone", "8 920 899-45-45"));
                        //наименование
                        ad.Add(new XElement("Title", b.NameLength(100)));
                        //изображения
                        var images = new XElement("Images");
                        foreach (var photo in GetSatomPhotos(b)) {
                            images.Add(new XElement("Image", new XAttribute("url", photo)));
                        }
                        ad.Add(images);
                        //описание
                        var d = b.DescriptionList(2990, _addDesc).Aggregate((a1, a2) => a1 + "\r\n" + a2);
                        ad.Add(new XElement("Description", new XCData(d)));
                        //имя менеджера
                        ad.Add(new XElement("ManagerName", "Менеджер 1"));
                        //добавляю элемент offer в контейнер offers
                        root.Add(ad);
                        //ограничение позиций
                        if (--offersLimit == 0)
                            break;
                    } catch (Exception x) {
                        Log.Add("GenerateXML: " + offersLimit + " - " + b.name + " - " + x.Message);
                    }
                }
                Log.Add("выгружено " + (1000 - offersLimit));
                //добавляю root в документ
                xml.Add(root);
                //сохраняю файл
                xml.Save(filename);
            });
            //если размер файла в порядке
            if (new FileInfo(filename).Length > 1900000)
                //отправляю файл на сервер
                await SftpClient.FtpUploadAsync(filename);
        }
        //категории авито
        Dictionary<string, string> GetCategoryAvito(RootObject b) {
            var name = b.name.ToLowerInvariant();
            var d = new Dictionary<string, string>();
            //добавляю подкатегорию
            if (name.StartsWith("фиксаторы") ||
                name.StartsWith("набор фиксаторов") ||
                name.StartsWith("домкрат") ||
                name.StartsWith("масло ")) {
                //пока в исключениях
            }
            //добавляю подкатегорию
            else if (name.StartsWith("венец") ||
                name.Contains("коленвал") ||
                name.Contains("маховик")) {
                d.Add("TypeId", "16-833");                          //Коленвал, маховик
            } else if (name.Contains("фазорегулятор") ||
                name.StartsWith("клапан газорасп") ||
                name.StartsWith("крышка дв") ||
                name.StartsWith("крышка лоб") ||
                name.StartsWith("натяжитель") ||
                name.StartsWith("клапан выпуск") ||
                name.StartsWith("муфта vvti") ||
                name.StartsWith("клапан впуск")) {
                d.Add("TypeId", "16-841");                          //Ремни, цепи, элементы ГРМ
            } else if (name.StartsWith("акпп ") ||
                name.StartsWith("трубка сцепления") ||
                name.Contains("цилиндр сцепления") ||
                name.Contains("бачок сцепления") ||
                name.StartsWith("кардан ") ||
                name.StartsWith("раздатка ") ||
                name.StartsWith("кулиса ") ||
                name.StartsWith("трубка вакуумная") ||
                name.StartsWith("трубка акпп") ||
                name.StartsWith("механизм селектора") ||
                name.StartsWith("селектор акпп") ||
                name.StartsWith("механизм кулисы") ||
                name.StartsWith("редуктор заднего моста") ||
                name.StartsWith("выжимной") ||
                name.StartsWith("ступица") ||
                name.StartsWith("мкпп ")) {
                d.Add("TypeId", "11-629");                           //Трансмиссия и привод
            } else if (name.StartsWith("клапанная крышка") ||
                name.StartsWith("крышка клапанная")) {
                d.Add("TypeId", "16-832");                           //Клапанная крышка
            } else if (name.StartsWith("стекло ") ||
                name.StartsWith("форточ")) {
                d.Add("TypeId", "11-626");                           // Стекла
            } else if (name.StartsWith("бачок гур") ||
                name.StartsWith("комплект гур") ||
                (name.Contains("рейк") && name.Contains("рул")) ||
                name.StartsWith("бачок гидроусилителя") ||
                name.StartsWith("насос гур") ||
                (name.StartsWith("шкив") || name.Contains("трубк") || name.Contains("шланг")) && name.Contains("гур") ||
                name.StartsWith("крышка бачка гур") ||
                name.StartsWith("карданчик рул") ||
                name.Contains("кардан") && name.Contains("рул") ||
                name.StartsWith("вал рул") ||
                name.StartsWith("колонка рулевая") ||
                name.StartsWith("рулевой вал") ||
                name.StartsWith("рулевой ред") ||
                name.StartsWith("рулевая колонка") ||
                name.Contains("рулевая") && name.Contains("тяга") ||
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
                name.StartsWith("зеркальный") ||
                name.StartsWith("стекло зеркала")) {
                d.Add("TypeId", "16-812");                              //Зеркала
            } else if (name.StartsWith("замок бага") ||
                name.StartsWith("замок зад") ||
                name.StartsWith("замок кап") ||
                name.StartsWith("замок крыш") ||
                name.StartsWith("замок люч") ||
                name.Contains("часть замка") ||
                name.StartsWith("замок пер") ||
                name.StartsWith("центрального замка") ||
                name.StartsWith("замок стек") ||
                name.StartsWith("личинка ") ||
                name.StartsWith("комплект замка") ||
                name.StartsWith("замок две")) {
                d.Add("TypeId", "16-810");                              //Замки
            } else if (name.StartsWith("дверь")) {
                d.Add("TypeId", "16-808");                              //Двери
            } else if (name.StartsWith("бампер") ||
                name.StartsWith("абсорбер зад") ||
                name.StartsWith("абсорбер пер") ||
                name.StartsWith("наполнитель бампера") ||
                name.StartsWith("направляющая заднего бампера") ||
                name.StartsWith("поглотитель заднего бампера") ||
                name.StartsWith("пыльник заднего бампера") ||
                name.StartsWith("решетка бампера") ||
                name.StartsWith("спойлер пер. бампера") ||
                name.StartsWith("усилитель бампера") ||
                name.StartsWith("усилитель заднего бампера") ||
                name.StartsWith("усилитель переднего бампера") ||
                name.StartsWith("усилитель, поглотитель заднего бампера") ||
                name.StartsWith("заглушка бампера")) {
                d.Add("TypeId", "16-806");                              //Бамперы
            } else if (name.StartsWith("амортизатор баг") ||
                name.StartsWith("амортизатор две") ||
                name.StartsWith("амортизатор кап") ||
                name.Contains("телевизор") ||
                name.StartsWith("ручка") ||
                name.Contains("жабо") ||
                name.StartsWith("решетка стеклоочист") ||
                name.StartsWith("люк ") ||
                name.StartsWith("задняя часть") ||
                name.StartsWith("четверть ") ||
                name.StartsWith("локер ") ||
                name.StartsWith("подкрыл") ||
                name.StartsWith("корпус возд") ||
                name.StartsWith("лючок бензобака") ||
                name.StartsWith("панель передняя") ||
                name.StartsWith("задняя панель") ||
                name.StartsWith("рамка") && name.Contains("двери") ||
                name.StartsWith("рамка радиато") ||
                name.StartsWith("лючок топл") ||
                name.StartsWith("бачок") && name.Contains("омывателя") ||
                name.StartsWith("амортизатор крыш")) {
                d.Add("TypeId", "16-819");                              //Кузов по частям
            } else if (name.StartsWith("блок управления печкой") ||
                name.StartsWith("блок управления отоп") ||
                name.StartsWith("блок управления клим") ||
                name.StartsWith("блок клим") ||
                name.StartsWith("моторчик печки") ||
                name.Contains("вентилятор") ||
                name.StartsWith("диффузор вентилятора") ||
                name.StartsWith("клапан заслон") ||
                name.StartsWith("клапан печк") ||
                name.StartsWith("клапан конди") ||
                name.StartsWith("патрубок ") ||
                name.StartsWith("компрессор кондиц") ||
                name.Contains("помпа") ||
                name.StartsWith("моторчик отоп") ||
                name.Contains("термостат") ||
                name.StartsWith("корпус отопит") ||
                name.StartsWith("испаритель конд") ||
                name.StartsWith("радиатор") ||
                name.StartsWith("моторчики засл") ||
                name.StartsWith("тросы печки") ||
                name.StartsWith("диффузор") ||
                name.StartsWith("бачок расширительный") ||
                name.StartsWith("привод заслонки") ||
                name.StartsWith("трубка") && name.Contains("охла") ||
                name.StartsWith("трубка конд") ||
                name.StartsWith("фланец") && name.Contains("охлаждения") ||
                name.StartsWith("сервопривод заслонки") ||
                name.StartsWith("шкив помпы") ||
                name.StartsWith("моторчик засл")) {
                d.Add("TypeId", "16-521");                              //Система охлаждения
            } else if (name.StartsWith("корпус маслянн") ||
                 name.Contains("масло") ||
                 name.StartsWith("сапун") ||
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
            } else if (name.StartsWith("диск") &&
                (name.Contains("лит") || name.Contains("штам") || name.Contains(" r1"))) {
                d.Add("TypeId", "10-046");                          //Шины, диски и колёса / Диски
                d.Add("RimDiameter", b.GetDiskSize());              //Диаметр диска
                d.Add("RimOffset", "0");                            //TODO Вылет, пока ставим 0, добавить определение из описания
                d.Add("RimBolts", b.GetNumberOfHoles());            //количество отверстий
                d.Add("RimWidth", "5");                             //TODO ширина диска, пока 5, добавить определ. из описания
                d.Add("RimBoltsDiameter", b.GetDiameterOfHoles());  //диаметр расп. отверстий                                               
                d.Add("RimType", b.DiskType());                     //тип
            } else if (name.StartsWith("колпак")) {
                d.Add("TypeId", "10-044");                          //Шины, диски и колёса / Колпаки
            } else if (name.StartsWith("шины")) {
                d.Add("TypeId", "10-048");                          //Шины, диски и колёса / Шины
            } else if (name.StartsWith("фонар") ||
                name.StartsWith("плафон") ||
                name.StartsWith("корректор") ||
                name.StartsWith("патрон") ||
                name.StartsWith("фара") ||
                name.StartsWith("стоп доп") ||
                name.StartsWith("поворотник") ||
                name.StartsWith("повторитель") ||
                name.StartsWith("патрон повор") ||
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
                  name.StartsWith("расходомер") ||
                  name.StartsWith("эбу") ||
                  b.GroupName() == "Электрика, зажигание") {
                d.Add("TypeId", "11-630");                          //Электрооборудование
            } else if (name.StartsWith("ящик") ||
                  name.StartsWith("обшивка") ||
                  name.Contains("бардачок") ||
                  name.Contains("воздуховод") ||
                  name.Contains("кожух подрул") ||
                  name.Contains("кожух рул") ||
                  name.StartsWith("подушка безопасности") ||
                  name.StartsWith("ремень безопасности") ||
                  name.StartsWith("ремень задний") ||
                  name.StartsWith("ремни безопасности") ||
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
                  name.StartsWith("полка ") ||
                  name.StartsWith("рамка ") ||
                  name.StartsWith("педали") ||
                  name.Contains("корпус подрулевых") ||
                  name.Contains("козырек солнц") ||
                  name.Contains("регулятор") && name.Contains("ремня") ||
                  name.StartsWith("дефлектор") ||
                  name.StartsWith("сиденье") ||
                  name.StartsWith("сиденья") ||
                  name.StartsWith("часы") ||
                  name.Contains("карман")) {
                d.Add("TypeId", "11-625");                        //Салон
            } else if (name.Contains("дворник") ||
                 name.Contains("стеклоподъемник") ||
                 name.StartsWith("поводок") ||
                 name.StartsWith("стабилизатор") ||
                 name.StartsWith("порог ") ||
                 name.StartsWith("спойлер") ||
                 name.StartsWith("рейлинг") ||
                 name.StartsWith("трос ") ||
                 name.StartsWith("передняя панель") ||
                 name.StartsWith("фаркоп") ||
                 name.StartsWith("панель верхняя")) {
                d.Add("TypeId", "16-819");                        //Кузов по частям
            } else if (name.StartsWith("генератор") ||
                  name.Contains("втягивающее") ||
                  name.StartsWith("подшипник генератора") ||
                  name.StartsWith("обгонная муфта генератора") ||
                  name.StartsWith("шкив генератора") ||
                  name.StartsWith("диодный") ||
                  name.StartsWith("бендикс") ||
                  name.StartsWith("стартер")) {
                d.Add("TypeId", "16-829");                          // Генераторы, стартеры
            } else if (name.StartsWith("амортизатор") ||
                  name.StartsWith("кулак") ||
                  name.StartsWith("пружин") ||
                  name.StartsWith("стойка") ||
                  name.StartsWith("рычаг") ||
                  name.StartsWith("тяга ") ||
                  name.StartsWith("опора амортиз")) {
                d.Add("TypeId", "11-623");                          // Подвеска
            } else if (name.StartsWith("абсорбер топл") ||
                   name.StartsWith("бак топл") ||
                   name.StartsWith("воздуховод") ||
                   name.StartsWith("воздухозаборник") ||
                   name.StartsWith("глушитель") ||
                   name.StartsWith("горловина") ||
                   name.StartsWith("заслонка дросс") ||
                   name.StartsWith("корпус воздуш") ||
                   name.StartsWith("крышка корпуса фильтра") ||
                   name.StartsWith("инжектор") ||
                   name.StartsWith("катализатор") ||
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
                   name.StartsWith("насос топл") ||
                   name.StartsWith("пламегаситель") ||
                   name.StartsWith("гофра") ||
                   name.StartsWith("форсунк") ||
                   name.StartsWith("труба прием") ||
                   name.StartsWith("трубка абсор") ||
                   name.StartsWith("трубка вент") ||
                   name.StartsWith("резонатор воздуш") ||
                   name.StartsWith("ресивер воздуш") ||
                   name.StartsWith("трубка топл") ||
                   name.StartsWith("трубки тнвд") ||
                   name.StartsWith("трубка карт") ||
                   name.StartsWith("трубка егр") ||
                   name.StartsWith("моновпрыск") ||
                   name.Contains("коллектора") ||
                   name.StartsWith("бензобак")) {
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
                     name.StartsWith("поддон")) {
                d.Add("TypeId", "16-827");                          //Блок цилиндров, головка, картер
            } else if (name.StartsWith("двигатель")) {
                d.Add("TypeId", "16-830");                          //Двигатель в сборе
            } else if (name.StartsWith("катушка заж")) {
                d.Add("TypeId", "16-831");                          //Катушка зажигания, свечи, электрика
            } else if (name.StartsWith("молдинг") ||
                name.StartsWith("бархотк") ||
                name.StartsWith("декоративная крышка") ||
                name.StartsWith("накладк") ||
                name.StartsWith("эмблема")) {
                d.Add("TypeId", "16-822");                          //Молдинги, накладки
            } else if (name.StartsWith("брызговик") ||
                 name.StartsWith("накладк")) {
                d.Add("TypeId", "16-807");                          //Брызговики
            } else if (name.StartsWith("суппорт") ||
                 name.StartsWith("барабан тор") ||
                 name.StartsWith("бачок торм") ||
                 name.StartsWith("крышка бачка торм") ||
                 name.StartsWith("пластины бараб") ||
                 name.StartsWith("разветвитель торм") ||
                 name.StartsWith("цилиндр торм") ||
                 name.StartsWith("ремкомплект бараб") ||
                 name.StartsWith("усилитель торм") ||
                 name.StartsWith("усилитель вакуум") ||
                 name.StartsWith("распределитель торм") ||
                 name.Contains("abs") && (name.StartsWith("насос") || name.StartsWith("блок")) ||
                 name.StartsWith("диск торм") ||
                 name.StartsWith("скоба") && name.Contains("суппор")) {
                d.Add("TypeId", "11-628");                          //Тормозная система
            } else if (name.StartsWith("болт") ||
                  name.StartsWith("кронштейн") ||
                  name.StartsWith("гайка") ||
                  name.StartsWith("креплени") ||
                  name.StartsWith("опора") ||
                  name.StartsWith("подушка") ||
                  name.StartsWith("упор ") ||
                  name.StartsWith("торсион") ||
                  name.StartsWith("направляющая") ||
                  name.StartsWith("петл") ||
                  name.Contains("держатель")) {
                d.Add("TypeId", "16-815");                          //Крепления
            } else if (name.Contains("грм") ||
                   name.StartsWith("вал баланс") ||
                   name.StartsWith("муфта распр") ||
                   name.StartsWith("вал кором") ||
                   name.StartsWith("распредвал") ||
                   name.StartsWith("промежуточный вал") ||
                   name.StartsWith("вал пром") ||
                   name.StartsWith("шестерня пром") ||
                   name.StartsWith("шестерня распр") ||
                   name.StartsWith("шкив распр") ||
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
                name.StartsWith("тормозной щит") ||
                name.StartsWith("пыльник")) {
                d.Add("TypeId", "16-811");                          //Защита
            } else if (name.StartsWith("поршен") ||
                name.StartsWith("кольца пор") ||
                name.Contains("шатун")) {
                d.Add("TypeId", "16-838");                          //Поршни, шатуны, кольца
            } else if (name.StartsWith("крыша ")) {
                d.Add("TypeId", "16-817");                          //Крыша
            } else if (name.StartsWith("привод ") ||
                name.Contains("вал карданный")) {
                d.Add("TypeId", "11-629");                           //Трансмиссия и привод
            } else if (name.StartsWith("турбина ")) {
                d.Add("TypeId", "16-842");                           //Турбины, компрессоры
            } else if (b.GroupName() == "АВТОХИМИЯ") {
                d.Add("TypeId", "4-942");                           //Автокосметика и автохимия
            }
            //else d.Add("TypeId", "11-621");//Запчасти для ТО

            if (d.Count > 0)
                d.Add("Category", "Запчасти и аксессуары"); //главная категория
            else
                throw new Exception("avitoxml: " + b.name + " - не описана категория");
            return d;
        }

        //категория на юле
        Dictionary<string, string> GetCategory(RootObject b) {
            var name = b.name.ToLowerInvariant();
            var d = new Dictionary<string, string>();
            //основная категория
            if (name.Contains("кронштейн ") || name.Contains("опора") || name.Contains("креплен") || name.Contains("подушка д") || name.Contains("подушка к")) { //пропуск
            } else if (name.Contains("планк") || name.Contains("молдинг") || name.Contains("обшивк") || name.Contains("накладк") ||
                name.Contains("катафот") || name.Contains("прокладка") || name.Contains("сальник")) {//пропуск
            } else if ((name.Contains("трубк") || name.Contains("шланг")) &&
                (name.Contains("гур") || name.Contains("гидроусилител") ||
                name.Contains("высокого") || name.Contains("низкого"))) {
                d.Add("avtozapchasti_tip", "11875");//Рулевое управление
                d.Add("kuzovnaya_detal", "170899");//Шланг ГУР
            } else if (name.Contains("трубк") || name.Contains("шланг")) {//пропуск
            } else if (name.Contains("трос ")) {//пропуск
            } else if (name.Contains("состав") || name.Contains("масло ") || name.Contains("полирол")) {//пропуск
            } else if (name.Contains("ступица")) {
                d.Add("avtozapchasti_tip", "11877");//Подвеска
                d.Add("kuzovnaya_detal", "170884");//Ступица
                d.Add("chast_detali", "171085");//Ступица
            } else if (name.StartsWith("блок") && name.Contains("управлени") &&
                (name.Contains("печко") || name.Contains("отопит") || name.Contains("климат"))) {
                d.Add("avtozapchasti_tip", "11879");//Системы охлаждения, обогрева
                d.Add("kuzovnaya_detal", "170948");//Блок управления печкой
            } else if (name.StartsWith("коммутатор ")) {
                d.Add("avtozapchasti_tip", "171183");//Система зажигания
                d.Add("kuzovnaya_detal", "170936");//Коммутатор зажигания
            } else if (name.StartsWith("катушка") && name.Contains("зажиган")) {
                d.Add("avtozapchasti_tip", "171183");//Система зажигания
                d.Add("kuzovnaya_detal", "170935");//Катушка зажигания
            } else if (name.StartsWith("замок") && name.Contains("зажиган")) {
                d.Add("avtozapchasti_tip", "171183");//Система зажигания
                d.Add("kuzovnaya_detal", "170937");//Контактная группа
            } else if (name.Contains("заслонки") && (name.Contains("печк") || name.Contains("отопит"))) {
                d.Add("avtozapchasti_tip", "11879");//Системы охлаждения, обогрева
                d.Add("kuzovnaya_detal", "170953");//Сервопривод
            } else if (name.Contains("моторчик ") && (name.Contains("печк") || name.Contains("отопит"))) {
                d.Add("avtozapchasti_tip", "11879");//Системы охлаждения, обогрева
                d.Add("kuzovnaya_detal", "170950");//Моторчик печки
            } else if ((name.Contains("корпус ") || name.Contains("крышка ")) && name.Contains("термостат")) {
                d.Add("avtozapchasti_tip", "11879");//Системы охлаждения, обогрева
                d.Add("kuzovnaya_detal", "170947");//Термостат
                d.Add("chast_detali", "171114");//Термостат
            } else if (name.Contains("коллектор") && name.Contains("впускной")) {
                d.Add("avtozapchasti_tip", "11870");//Двигатель, ГРМ, турбина
                d.Add("kuzovnaya_detal", "170869");//Коллектор впускной
            } else if (name.Contains("проводка") || name.Contains("жгут провод")) {
                d.Add("avtozapchasti_tip", "11882");//Электрооборудование
                d.Add("kuzovnaya_detal", "171007");//Жгут проводов
            } else if (name.StartsWith("бампер ") && (name.Contains("перед") || name.Contains("задн"))) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "165678");//Бампер и комплектующие
                d.Add("chast_detali", "170676");//Бампер
            } else if (name.Contains("бампер") && name.Contains("усилитель")) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "165678");//Бампер и комплектующие
                d.Add("chast_detali", "170682");//Усилитель бампера
            } else if (name.Contains("бампер") && name.Contains("абсорбер")) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "165678");//Бампер и комплектующие
                d.Add("chast_detali", "170675");//Абсорбер бампера
            } else if (name.StartsWith("замок") && name.Contains("двери")) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "170448");//Замки
                d.Add("chast_detali", "170696");//Замок двери
            } else if (name.Contains("замок") && name.Contains("капота")) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "170448");//Замки
                d.Add("chast_detali", "170697");//Замок капота
            } else if (name.StartsWith("замок") && name.Contains("багажник")) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "170448");//Замки
                d.Add("chast_detali", "170694");//Замок багажника
            } else if (name.Contains("замка") && name.Contains("компрессор")) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "170448");//Замки
                d.Add("chast_detali", "170698");//Центральный замок
            } else if (name.StartsWith("крыло ") && (name.Contains("лев") || name.Contains("прав"))) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "165685");//Крылья и комплектующие
                d.Add("chast_detali", "170721");//Крылья
            } else if (name.StartsWith("крыша ")) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "170449");//Крыша и комплектующие
                d.Add("chast_detali", "170726");//Крыша
            } else if (name.Contains("вкладыш") && name.Contains("шатун")) {
                d.Add("avtozapchasti_tip", "11870");//Двигатель, ГРМ, турбина
                d.Add("kuzovnaya_detal", "170864");//Блок цилиндров и детали
                d.Add("chast_detali", "171032");//Полукольца
            } else if (name.Contains("насос") && name.Contains("топлив")) {
                d.Add("avtozapchasti_tip", "11872");//Топливная система
                d.Add("kuzovnaya_detal", "170963");//Бензонасос
                d.Add("chast_detali", "171117");//Бензонасос
            } else if (name.Contains("карбюратор")) {
                d.Add("avtozapchasti_tip", "11872");//Топливная система
                d.Add("kuzovnaya_detal", "170965");//Карбюратор
            } else if (name.Contains("шатун")) {
                d.Add("avtozapchasti_tip", "11870");//Двигатель, ГРМ, турбина
                d.Add("kuzovnaya_detal", "170864");//Блок цилиндров и детали
                d.Add("chast_detali", "171044");//Шатун
            } else if (name.Contains("заслонка") && name.Contains("дросс")) {
                d.Add("avtozapchasti_tip", "11872");//Топливная система
                d.Add("kuzovnaya_detal", "170964");//Дроссель
                d.Add("chast_detali", "171120");//Дроссельная заслонка
            } else if (name.Contains("стабилизатор") && (name.Contains("перед") || name.Contains("зад"))) {
                d.Add("avtozapchasti_tip", "11877");//Подвеска
                d.Add("kuzovnaya_detal", "170885");//Стабилизатор
                d.Add("chast_detali", "171078");//Стабилизатор
            } else if (name.Contains("рулев") && (name.Contains("карданч") || name.Contains("вал") || name.Contains("колонк"))) {
                d.Add("avtozapchasti_tip", "11875");//Рулевое управление
                d.Add("kuzovnaya_detal", "170897");//Рулевая колонка
            } else if (name.Contains("трапеция") && (name.Contains("дворник") || name.Contains("очистител"))) {
                d.Add("avtozapchasti_tip", "171184");//Система очистки
                d.Add("kuzovnaya_detal", "170960");//Трапеция дворников
            } else if (name.Contains("рем") && name.Contains("безопас")) {
                d.Add("avtozapchasti_tip", "165967");//Безопасность
                d.Add("kuzovnaya_detal", "170928");//Ремни безопасности
            } else if (name.Contains("расходомер") || name.Contains("дмрв") || name.Contains("кислорода") || name.Contains("лямбда")) {
                d.Add("avtozapchasti_tip", "11871");//Выхлопная система
                d.Add("kuzovnaya_detal", "170850");//Датчики
            } else if (name.Contains("глушитель")) {
                d.Add("avtozapchasti_tip", "11871");//Выхлопная система
                d.Add("kuzovnaya_detal", "170848");//Глушитель
            } else if (name.Contains("коллектор") && name.Contains("выпускной")) {
                d.Add("avtozapchasti_tip", "11871");//Выхлопная система
                d.Add("kuzovnaya_detal", "170853");//Коллектор выпускной
            } else if (name.Contains("клапан") && (name.Contains("егр") || name.Contains("egr"))) {
                d.Add("avtozapchasti_tip", "11871");//Выхлопная система
                d.Add("kuzovnaya_detal", "170861");//EGR/SCR система
            } else if ((name.Contains("блок ") || name.Contains("комплект ")) &&
                (name.Contains("управлени") || name.Contains("комфорта")) || name.Contains("ЭБУ ")) {
                d.Add("avtozapchasti_tip", "11882");//Электрооборудование
                d.Add("kuzovnaya_detal", "171006");//Блок управления
            } else if (name.Contains("привод") && (name.Contains("левый") || name.Contains("правый") || name.Contains("передн") || name.Contains("задни") || name.Contains("полуос"))) {
                d.Add("avtozapchasti_tip", "11881");//Трансмиссия, привод
                d.Add("kuzovnaya_detal", "170996");//Привод и дифференциал
                d.Add("chast_detali", "171151");//Приводной вал
            } else if (name.Contains("маховик") || name.Contains("венец")) {
                d.Add("avtozapchasti_tip", "11881");//Трансмиссия, привод
                d.Add("kuzovnaya_detal", "170997");//Сцепление
                d.Add("chast_detali", "171170");//Маховик
            } else if (name.Contains("диск") && name.Contains("сцеплен")) {
                d.Add("avtozapchasti_tip", "11881");//Трансмиссия, привод
                d.Add("kuzovnaya_detal", "170997");//Сцепление
                d.Add("chast_detali", "171166");//Диск сцепления
            } else if (name.Contains("противотум") && name.Contains("фара")) {
                d.Add("avtozapchasti_tip", "11868");//Автосвет, оптика
                d.Add("kuzovnaya_detal", "170925");//Противотуманная фара (ПТФ)
            } else if ((name.Contains("кулис") || name.Contains("переключения") || name.Contains("селектор")) &&
                name.Contains("кпп")) {
                d.Add("avtozapchasti_tip", "11881");//Трансмиссия, привод
                d.Add("kuzovnaya_detal", "170993");//Ручка КПП и кулиса
            } else if (name.Contains("мкпп")) {
                d.Add("avtozapchasti_tip", "11881");//Трансмиссия, привод
                d.Add("kuzovnaya_detal", "170984");//Коробка передач
                d.Add("chast_detali", "171133");//МКПП
            } else if (name.Contains("переключат") &&
                      (name.Contains("подрулев") || name.Contains("дворник") ||
                       name.Contains("поворот") || name.Contains("стеклоочист"))) {
                d.Add("avtozapchasti_tip", "11882");//Электрооборудование
                d.Add("kuzovnaya_detal", "171010");//Подрулевой переключатель
            } else if (name.Contains("фара")) {
                d.Add("avtozapchasti_tip", "11868");//Автосвет, оптика
                d.Add("kuzovnaya_detal", "170918");//Фара
            } else if (name.Contains("фонарь")) {
                d.Add("avtozapchasti_tip", "11868");//Автосвет, оптика
                d.Add("kuzovnaya_detal", "170919");//Задний фонарь
            } else if (name.Contains("компрессор кондиционера")) {
                d.Add("avtozapchasti_tip", "11879");//Системы охлаждения, обогрева
                d.Add("kuzovnaya_detal", "170944");//Детали кондиционера
                d.Add("chast_detali", "171100");//Компрессор кондиционера
            } else if (name.Contains("шкив") && name.Contains("коленвал")) {
                d.Add("avtozapchasti_tip", "11870");//Двигатель, ГРМ, турбина
                d.Add("kuzovnaya_detal", "170866");//ГРМ система и цепь
                d.Add("chast_detali", "171056");//Шкив коленвала
            } else if (name.Contains("коленвал")) {
                d.Add("avtozapchasti_tip", "11870");//Двигатель, ГРМ, турбина
                d.Add("kuzovnaya_detal", "170864");//Блок цилиндров и детали
                d.Add("chast_detali", "171026");//Коленвал
            } else if (name.Contains("балка ") &&
                      (name.Contains("зад") || name.Contains("передняя") || name.Contains("подмоторная"))) {
                d.Add("avtozapchasti_tip", "11877");//Подвеска
                d.Add("kuzovnaya_detal", "170873");//Балка
            } else if (name.Contains("кулак ") && (name.Contains("зад") || name.Contains("перед") ||
                name.Contains("поворотн") || name.Contains("правый") || name.Contains("левый"))) {
                d.Add("avtozapchasti_tip", "11877");//Подвеска
                d.Add("kuzovnaya_detal", "170887");//Поворотный кулак
            } else if (name.Contains("крышка") && name.Contains("багажник")) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "165686");//Багажник и комплектующие
                d.Add("chast_detali", "170688");//Дверь багажника
            } else if (name.Contains("стеклоподъемник")) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "165680");//Двери
                d.Add("chast_detali", "170736");//Стеклоподъемный механизм и комплектующие
            } else if (name.Contains("насос") && name.Contains("гур")) {
                d.Add("avtozapchasti_tip", "11875");//Рулевое управление
                d.Add("kuzovnaya_detal", "170895");//Гидроусилитель и электроусилитель
            } else if (name.Contains("панель") && name.Contains("прибор")) {
                d.Add("avtozapchasti_tip", "11876");//Салон, интерьер
                d.Add("kuzovnaya_detal", "170912");//Спидометр
            } else if (name.Contains("рулевая") && name.Contains("рейка")) {
                d.Add("avtozapchasti_tip", "11875");//Рулевое управление
                d.Add("kuzovnaya_detal", "170894");//Рулевая рейка
                d.Add("chast_detali", "171090");//Рулевая рейка
            } else if (name.Contains("стартер")) {
                d.Add("avtozapchasti_tip", "11882");//Электрооборудование
                d.Add("kuzovnaya_detal", "171014");//Стартер
                d.Add("chast_detali", "171705");//Стартер в сборе
            } else if (name.Contains("генератор")) {
                d.Add("avtozapchasti_tip", "11882");//Электрооборудование
                d.Add("kuzovnaya_detal", "171015");//Генератор
                d.Add("chast_detali", "171257");//Генератор в сборе
            } else if (name.Contains("кулиса")) {
                d.Add("avtozapchasti_tip", "11881");//Трансмиссия, привод
                d.Add("kuzovnaya_detal", "170993");//Ручка КПП и кулиса
            } else if (name.Contains("усилитель") && name.Contains("вакуумный")) {
                d.Add("avtozapchasti_tip", "11873");//Тормозная система
                d.Add("kuzovnaya_detal", "170982");//Тормозной цилиндр
            } else if (name.Contains("подрамник")) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "165688");//Силовые элементы
                d.Add("chast_detali", "170730");//Рама
            } else if (name.Contains("люк") && (name.Contains("крыш") || name.Contains("электр"))) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "170449");//Крыша и комплектующие
                d.Add("chast_detali", "170726");//Крыша
            } else if (name.Contains("зеркал") && (name.Contains("лево") || name.Contains("прав"))) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "165682");//Зеркала
                d.Add("chast_detali", "170704");//Боковые зеркала заднего вида
            } else if (name.Contains("форсун") && name.Contains("топлив")) {
                d.Add("avtozapchasti_tip", "11872");//Топливная система
                d.Add("kuzovnaya_detal", "170973");//Форсунка топливная
                d.Add("chast_detali", "171043");//Форсунка топливная
            } else if (name.Contains("вентилятор") && name.Contains("охлаждения")) {
                d.Add("avtozapchasti_tip", "11879");//Системы охлаждения, обогрева
                d.Add("kuzovnaya_detal", "170943");//Радиатор и детали
                d.Add("chast_detali", "171104");//Вентилятор радиатора
            } else if (name.Contains("подушка")) {
                d.Add("avtozapchasti_tip", "165967");//Безопасность
                d.Add("kuzovnaya_detal", "170927");//Подушка безопасности
            } else if (name.Contains("руль")) {
                d.Add("avtozapchasti_tip", "11875");//Рулевое управление
                d.Add("kuzovnaya_detal", "170898");//Руль
            } else if (name.Contains("сиденья ") && name.Contains("передние") ||
                       name.Contains("сиденье ") && name.Contains("переднее") ||
                       name.Contains("регулир") && name.Contains("сиденья") ||
                       name.Contains("сидень") && name.Contains("водител")) {
                d.Add("avtozapchasti_tip", "11876");//Салон, интерьер
                d.Add("kuzovnaya_detal", "170910");//Сиденья
            } else if (name.StartsWith("двигатель")) {
                d.Add("avtozapchasti_tip", "11870");//Двигатель, ГРМ, турбина
                d.Add("kuzovnaya_detal", "170863");//Двигатель в сборе
                d.Add("chast_detali", "171059");//Двигатель внутреннего сгорания
            } else if (name.StartsWith("дверь ")) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "165680");//Двери
                d.Add("chast_detali", "170733");//Дверь боковая
            } else if (name.Contains("капот ")) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "165683");//Капоты и комплектующие
                d.Add("chast_detali", "170713");//Капот
            } else if (name.Contains("акпп ")) {
                d.Add("avtozapchasti_tip", "11881");//Трансмиссия, привод
                d.Add("kuzovnaya_detal", "170984");//Коробка передач
                d.Add("chast_detali", "171132");//АКПП
            } else if (name.Contains("трамблер ")) {
                d.Add("avtozapchasti_tip", "171183");//Система зажигания
                d.Add("kuzovnaya_detal", "170942");//Трамблер
            } else if (name.Contains("реле ") && name.Contains("накала")) {
                d.Add("avtozapchasti_tip", "171183");//Система зажигания
                d.Add("kuzovnaya_detal", "170939");//Реле свечей накала
            } else if (name.Contains("стойк") && name.Contains("стабилизат")) {
                d.Add("avtozapchasti_tip", "11877");//Подвеска
                d.Add("kuzovnaya_detal", "170885");//Стабилизатор
                d.Add("chast_detali", "171079");//Стойки стабилизатора
            } else if (name.Contains("втулк") && name.Contains("стабилизат")) {
                d.Add("avtozapchasti_tip", "11877");//Подвеска
                d.Add("kuzovnaya_detal", "170885");//Стабилизатор
                d.Add("chast_detali", "171077");//Втулки стабилизатора
            } else if (name.Contains("стабилизатор ")) {
                d.Add("avtozapchasti_tip", "11877");//Подвеска
                d.Add("kuzovnaya_detal", "170885");//Стабилизатор
                d.Add("chast_detali", "171078");//Стабилизатор
            } else if ((name.Contains("стойка ") || name.Contains("амортизатор")) && (name.Contains("передн") || name.Contains("задн"))) {
                d.Add("avtozapchasti_tip", "11877");//Подвеска
                d.Add("kuzovnaya_detal", "170872");//Амортизаторы
            } else if (name.Contains("суппорт ") && (name.Contains("передн") || name.Contains("задн"))) {
                d.Add("avtozapchasti_tip", "11873");//Тормозная система
                d.Add("kuzovnaya_detal", "170980");//Суппорт
            } else if (name.Contains("блок ") && name.Contains("abs")) {
                d.Add("avtozapchasti_tip", "11873");//Тормозная система
                d.Add("kuzovnaya_detal", "170979");//Система ABS
            } else if ((name.Contains("стеклоочист") || name.Contains("дворник")) && name.Contains("решетк") || name.Contains("жабо ")) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "165683");//Капоты и комплектующие
                d.Add("chast_detali", "170716");//Решетка капота
            } else if ((name.Contains("стеклоочист") || name.Contains("дворник")) && name.Contains("мотор")) {
                d.Add("avtozapchasti_tip", "171184");//Система очистки
                d.Add("kuzovnaya_detal", "170957");//Мотор стеклоочистителя
            } else if (name.Contains("блок ") && name.Contains("предохранителей")) {
                d.Add("avtozapchasti_tip", "11882");//Электрооборудование
                d.Add("kuzovnaya_detal", "171005");//Блок предохранителей
            } else if (name.Contains("радиатор ") && name.Contains("охлаждения")) {
                d.Add("avtozapchasti_tip", "11879");//Системы охлаждения, обогрева
                d.Add("kuzovnaya_detal", "170943");//Радиатор и детали
                d.Add("chast_detali", "171111");//Радиатор охлаждения
            } else if (name.Contains("радиатор ") && name.Contains("кондиционера")) {
                d.Add("avtozapchasti_tip", "11879");//Системы охлаждения, обогрева
                d.Add("kuzovnaya_detal", "170943");//Радиатор и детали
                d.Add("chast_detali", "171110");//Радиатор кондиционера
            } else if (name.Contains("расширительный") && name.Contains("бачок")) {
                d.Add("avtozapchasti_tip", "11879");//Системы охлаждения, обогрева
                d.Add("kuzovnaya_detal", "170943");//Радиатор и детали
                d.Add("chast_detali", "171112");//Расширительный бачок
            } else if (name.Contains("бак ") && name.Contains("топливный") || name.Contains("бензобак")) {
                d.Add("avtozapchasti_tip", "11872");//Топливная система
                d.Add("kuzovnaya_detal", "170970");//Топливный бак
            } else if (name.Contains("вискомуфта ")) {
                d.Add("avtozapchasti_tip", "11879");//Системы охлаждения, обогрева
                d.Add("kuzovnaya_detal", "170943");//Радиатор и детали
                d.Add("chast_detali", "171105");//Вискомуфта
            } else if (name.Contains("крышка ") && (name.Contains("клапанная") || name.Contains("гбц"))) {
                d.Add("avtozapchasti_tip", "11870");//Двигатель, ГРМ, турбина
                d.Add("kuzovnaya_detal", "170865");//Клапанная крышка
                d.Add("chast_detali", "171061");//Клапанная крышка
            } else if (name.Contains("крышка ") && (name.Contains("двигателя") || name.Contains("двс"))) {
                d.Add("avtozapchasti_tip", "11870");//Двигатель, ГРМ, турбина
                d.Add("kuzovnaya_detal", "170871");//Корпус и крышки
                d.Add("chast_detali", "171066");//Крышка двигателя
            } else if (name.Contains("гбц ")) {
                d.Add("avtozapchasti_tip", "11870");//Двигатель, ГРМ, турбина
                d.Add("kuzovnaya_detal", "170864");//Блок цилиндров и детали
                d.Add("chast_detali", "171024");//Головка блока цилиндров
            } else if (name.Contains("турбина ")) {
                d.Add("avtozapchasti_tip", "11870");//Двигатель, ГРМ, турбина
                d.Add("kuzovnaya_detal", "170867");//Турбина, компрессор
                d.Add("chast_detali", "171064");//Турбина
            } else if (name.Contains("поддон ") &&
                (name.Contains("двс") || name.Contains("двигател") || name.Contains("масл"))) {
                d.Add("avtozapchasti_tip", "11870");//Двигатель, ГРМ, турбина
                d.Add("kuzovnaya_detal", "170868");//Прокладки и поддон
                d.Add("chast_detali", "171076");//Поддон картера
            } else if (name.Contains("турбокомпрессор ")) {
                d.Add("avtozapchasti_tip", "11870");//Двигатель, ГРМ, турбина
                d.Add("kuzovnaya_detal", "170867");//Турбина, компрессор
                d.Add("chast_detali", "171063");//Компрессор
            } else if (name.Contains("корпус ") && (name.Contains("воздушн"))) {
                d.Add("avtozapchasti_tip", "11870");//Двигатель, ГРМ, турбина
                d.Add("kuzovnaya_detal", "170871");//Корпус и крышки
                d.Add("chast_detali", "171065");//Корпус фильтра
            } else if ((name.Contains("порог ") || name.Contains("пороги ")) &&
                       (name.Contains("левы") || name.Contains("правы"))) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "165688");//Силовые элементы
                d.Add("chast_detali", "170729");//Пороги
            } else if (name.Contains("лонжерон ") || name.Contains("лонжероны ") ||
                       (name.Contains("морда"))) {
                d.Add("avtozapchasti_tip", "11874");//Кузовные запчасти
                d.Add("kuzovnaya_detal", "165688");//Силовые элементы
                d.Add("chast_detali", "170728");//Лонжероны
            } else if (name.Contains("стекло ")) {
                d.Add("avtozapchasti_tip", "11880");//Стекла
            } else if (name.StartsWith("диск ")) {
                d.Add("shiny_diski_tip", "165958"); //Диски
                d.Add("shiny_naznachenie", "11549");//Для легкового автомобиля
                d.Add("shiny_diametr", GetDiskDiam(b));//диаметр диска
                d.Add("disk_tip", name.Contains(" лит") ? "11555" //Литые
                                                        : name.Contains(" кован") ? "11554"//Кованые
                                                                                  : "11557");//Штампованные
                d.Add("diski_kolichestvo_otverstiy", GetNumHoles(b)); //количество отверстий
                d.Add("diski_diametr_raspolozheniya_otverstiy", GetDiameterOfHoles(b));//диаметр расположения отверстий
            }
            //если ничего не подошло - категория не описана, пропуск
            if (d.Count == 0) {
                Log.Add("youla.ru: не описана категория " + b.name);
                throw new Exception("не найдена категория");
            }
            //указываю главную категорию 
            d.Add("youlaCategoryId", "1");//Запчасти и автотовары
            //подкатегории
            if (name.StartsWith("диск "))
                d.Add("youlaSubcategoryId", "110"); //диски
            else {
                d.Add("youlaSubcategoryId", "109"); //запчасти
                d.Add("avtozapchasti_vid_transporta", "11883");//Для автомобилей
            }
            //состояние
            if (b.IsNew())
                d.Add("zapchast_sostoyanie", "165658");//Новые
            else
                d.Add("zapchast_sostoyanie", "165659");//Б/у
            //тип объявления
            d.Add("type_classified", "172363");//Магазин
            return d;
        }
        //диаметр диска для выгрузки
        string GetDiskDiam(RootObject b) {
            switch (b.GetDiskSize()) {
                case "13":
                    return "11500";
                case "14":
                    return "11501";
                case "15":
                    return "11502";
                case "15.5":
                    return "11504";
                case "16":
                    return "11505";
                case "16.5":
                    return "11506";
                case "17":
                    return "11507";
                case "17.5":
                    return "11508";
                case "18":
                    return "11509";
                case "19":
                    return "11510";
                case "19.5":
                    return "11511";
                case "20":
                    return "11512";
                case "21":
                    return "11513";
                case "22":
                    return "11514";
                case "23":
                    return "11516";
                default:
                    return "11501";
            }
        }
        //количество отверстий в дисках
        string GetNumHoles(RootObject b) {
            switch (b.GetNumberOfHoles()) {
                case "4":
                    return "11804";
                case "5":
                    return "11805";
                case "6":
                    return "11806";
                default:
                    return "11804";
            }
        }
        //расположение отверстий
        string GetDiameterOfHoles(RootObject b) {
            switch (b.GetDiameterOfHoles()) {
                case "98":
                    return "11810";
                case "100":
                    return "11811";
                case "105":
                    return "11812";
                case "108":
                    return "11813";
                case "110":
                    return "11814";
                case "112":
                    return "11815";
                case "114.3":
                    return "11816";
                case "115":
                    return "11817";
                case "118":
                    return "11818";
                case "120":
                    return "11819";
                default:
                    return "11811";
            }
        }

    }
}
