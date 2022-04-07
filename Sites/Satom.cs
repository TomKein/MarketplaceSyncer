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
                } catch (Exception x) {
                    Log.Add("satom: ошибка выгрузки - "+x.Message);
                }
            });
            //если размер файла в порядке
            if (new FileInfo(_fexp).Length > 1200000)
                //отправляю файл на сервер
                await SftpClient.FtpUploadAsync(_fexp);
        }

        public void GenerateXlsx() {
            string[] _addDesc = JsonConvert.DeserializeObject<string[]>(DB._db.GetParamStr("satom.addDescription"));
            //готовлю файл
            File.Copy(_ftmp, _fexp, overwrite: true);
            Log.Add("satom: шаблон скопирован");
            var workbook = new XLWorkbook(_fexp);
            Log.Add("satom: файл успешно загружен");
            //беру первую таблицу
            var ws = workbook.Worksheets.First();
            //перебираю карточки товара
            for (int b = 0, i = 2; b<_bus.Count; b++) {
                //если остаток положительный, есть фото и цена, группа не в исключении - выгружаем
                if (_bus[b].amount > 0 &&
                    _bus[b].price > 0 &&
                    _bus[b].images.Count > 0 &&
                    !_blockedGroupsIds.Contains(_bus[b].group_id)
                    ) {
                    var category = GetCategory(b);
                    if (category.Length == 0) continue;
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
                    ws.Cell(i, 9).Value = category;
                    //идентификатор рубрики
                        ws.Cell(i, 10).Value = category.TrimEnd('/').Split('-').Last();
                    //доп. описание
                    ws.Cell(i, 11).Value = _bus[b].DescriptionList(dop: _addDesc).Aggregate((a1, a2) => a1+"<br>"+a2)+
                        "<br><br><p style=\"color:#D3D3D3;font-size:5px\">код на складе:" + _bus[b].id+"</p>";
                    //идентификатор товара
                    ws.Cell(i, 17).Value = _bus[b].id;
                    //артикул
                    ws.Cell(i, 22).Value = _bus[b].part??"";
                    //статус
                    ws.Cell(i, 39).Value = "опубликован";
                    //состояние
                    ws.Cell(i, 44).Value = "Состояние"; 
                    ws.Cell(i, 45).Value = _bus[b].IsNew()?"новый": "б/у";
                    //тип
                    ws.Cell(i, 51).Value = "Тип по изготовителю";
                    ws.Cell(i, 52).Value = _bus[b].IsOrigin()? "оригинал" : "аналог";
                    //артикул
                    if (!String.IsNullOrEmpty(_bus[b].part)) {
                        ws.Cell(i, 46).Value = "Номер запчасти";
                        ws.Cell(i, 47).Value = _bus[b].part;
                    }
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
            if (n.Contains("реш") && (n.Contains("бампер") || n.Contains("радиатор")))
                return "https://satom.ru/t/reshetki-avtomobilnye-na-bampery-i-radiatory-120/";
            if (n.Contains("поворотник ") || n.StartsWith("поворот")||n.StartsWith("указатель"))
                return "https://satom.ru/t/svetovye-pribory-avtomobilya-naruzhnye-185/";
            if (n.Contains("бампер "))
                return "https://satom.ru/t/bampery-17030/";
            if (n.Contains("противотуманка ") || n.Contains("фара ") && n.Contains("противотум"))
                return "https://satom.ru/t/protivotumannye-fary-18710/";
            if (n.Contains("фара "))
                return "https://satom.ru/t/fary-18694/";
            if (n.Contains("фонарь "))
                return "https://satom.ru/t/fonari-18703/";
            if (n.StartsWith("двигатель "))
                return "https://satom.ru/t/dvigateli-avtomobilnye-8347/";
            if (n.Contains("дросс") || 
                n.Contains("моновпр") ||
                n.Contains("арбюратор") ||
                n.StartsWith("инжектор"))
                return "https://satom.ru/t/drosselnye-zaslonki-9190/";
            if (n.Contains("дверь "))
                return "https://satom.ru/t/dveri-avtomobilnye-15893/";
            if (n.Contains("динамик ") || n.Contains("динамики ") || n.Contains("абвуфер")||n.Contains("твиттер "))
                return "https://satom.ru/t/akusticheskie-sistemy-avtomobilnye-9251/";
            if (n.Contains("педаль") || n.Contains("педали"))
                return "https://satom.ru/t/pedali-avtomobilnye-9143/";
            if (n.Contains("термост")||n.Contains("ерморегулятор"))
                return "https://satom.ru/t/detali-sistemy-ohlazhdeniya-avtomobilya-205/";
            if (n.Contains("крыша ") || 
                n.Contains("телевиз") ||
                n.Contains("щиток") ||
                n.Contains("экран") ||
                n.Contains("суппорт") && n.Contains("радиат"))
                return "https://satom.ru/t/detali-kuzova-9088/";
            if (n.Contains("кулиса"))
                return "https://satom.ru/t/komplektuyushchie-korobki-pereklyucheniya-peredach-kpp-10848/";
            if (n.Contains("трос")) {
                if (n.Contains("газа") || n.Contains("бензобак") ||n.Contains("подсос") ||n.Contains("топлив") ||n.Contains("карбюр"))
                    return "https://satom.ru/t/detali-toplivnoy-sistemy-avtomobilya-9013/";
                if (n.Contains("кпп")||n.Contains("круиз"))
                    return "https://satom.ru/t/komplektuyushchie-korobki-pereklyucheniya-peredach-kpp-10848/";
                if (n.Contains("печки")||n.Contains("отопител"))
                    return "https://satom.ru/t/detali-sistemy-otopleniya-ventilyacii-i-kondicionirovaniya-11426/";
                if (n.Contains("сцеплен"))
                    return "https://satom.ru/t/tros-scepleniya-dlya-legkovyh-avtomobiley-19065/";
                return "https://satom.ru/t/zapchasti-dlya-avtomobilnogo-elektrooborudovaniya-11389/";
            }
            if (n.Contains("капот "))
                return "https://satom.ru/t/kapoty-16302/";
            if (n.Contains("капот"))
                return "https://satom.ru/t/komplektuyushchie-k-kapotam-9086/";
            if (n.Contains("замок ")|| n.Contains("замка ")||n.Contains("личинка")) {
                if (n.Contains("зажиган")||n.Contains("рулев"))
                    return "https://satom.ru/t/zamki-zazhiganiya-avtomobilnye-9104/";
                return "https://satom.ru/t/zamki-i-klyuchi-avtomobilnye-9076/";
            }
            if (n.Contains("реле ")) {
                if (n.Contains("блок "))
                    return "https://satom.ru/t/zapchasti-dlya-avtomobilnogo-elektrooborudovaniya-11389/";
                return "https://satom.ru/t/rele-avtomobilnye-399/";
            }
            if (n.Contains("накладка") ||
                n.Contains("накладки")|| 
                n.Contains("молдинг")|| 
                n.Contains("надпись")|| 
                n.Contains("обшивка арки"))
                return "https://satom.ru/t/nakladki-avtomobilnye-131/";
            if (n.Contains("багажник")||n.Contains("спойлер"))
                return "https://satom.ru/t/kryshki-bagazhnika-i-komplektuyushchie-9077/";
            if (n.Contains("амортизатор ") || n.Contains("стойка "))
                return "https://satom.ru/t/amortizatory-podveski-9147/";
            if (n.Contains("амортиза") && n.Contains("опор"))
                return "https://satom.ru/t/opory-amortizatorov-9149/";
            if (n.Contains("шумоизоляция "))
                return "https://satom.ru/t/izolyacionnye-materialy-dlya-avtomobilya-9903/";
            if (n.Contains("магнитола ")||
                n.Contains("чейнджер")||
                n.StartsWith("дисплей магнитолы"))
                return "https://satom.ru/t/avtomagnitoly-221/";
            if (n.Contains("бампера "))
                return "https://satom.ru/t/bampery-i-komplektuyushchie-9084/";
            if (n.Contains("эмблема "))
                return "https://satom.ru/t/emblemy-avtomobilnye-9066/";
            if (n.Contains("шкив "))
                return "https://satom.ru/t/shkivy-na-dvigatel-19315/";
            if (n.Contains("шестерня "))
                return "https://satom.ru/t/shesterni-dvigatelya-avtomobilya-19188/";
            if (n.Contains("шатун"))
                return "https://satom.ru/t/shatuny-na-dvigatel-avtomobilya-19304/";
            if (n.Contains("цилиндр ")) {
                if (n.Contains("главный"))
                    return "https://satom.ru/t/glavnyy-cilindr-scepleniya-dlya-legkovyh-avto-19030/";
                if (n.Contains("рабочий"))
                    return "https://satom.ru/t/rabochiy-cilindr-scepleniya-dlya-legkovyh-avto-26331/";
                if (n.Contains("тормозной"))
                    return "https://satom.ru/t/tormoznye-cilindry-210/";
            }
            if (n.StartsWith("руль"))
                return "https://satom.ru/t/ruli-avtomobilnye-132/";
            if (n.Contains("безопасн")&&n.Contains("подушк") || n.Contains("airbag"))
                return "https://satom.ru/t/sistemy-passivnoy-bezopasnosti-avtomobilya-9224/";
            if ((n.Contains("ремень") || n.Contains("ремн"))&&n.Contains("безопасн"))
                return "https://satom.ru/t/remni-bezopasnosti-9175/";
            if (n.Contains("двер"))
                return "https://satom.ru/t/dveri-avtomobilnye-i-komplektuyushchie-9082/";
            if (n.Contains("натяжител") || n.StartsWith("ролик"))
                return "https://satom.ru/t/roliki-i-natyazhiteli-remney-cepey-9206/";
            if (n.StartsWith("генератор"))
                return "https://satom.ru/t/generatory-avtomobilnye-398/";
            if (n.Contains("стартер")||n.Contains("бендикс")||n.Contains("втягиваю"))
                return "https://satom.ru/t/startery-i-komplektuyushchie-9105/";
            if (n.Contains("ручник")||n.Contains("ручного тормоза"))
                return "https://satom.ru/t/stoyanochnyy-tormoz-ruchnik-9017/";
            if (n.Contains("ручка "))
                return "https://satom.ru/t/avtomobilnye-dvernye-ruchki-15914/";
            if (n.Contains("стабилизатор "))
                return "https://satom.ru/t/stabilizatory-stoyki-stabilizatorov-podveski-9153/";
            if (n.Contains("антенн"))
                return "https://satom.ru/t/antenny-avtomobilnye-222/";
            if (n.Contains("коллектор")) {
                if (n.Contains("выпускн"))
                    return "https://satom.ru/t/vypusknye-kollektory-avtomobilya-9195/";
                if (n.Contains("впускн"))
                    return "https://satom.ru/t/vpusknye-kollektory-9192/";
            }
            if (n.Contains("турбин"))
                return "https://satom.ru/t/turbiny-turbokompressory-avtomobilnye-158/";
            if (n.Contains("жабо") || 
                n.Contains("дворник") ||
                n.Contains("ефлектор водяной") ||
                n.Contains("трапеция") ||
                n.Contains("омывател") 
                ||                
                (n.Contains("насос ") || n.Contains("поводок ")||
                n.Contains("бачок")|| n.Contains("мотор")|| n.Contains("решетк")) &&
                (n.Contains("очист") || n.Contains("омывател"))
                )
                return "https://satom.ru/t/zapchasti-sistemy-ochistki-okon-i-far-11257/";
            if (n.Contains("насос ") && n.Contains("топлив") || n.Contains("бензонасос "))
                return "https://satom.ru/t/toplivnye-nasosy-avtomobilnye-9038/";
            if (n.Contains("насос ") && n.Contains("масл"))
                return "https://satom.ru/t/maslyanye-nasosy-avtomobilnye-9200/";
            if ((n.Contains("насос ") || n.Contains("шланг")|| n.Contains("комплект")||
                n.Contains("трубк")|| n.Contains("бачок")||n.Contains("бачка"))
                &&
                (n.Contains("гур")||n.Contains("гидроусил")||n.Contains("высокого давления")||
                n.Contains("низкого давления")||n.Contains("обратк")) || n.Contains("эур"))
                return "https://satom.ru/t/usiliteli-rulevogo-upravleniya-9127/";
            if (n.Contains("насос ") && n.Contains("водян") || n.Contains("помпа"))
                return "https://satom.ru/t/nasosy-ohlazhdayushchey-zhidkosti-9202/";
            if ((n.Contains("насос ") ||n.Contains("блок ")) && n.Contains("abs"))
                return "https://satom.ru/t/detali-tormoznoy-sistemy-avtomobilya-9217/";
            if (n.Contains("насос в"))
                return "https://satom.ru/t/detali-toplivnoy-sistemy-avtomobilya-9013/";
            if (n.Contains("абсорбер"))
                return "https://satom.ru/t/detali-toplivnoy-sistemy-avtomobilya-9013/";
            if (n.Contains("датчик")||
                n.Contains("дмрв")||
                n.Contains("лямбда")||
                n.Contains("расходомер"))
                return "https://satom.ru/t/datchiki-avtomobilnye-9102/";
            if (n.Contains("гбц ") ||n.Contains("цилиндр") ||n.Contains("масл"))
                return "https://satom.ru/t/detali-dvigatelya-avtomobilya-193/";
            if (n.Contains("кронштейн")||
                n.Contains("креплени")||
                n.Contains("опора")||
                n.Contains("площадка ак")||
                n.StartsWith("заглушк")) {
                if (n.Contains("кпп"))
                    return "https://satom.ru/t/komplektuyushchie-korobki-pereklyucheniya-peredach-kpp-10848/";
                if (n.Contains("двс") || n.Contains("двигат"))
                    return "https://satom.ru/t/krepleniya-i-kronshteyny-dvigatelya-avtomobilya-19277/";
                return "https://satom.ru/t/krepezhnye-elementy-i-zaglushki-avtomobilnye-9182/";
            }
            if (n.StartsWith("подушка") && n.Contains("дв"))
                return "https://satom.ru/t/podushki-dvigatelya-15865/";
            if (n.Contains("провод") || n.Contains("шлейф"))
                return "https://satom.ru/t/provodka-provoda-200/";
            if (n.Contains("форсунк")) {
                if (n.Contains("омывател"))
                    return "https://satom.ru/t/zapchasti-sistemy-ochistki-okon-i-far-11257/";
                return "https://satom.ru/t/forsunki-inzhektory-9219/";
            }
            if (n.Contains("усилител")) {
                if (n.Contains("антен") || n.Contains("fm"))
                    return "https://satom.ru/t/usilitel-antennyy-5253/";
                if (n.Contains("вакуум") ||n.Contains("тормоз"))
                    return "https://satom.ru/t/usiliteli-tormozov-9141/";
            }
            if (n.Contains("кнопк") || n.Contains("кнопок") ||
                n.Contains("выключатель")|| n.Contains("трамбл"))
                return "https://satom.ru/t/zapchasti-dlya-avtomobilnogo-elektrooborudovaniya-11389/";
            if (n.Contains("стекло ")|| n.Contains("зеркал") || n.Contains("форточк"))
                return "https://satom.ru/t/stekla-zerkala-avtomobilnye-188/";
            if (n.Contains("бачок")) {
                if (n.Contains("расшири"))
                    return "https://satom.ru/t/detali-sistemy-ohlazhdeniya-avtomobilya-205/";
                if (n.Contains("сцеплен"))
                    return "https://satom.ru/t/transmissiya-sceplenie-legkovyh-avto-194/";
                if (n.Contains("вакуум"))
                    return "https://satom.ru/t/detali-toplivnoy-sistemy-avtomobilya-9013/";
                if (n.Contains("тормоз"))
                    return "https://satom.ru/t/detali-tormoznoy-sistemy-avtomobilya-9217/";
            }
            if (n.Contains("балка") || n.Contains(" моста") || n.Contains("подрамник")||n.Contains("аздатка"))
                return "https://satom.ru/t/mosty-i-balki-avtomobilnye-9157/";
            if (n.Contains("ящик") || n.Contains("бардачок") || n.Contains("вещев")||
                n.Contains("карман")|| n.Contains("часы"))
                return "https://satom.ru/t/elementy-salona-i-bagazhnogo-otseka-199/";
            if ((n.Contains("плафон")|| n.Contains("светил")) && 
                (n.Contains("салон")||n.Contains("багажн")))
                return "https://satom.ru/t/svetovye-pribory-avtomobilya-vnutrennie-9116/";
            if (n.Contains("охлажд") || n.Contains("вентилят"))
                return "https://satom.ru/t/detali-sistemy-ohlazhdeniya-avtomobilya-205/";
            if (n.Contains("стеклоподъемник") ||n.Contains("стеклоподъёмник"))
                return "https://satom.ru/t/steklopodemniki-15907/";
            if (n.Contains("моторчик")) {
                if (n.Contains("печк")||n.Contains("отопит")||n.Contains("заслонк"))
                    return "https://satom.ru/t/detali-sistemy-otopleniya-ventilyacii-i-kondicionirovaniya-11426/";
                if (n.Contains("люк")||n.Contains("антен"))
                    return "https://satom.ru/t/zapchasti-dlya-avtomobilnogo-elektrooborudovaniya-11389/";
            }
            if (n.StartsWith("радиатор ") || n.StartsWith("интеркулер"))
                return "https://satom.ru/t/radiatory-avtomobilnye-15875/";
            if (n.Contains("радиатор"))
                return "https://satom.ru/t/detali-sistemy-ohlazhdeniya-avtomobilya-205/";
            if (n.Contains("кондиционер"))
                return "https://satom.ru/t/detali-sistemy-otopleniya-ventilyacii-i-kondicionirovaniya-11426/";
            if (n.Contains("лючок") || 
                n.StartsWith("лонжерон")||
                n.StartsWith("защита"))
                return "https://satom.ru/t/detali-kuzova-9088/";
            if (n.Contains("топлив"))
                return "https://satom.ru/t/detali-toplivnoy-sistemy-avtomobilya-9013/";
            if (n.Contains("блок") && n.Contains("предохранит"))
                return "https://satom.ru/t/zapchasti-dlya-avtomobilnogo-elektrooborudovaniya-11389/";
            if (n.Contains("блок") && n.Contains("розжиг"))
                return "https://satom.ru/t/bloki-rozzhiga-3178/";
            if (n.Contains("печк") ||
                n.Contains("отопит")||
                n.Contains("климат")||
                n.Contains("оздухоочистител"))
                return "https://satom.ru/t/detali-sistemy-otopleniya-ventilyacii-i-kondicionirovaniya-11426/";
            if (n.Contains("блок ")||
                n.Contains("жойстик регулировк")||
                n.Contains("эбу")||
                n.Contains("модуль")||
                n.Contains("комфорта")
                )
                return "https://satom.ru/t/bloki-upravleniya-avtomobilnye-9101/";
            if (n.Contains("предохранитель"))
                return "https://satom.ru/t/predohraniteli-avtomobilnye-9097/";
            if (n.Contains("рулев")) {
                if (n.Contains("вал")||n.Contains("колонк"))
                    return "https://satom.ru/t/rulevye-valy-9165/";
                if (n.Contains("рейка"))
                    return "https://satom.ru/t/rulevye-reyki-9173/";
                if (n.Contains("кожух") ||n.Contains("чехол"))
                    return "https://satom.ru/t/elementy-salona-i-bagazhnogo-otseka-199/";
                return "https://satom.ru/t/prochie-elementy-rulevogo-upravleniya-9134/";
            }
            if (n.Contains("переключател"))
                return "https://satom.ru/t/pereklyuchateli-avtomobilnye-9115/";
            if (n.Contains("венец"))
                return "https://satom.ru/t/komplektuyushchie-korobki-pereklyucheniya-peredach-kpp-10848/";
            if (n.Contains("болт"))
                return "https://satom.ru/t/krepezhnye-elementy-i-zaglushki-avtomobilnye-9182/";
            if (n.Contains("тяга"))
                return "https://satom.ru/t/tyagi-avtomobilnye-9164/";
            if (n.Contains("суппорт"))
                return "https://satom.ru/t/tormoznye-supporty-9018/";
            if (n.Contains("стоп ")&&n.Contains("дополн"))
                return "https://satom.ru/t/stop-signaly-18734/";
            if (n.Contains("гофра"))
                return "https://satom.ru/t/detali-vpusknoy-vypusknoy-sistemy-avtomobilya-191/";
            if (n.Contains("проклад"))
                return "https://satom.ru/t/prokladki-dvigatelya-avtomobilya-19207/";
            if (n.Contains("воздуховод") ||
                n.Contains("дефлектор")||
                n.Contains("ресивер возд"))
                return "https://satom.ru/t/vozduhovody-3671/";
            if (n.Contains("кулак"))
                return "https://satom.ru/t/povorotnye-kulaki-9163/";
            if (n.Contains("диск") ||n.Contains("докатка")) {
                if (n.Contains("тормоз"))
                    return "https://satom.ru/t/tormoznye-diski-208/";
                return "https://satom.ru/t/kolesnye-diski-8361/";
            }
            if (n.Contains("сцеплен"))
                return "https://satom.ru/t/transmissiya-sceplenie-legkovyh-avto-194/";
            if (n.StartsWith("привод")) {
                return "https://satom.ru/t/transmissiya-sceplenie-legkovyh-avto-194/";
            }
            if (n.Contains("маховик"))
                return "https://satom.ru/t/mahovik-dvigatelya-avtomobilya-19288/";
            if (n.StartsWith("мкпп")|| n.StartsWith("акпп"))
                return "https://satom.ru/t/korobki-pereklyucheniya-peredach-kpp-9225/";
            if (n.Contains("кпп"))
                return "https://satom.ru/t/komplektuyushchie-korobki-pereklyucheniya-peredach-kpp-10848/";
            if (n.Contains("четверть") ||n.Contains("крыло зад") ||n.Contains("морда")||n.Contains("задняя часть"))
                return "https://satom.ru/t/bokoviny-kuzova-9085/";
            if (n.Contains("подкрылок")||n.Contains("локер")||n.Contains("одкрылки"))
                return "https://satom.ru/t/podkrylki-avtomobilnye-7580/";
            if (n.Contains("крыло")||n.Contains("крыла"))
                return "https://satom.ru/t/krylya-avtomobilnye-i-komplektuyushchie-9090/";
            if (n.Contains("патрон"))
                return "https://satom.ru/t/komplektuyushchie-svetovyh-priborov-avtomobilya-9558/";
            if (n.Contains("повторитель")||n.Contains("локер"))
                return "https://satom.ru/t/svetovye-pribory-avtomobilya-naruzhnye-185/";
            if (n.Contains("сидень"))
                return "https://satom.ru/t/sidenya-avtomobilnye-9181/";
            if (n.StartsWith("коленвал"))
                return "https://satom.ru/t/kolenchatye-valy-avtomobilya-19168/";
            if (n.Contains("коленвал"))
                return "https://satom.ru/t/detali-dvigatelya-avtomobilya-193/";
            if (n.Contains("глушитель"))
                return "https://satom.ru/t/detali-dvigatelya-avtomobilya-193/";
            if (n.Contains("при")&&n.Contains("труба"))
                return "https://satom.ru/t/priemnaya-truba-glushitelya-17051/";
            if (n.Contains("бензобак")||
                n.Contains("бака")||
                n.Contains("горловин"))
                return "https://satom.ru/t/detali-toplivnoy-sistemy-avtomobilya-9013/";
            if (n.Contains("бараб")&&n.Contains("торм"))
                return "https://satom.ru/t/tormoznye-barabany-207/";
            if (n.Contains("сайлентблок"))
                return "https://satom.ru/t/saylentbloki-9154/";
            if (n.Contains("брызговик"))
                return "https://satom.ru/t/bryzgoviki-avtomobilnye-113/";
            if (n.Contains("рычаг"))
                return "https://satom.ru/t/rychagi-podveski-9152/";
            if (n.Contains("пружин"))
                return "https://satom.ru/t/pruzhiny-podveski-9150/";
            if (n.Contains("кардан"))
                return "https://satom.ru/t/kardannyy-val-dlya-legkovyh-avtomobiley-19087/";
            if (n.Contains("распредвал "))
                return "https://satom.ru/t/raspredvaly-i-privody-raspredvala-avtomobilya-19177/";
            if (n.Contains("фиксаторы") ||n.Contains("фиксаторов"))
                return "https://satom.ru/t/nabory-instrumentov-1063/";
            if (n.Contains("вал")&&(n.Contains("пром")||n.Contains("баланс")||n.Contains("кором")) ||
                n.Contains("ванос"))
                return "https://satom.ru/t/detali-dvigatelya-avtomobilya-193/";
            if (n.Contains("патрубок")||n.Contains("трубк")) {
                if (n.Contains("вент")||n.Contains("возд")||
                    n.Contains("интеркул")||n.Contains("карт")||
                    n.Contains("турбо"))
                    return "https://satom.ru/t/detali-vpusknoy-vypusknoy-sistemy-avtomobilya-191/";
                if (n.Contains("вакуум"))
                    return "https://satom.ru/t/transmissiya-sceplenie-legkovyh-avto-194/";
                if (n.Contains("тнвд"))
                    return "https://satom.ru/t/detali-toplivnoy-sistemy-avtomobilya-9013/";
                return "https://satom.ru/t/detali-sistemy-ohlazhdeniya-avtomobilya-205/";
            }
            if (n.Contains("воздухозаборник"))
                return "https://satom.ru/t/vozduhozaborniki-127/";
            if (n.Contains("втулк"))
                return "https://satom.ru/t/vtulki-dlya-podveski-9168/";
            if (n.Contains("выжимн") && n.Contains("подши"))
                return "https://satom.ru/t/transmissiya-sceplenie-legkovyh-avto-194/";
            if (n.Contains("прикуривател"))
                return "https://satom.ru/t/prikurivateli-avtomobilnye-12047/";
            if (n.Contains("сигнал") ||
                n.Contains("клаксон")||
                n.Contains("сирена")||
                n.Contains("зуммер"))
                return "https://satom.ru/t/zvukovye-signaly-klaksony-396/";
            if (n.Contains("шрус ")||n.Contains("пыльник шрус"))
                return "https://satom.ru/t/shrus-dlya-legkovyh-avtomobiley-19079/";
            if (n.Contains("катуш") && n.Contains("зажиг"))
                return "https://satom.ru/t/katushki-zazhiganiya-avtomobilnye-9110/";
            if (n.Contains("резонатор"))
                return "https://satom.ru/t/rezonatory-17065/";
            if (n.Contains("егр") ||n.Contains("egr"))
                return "https://satom.ru/t/detali-vpusknoy-vypusknoy-sistemy-avtomobilya-191/";
            if (n.Contains("ступиц"))
                return "https://satom.ru/t/stupicy-9156/";
            if (n.Contains("козыр")&& n.Contains("солнц")||
                n.Contains("консоль")||
                n.Contains("часы")||
                n.Contains("обшивка")||
                n.Contains("рамка")||
                n.Contains("одежд")||
                n.Contains("епельниц")||
                n.Contains("одголовник")||
                n.Contains("переходник магнит")||
                n.Contains("очечник")||
                n.Contains("одстаканник")||
                n.Contains("одлокотник")||
                n.Contains("полка")||
                n.Contains("решетка")||
                n.Contains("торпеды")||
                n.Contains("торпедо"))
                return "https://satom.ru/t/elementy-salona-i-bagazhnogo-otseka-199/";
            if (n.Contains("панель") && n.Contains("прибор")||
                n.StartsWith("дисплей")||
                n.StartsWith("индикатор"))
                return "https://satom.ru/t/spidometry-tahometry-paneli-priborov-avtomobilnye-186/";
            if (n.Contains("катализатор"))
                return "https://satom.ru/t/kataliticheskie-neytralizatory-katalizatory-9197/";
            if ((n.Contains("заглушка") ||n.Contains("рамка") || n.Contains("крышка"))
                && n.Contains("птф"))
                return "https://satom.ru/t/nakladki-avtomobilnye-131/";
            if (n.Contains("промыв") ||
                n.Contains("присадк") || 
                n.Contains("аскоксовыв") || 
                n.Contains("риботехнич"))
                return "https://satom.ru/t/prisadki-avtomobilnye-3653/";
            if (n.Contains("кожух"))
                return "https://satom.ru/t/detali-dvigatelya-avtomobilya-193/";
            if (n.Contains("сапун"))
                return "https://satom.ru/t/maslouloviteli-sapuny-9040/";
            if ((n.Contains("возд") || n.Contains("корп")) && n.Contains("фильтр"))
                return "https://satom.ru/t/filtry-vozdushnye-avtomobilnye-162/";
            if (n.Contains("грм") || 
                n.Contains("двигателя") || 
                n.Contains("клапанн")|| 
                n.Contains("успокоит")|| 
                n.Contains("поршнев")|| 
                n.Contains("поддон")|| 
                n.Contains("распредвала")|| 
                n.Contains("рышка декоратив")|| 
                n.Contains("рышка лобов")|| 
                n.Contains("муфта")|| 
                n.Contains("двс"))
                return "https://satom.ru/t/detali-dvigatelya-avtomobilya-193/";
            if (n.StartsWith("клапан")) {
                if (n.Contains("холостого хода"))
                    return "https://satom.ru/t/detali-toplivnoy-sistemy-avtomobilya-9013/";
                return "https://satom.ru/t/detali-vpusknoy-vypusknoy-sistemy-avtomobilya-191/";
            }
            if (n.StartsWith("планка ") ||
                n.StartsWith("дождевик"))
                return "https://satom.ru/t/detali-kuzova-9088/";
            if (n.StartsWith("фаркоп"))
                return "https://satom.ru/t/farkopy-136/";
            if (n.StartsWith("ус ")||n.Contains("люк"))
                return "https://satom.ru/t/detali-kuzova-9088/";
            if (n.Contains("фонар") || n.Contains("фар"))
                return "https://satom.ru/t/komplektuyushchie-svetovyh-priborov-avtomobilya-9558/";
            if (n.Contains("оммутатор") ||
                n.Contains("ммобилайзер"))
                return "https://satom.ru/t/sistemy-zazhiganiya-avto-9114/";
            if (n.Contains("скотч"))
                return "https://satom.ru/t/skotch-upakovochnyy-8693/";
            if (n.Contains("стрейч") && n.Contains("плен"))
                return "https://satom.ru/t/plenki-upakovochnye-10730/";
            if (n.StartsWith("порог"))
                return "https://satom.ru/t/porogi-avtomobilnye-9087/";
            if (n.Contains("струна"))
                return "https://satom.ru/t/avtoinstrument-9354/";
            if (n.Contains("пламегас"))
                return "https://satom.ru/t/detali-vpusknoy-vypusknoy-sistemy-avtomobilya-191/";
            if (n.Contains("катафот"))
                return "https://satom.ru/t/katafoty-lenty-dlya-konturnoy-markirovki-transportnyh-sredstv-130/";
            if (n.Contains("фишка") ||
                n.Contains("разъем")||
                n.Contains("аккумулят")||
                n.Contains("круиз")||
                n.Contains("камера")||
                n.Contains("резист")||
                n.Contains("регулятор напряж")||
                n.Contains("регулятор яркос")||
                n.Contains("онденсатор")||
                n.StartsWith("клемма"))
                return "https://satom.ru/t/zapchasti-dlya-avtomobilnogo-elektrooborudovaniya-11389/";
            if (n.Contains("домкрат"))
                return "https://satom.ru/t/domkraty-podemniki-gidravlicheskie-1034/";
            if (n.Contains("ключ балонный"))
                return "https://satom.ru/t/klyuchi-balonnye-1103/";
            if (n.Contains("плафон")||n.Contains("подсветка"))
                return "https://satom.ru/t/svetovye-pribory-avtomobilya-naruzhnye-185/";
            if (n.Contains("ксенон"))
                return "https://satom.ru/t/ksenonovye-lampy-i-komplekty-128/";
            if (n.Contains("зажиган"))
                return "https://satom.ru/t/zamki-zazhiganiya-avtomobilnye-9104/";
            if (n.Contains("тормоз"))
                return "https://satom.ru/t/detali-tormoznoy-sistemy-avtomobilya-9217/";
            if (n.Contains("ремень") || n.Contains("ремн"))
                return "https://satom.ru/t/remni-bezopasnosti-9175/";
            return "https://satom.ru/t/detali-kuzova-9088/";
        }
    }
}
