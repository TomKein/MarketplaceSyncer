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
using System.Xml.Linq;

namespace Selen.Sites {
    class Izap24 {
        string _l = "izap24: ";
        //список товаров
        List<RootObject> _bus;
        //файл выгрузки
        string _fexp = @"..\iz_export.csv";
        //файл ошибок
        string _ferr = @"..\iz_errors.csv";
        //каталог сатом
        XDocument satomYML;
        //блокируемые группы
        string[] _blockedGroupsIds = new[] {
            "2060149",    //"РАЗБОРКА (ЧЕРНОВИКИ)" 
            "75573",    //"УСЛУГИ"
            "77369",    //"Импортированные"
            "168723",   //"Аудио-видеотехника"
            "168807",   //"Шины, диски, колеса"
            "209277",   //"ЗАКАЗЫ"
            "289732",   //"АВТОХИМИЯ"
            "430926",   //"МАСЛА"
            "491841",   //"ИНСТРУМЕНТЫ (НОВЫЕ)"
            "2281135",   //"ИНСТРУМЕНТЫ (аренда)"
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
        public async Task<bool> SyncAsync(List<RootObject> bus) {
            //интервал проверки
                var uploadInterval = await DB.GetParamIntAsync("izap24.uploadInterval");
            if (uploadInterval == 0 || DateTime.Now.Hour == 0 || DateTime.Now.Hour % uploadInterval != 0)
                return true;
            Log.Add(_l + "начало выгрузки...");
            try {
                _bus = bus;
                satomYML = XDocument.Load(@"..\satom_import.xml");
                //проверка каталога xml
                if (satomYML.Root.Name != "yml_catalog")
                    throw new Exception("корневой элемент каталога satom не найден!");
                if (satomYML.Descendants("offer").Count() < 10000)
                    throw new Exception("количество элементов в каталоге satom меньше 10000");
                await CreateCsvAsync();
                await SftpClient.FtpUploadAsync(_fexp);
                await SftpClient.FtpUploadAsync(_ferr);
                //SmtpMailClient.SendAsync(_fexp,"izap24-ilnur@mail.ru");
                Log.Add(_l + "выгрузка успешно завершена");
                return true;
            } catch (Exception x) {
                Log.Add(_l + "ошибка выгрузки! - " + x.Message);
                return false;
            }
        }
        //формирование файла выгрузки
        private async Task CreateCsvAsync() {
            await Task.Factory.StartNew(() => {
                //цены для рассрочки
                var creditPriceMin = DB.GetParamInt("creditPriceMin");
                var creditPriceMax = DB.GetParamInt("creditPriceMax");
                var creditDescription = DB.GetParamStr("creditDescription");
                //получаю список товаров
                var offers = _bus.Where(w =>
                    w.images.Count > 0 &&                           //есть фото
                    w.amount > 0 &&                                 //с положительным остатком
                    w.price > 0 &&                                  //с положительной ценой
                    !_blockedGroupsIds.Contains(w.group_id) &&      //группа товара не заблокирована
                    !w.archive &&                                   //товар не в архиве
                    !w.IsNew())                                     //и товар НЕ новый
                    .OrderByDescending(o => int.Parse(o.id));       //сортировка от новых к старым товарам
                var n = 0;                                          //счетчик позиций
                var e = 0;                                          //счетчик ошибок
                StringBuilder s = new StringBuilder();              //строка для выгрузки
                StringBuilder err = new StringBuilder();            //строка для ошибок
                RootObject.ResetAutos();                            //обновляю список моделей
                File.Delete(_ferr);                                 //затираю файл ошибок
                s.AppendLine("ID_EXT;Name;Mark;Model;Price;OriginalNumber;Description;PhotoUrls"); //первая строка выгрузки
                foreach (var offer in offers) {
                    var m = offer.GetNameMarkModel();               //определяю марку и модель авто
                    if (m == null) {
                        err.Append(offer.name).AppendLine(";не определена марка/модель");
                        e++;
                        continue;
                    }
                    //получаю фото с бизнес.ру
                    var photoUrls = offer.images.Select(o => o.url).ToList();
                    s.Append(offer.id).Append(";");                     //ID_EXT
                    s.Append(m[0]).Append(";");                         //name
                    s.Append(m[1]).Append(";");                         //mark
                    s.Append(m[2]).Append(";");                         //model
                    s.Append(offer.price.ToString("0")).Append(";");    //price
                    s.Append(offer.Part).Append(";");                   //part
                    var desc = Regex.Match(offer.HtmlDecodedDescription(), @"([бБ][\\\/][уУ].+)")
                                    .Groups[1].Value;
                    if (offer.price >= creditPriceMin && offer.price <= creditPriceMax)
                        desc = desc.Insert(0, creditDescription+" ");
                    s.Append(desc).Append(";");                         //desc
                    s.AppendLine(photoUrls.Aggregate((a, b) => a + "," + b));  //photos
                    n++;
                    if (n % 1000 == 0)
                        Log.Add(_l + "выгружено товаров " + n);
                }
                Log.Add(_l + "всего успешно выгружено товаров" + n);
                File.WriteAllText(_fexp, s.ToString(), Encoding.UTF8);
                Log.Add(_l + "пропущено " + e + " товаров");
                File.WriteAllText(_ferr, err.ToString(), Encoding.UTF8);
            });
        }
    }
}
