using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Selen.Base;

namespace Selen.Sites {
    class YoulaXml {
        string filename = @"..\youla.xml";
        public async Task GenerateXML(List<RootObject> _bus) {
            //количество объявлений в тарифе
            var offersLimit = await DB._db.GetParamIntAsync("youla.offersLimit");
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
            //список карточек с положительным остатком и ценой, у которых есть ссылка на юлу
            var bus = _bus.Where(w => w.amount > 0 && w.price > 0 && w.youla.Contains("http"));
            //для каждой карточки
            foreach (var b in bus.Take(5)) {
                //id объявления на юле из ссылки
                var youlaId = b.youla.Split('-').Last();
                //элемент offer
                var offer = new XElement("offer", new XAttribute("id", youlaId));
                //категория товара
                offer.Add(new XElement("youlaCategoryId", GetCategory()));
                //подкатегория товара
                offer.Add(new XElement("youlaSubcategoryId", GetSubCategory()));
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
                offer.Add(new XElement("description", b.DescriptionList(3000).Aggregate((a1,a2)=>a1+"\r\n"+a2)));
                //имя менеджера
                offer.Add(new XElement("managerName", "Менеджер 1"));
                

                //добавляю элемент offer в контейнер offers
                offers.Add(offer);
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

        private object GetCategory() {
            throw new NotImplementedException();
        }

        private object GetSubCategory() {
            throw new NotImplementedException();
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
