using Selen.Base;
using Selen.Tools;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Selen.Sites {
    class GdeRu {
        Selenium _dr;               //браузер
        DB _db;                     //база данных
        string _url;                //ссылка в карточке товара
        string[] _addDesc;          //дополнительное описание
        List<RootObject> _bus;      //ссылка на товары
        Random _rnd = new Random(); //генератор случайных чисел
        //конструктор
        public GdeRu() {
            _db = DB._db;
        }
        //загрузка кукис
        public void LoadCookies() {
            if (_dr != null) {
                _dr.Navigate("https://vip.kupiprodai.ru/");
                var c = _db.GetParamStr("kupiprodai.cookies");
                _dr.LoadCookies(c);
                Thread.Sleep(1000);
            }
        }
        //сохранение кукис
        public void SaveCookies() {
            if (_dr != null) {
                _dr.Navigate("https://vip.kupiprodai.ru/");
                var c = _dr.SaveCookies();
                if (c.Length > 20)
                    _db.SetParam("kupiprodai.cookies", c);
            }
        }
        //закрытие браузера
        public void Quit() {
            _dr?.Quit();
            _dr = null;
        }

    }
}
