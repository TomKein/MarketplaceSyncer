using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML;

namespace Selen.Sites {
    internal class Satom {
        public Satom() {

        }

        public void GenerateXlsx() {
            var workbook = new ClosedXML(@"..\satom.xlsx");
            var ws = workbook.WorkSheet(1);

            var test = ws.Cell("").Value;

        }

    }
}
