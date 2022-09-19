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
        readonly string _l = "avitoXml: ";
        readonly string filename = @"..\avito.xml";
        readonly string satomUrl = "https://xn--80aejmkqfc6ab8a1b.xn--p1ai/yml-export/889dec0b799fb1c3efb2eb1ca4d7e41e/?full=1&save";
        //string satomUrl = "https://xn--80aejmkqfc6ab8a1b.xn--p1ai/yml-export/889dec0b799fb1c3efb2eb1ca4d7e41e/?full=1";
        //"https://автотехношик.рф/yml-export/889dec0b799fb1c3efb2eb1ca4d7e41e/?full=1&save"
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
                    Log.Add(_l+"ошибка запроса xml с satom.ru - " + x.Message);
                }
            }
            satomYML = XDocument.Load(satomFile);
            Log.Add(_l + "каталог загружен!");
        }
        //получаю прямые ссылки на фото из каталога сатом
        List<string> GetSatomPhotos(RootObject b) {
            //проверка каталога xml
            if (satomYML.Root.Name != "yml_catalog")
                throw new Exception("GetSatomPhotos: ошибка чтения каталога satom! - корневой элемент не найден");
            //ищем товар в каталоге
            XElement offer = null;
            var tmp = satomYML.Descendants("offer")
                              .Where(w => w.Element("description")?
                                           .Value?
                                           .Split(':')
                                           .Last()
                                           .Split('<')
                                           .First()
                                           .Trim() == b.id);
            if (tmp?.Count() > 0) offer = tmp.First();
            //получаем фото
            if (offer == null) {
                if (b.amount < 0)
                    return new List<string>();
                throw new Exception("оффер не найден в каталоге satom");
            }
            var list = offer.Elements("picture")
                            .Select(s => s.Value)
                            .Take(10)
                            .ToList();
            //проверка наличия фото
            if (list.Count == 0 && b.amount>0)
                throw new Exception("фото не найдены в каталоге satom");
            //сортировка фото - первое остается, остальные разворачиваем
            list.Reverse(1, list.Count - 1);
            return list;
        }
        //генерация xml
        public async Task GenerateXML(List<RootObject> _bus) {
            await Task.Factory.StartNew(() => {
                //загружаю xml с satom
                GetSatomXml();
                //количество объявлений в тарифе
                var offersLimit = DB._db.GetParamInt("avito.offersLimit");
                //ценовой порог
                var priceLevel = DB._db.GetParamInt("avito.priceLevel");
                //доп. описание
                string[] _addDesc = JsonConvert.DeserializeObject<string[]>(
                    DB._db.GetParamStr("avito.addDescription"));
                string[] _addDesc2 = JsonConvert.DeserializeObject<string[]>(
                        DB._db.GetParamStr("avito.addDescription2"));
                //создаю новый xml
                var xml = new XDocument();
                //корневой элемент yml_catalog
                var root = new XElement("Ads", new XAttribute("formatVersion", "3"), new XAttribute("target", "Avito.ru"));
                //список карточек с положительным остатком и ценой, у которых есть фотографии
                var bus = _bus.Where(w => w.price >= priceLevel && w.images.Count > 0
                                      && (w.amount > 0 || DateTime.Parse(w.updated).AddDays(5) > DateTime.Now))
                              .OrderByDescending(o => o.price);
                
                Log.Add(_l+"найдено " + bus.Count() + " потенциальных объявлений");
                //для каждой карточки
                int i=0;
                foreach (var b in bus) {
                    try {
                        var ad = new XElement("Ad");
                        //id
                        ad.Add(new XElement("Id", b.id));
                        //категория товара
                        foreach (var item in GetCategoryAvito(b)) {
                            ad.Add(new XElement(item.Key, item.Value));
                        }
                        //изображения
                        var images = new XElement("Images");
                        foreach (var photo in GetSatomPhotos(b)) {
                            images.Add(new XElement("Image", new XAttribute("url", photo)));
                        }
                        ad.Add(images);
                        //если надо снять
                        if (b.amount <= 0) {
                            ad.Add(new XElement("DateEnd", DateTime.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss")+"+03:00"));
                        }
                        //else {
                        //    ad.Add(new XElement("DateBegin", DateTime.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss")+"+03:00"));
                        //}
                        //адрес магазина
                        ad.Add(new XElement("Address", "Россия, Калуга, Московская улица, 331"));
                        //цена
                        ad.Add(new XElement("Price", b.price));
                        //телефон
                        ad.Add(new XElement("ContactPhone", "8 920 899-45-45"));
                        //наименование
                        ad.Add(new XElement("Title", b.NameLength(100)));
                        //описание
                        var d = b.DescriptionList(2990, _addDesc);
                        d.AddRange(_addDesc2);
                        ad.Add(new XElement("Description", new XCData(d.Aggregate((a1, a2) => a1 + "\r\n" + a2))));
                        //имя менеджера
                        ad.Add(new XElement("ManagerName", "Менеджер"));
                        //добавляю элемент offer в контейнер offers
                        ad.Add(new XElement("AdType", "Товар приобретен на продажу"));
                        //состояние
                        ad.Add(new XElement("Condition", b.IsNew() ? "Новое" : "Б/у"));
                        //доступность
                        ad.Add(new XElement("Availability", "В наличии"));
                        //номер запчасти
                        if (!string.IsNullOrEmpty(b.part))
                            ad.Add(new XElement("OEM", b.part));
                        //оригинальность
                        ad.Add(new XElement("Originality", b.IsOrigin() ? "Оригинал" : "Аналог"));
                        //добавляю объявление в дерево
                        root.Add(ad);
                        //считаем только объявления с остатком
                        if (b.amount > 0)
                            i++;
                        if (i >= offersLimit)
                            break;
                    } catch (Exception x) {
                        Log.Add(_l+ i + " - " + b.name + " - " + x.Message);
                    }
                }
                Log.Add(_l+"выгружено " + i);
                //добавляю root в документ
                xml.Add(root);
                //сохраняю файл
                xml.Save(filename);
            });
            //если размер файла в порядке
            if(new FileInfo(filename).Length > await DB._db.GetParamIntAsync("avito.xmlMinSize"))
            //отправляю файл на сервер
                await SftpClient.FtpUploadAsync(filename);
        }
        //категории авито
        public static Dictionary<string, string> GetCategoryAvito(RootObject b) {
            var name = b.name.ToLowerInvariant();
            var d = new Dictionary<string, string>();
            if (name.StartsWith("масло ")) {
                //пока в исключениях
            } else if (name.StartsWith("фиксаторы") ||
                name.StartsWith("набор фиксаторов") ||
                name.StartsWith("домкрат") ||
                name.StartsWith("ключ балонный") ||
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
                name.StartsWith("крышка лоб") ||
                name.StartsWith("натяжитель") ||
                name.StartsWith("клапан выпуск") ||
                name.StartsWith("крышка сапуна") ||
                name.StartsWith("муфта vvti") ||
                name.StartsWith("клапан впуск")) {
                d.Add("TypeId", "16-841");                          //Ремни, цепи, элементы ГРМ
            } else if (name.StartsWith("акпп ") ||
                (name.StartsWith("трубк")||
                 name.StartsWith("диск") ||
                 name.Contains("комплект") ||
                 name.Contains("к-т")
                 ) && name.Contains("сцеплен") ||
                name.StartsWith("гидротрансформатор акпп") ||
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
                name.Contains("выжимной") ||
                name.StartsWith("корзина") ||
                name.StartsWith("переделка с акпп") ||
                name.StartsWith("соленоид акпп") ||
                name.StartsWith("ступица") ||
                name.StartsWith("подшипник ступичный") ||
                name.StartsWith("Подшипник выжимной") || 
                name.StartsWith("фланец") && name.Contains("раздат") ||
                name.StartsWith("фланец") && name.Contains("кардан") ||
                name.StartsWith("шарнир штока кпп") ||
                name.StartsWith("фильтр акпп") ||
                name.StartsWith("шрус") ||
                name.StartsWith("цапфа") ||
                name.StartsWith("пластина мкпп") ||
                name.StartsWith("мкпп ")) {
                d.Add("TypeId", "11-629");                           //Трансмиссия и привод
            } else if (name.StartsWith("клапанная крышка") ||
                name.StartsWith("крышка клапанная") ||
                name.StartsWith("крышка гбц")) {
                d.Add("TypeId", "16-832");                           //Клапанная крышка
            } else if (name.StartsWith("стекло ") ||
                name.StartsWith("форточ")) {
                d.Add("TypeId", "11-626");                           // Стекла
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
                name.StartsWith("рулевая колонка") ||
                name.StartsWith("насос усилителя руля") ||
                name.StartsWith("трубка гуp обратка") ||
                name.StartsWith("крышка эур") ||
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
                name.Contains("часть замка") ||
                name.StartsWith("замок пер") ||
                name.Contains("централь") && name.Contains("замка") ||
                name.Contains("крю") && name.Contains("капота") ||
                name.StartsWith("фиксатор") && (name.Contains("капота") || name.Contains("замка")) ||
                name.StartsWith("замок стек") ||
                name.StartsWith("личинк") ||
                name.StartsWith("комплект замка") ||
                name.StartsWith("замок") && name.Contains("двер")) {
                d.Add("TypeId", "16-810");                              //Замки
            } else if (name.StartsWith("дверь") ||
                name.StartsWith("панель двери")) {
                d.Add("TypeId", "16-808");                              //Двери
            } else if (name.StartsWith("бампер") ||
                name.StartsWith("абсорбер зад") ||
                name.StartsWith("абсорбер пер") || 
                name.StartsWith("решетка птф") ||                
                (name.StartsWith("решетк")|| 
                 name.StartsWith("спойлер")||
                 name.StartsWith("поглотитель") ||
                 name.StartsWith("усилитель")||
                 name.StartsWith("заглушка")||
                 name.StartsWith("направляющ")||
                 name.StartsWith("пыльник") ||
                 name.StartsWith("буфер")||
                 name.StartsWith("боковина") ||
                 name.StartsWith("наполнитель")||
                 name.StartsWith("абсорбер")
                ) && name.Contains("бампер")) {
                d.Add("TypeId", "16-806");                              //Бамперы
            } else if (name.StartsWith("амортизатор баг") ||
                name.StartsWith("амортизатор две") ||
                name.StartsWith("амортизатор кап") ||
                name.Contains("телевизор") ||
                name.StartsWith("ручка") ||
                name.Contains("жабо") ||
                name.StartsWith("решетка стеклоочист") ||
                name.StartsWith("дождевик") ||
                name.StartsWith("люк ") ||
                name.StartsWith("задняя часть") ||
                name.StartsWith("четверть ") ||
                name.StartsWith("локер ") ||
                name.StartsWith("траверса ") ||
                name.StartsWith("ус ") ||
                name.StartsWith("ресничка фары") ||
                name.StartsWith("планк") && name.Contains("фар") ||
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
            } else if (name.StartsWith("блок управления печкой") ||
                name.StartsWith("блок управления отоп") ||
                name.StartsWith("блок управления клим") ||
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
                name.StartsWith("моторчик отоп") ||
                name.Contains("термостат") ||
                name.StartsWith("корпус отопит") ||
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
                name.StartsWith("сервопривод") && 
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
                name.StartsWith("моторчик засл")) {
                d.Add("TypeId", "16-521");                              //Система охлаждения
            } else if (name.StartsWith("корпус маслянн") ||
                 name.Contains("масло") ||
                 name.StartsWith("сапун") ||
                 name.StartsWith("охладитель масл") ||
                 name.StartsWith("втулка масл") ||
                 name.StartsWith("корпус регулятора масла") ||
                 name.StartsWith("крышка заливная") ||
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
            } else if (name.StartsWith("диск торм")) {
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
            } else if (name.StartsWith("шины")||
                       name.StartsWith("резина")) {
                d.Add("TypeId", "10-048");                          //Шины, диски и колёса / Шины
            } else if (name.StartsWith("фонар") ||
                name.StartsWith("плафон") ||
                name.StartsWith("корректор") ||
                name.StartsWith("патрон") ||
                name.StartsWith("фара") ||
                name.StartsWith("подсветка") && name.Contains("номера") ||
                name.StartsWith("стоп доп") ||
                name.StartsWith("поворотник") ||
                name.StartsWith("повторитель") ||
                name.StartsWith("патрон повор") ||
                name.StartsWith("катафот") ||
                name.Contains("фонар") &&
                (name.StartsWith("плата") ||
                name.StartsWith("провод") ||
                name.StartsWith("крышка фары") ||
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
                  name.Contains("контактная") && name.Contains("группа") ||
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
                  name.StartsWith("подголовник") ||
                  name.StartsWith("треугольник задний") ||
                  name.StartsWith("лоток для домкрата") ||
                  name.StartsWith("облицовка рычага") ||
                  name.StartsWith("обшивки дверей") ||
                  name.StartsWith("ограничитель двери") ||
                  name.StartsWith("механизм регулировки ремня") ||
                  name.StartsWith("панель блока печки") ||
                  name.StartsWith("панель кожух") ||
                  name.StartsWith("решетка динамика") ||
                  name.StartsWith("ответная часть ремня") ||
                  name.StartsWith("крючок для одежды") ||
                  name.StartsWith("заглушка крепления сидения") ||
                  name.StartsWith("крышка запаски") ||
                  name.StartsWith("уплотнитель стекла") ||
                  name.StartsWith("заглушка болта запасного колеса") ||
                  name.StartsWith("кожух") && name.Contains("ремня безопасности") ||
                  name.StartsWith("крышка") && (
                    name.Contains("приборной панели") ||
                    name.Contains("подушки безопасности") ||
                    name.Contains("центральной консоли")) ||
                  name.StartsWith("часы") ||
                  name.StartsWith("шторка") ||
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
                 name.StartsWith("мотор переднего стеклоочистителя") ||
                 name.StartsWith("фаркоп") ||
                 name.StartsWith("ниша запасного колеса") ||
                 name.StartsWith("панель верхняя")) {
                d.Add("TypeId", "16-819");                        //Кузов по частям
            } else if (name.StartsWith("генератор") ||
                  name.Contains("втягивающее") ||
                  name.StartsWith("подшипник генератора") ||
                  name.StartsWith("обгонная муфта генератора") ||
                  name.StartsWith("шкив генератора") ||
                  name.StartsWith("диодный") ||
                  name.StartsWith("бендикс") ||
                  name.StartsWith("стартер") ||
                  name.StartsWith("щетки стартера")) {
                d.Add("TypeId", "16-829");                          // Генераторы, стартеры
            } else if (name.StartsWith("амортизатор") ||
                  name.StartsWith("кулак") ||
                  name.StartsWith("пружин") ||
                  name.StartsWith("стойка") ||
                  name.StartsWith("рычаг") ||
                  name.StartsWith("тарелка пружины") ||
                  name.StartsWith("тяга ") ||
                  name.StartsWith("опора амортиз")) {
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
                   name.StartsWith("крышка корпуса фильтра") ||
                   name.StartsWith("инжектор") ||
                   name.Contains("катализатор") ||
                   name.StartsWith("шланг") && name.Contains("топлив") ||
                   name.Contains("труба") && name.Contains("промежуточная") ||
                   name.Contains("труба") && name.Contains("приемная") ||
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
                   name.Contains("фильтр") && name.Contains("топлив") ||
                   name.StartsWith("крышка") && name.Contains("воздушного") ||
                   name.StartsWith("площадка") && name.Contains("приемной трубы") ||
                   name.StartsWith("потенциометр") && name.Contains("заслонки") ||
                   name.StartsWith("шланг") && name.Contains("картер") ||
                   name.StartsWith("бачок вакуумный") ||
                   name.StartsWith("моновпрыск") ||
                   name.StartsWith("рампа топливная") ||
                   name.StartsWith("ремонтная площадка") ||
                   name.StartsWith("насос вторичного воздуха") ||
                   name.StartsWith("трубки вентиляции картера") ||
                   name.StartsWith("потенциометр дроссельной заслонки") ||
                   name.StartsWith("труба соединительная") ||
                   name.StartsWith("насадка глушителя") ||
                   name.StartsWith("обратный клапан") ||
                   name.StartsWith("тнвд") ||
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
                name.StartsWith("крышка аккумулятора") ||
                name.StartsWith("крышка омывателя фары") ||
                name.StartsWith("надпись ") ||
                name.StartsWith("заглушка противотуманных") ||
                name.StartsWith("накладк") ||
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
                 name.StartsWith("цилиндр торм") ||
                 name.StartsWith("электро ручник") ||
                 name.StartsWith("ремкомплект бараб") ||
                 name.StartsWith("трубк") && (name.Contains("тормоз") || name.Contains("вакуум")) ||
                 name.StartsWith("усилитель торм") ||
                 name.StartsWith("усилитель вакуум") ||
                 name.Contains("барабан") && name.Contains("тормоз")||
                 name.StartsWith("бачок гтц") ||
                 name.StartsWith("штуцер прокачки") ||
                 name.StartsWith("распределитель торм") ||
                 name.Contains("abs") && (name.StartsWith("насос") || name.StartsWith("блок")) ||
                 name.StartsWith("скоба") && name.Contains("суппор")) {
                d.Add("TypeId", "11-628");                          //Тормозная система
            } else if (name.StartsWith("болт") ||
                  name.StartsWith("кронштейн") ||
                  name.StartsWith("гайка") ||
                  name.StartsWith("креплени") ||
                  name.StartsWith("опора") ||
                  name.StartsWith("подушка") ||
                  name.StartsWith("площадка ак") ||
                  name.StartsWith("упор ") ||
                  name.StartsWith("гидроопора дв") ||
                  name.StartsWith("гидро опора дв") ||
                  name.StartsWith("торсион") ||
                  name.StartsWith("штырь") && name.Contains("капот") ||
                  name.StartsWith("хомут") && name.Contains("бака") ||
                  name.StartsWith("фиксатор") && name.Contains("стекл") ||
                  name.StartsWith("ограничит") && name.Contains("двер") ||
                  name.StartsWith("направляющая") ||
                  name.StartsWith("петл") ||
                  name.StartsWith("скобы приёмной трубы") ||
                  name.StartsWith("скоба лампы") ||
                  name.StartsWith("сайлентблок") ||
                  name.StartsWith("крюк буксировочный") ||
                  name.StartsWith("ролик") && name.Contains("двер") ||
                  name.StartsWith("механизм") && name.Contains("двер") ||
                  name.StartsWith("салазка") && name.Contains("двер") ||
                  name.Contains("держатель")) {
                d.Add("TypeId", "16-815");                          //Крепления
            } else if (name.Contains("грм") ||
                   name.StartsWith("ролик") && (name.Contains("натяж") || name.Contains("ремн")) ||
                   name.StartsWith("вал баланс") ||
                   name.StartsWith("муфта распр") ||
                   name.StartsWith("ванос") ||
                   name.StartsWith("вал кором") ||
                   name.StartsWith("распредвал") ||
                   name.StartsWith("промежуточный вал") ||
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
            } else if (name.StartsWith("турбина ") ||
                name.StartsWith("трубк") && name.Contains("турб")
                ) {
                d.Add("TypeId", "16-842");                           //Турбины, компрессоры
            } else if (b.GroupName() == "АВТОХИМИЯ") {
                d.Add("TypeId", "4-942");                           //Автокосметика и автохимия
            } else if (name.StartsWith("прокладка")||
                name.StartsWith("ремкомплект задних колодок") ||
                name.StartsWith("комплект прокладок") ||
                name.StartsWith("сальник привода")) {
                d.Add("TypeId", "11-621");                          //Запчасти для ТО
            }

            if (d.Count > 0)
                d.Add("Category", "Запчасти и аксессуары"); //главная категория
            else
                throw new Exception("категория не определена!");
            return d;
        }
    }
}
