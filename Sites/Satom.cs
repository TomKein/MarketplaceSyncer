using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Newtonsoft.Json;
using Selen.Base;
using Selen.Tools;

namespace Selen.Sites {
    internal class Satom {
        //список товаров
        List<RootObject> _bus = null;
        //файл выгрузки
        string _fexp = @"..\satom.xlsx";
        //файл шаблона
        string _ftmp = @"..\satom_tmp.xlsx";
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
                    SftpClient.Upload(_fexp);
                } catch (Exception x) {
                    Log.Add("satom: ошибка выгрузки - "+x.Message);
                }
            });
        }

        public void GenerateXlsx() {
            string[] _addDesc = JsonConvert.DeserializeObject<string[]>(DB._db.GetParamStr("satom.addDescription"));
            //готовлю файл
            File.Copy(_ftmp, _fexp,overwrite:true);
            Log.Add("satom: шаблон скопирован");
            var workbook = new XLWorkbook(_fexp);
            Log.Add("satom: файл успешно загружен");
            //беру первую таблицу
            var ws = workbook.Worksheets.First();
            //перебираю карточки товара
            for (int b=0, i=2; b<_bus.Count; b++) {
                //если остаток положительный, есть фото и цена, группа не в исключении - выгружаем
                if (_bus[b].amount > 0 &&
                    _bus[b].price > 0 &&
                    _bus[b].images.Count > 0 &&
                    !_blockedGroupsIds.Contains(_bus[b].group_id)
                    ) {
                    //наименование
                    ws.Cell(i, 1).Value = _bus[b].name;
                    //цена
                    ws.Cell(i, 2).Value = _bus[b].price;
                    //валюта
                    ws.Cell(i, 3).Value = "RUR";
                    //ед. измерения
                    ws.Cell(i, 4).Value = _bus[b].measure_id == "11" ? "пара"
                                        : _bus[b].measure_id == "13" ? "компл."
                                        : "шт.";
                    //ссылка на фото
                    ws.Cell(i, 5).Value = _bus[b].images.Select(s => s.url).Aggregate((a1, a2) => a1+","+a2);
                    //наличие
                    ws.Cell(i, 7).Value = "+";
                    //количество
                    ws.Cell(i, 8).Value = String.Format("{0:0.##}", _bus[b].amount);
                    //рубрика
                    ws.Cell(i, 9).Value = GetCategory(b);
                    //доп. описание
                    ws.Cell(i, 11).Value = _bus[b].DescriptionList(dop:_addDesc).Aggregate((a1,a2)=>a1+"<br>"+a2);
                    //идентификатор товара
                    ws.Cell(i, 17).Value = _bus[b].id;
                    //артикул
                    ws.Cell(i, 22).Value = _bus[b].part??"";
                    //статус
                    ws.Cell(i, 39).Value = "опубликован";
                    //следующая строка
                    i++;
                }
            }
            workbook.Save();
            Log.Add("файл успешно сохранен");

            {
                //определяю границы данных
                //var range = ws.RangeUsed();
                //количество столбцов
                //var colCount = range.ColumnCount();
                //количесвто строк
                //var rowCount = range.RowCount();
                //Log.Add("satom: rowCount:" +rowCount + " colCount:"+colCount);
                //перебираю строки, чтобы заменить идентификаторы tiu на идентификаторы business
                //for (int b = 2; i <= rowCount/1000; i++) {
                //    //наименование позиции
                //    var name = ws.Cell(i, 1).GetString();
                //    //цена
                //    var price = ws.Cell(i, 2).GetString();
                //    //ищем карточки товара
                //    List<RootObject> bus = _bus.Where(b => b.name==name).ToList();
                //    //если нашли 1
                //    if (bus.Count == 1) {
                //        //нет на остатках - удаляем строку
                //        if (bus[0].amount <= 0) {
                //            Log.Add("satom: "+i+" УДАЛЯЮ СТРОКУ, нет на остатках - " + ws.Row(i).ToString() + " - " + bus[0].name);
                //            ws.Row(i).Delete();
                //            i--;
                //            rowCount--;
                //            continue;
                //        } else {
                //            if (bus[0].price.ToString() != price) {
                //                Log.Add("satom: "+i+" МЕНЯЮ ЦЕНУ - " + name + " старая: " + price + " - новая: " + bus[0].price);
                //                ws.Cell(i, 2).Value = bus[0].price.ToString();
                //            }
                //            //меняю id
                //            ws.Cell(i, 17).Value = bus[0].id;
                //            //заполняю количество на остатках
                //            ws.Cell(i, 8).Value = String.Format("{0:0.##}", bus[0].amount);
                //            //фото
                //            ws.Cell(i, 5).Value = bus[0].images.Select(s => s.url).Aggregate((a, b) => a+","+b);
                //        }
                //    } else {
                //        Log.Add("satom: "+i+" УДАЛЯЮ СТРОКУ, cnt="+bus.Count+" - " + name);
                //        ws.Row(i).Delete();
                //        i--;
                //        rowCount--;
                //    }

                //}
            }
        }
        //определение категории на сатоме
        private string GetCategory(int b) {
            var g = _bus[b].GroupName().ToLowerInvariant();
            var n = _bus[b].name.ToLowerInvariant();
            if (n.Contains("решетка ") && (n.Contains("бампер") || n.Contains("радиатор")))
                return "https://satom.ru/t/reshetki-avtomobilnye-na-bampery-i-radiatory-120/";
            if (n.Contains("поворотник ")) return "https://satom.ru/t/svetovye-pribory-avtomobilya-naruzhnye-185/";
            if (n.Contains("бампер ")) return "https://satom.ru/t/bampery-17030/";
            if (n.Contains("противотуманка ") || n.Contains("фара ") && n.Contains("противотум"))
                return "https://satom.ru/t/protivotumannye-fary-18710/";
            if (n.Contains("фара ")) return "https://satom.ru/t/fary-18694/";
            if (n.Contains("фонарь ")) return "https://satom.ru/t/fonari-18703/";
            if (n.Contains("двигатель ")) return "https://satom.ru/t/dvigateli-avtomobilnye-8347/";
            if (n.Contains("дверь ")) return "https://satom.ru/t/dveri-avtomobilnye-15893/";
            if (n.Contains("динамик ") || n.Contains("динамики ") || n.Contains("абвуфер")||n.Contains("твиттер "))
                return "https://satom.ru/t/akusticheskie-sistemy-avtomobilnye-9251/";
            if (n.Contains("крыша ")) return "https://satom.ru/t/detali-kuzova-9088/";
            if (n.Contains("амортизатор ") || n.Contains("стойка ")) return "https://satom.ru/t/amortizatory-podveski-9147/";
            if (n.Contains("шумоизоляция ")) return "https://satom.ru/t/izolyacionnye-materialy-dlya-avtomobilya-9903/";
            if (n.Contains("магнитола ")) return "https://satom.ru/t/avtomagnitoly-221/";
            if (n.Contains("бампера ")) return "https://satom.ru/t/bampery-i-komplektuyushchie-9084/";
            if (n.Contains("эмблема ")) return "https://satom.ru/t/emblemy-avtomobilnye-9066/";
            if (n.Contains("шкив ")) return "https://satom.ru/t/shkivy-na-dvigatel-19315/";
            if (n.Contains("шестерня ")) return "https://satom.ru/t/shesterni-dvigatelya-avtomobilya-19188/";
            if (n.Contains("шатун ")) return "https://satom.ru/t/shatuny-na-dvigatel-avtomobilya-19304/";
            if (n.Contains("цилиндр ")){
                if(n.Contains("главный")) return "https://satom.ru/t/glavnyy-cilindr-scepleniya-dlya-legkovyh-avto-19030/";
                if(n.Contains("рабочий")) return "https://satom.ru/t/rabochiy-cilindr-scepleniya-dlya-legkovyh-avto-26331/";
                if(n.Contains("тормозной")) return "https://satom.ru/t/tormoznye-cilindry-210/";
            }
            if (n.Contains("замок ")){
                if(n.Contains("зажиган")||n.Contains("рулев")) return "https://satom.ru/t/zamki-zazhiganiya-avtomobilnye-9104/";
                return "https://satom.ru/t/zamki-i-klyuchi-avtomobilnye-9076/";
            }
            if (n.Contains("ручник")) return "https://satom.ru/t/stoyanochnyy-tormoz-ruchnik-9017/";
            if (n.Contains("ручка ")) return "https://satom.ru/t/avtomobilnye-dvernye-ruchki-15914/";
            if (n.Contains("стабилизатор ")) return "https://satom.ru/t/stabilizatory-stoyki-stabilizatorov-podveski-9153/";
            if (n.Contains("коллектор ")) {
                if (n.Contains("выпускной")) return "https://satom.ru/t/vypusknye-kollektory-avtomobilya-9195/";
                if (n.Contains("впускной")) return "https://satom.ru/t/vpusknye-kollektory-9192/";
            }
            if (n.Contains("турбин")) return "https://satom.ru/t/turbiny-turbokompressory-avtomobilnye-158/";
            if(n.Contains("блок управления")) {
                if (n.Contains("печк") ||n.Contains("отопит")||n.Contains("климат"))
                    return "https://satom.ru/t/detali-sistemy-otopleniya-ventilyacii-i-kondicionirovaniya-11426/";
                return "https://satom.ru/t/bloki-upravleniya-avtomobilnye-9101/";
            }
            if((n.Contains("насос ") || n.Contains("поводок ")) && 
                (n.Contains("дворник") || n.Contains("очист") || n.Contains("омывател")))
                return "https://satom.ru/t/zapchasti-sistemy-ochistki-okon-i-far-11257/";
            if(n.Contains("насос ") && n.Contains("топлив") || n.Contains("бензонасос "))
                return "https://satom.ru/t/toplivnye-nasosy-avtomobilnye-9038/";
            if (n.Contains("насос ") && n.Contains("масл"))
                return "https://satom.ru/t/maslyanye-nasosy-avtomobilnye-9200/";
            if ((n.Contains("насос ") || n.Contains("шланг")|| n.Contains("трубк")) && 
                (n.Contains("гур")||n.Contains("гидроусил")||n.Contains("высокого давления")||
                n.Contains("низкого давления")||n.Contains("обратк")))
                return "https://satom.ru/t/usiliteli-rulevogo-upravleniya-9127/";
            if (n.Contains("насос ") && n.Contains("водян") || n.Contains("помпа"))
                return "https://satom.ru/t/nasosy-ohlazhdayushchey-zhidkosti-9202/";
            if ((n.Contains("насос ") ||n.Contains("блок ")) && n.Contains("abs"))
                return "https://satom.ru/t/detali-tormoznoy-sistemy-avtomobilya-9217/";
            if (n.Contains("абсорбер"))
                return "https://satom.ru/t/detali-toplivnoy-sistemy-avtomobilya-9013/";
            if (n.Contains("датчик"))
                return "https://satom.ru/t/datchiki-avtomobilnye-9102/";





            //https://satom.ru/t/dveri-avtomobilnye-i-komplektuyushchie-9082/
            //https://satom.ru/t/elementy-salona-i-bagazhnogo-otseka-199/
            //https://satom.ru/t/bokoviny-kuzova-9085/

            return "";
        }
    }
}
