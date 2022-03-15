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
            foreach (var b in bus) {
                //id объявления на юле из ссылки
                var youlaId = b.youla.Split('-').Last();
                //элемент offer
                var offer = new XElement("offer", new XAttribute("id", youlaId));
                //добавляю элемент offer в контейнер offers
                offers.Add(offer);
            }
            //добавляю контейнер offers в shop
            shop.Add(offers);
            //добавляю shop в корневой элемент yml_catalog
            root.Add(shop);
            
            //сохраняю файл
            xml.Save(filename);
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
