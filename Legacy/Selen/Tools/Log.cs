using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Selen.Base;

namespace Selen.Tools {
    public delegate void EventDelegate();
    static class Log {
        public static event EventDelegate LogUpdate = null;
        private static Object _thisLock = new Object();
        private static List<string> _log = new List<string>();
        public static int Level { set; get; }
        public static string GetLogLastAdded {
            get {
                return _log.Last();
            } }
        public static string OutString { get {
                return _log.Aggregate((a, b) => a + "\n" + b);
            } }
        public static void Add(string s, bool writeDb = true, bool writeToFile = false) {
            if (writeDb)
                DB.AddLogAsync(s);
            var dt = DateTime.Now;
            s = dt + ": " + s;
            lock (_thisLock) {
                if (writeToFile) File.AppendAllText(@"..\log.txt", s+"\r\n", System.Text.Encoding.UTF8);
                else _log.Add(s);
                if (Level > 10 && _log.Count > Level) _log.RemoveRange(0, 10);
            }
            if (!writeToFile)
                LogUpdate.Invoke();
        }
    }
}