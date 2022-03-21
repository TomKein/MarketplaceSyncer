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
            if (File.Exists(satomFile) && File.GetLastWriteTime(satomFile).AddHours(6) < DateTime.Now) {
                try {
                    satomYML = XDocument.Load(satomUrl);
                    satomYML.Save(satomFile);
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
            var root = new XElement("Ads", new XAttribute("formatVersion", "3"), new XAttribute("target", "Avito.ru"));
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
                    var ad = new XElement("Ad");
                    //id
                    ad.Add(new XElement("Id", b.id));
                    //категория товара
                    foreach (var item in GetCategory(b)) {
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
                        images.Add(new XElement("Image", new XAttribute("url",photo)));
                    }
                    ad.Add(images);
                    //описание
                    var d = b.DescriptionList(2990, _addDesc).Aggregate((a1, a2) => a1 + "\r\n" + a2);
                    ad.Add(new XElement("Description", new XCData(d)));
                    //имя менеджера
                    ad.Add(new XElement("ManagerName", "Менеджер 1"));
                    //добавляю элемент offer в контейнер offers
                    root.Add(ad);
                } catch (Exception x) {
                    Log.Add("GenerateXML: " + b.name + " - " + x.Message);
                }
            }
            //добавяю время генерации
            //offers.Add(new XElement("generation-date", dt));
            //добавляю контейнер offers в shop
            //shop.Add(offers);
            //добавляю shop в корневой элемент yml_catalog
            //root.Add(shop);
            //добавляю root в документ
            xml.Add(root);
            //сохраняю файл
            xml.Save(filename);
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
                case "13":return "11500";
                case "14":return "11501";
                case "15":return "11502";
                case "15.5":return "11504";
                case "16":return "11505";
                case "16.5":return "11506";
                case "17":return "11507";
                case "17.5":return "11508";
                case "18":return "11509";
                case "19":return "11510";
                case "19.5":return "11511";
                case "20":return "11512";
                case "21":return "11513";
                case "22":return "11514";
                case "23":return "11516";
            default:
                   return "11501";
            }
        }
        //количество отверстий в дисках
        string GetNumHoles(RootObject b) {
            switch (b.GetNumberOfHoles()) {
                case "4":return "11804";
                case "5":return "11805";
                case "6":return "11806";
                default:
                    return "11804";
            }
        }
        //расположение отверстий
        string GetDiameterOfHoles(RootObject b) {
            switch (b.GetDiameterOfHoles()) {
                case "98":return "11810";
                case "100":return "11811";
                case "105":return "11812";
                case "108":return "11813";
                case "110":return "11814";
                case "112":return "11815";
                case "114.3":return "11816";
                case "115":return "11817";
                case "118":return "11818";
                case "120":return "11819";
                default:
                    return "11811";
            }
        }

    }
}

//    Общая структура XML-файла:

//<? xml version="1.0" encoding="UTF-8"?>
//<yml_catalog date = "2017-02-05 17:22" >
//  < shop >
//    < offers >
//       < offer id="">
//       ...
//       </offer>
//       ...     
//    </offers>
//   </shop>
//</yml_catalog>

