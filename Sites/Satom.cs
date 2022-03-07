using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Selen.Base;
using Selen.Tools;

namespace Selen.Sites {
    internal class Satom {        
        //список товаров
        List<RootObject> _bus = null;
        //файл выгрузки
        string _fexp = @"..\satom.xlsx";
        //блокируемые группы
        string[] _blockedGroupsIds = new[] {
            "2060149",  //"РАЗБОРКА (ЧЕРНОВИКИ)" 
            "75573",    //"УСЛУГИ"
            "209277",   //"ЗАКАЗЫ"
            "430926",   //"МАСЛА"
            "491841",   //"ИНСТРУМЕНТЫ (НОВЫЕ)"
            "506517",   //"НАКЛЕЙКИ"
            "496105",   //"ЗАРЯДНЫЕ УСТРОЙСТВА"
            "1721460",  //"КОВРИКИ (НОВЫЕ)"
            "490003",   //"ДОМКРАТЫ"
            "473940",   //"ТРОСЫ БУКСИРОВОЧНЫЕ"
            "467920",   //"ОЧИСТИТЕЛИ, ПОЛИРОЛИ"
            "460974",   //"АКСЕССУАРЫ"
            "452852",   //"ПРОВОДА ПРИКУРИВАНИЯ"
            "452386",   //"АРОМАТИЗАТОРЫ"
            "451921",   //"КОМПРЕССОРЫ"
            "451920",   //"КАНИСТРЫ"
            "451919",   //"ЩЕТКИ, ПЕРЧАТКИ"
            "450171",   //"ХОМУТЫ, СТЯЖКИ"
            "449585",   //"ГЕРМЕТИКИ"
            "439231",   //"АККУМУЛЯТОРЫ"
            "436634",   //"ЖИДКОСТИ, СМАЗКИ"
            "434159",   //"ДВОРНИКИ, ЩЕТКИ"
            "289732",   //"АВТОХИМИЯ"
        };
        public Satom() {
            
        }
        //старт выгрузки
        public async Task SyncAsync(List<RootObject> bus) {
            await Task.Factory.StartNew(() => {
                _bus = bus;
                GenerateXlsx();
                SftpClient.Upload(_fexp);
            });
        }


        public void GenerateXlsx() {
            var workbook = new XLWorkbook(_fexp);
            var ws = workbook.Worksheets.First();
            var range = ws.RangeUsed();
            var colCount = range.ColumnCount();
            var rowCount = range.RowCount();

            var test = ws.Cell(1,1).Value;

            Log.Add("satom: col="+colCount+" row="+rowCount+" test="+test);
        }

    }
}
