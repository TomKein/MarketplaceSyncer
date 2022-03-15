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
        public async Task GenerateXML() {
            //количество объявлений в тарифе
            var offersLimit = await DB._db.GetParamIntAsync("youla.offersLimit");
            //созадю новый xml
            var xml = new XDocument(
                new XElement("yml_catalog", new XAttribute("date",DateTime.Now))
                );



            //сохраняю файл
            xml.Save(filename);
        }
    }

//    Общая структура XML-файла:

//<? xml version="1.0" encoding="UTF-8"?>
//<yml_catalog date = "2017-02-05 17:22" >
//  < shop >
//    < offers >

//    < offer id="">
// ...
//</offer>
//      ...     
//    </offers>
//   </shop>
//</yml_catalog>
}
