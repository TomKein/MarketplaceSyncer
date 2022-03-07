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
        public async void SyncAsync(List<RootObject> bus) {
            await Task.Factory.StartNew(() => {
                try {
                    _bus = bus;
                    GenerateXlsx();
                    //SftpClient.Upload(_fexp);
                } catch (Exception x) {
                    Log.Add("satom: ошибка выгрузки - "+x.Message);
                }
            });
        }

        public void GenerateXlsx() {
            //открываю файл с данными
            Log.Add("загружаю файл");
            var workbook = new XLWorkbook(_fexp);
            Log.Add("файл успешно загружен");
            //беру первую таблицу
            var ws = workbook.Worksheets.First();
            //определяю границы данных
            var range = ws.RangeUsed();
            //количество столбцов
            var colCount = range.ColumnCount();
            //количесвто строк
            var rowCount = range.RowCount();
            Log.Add("rowCount:" +rowCount + " colCount:"+colCount);
            //перебираю строки, чтобы заменить идентификаторы tiu на идентификаторы business
            for (int i = 2; i < 100/*rowCount*/; i++) {
                //наименование позиции
                var name = ws.Cell(i, 1).GetString();
                //цена
                var price = ws.Cell(i, 2).GetValue<int>();
                Log.Add("имя:" +name + " цена:"+price);
                
                //ищем карточки товара
                var bus = _bus.Where(b => b.name==name);
                Log.Add("bus: "+bus.Count());
                //если нашли несколько
                if (bus.Count()>1) {
                    Log.Add("bus: ДУБЛИ! "+bus.Count()+"\n"+bus.Select(s => s.name+" - "+s.id+" - "+s.description).Aggregate((a, b) => a+"\n"+b));
                }
                //если нашли 1
                if (bus.Count() == 1) {
                    if (bus.First().amount<0) {
                        Log.Add("bus: УДАЛЯЮ СТРОКУ (нет на остатках) - " + ws.Row(i).ToString()+" - "+bus.First().name);
                        ws.Row(i).Delete();
                        i--;
                    } else if (bus.First().price != price) {
                        Log.Add("bus: МЕНЯЮ ЦЕНУ - " + price +" - "+bus.First().price);
                        ws.Cell(i, 2).SetValue<int>(bus.First().price);                        
                    }else
                        ws.Cell(i, 17).Value = bus.First().id;
                } else {
                    Log.Add("bus: УДАЛЯЮ ИДЕНТИФИКАТОР (не найден) - "+name);
                    ws.Cell(i, 17).Value = "---";
                }

            }
            workbook.SaveAs(_fexp.Replace("satom", "satom_out"));
        }

    }
}
