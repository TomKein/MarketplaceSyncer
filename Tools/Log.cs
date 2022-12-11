using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Selen.Base;

namespace Selen.Tools {

    public delegate void EventDelegate();

    static class Log {
        //сохраняю ссылку на объект базы данных
        static DB _db = DB._db;
        //событие - изменение лога
        public static event EventDelegate LogUpdate = null;
        //объект для потокобезопасности при работе со списком
        private static Object _thisLock = new Object();
        //объект для хранения строк
        private static List<string> _log = new List<string>();
        //уровень логирования (количество строк)
        public static int Level { set; get; }
        //вывод последней строки в логе
        public static string LogLastAdded {
            get {
                return _log.Last();
            } }
        //вывод всего лога в виде строки
        public static string OutString { get {
                return _log.Aggregate((a, b) => a + "\n" + b);
            } }
        //добавить в лог
        //s - строка, которая должна быть записана
        //writeDb - писать лог в базу данных
        public static void Add(string s, bool writeDb = true, bool writeToFile = false) {
            if (writeDb) _db.AddLogAsync(s);
            var dt = DateTime.Now;
            s = dt + ": " + s;
            lock (_thisLock) {
                if (writeToFile) File.AppendAllText(@"..\log.txt", s);
                else _log.Add(s);
                if (_log.Count > Level) _log.RemoveRange(0, 10);
            }
            LogUpdate.Invoke();
        }
    }
}
