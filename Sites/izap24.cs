using Selen.Base;
using Selen.Tools;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Selen.Sites {
    class Izap24 {
        //список товаров
        List<RootObject> _bus = null;
        //товары тиу
        DataSet _ds = null;
        //файл выгрузки
        string _fexp = "iz_export.csv";
        //файл ошибок
        string _ferr = "iz_errors.csv";
        //блокируемые группы
        string[] _blockedGroupsIds = new[] {
            "75573",    //"УСЛУГИ"
            "77369",    //"Импортированные"
            "168723",   //"Аудио-видеотехника"
            "168807",   //"Шины, диски, колеса"
            "209277",   //"ЗАКАЗЫ"
            "289732",   //"АВТОХИМИЯ"
            "430926",   //"МАСЛА"
            "491841",   //"ИНСТРУМЕНТЫ (НОВЫЕ)"
            "506517",   //"НАКЛЕЙКИ"
            "496105",   //"ЗАРЯДНЫЕ УСТРОЙСТВА"
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
            "1721460",  //"КОВРИКИ (НОВЫЕ)"
        };
        //старт выгрузки
        public async Task SyncAsync(List<RootObject> bus, DataSet ds) {
            _bus = bus;
            _ds = ds;
            await CreateCsvAsync();
            await Task.Factory.StartNew(() => {
                SftpClient.Upload(_fexp);
                SftpClient.Upload(_ferr);
                //отправка прайса по почте, если потребуется
                //SmtpMailClient.SendAsync(_fexp,"izap24-ilnur@mail.ru");
            });
        }
        //формирую файл выгрузки
        private async Task CreateCsvAsync() {
            //получаю список товаров
            var offers = _bus.Where(w =>
                w.tiu.Contains("http") &&                       //есть ссылка на tiu
                w.amount > 0 &&                                 //с положительным остатком
                w.price > 0 &&                                  //с положительной ценой
                !_blockedGroupsIds.Contains(w.group_id) &&      //группа товара не заблокирована
                !w.archive &&                                   //товар не в архиве
                !w.IsNew())                                     //и товар НЕ новый
                .OrderByDescending(o=>int.Parse(o.id));         //сортировка от новых к старым товарам
            var n = 0;                                          //счетчик позиций
            var e = 0;                                          //счетчик ошибок
            StringBuilder s = new StringBuilder();              //строка для выгрузки
            StringBuilder err = new StringBuilder();            //строка для ошибок
            RootObject.ResetAutos();                            //перечитываю список моделей
            File.Delete(_ferr);                                 //затираю файл ошибок
            s.AppendLine("ID_EXT;Name;Mark;Model;Price;OriginalNumber;Description;PhotoUrls"); //первая строка выгрузки
            foreach (var offer in offers) {
                var m = await offer.GetNameMarkModelAsync();    //определяю марку и модель авто
                if (m==null) {
                    err.Append(offer.name).AppendLine(";не определена марка или модель");
                    e++; continue;
                }
                s.Append(offer.id).Append(";");                 //ID_EXT
                s.Append(m[0]).Append(";");                     //name
                s.Append(m[1]).Append(";");                     //mark
                s.Append(m[2]).Append(";");                     //model
                s.Append(offer.price.ToString("0")).Append(";");//price
                s.Append(offer.part).Append(";");               //part
                var desc = Regex.Match(offer.HtmlDecodedDescription(), @"([бБ][\\\/][уУ].+)")
                                .Groups[1].Value;
                s.Append(desc).Append(";");                     //desc
                //получаю ссылки на фотографии товаров из каталога tiu.ru
                var urls = new StringBuilder();
                var tiuId = offer.tiu.Split('/').Last();        //ищу id товара в каталоге tiu
                if (tiuId.Length > 0) {
                    //ищу запись в таблице offer с таким id - нахожу соответствующий ключ id к таблице picture
                    var idRow = _ds.Tables["offer"].Select("id = '" + tiuId + "'");
                    if (idRow.Length == 0) Log.Add("ошибка! товар с id = " + tiuId + " - не найден в каталоге тиу!");
                    else {
                        var id = idRow[0]["offer_id"]; //беру поле offer_id из первой найденной строки
                        var image_rows = _ds.Tables["picture"].Select("offer_id = '" + id.ToString() + "'"); //все строки со ссылками на фото
                        for (int i = 0; i < image_rows.Length; i++) { //собираю строку со ссылками для выгрузки
                            urls.Append(image_rows[i]["picture_Text"].ToString());
                            if (i < image_rows.Length - 1) urls.Append(",");
                        }
                    }
                }
                s.AppendLine(urls.ToString());                  //photos
                n++;
                if (n % 500 == 0) Log.Add("izap24.ru: выгружено " + n + " товаров");
            }
            Log.Add("izap24.ru: всего выгружено " + n + " товаров");
            File.WriteAllText(_fexp, s.ToString(), Encoding.UTF8);
            Log.Add("izap24.ru: пропущено " + e + " товаров");
            File.WriteAllText(_ferr, err.ToString(), Encoding.UTF8);
        }
    }
}
