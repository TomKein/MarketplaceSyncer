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
            if (File.Exists(satomFile) && File.GetLastWriteTime(satomFile).AddHours(6) > DateTime.Now) {
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
            var root = new XElement("yml_catalog", new XAttribute("date",dt));
            //создаю главный контейнер shop
            var shop = new XElement("shop");
            //создаю контейнер offers
            var offers = new XElement("offers");
            //список карточек с положительным остатком и ценой, у которых есть ссылка на юлу и фотографии
            var bus = _bus.Where(w => w.amount > 0 && w.price > 0 && w.youla.Contains("http") && w.images.Count>0);
            //для каждой карточки
            foreach (var b in bus.Take(3)) {
                try {
                    //id объявления на юле из ссылки
                    var youlaId = b.youla.Split('-').Last();
                    //элемент offer
                    var offer = new XElement("offer", new XAttribute("id", youlaId));
                    //категория товара
                    var cat = GetCategory(b);
                    offer.Add(new XElement("youlaCategoryId", cat[""]));
                    //подкатегория товара
                    offer.Add(new XElement("youlaSubcategoryId", cat[""]));
                    //адрес магазина
                    offer.Add(new XElement("adress", "Россия, Калуга, Московская улица, 331"));
                    //цена
                    offer.Add(new XElement("price", b.price));
                    //телефон
                    offer.Add(new XElement("phone", "8 920 899-45-45"));
                    //наименование
                    offer.Add(new XElement("name", b.NameLength(100)));
                    //изображения - пока только первое
                    offer.Add(new XElement("picture", b.images.First().url));
                    //описание
                    offer.Add(new XElement("description", b.DescriptionList(2990, _addDesc).Aggregate((a1, a2) => a1+"\r\n"+a2)));
                    //имя менеджера
                    offer.Add(new XElement("managerName", "Менеджер 1"));
                    //добавляю элемент offer в контейнер offers
                    offers.Add(offer);
                } catch (Exception x) {
                    Log.Add(b.name + " - " +x.Message);
                }
            }
            //добавляю контейнер offers в shop
            shop.Add(offers);
            //добавляю shop в корневой элемент yml_catalog
            root.Add(shop);
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
            if (name.Contains("кронштейн ") || name.Contains("опора") || name.Contains("креплен") || name.Contains("подушк")) {
            } else if (name.Contains("планк") || name.Contains("молдинг") || name.Contains("обшивк") || name.Contains("накладк") ||
                name.Contains("катафот") || name.Contains("прокладка") || name.Contains("сальник")) {
            } else if ((name.Contains("трубк") || name.Contains("шланг")) &&
                (name.Contains("гур") || name.Contains("гидроусилител") ||
                name.Contains("высокого") || name.Contains("низкого"))) {
                d.Add("avtozapchasti_tip", "Рулевое управление");
                d.Add("kuzovnaya_detal", "Шланг ГУР");
            } else if (name.Contains("трубк") || name.Contains("шланг")) {
            } else if (name.Contains("трос ")) {
            } else if (name.Contains("состав") || name.Contains("масло ") || name.Contains("полирол")) {
            } else if (name.Contains("ступица")) {
                d.Add("avtozapchasti_tip", "Подвеска");
                d.Add("kuzovnaya_detal", "Ступица");
                d.Add("chast_detali", "Ступица");
            } else if (name.Contains("блок") && name.Contains("управлени") &&
                (name.Contains("печко") || name.Contains("отопит") || name.Contains("климат"))) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Блок управления печкой");
            } else if (name.Contains("коммутатор ")) {
                d.Add("avtozapchasti_tip", "Система зажигания");
                d.Add("kuzovnaya_detal", "Коммутатор зажигания");
            } else if (name.Contains("катушка") && name.Contains("зажиган")) {
                d.Add("avtozapchasti_tip", "Система зажигания");
                d.Add("kuzovnaya_detal", "Катушка зажигания");
            } else if (name.Contains("замок") && name.Contains("зажиган")) {
                d.Add("avtozapchasti_tip", "Система зажигания");
                d.Add("kuzovnaya_detal", "Контактная группа");
            } else if (name.Contains("заслонки") && (name.Contains("печк") || name.Contains("отопит"))) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Сервопривод");
            } else if (name.Contains("моторчик ") && (name.Contains("печк") || name.Contains("отопит"))) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Моторчик печки");
            } else if ((name.Contains("корпус ") || name.Contains("крышка ")) && name.Contains("термостат")) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Термостат");
                d.Add("chast_detali", "Термостат");
            } else if (name.Contains("коллектор") && name.Contains("впускной")) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Коллектор впускной");
            } else if (name.Contains("проводка") || name.Contains("жгут провод")) {
                d.Add("avtozapchasti_tip", "Электрооборудование");
                d.Add("kuzovnaya_detal", "Жгут проводов");
            } else if (name.Contains("бампер ") && (name.Contains("перед") || name.Contains("задн"))) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Бампер и комплектующие");
                d.Add("chast_detali", "Бампер");
            } else if (name.Contains("бампер") && name.Contains("усилитель")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Бампер и комплектующие");
                d.Add("chast_detali", "Усилитель бампера");
            } else if (name.Contains("бампер") && name.Contains("абсорбер")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Бампер и комплектующие");
                d.Add("chast_detali", "Абсорбер бампера");
            } else if (name.Contains("замок") && name.Contains("двери")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Замки");
                d.Add("chast_detali", "Замок двери");
            } else if (name.Contains("замок") && name.Contains("капота")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Замки");
                d.Add("chast_detali", "Замок капота");
            } else if (name.Contains("замок") && name.Contains("багажник")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Замки");
                d.Add("chast_detali", "Замок багажника");
            } else if (name.Contains("замка") && name.Contains("компрессор")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Замки");
                d.Add("chast_detali", "Центральный замок");
            } else if (name.Contains("крыло ") && (name.Contains("лев") || name.Contains("прав"))) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Крылья и комплектующие");
                d.Add("chast_detali", "Крылья");
            } else if (name.Contains("крыша ")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Крыша и комплектующие");
                d.Add("chast_detali", "Крыша");
            } else if (name.Contains("вкладыш") && name.Contains("шатун")) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Блок цилиндров и детали");
                d.Add("chast_detali", "Полукольца");
            } else if (name.Contains("насос") && name.Contains("топлив")) {
                d.Add("avtozapchasti_tip", "Топливная система");
                d.Add("kuzovnaya_detal", "Бензонасос");
                d.Add("chast_detali", "Бензонасос");
            } else if (name.Contains("карбюратор")) {
                d.Add("avtozapchasti_tip", "Топливная система");
                d.Add("kuzovnaya_detal", "Карбюратор");
            } else if (name.Contains("шатун")) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Блок цилиндров и детали");
                d.Add("chast_detali", "Шатун");
            } else if (name.Contains("заслонка") && name.Contains("дросс")) {
                d.Add("avtozapchasti_tip", "Топливная система");
                d.Add("kuzovnaya_detal", "Дроссель");
                d.Add("chast_detali", "Дроссельная заслонка");
            } else if (name.Contains("стабилизатор") && (name.Contains("перед") || name.Contains("зад"))) {
                d.Add("avtozapchasti_tip", "Подвеска");
                d.Add("kuzovnaya_detal", "Стабилизатор");
                d.Add("chast_detali", "Стабилизатор");
            } else if (name.Contains("рулев") && (name.Contains("карданч") || name.Contains("вал") || name.Contains("колонк"))) {
                d.Add("avtozapchasti_tip", "Рулевое управление");
                d.Add("kuzovnaya_detal", "Рулевая колонка");
            } else if (name.Contains("трапеция") && (name.Contains("дворник") || name.Contains("очистител"))) {
                d.Add("avtozapchasti_tip", "Система очистки");
                d.Add("kuzovnaya_detal", "Трапеция дворников");
            } else if (name.Contains("рем") && name.Contains("безопас")) {
                d.Add("avtozapchasti_tip", "Безопасность");
                d.Add("kuzovnaya_detal", "Ремни безопасности");
            } else if (name.Contains("расходомер") || name.Contains("дмрв") || name.Contains("кислорода") || name.Contains("лямбда")) {
                d.Add("avtozapchasti_tip", "Выхлопная система");
                d.Add("kuzovnaya_detal", "Датчики");
            } else if (name.Contains("глушитель")) {
                d.Add("avtozapchasti_tip", "Выхлопная система");
                d.Add("kuzovnaya_detal", "Глушитель");
            } else if (name.Contains("коллектор") && name.Contains("выпускной")) {
                d.Add("avtozapchasti_tip", "Выхлопная система");
                d.Add("kuzovnaya_detal", "Коллектор выпускной");
            } else if (name.Contains("клапан") && (name.Contains("егр") || name.Contains("egr"))) {
                d.Add("avtozapchasti_tip", "Выхлопная система");
                d.Add("kuzovnaya_detal", "EGR/SCR система");
            } else if ((name.Contains("блок ") || name.Contains("комплект ")) &&
                (name.Contains("управлени") || name.Contains("комфорта")) || name.Contains("ЭБУ ")) {
                d.Add("avtozapchasti_tip", "Электрооборудование");
                d.Add("kuzovnaya_detal", "Блок управления");
            } else if (name.Contains("привод") && (name.Contains("левый") || name.Contains("правый") || name.Contains("передн") || name.Contains("задни") || name.Contains("полуос"))) {
                d.Add("avtozapchasti_tip", "Трансмиссия, привод");
                d.Add("kuzovnaya_detal", "Привод и дифференциал");
                d.Add("chast_detali", "Приводной вал");
            } else if (name.Contains("маховик") || name.Contains("венец")) {
                d.Add("avtozapchasti_tip", "Трансмиссия, привод");
                d.Add("kuzovnaya_detal", "Сцепление");
                d.Add("chast_detali", "Маховик");
            } else if (name.Contains("диск") && name.Contains("сцеплен")) {
                d.Add("avtozapchasti_tip", "Трансмиссия, привод");
                d.Add("kuzovnaya_detal", "Сцепление");
                d.Add("chast_detali", "Диск сцепления");
            } else if (name.Contains("противотум") && name.Contains("фара")) {
                d.Add("avtozapchasti_tip", "Автосвет, оптика");
                d.Add("kuzovnaya_detal", "Противотуманная фара (ПТФ)");
            } else if ((name.Contains("кулис") || name.Contains("переключения") || name.Contains("селектор")) &&
                name.Contains("кпп")) {
                d.Add("avtozapchasti_tip", "Трансмиссия, привод");
                d.Add("kuzovnaya_detal", "Ручка КПП и кулиса");
            } else if (name.Contains("мкпп")) {
                d.Add("avtozapchasti_tip", "Трансмиссия, привод");
                d.Add("kuzovnaya_detal", "Коробка передач");
                d.Add("chast_detali", "МКПП");
            } else if (name.Contains("переключат") &&
                      (name.Contains("подрулев") || name.Contains("дворник") ||
                       name.Contains("поворот") || name.Contains("стеклоочист"))) {
                d.Add("avtozapchasti_tip", "Электрооборудование");
                d.Add("kuzovnaya_detal", "Подрулевой переключатель");
            } else if (name.Contains("фара")) {
                d.Add("avtozapchasti_tip", "Автосвет, оптика");
                d.Add("kuzovnaya_detal", "Фара");
            } else if (name.Contains("фонарь")) {
                d.Add("avtozapchasti_tip", "Автосвет, оптика");
                d.Add("kuzovnaya_detal", "Задний фонарь");
            } else if (name.Contains("компрессор кондиционера")) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Детали кондиционера");
                d.Add("chast_detali", "Компрессор кондиционера");
            } else if (name.Contains("шкив") && name.Contains("коленвал")) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "ГРМ система и цепь");
                d.Add("chast_detali", "Шкив коленвала");
            } else if (name.Contains("коленвал")) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Блок цилиндров и детали");
                d.Add("chast_detali", "Коленвал");
            } else if (name.Contains("балка ") &&
                      (name.Contains("зад") || name.Contains("передняя")||name.Contains("подмоторная"))) {
                d.Add("avtozapchasti_tip", "Подвеска");
                d.Add("kuzovnaya_detal", "Балка");
            } else if (name.Contains("кулак ") && (name.Contains("зад") || name.Contains("перед") ||
                name.Contains("поворотн") || name.Contains("правый") || name.Contains("левый"))) {
                d.Add("avtozapchasti_tip", "Подвеска");
                d.Add("kuzovnaya_detal", "Поворотный кулак");
            } else if (name.Contains("крышка") && name.Contains("багажник")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Багажник и комплектующие");
                d.Add("chast_detali", "Дверь багажника");
            } else if (name.Contains("стеклоподъемник")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Двери");
                d.Add("chast_detali", "Стеклоподъемный механизм и комплектующие");
            } else if (name.Contains("насос") && name.Contains("гур")) {
                d.Add("avtozapchasti_tip", "Рулевое управление");
                d.Add("kuzovnaya_detal", "Гидроусилитель и электроусилитель");
            } else if (name.Contains("панель") && name.Contains("прибор")) {
                d.Add("avtozapchasti_tip", "Салон, интерьер");
                d.Add("kuzovnaya_detal", "Спидометр");
            } else if (name.Contains("рулевая") && name.Contains("рейка")) {
                d.Add("avtozapchasti_tip", "Рулевое управление");
                d.Add("kuzovnaya_detal", "Рулевая рейка");
                d.Add("chast_detali", "Рулевая рейка");
            } else if (name.Contains("стартер")) {
                d.Add("avtozapchasti_tip", "Электрооборудование");
                d.Add("kuzovnaya_detal", "Стартер");
                d.Add("chast_detali", "Стартер в сборе");
            } else if (name.Contains("генератор")) {
                d.Add("avtozapchasti_tip", "Электрооборудование");
                d.Add("kuzovnaya_detal", "Генератор");
                d.Add("chast_detali", "Генератор в сборе");
            } else if (name.Contains("кулиса")) {
                d.Add("avtozapchasti_tip", "Трансмиссия, привод");
                d.Add("kuzovnaya_detal", "Ручка КПП и кулиса");
            } else if (name.Contains("усилитель") && name.Contains("вакуумный")) {
                d.Add("avtozapchasti_tip", "Тормозная система");
                d.Add("kuzovnaya_detal", "Тормозной цилиндр");
            } else if (name.Contains("подрамник")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Силовые элементы");
                d.Add("chast_detali", "Рама");
            } else if (name.Contains("люк") && (name.Contains("крыш") || name.Contains("электр"))) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Крыша и комплектующие");
                d.Add("chast_detali", "Крыша");
            } else if (name.Contains("зеркал") && (name.Contains("лево") || name.Contains("прав"))) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Зеркала");
                d.Add("chast_detali", "Боковые зеркала заднего вида");
            } else if (name.Contains("форсун") && name.Contains("топлив")) {
                d.Add("avtozapchasti_tip", "Топливная система");
                d.Add("kuzovnaya_detal", "Форсунка топливная");
                d.Add("chast_detali", "Форсунка топливная");
            } else if (name.Contains("вентилятор") && name.Contains("охлаждения")) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Радиатор и детали");
                d.Add("chast_detali", "Вентилятор радиатора");
            } else if (name.Contains("подушка")) {
                d.Add("avtozapchasti_tip", "Безопасность");
                d.Add("kuzovnaya_detal", "Подушка безопасности");
            } else if (name.Contains("руль")) {
                d.Add("avtozapchasti_tip", "Рулевое управление");
                d.Add("kuzovnaya_detal", "Руль");
            } else if (name.Contains("сиденья ") && name.Contains("передние") ||
                       name.Contains("сиденье ") && name.Contains("переднее") ||
                       name.Contains("регулир") && name.Contains("сиденья")||
                       name.Contains("сидень") && name.Contains("водител")) {
                d.Add("avtozapchasti_tip", "Салон, интерьер");
                d.Add("kuzovnaya_detal", "Сиденья");
            } else if (name.Contains("двигатель")) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Двигатель в сборе");
                d.Add("chast_detali", "Двигатель внутреннего сгорания");
            } else if (name.Contains("дверь ")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Двери");
                d.Add("chast_detali", "Дверь боковая");
            } else if (name.Contains("капот ")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Капоты и комплектующие");
                d.Add("chast_detali", "Капот");
            } else if (name.Contains("акпп ")) {
                d.Add("avtozapchasti_tip", "Трансмиссия, привод");
                d.Add("kuzovnaya_detal", "Коробка передач");
                d.Add("chast_detali", "АКПП");
            } else if (name.Contains("трамблер ")) {
                d.Add("avtozapchasti_tip", "Система зажигания");
                d.Add("kuzovnaya_detal", "Трамблер");
            } else if (name.Contains("реле ") && name.Contains("накала")) {
                d.Add("avtozapchasti_tip", "Система зажигания");
                d.Add("kuzovnaya_detal", "Реле свечей накала");
            } else if (name.Contains("стойк") && name.Contains("стабилизат")) {
                d.Add("avtozapchasti_tip", "Подвеска");
                d.Add("kuzovnaya_detal", "Стабилизатор");
                d.Add("chast_detali", "Стойки стабилизатора");
            } else if (name.Contains("втулк") && name.Contains("стабилизат")) {
                d.Add("avtozapchasti_tip", "Подвеска");
                d.Add("kuzovnaya_detal", "Стабилизатор");
                d.Add("chast_detali", "Втулки стабилизатора");
            } else if (name.Contains("стабилизатор ")) {
                d.Add("avtozapchasti_tip", "Подвеска");
                d.Add("kuzovnaya_detal", "Стабилизатор");
                d.Add("chast_detali", "Стабилизатор");
            } else if ((name.Contains("стойка ")|| name.Contains("амортизатор")) && (name.Contains("передн") || name.Contains("задн"))) {
                d.Add("avtozapchasti_tip", "Подвеска");
                d.Add("kuzovnaya_detal", "Амортизаторы");
            } else if (name.Contains("суппорт ") && (name.Contains("передн") || name.Contains("задн"))) {
                d.Add("avtozapchasti_tip", "Тормозная система");
                d.Add("kuzovnaya_detal", "Суппорт");
            } else if (name.Contains("блок ") && name.Contains("abs")) {
                d.Add("avtozapchasti_tip", "Тормозная система");
                d.Add("kuzovnaya_detal", "Система ABS");
            } else if ((name.Contains("стеклоочист") || name.Contains("дворник")) && name.Contains("решетк") || name.Contains("жабо ")) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Капоты и комплектующие");
                d.Add("chast_detali", "Решетка капота");
            } else if ((name.Contains("стеклоочист") || name.Contains("дворник")) && name.Contains("мотор")) {
                d.Add("avtozapchasti_tip", "Система очистки");
                d.Add("kuzovnaya_detal", "Мотор стеклоочистителя");
            } else if (name.Contains("блок ") && name.Contains("предохранителей")) {
                d.Add("avtozapchasti_tip", "Электрооборудование");
                d.Add("kuzovnaya_detal", "Блок предохранителей");
            } else if (name.Contains("радиатор ") && name.Contains("охлаждения")) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Радиатор и детали");
                d.Add("chast_detali", "Радиатор охлаждения");
            } else if (name.Contains("радиатор ") && name.Contains("кондиционера")) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Радиатор и детали");
                d.Add("chast_detali", "Радиатор кондиционера");
            } else if (name.Contains("расширительный") && name.Contains("бачок")) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Радиатор и детали");
                d.Add("chast_detali", "Расширительный бачок");
            } else if (name.Contains("бак ") && name.Contains("топливный") || name.Contains("бензобак")) {
                d.Add("avtozapchasti_tip", "Топливная система");
                d.Add("kuzovnaya_detal", "Топливный бак");
            } else if (name.Contains("вискомуфта ")) {
                d.Add("avtozapchasti_tip", "Системы охлаждения, обогрева");
                d.Add("kuzovnaya_detal", "Радиатор и детали");
                d.Add("chast_detali", "Вискомуфта");
            } else if (name.Contains("крышка ") && (name.Contains("клапанная") || name.Contains("гбц"))) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Клапанная крышка");
                d.Add("chast_detali", "Клапанная крышка");
            } else if (name.Contains("крышка ") && (name.Contains("двигателя") || name.Contains("двс"))) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Корпус и крышки");
                d.Add("chast_detali", "Крышка двигателя");
            } else if (name.Contains("гбц ")) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Блок цилиндров и детали");
                d.Add("chast_detali", "Головка блока цилиндров");
            } else if (name.Contains("турбина ")) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Турбина, компрессор");
                d.Add("chast_detali", "Турбина");
            } else if (name.Contains("поддон ") &&
                (name.Contains("двс") || name.Contains("двигател") || name.Contains("масл"))) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Прокладки и поддон");
                d.Add("chast_detali", "Поддон картера");
            } else if (name.Contains("турбокомпрессор ")) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Турбина, компрессор");
                d.Add("chast_detali", "Компрессор");
            } else if (name.Contains("корпус ") && (name.Contains("воздушн"))) {
                d.Add("avtozapchasti_tip", "Двигатель, ГРМ, турбина");
                d.Add("kuzovnaya_detal", "Корпус и крышки");
                d.Add("chast_detali", "Корпус фильтра");
            } else if ((name.Contains("порог ") || name.Contains("пороги ")) &&
                       (name.Contains("левы") || name.Contains("правы"))) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Силовые элементы");
                d.Add("chast_detali", "Пороги");
            } else if (name.Contains("лонжерон ") || name.Contains("лонжероны ") ||
                       (name.Contains("морда"))) {
                d.Add("avtozapchasti_tip", "Кузовные запчасти");
                d.Add("kuzovnaya_detal", "Силовые элементы");
                d.Add("chast_detali", "Лонжероны");
            } else if (name.Contains("стекло ")) {
                d.Add("avtozapchasti_tip", "Стекла");
            } else if (name.Contains("диск ")) {
                d.Add("shiny_diski_tip", "Диски");
                d.Add("shiny_naznachenie", "Для легкового автомобиля");
                d.Add("shiny_diametr", b.GetDiskSize());
                d.Add("disk_tip", name.Contains(" лит") ? "Литые"
                                                        : name.Contains(" кован") ? "Кованые"
                                                                                  : "Штампованные");
                d.Add("diski_kolichestvo_otverstiy", b.GetNumberOfHoles());
                d.Add("diski_diametr_raspolozheniya_otverstiy", b.GetDiameterOfHoles());
            }
            if (d.Count == 0) {
                Log.Add("youla.ru: не описана категория " + b.name);
                return d;
            }
            //вид транспорта
            d.Add("avtozapchasti_vid_transporta", "Для автомобилей");
            //состояние
            if (b.IsNew())
                d.Add("zapchast_sostoyanie", "Новые");
            else
                d.Add("zapchast_sostoyanie", "Б/у");
            //тип объявления
            d.Add("type_classified", "Магазин");
            return d;
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
}
