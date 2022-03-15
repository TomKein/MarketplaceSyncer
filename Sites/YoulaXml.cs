using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Selen.Base;



namespace Selen.Sites {
    class YoulaXml {
        public void GenerateXML() {
            //количество объявлений в тарифе
            var offersLimit = DB._db.GetParamIntAsync("youla.offersLimit");

        }
    }
}
