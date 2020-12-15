using Selen.Base;
using Selen.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Selen.Sites {
    class EuroAuto {
        //список товаров
        List<RootObject> _bus = null;
        //файл выгрузки
        string _fexp = "ea_export.csv";
        //файл ошибок
        string _ferr = "ea_errors.csv";
        //блокируемые группы
        string[] _blockedGroupsIds = new[] {
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
        //старт выгрузки
        public async Task SyncAsync(List<RootObject> bus) {
            await Task.Factory.StartNew(() => {
                _bus = bus;
                CreateCsv();
                SftpClient.Upload(_fexp);
                SftpClient.Upload(_ferr);

            });
        }
        //формирую файл выгрузки
        private void CreateCsv() {
            //получаю список товаров
            var offers = _bus.Where(w =>
                w.amount > 0 &&                             //с положительным остатком
                w.price > 0 &&                              //с положительной ценой
                !_blockedGroupsIds.Contains(w.group_id) &&  //группа товара не заблокирована
                !w.archive &&                               //товар не в архиве
                w.IsNew());                                 //и товар новый
            var n = 0;                                  //счетчик позиций
            var e = 0;                                  //счетчик ошибок
            StringBuilder s = new StringBuilder();      //строка для выгрузки
            StringBuilder err = new StringBuilder();    //строка для ошибок
            RootObject.ResetManufactures();             //обновляю список производителей
            File.Delete(_ferr);                         //затираю файл ошибок
            foreach (var offer in offers) {
                //проверяю артикул (номер) запчасти
                if (string.IsNullOrEmpty(offer.part)) {
                    err.Append(offer.name).AppendLine(";нет артикула");
                    e++;
                    continue;
                }
                //определяю производителя
                var manufacture = offer.GetManufacture();
                if (string.IsNullOrEmpty(manufacture)) {
                    err.Append(offer.name).AppendLine(";нет производителя");
                    e++;
                    continue;
                }
                //записываю строку
                s.Append(manufacture).Append(";");
                s.Append(offer.part).Append(";");
                s.Append(offer.name).Append(";");
                s.Append(offer.amount).Append(";");
                s.AppendLine(offer.price.ToString("0"));
                n++;
                if(n%200==0) Log.Add("euroauto.ru: выгружено " + n + " товаров");
            }
            Log.Add("euroauto.ru: всего выгружено " + n + " товаров");
            File.WriteAllText(_fexp, s.ToString(), Encoding.UTF8);
            Log.Add("euroauto.ru: пропущено " + e + " товаров");
            File.WriteAllText(_ferr, err.ToString(), Encoding.UTF8);

            //отправка прайса по почте
            SmtpMailClient.SendAsync();
        }
    }
}
