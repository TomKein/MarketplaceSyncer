using MySql.Data.MySqlClient;
using Selen.Tools;
using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Selen.Base {
    /// <summary>
    /// 1. класс будет обеспечивать соединение с бд
    /// 2. связь с таблицей logs
    /// 3. связь с таблицей settings
    /// 4. связь с таблицей goods
    /// </summary>
    public class DB {
        //строка подключения mysql
        //readonly string filenameConnectionString = @"..\connection.bak";
        static readonly string filenameConnectionString = @"..\connection.ini";
        //создаю подключение
        public static MySqlConnection connection;
        static readonly object _lock = new object();
        //конструктор по умолчанию - открывает соединение сразу
        static DB() {
            try {
                string connectionString = File.ReadAllText(filenameConnectionString);
                connection = new MySqlConnection(connectionString);
                OpenConnection();
                //утанавливаю кодировку принудительно
                var query = "SET NAMES utf8; " +
                            "SET character_set_server=`utf8`;";
                MySqlCommand command = new MySqlCommand(query, connection);
                command.ExecuteNonQuery();
                //сохраняю ссылку на себя
            } catch (Exception x) {
                Debug.WriteLine(x.Message);
            }
        }
        //деструктор - закрывает соединение на всякий случай
        ~DB() {
            CloseConnection();
        }
        //открыть соединение
        public static void OpenConnection() {
            if (connection.State == ConnectionState.Closed) { 
                connection.Open();
                Thread.Sleep(1000);
            }
        }
        //закрыть соединение
        public static void CloseConnection() {
            if (connection.State == ConnectionState.Open)
                connection.Close();
                Thread.Sleep(1000);
        }
        //ссылка на текущее соединение
        public static MySqlConnection GetConnection() {
            return connection;
        }
        //исполнить sql строку с возвратом данных в виде таблицы
        public static DataTable SqlQuery(string query) {
            MySqlCommand command = new MySqlCommand(query, connection);
            return ExecuteCommandQuery(command);
        }
        //вызов комманды с получением данных в виде таблицы
        private static DataTable ExecuteCommandQuery(MySqlCommand query) {
            DataTable table = new DataTable();
            MySqlDataAdapter adapter = new MySqlDataAdapter();
            adapter.SelectCommand = query;
            for (int i = 1; ; i++) {
                try {
                    lock (_lock) {
                        OpenConnection();
                        adapter.Fill(table);
                        break;
                    }
                } catch (Exception x) {
                    Log.Add("mysql: ошибка обращения к базе данных! (" + i + ") - " + x.Message, writeDb: false);
                    Thread.Sleep(10000);
                }
                if (i >= 5) {
                    Log.Add("mysql: ошибка обращения к базе данных! - превышено количество попыток обращений!", writeDb: false);
                    break;
                }
            }
            return table;
        }
        //вызов комманды без получения данных, возвращает количество затронутых строк
        static int ExecuteCommandNonQuery(MySqlCommand command) {
            int result = 0;
            lock (_lock) {
                for (int i = 0; ; i++) {
                    try {
                        OpenConnection();
                        result = command.ExecuteNonQuery();
                        break;
                    } catch (Exception x) {
                        Log.Add("mysql: ошибка обращения к базе данных! (" + i + ") - " + x.Message, writeDb: false);
                        Thread.Sleep(10000);
                    }
                    if (i >= 10) {
                        Log.Add("mysql: ошибка обращения к базе данных! - превышено количество попыток обращений!", writeDb: false);
                        break;
                    }
                }
            }
            return result;
        }
        //изменение названия параметра в таблице settings
        public static int UpdateParamName(string nameOld, string nameNew) {
            var query = "UPDATE settings SET name = '" + nameNew + "' WHERE name = '" + nameOld + "';";
            //создаем команду
            MySqlCommand command = new MySqlCommand(query, connection);
            //отправляем запрос, возвращаем результат
            return ExecuteCommandNonQuery(command);
        }
        //добавление либо обновление параметра в таблице settings
        public static int SetParam(string name, string value) {
            //запрос для сохранения настроек
            string query = "INSERT INTO `settings` (`name`, `value`) " +
                           "VALUES (@name, @value) " +
                           "ON DUPLICATE KEY UPDATE `value` = @value;";
            //создаем команду
            MySqlCommand command = new MySqlCommand(query, connection);
            command.Parameters.Add("@name", MySqlDbType.VarChar).Value = name;
            command.Parameters.Add("@value", MySqlDbType.Text).Value = value;
            //отправляем запрос, возвращаем результат
            return ExecuteCommandNonQuery(command);
        }
        //обновление параметра асинхронно
        public static async Task<int> SetParamAsync(string name, string value) {
            return await Task.Factory.StartNew(() => {
                return SetParam(name, value);
            });
        }
        //получаю строку асинхронно
        public static async Task<string> GetParamStrAsync(string key) => await Task.Factory.StartNew(() => {
            return GetParamStr(key);
        });
        //получаем настройки как строку
        public static string GetParamStr(string key) {
            //запрос для получения настроек
            var query = "SELECT `value` " +
                        "FROM `settings` " +
                        "WHERE `name`= @key;";
            //создаем команду
            MySqlCommand command = new MySqlCommand(query, connection);
            //добавляем параметр
            command.Parameters.Add("@key", MySqlDbType.VarChar).Value = key;
            //отправляем запрос и возвращаю таблицу значений
            var result = ExecuteCommandQuery(command);
            return First(result);
        }
        //получаем настройки как число
        public static int GetParamInt(string key) {
            //перевызываем метод получения строки
            var result = GetParamStr(key);
            //приводим к числовому типу
            int i;
            if (int.TryParse(result, out i))
                return i;
            return -1;
        }
        //получаем настройки как число c плавающей точкой
        public static float GetParamFloat(string key) {
            //перевызываем метод получения строки
            var result = GetParamStr(key).Replace(".", ",");
            //приводим к числовому типу
            float i;
            if (float.TryParse(result, out i))
                return i;
            return -1;
        }
        //получаем настройки как число long
        public static long GetParamLong(string key) {
            //перевызываем метод получения строки
            var result = GetParamStr(key);
            //приводим к числовому типу
            long i;
            if (long.TryParse(result, out i))
                return i;
            return -1;
        }
        //получаю число асинхронно
        public static async Task<int> GetParamIntAsync(string key) => await Task.Factory.StartNew(() => {
            return GetParamInt(key);
        });

        //получаем настройки как булевое значение
        public static bool GetParamBool(string key) {
            //перевызываем метод получения строки
            var result = GetParamStr(key).ToLowerInvariant();
            switch (result) {
                case "true":
                case "yes":
                case "1":
                    return true;
                default:
                    return false;
            }
        }
        //получаю настройку как булево значение асинхронно
        public static async Task<bool> GetParamBoolAsync(string key) => await Task.Factory.StartNew(() => {
            return GetParamBool(key);
        });
        //получаем параметр как дату-время
        public static DateTime GetParamDateTime(string key) {
            //перевызываем метод получения строки
            var result = GetParamStr(key);
            //приводим тип к дате
            DateTime i;
            if (DateTime.TryParse(result, out i))
                return i;
            //если парсинг неудачный - возвращаем минимальное значение
            return DateTime.MinValue;
        }
        //получаем параметр как дату-время
        public static async Task<DateTime> GetParamDateTimeAsync(string key) {
            return await Task.Factory.StartNew(() => { return GetParamDateTime(key); });
        }
        //получаем параметры
        public static async Task<DataTable> GetParamsAsync(string filter = null) {
            return await Task.Factory.StartNew(() => {
                //строка запроса
                string query = "SELECT * FROM settings";
                //если параметр не нулевой - добавляем в запрос
                if (!string.IsNullOrEmpty(filter))
                    query += " WHERE name LIKE '%" + filter + "%' OR value LIKE '%" + filter + "%' ";
                //возвращаю результат запроса таблицей
                return SqlQuery(query);
            });
        }
        //возвращает первый элемент из таблицы как строку
        private static string First(DataTable dataTable) {
            //если строк больше 0, возвращаем значение первого столбца первой строки
            if (dataTable.Rows.Count > 0)
                return dataTable.Rows[0].ItemArray[0].ToString();
            //иначе
            return null;
        }
        //метод для записи логов в базу
        public static void AddLogAsync(string message, string site = "") {
            Task.Factory.StartNew(() => {
                //запрос для записи в лог
                var query = "INSERT INTO `logs` (`datetime`, `site`, `text`) " +
                            "VALUES (@datetime, @site, @message);";
                //создаю команду
                MySqlCommand command = new MySqlCommand(query, connection);
                command.Parameters.Add("@site", MySqlDbType.VarChar).Value = site;
                command.Parameters.Add("@message", MySqlDbType.Text).Value = message;
                command.Parameters.Add("@datetime", MySqlDbType.DateTime).Value = DateTime.Now;
                //выполняю запрос
                ExecuteCommandNonQuery(command);
                //чистка лога
                if (DateTime.Now.Millisecond <= 5)
                    TruncLog();
            });
        }
        //удаляю из лога записи старше 30 дней
        private static void TruncLog() {
            var query = "DELETE FROM `logs`" +
                        "WHERE DATE_SUB(CURDATE(),INTERVAL 90 DAY) > `datetime`";
            MySqlCommand command = new MySqlCommand(query, connection);
            ExecuteCommandNonQuery(command);
        }
        //метод для запроса логов из базы
        public static async Task<DataTable> GetLogAsync(string filter, int limit = 100) {
            return await Task.Factory.StartNew(() => {
                var query = "SELECT * FROM logs ";
                //если параметр не нулевой - добавляем в запрос
                if (!string.IsNullOrEmpty(filter))
                    query += " WHERE text LIKE '%" + filter + "%' OR datetime LIKE '%" + filter + "%' ";
                //ограничение списка
                query += "ORDER BY id DESC " +
                         "LIMIT " + limit;
                //возвращаю результат запроса таблицей
                return SqlQuery(query);
            });
        }
        //запрос карточки товара из базы данных
        public static string GetGood(string arg, string text) {
            //формируем строку запроса sql
            var query = "SELECT json " +
                        "FROM goods " +
                        "WHERE json->'$." + arg + "' LIKE '%" + text + "%';";
            //передаю запрос
            MySqlCommand command = new MySqlCommand(query, connection);
            //command.Parameters.Add("@text", MySqlDbType.VarChar).Value = text;
            //command.Parameters.Add("@arg", MySqlDbType.VarChar).Value = arg;
            var res = command.ExecuteScalar();
            //возвращаю строку
            return res.ToString();
        }
        //обновление данных о товаре
        //dateTime - строка с временем изменения карточки
        //json - строковое представление карточки товара
        //возвращает число измененных строк
        public static int SetGood(int id, string updated, string json) {
            //строка запроса
            var query = "INSERT INTO `goods` (`id`, `json`, `updated`) " +
                        "VALUES (@id, @json, @updated) " +
                        "ON DUPLICATE KEY UPDATE `json` = @json, `updated`=@updated;";
            MySqlCommand command = new MySqlCommand(query, connection);
            command.Parameters.Add("@json", MySqlDbType.JSON).Value = json;
            command.Parameters.Add("@updated", MySqlDbType.VarChar).Value = updated;
            command.Parameters.Add("@id", MySqlDbType.UInt32).Value = id;
            return ExecuteCommandNonQuery(command);
        }
    }
}
