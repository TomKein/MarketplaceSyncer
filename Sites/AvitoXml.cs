using Newtonsoft.Json;
using Selen.Base;
using Selen.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Selen.Sites {
    internal class AvitoXml {

        string filename = @"..\avito.xml";

        string satomUrl = "https://xn--80aejmkqfc6ab8a1b.xn--p1ai/yml-export/889dec0b799fb1c3efb2eb1ca4d7e41e/?full=1";
        string satomFile = @"..\satom_import.xml";
        XDocument satomYML;

        public AvitoXml() {
            //загружаю xml с satom: если файлу больше 6 часов - пытаюсь запросить новый, иначе загружаю с диска
            if (File.Exists(satomFile) && File.GetLastWriteTime(satomFile).AddHours(6) < DateTime.Now) {
                try {
                    satomYML = XDocument.Load(satomUrl);
                    if (satomYML.Nodes().Count() > 10000) 
                        satomYML.Save(satomFile);
                    else throw new Exception("мало элементов");
                    return;
                } catch (Exception x) {
                    Log.Add("YoulaXml: ошибка запроса xml с satom.ru - " + x.Message);
                }
            }
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
            var offersLimit = await DB._db.GetParamIntAsync("avito.offersLimit");
            //доп. описание
            string[] _addDesc = JsonConvert.DeserializeObject<string[]>(
                    await DB._db.GetParamStrAsync("avito.addDescription"));
            string[] _addDesc2 = JsonConvert.DeserializeObject<string[]>(
                    await DB._db.GetParamStrAsync("avito.addDescription2"));
            //создаю новый xml
            var xml = new XDocument();
            //корневой элемент yml_catalog
            var root = new XElement("Ads", new XAttribute("formatVersion", "3"), new XAttribute("target", "Avito.ru"));
            //список карточек с положительным остатком и ценой, у которых есть фотографии
            //var bus = _bus.Where(w => w.amount > 0 && w.price > 0 && w.youla.Contains("http") && w.images.Count > 0);
            var bus = _bus.Where(w => w.amount > 0 && w.price > 500 && !w.youla.Contains("http") && w.images.Count > 0);
            //для каждой карточки
            foreach (var b in bus.Take(offersLimit)) {
                try {
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
                    var d = b.DescriptionList(2990, _addDesc);
                    d.AddRange(_addDesc2);
                    ad.Add(new XElement("Description", new XCData(d.Aggregate((a1, a2) => a1 + "\r\n" + a2))));
                    //имя менеджера
                    ad.Add(new XElement("ManagerName", "Менеджер 1"));
                    //добавляю элемент offer в контейнер offers
                    ad.Add(new XElement("AdType", "Товар приобретен на продажу"));

                    root.Add(ad);
                } catch (Exception x) {
                    Log.Add("GenerateXML: " + b.name + " - " + x.Message);
                }
            }
            //добавляю root в документ
            xml.Add(root);
            //сохраняю файл
            xml.Save(filename);
        }
        //категории авито
        Dictionary<string, string> GetCategoryAvito(RootObject b) {
            var name = b.name.ToLowerInvariant();
            var d = new Dictionary<string, string>();
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
                name.Contains("кардан")&&name.Contains("рул") ||
                name.StartsWith("вал рул") ||
                name.StartsWith("колонка рулевая") ||
                name.StartsWith("рулевой вал") ||
                name.StartsWith("рулевой ред") ||
                name.StartsWith("рулевая колонка") ||
                name.Contains("рулевая")&&name.Contains("тяга") ||
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
                name.StartsWith("рамка")&&name.Contains("двери") ||
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
                name.StartsWith("трубка")&&name.Contains("охла") ||
                name.StartsWith("трубка конд") ||
                name.StartsWith("фланец")&&name.Contains("охлаждения") ||
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
                d.Add("RimBolts", "5");                             //TODO ширина диска, пока 0, добавить определ. из описания
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
                d.Add("RimBolts", "5");                             //TODO ширина диска, пока 0, добавить определ. из описания
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
                  name.StartsWith("рычаг")&&name.Contains("кпп") ||
                  name.StartsWith("рычаг")&&name.Contains("тормоз") ||
                  name.StartsWith("рычаг")&&name.Contains("руля") ||
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
                  name.Contains("регулятор")&&name.Contains("ремня") ||
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
                name.StartsWith("накладк")||
                name.StartsWith("эмблема")) {
                d.Add("TypeId", "16-822");                          //Молдинги, накладки
            } else if (name.StartsWith("брызговик") ||
                 name.StartsWith("накладк")) {
                d.Add("TypeId", "16-807");                          //Брызговики
            } else if (name.Contains("блок abs") ||
                 name.StartsWith("суппорт") ||
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
            } else if (name.StartsWith("защита")||
                name.Contains("экран")&&name.Contains("тепл") ||
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
                name.Contains("вал карданный")){
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
    }
}
