using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Selen.Base {
    /// <summary>
    /// 1. класс будет обеспечивать соединение с бд
    /// 2. связь с таблицей logs
    /// 3. связь с таблицей settings
    /// 4. связь с таблицей goods
    /// </summary>
    class DB {
        //строка подключения
        private static readonly string connectionString =
            "server=31.31.196.233;database=u0573801_business.ru;uid=u0573_businessru;pwd=123abc123;charset=utf8;";
        //ссылка на экземпляр себя
        public static DB _db = null;
        //создаю подключение
        public MySqlConnection connection = new MySqlConnection(connectionString);
        private readonly object _lock = new object();
        //конструктор по умолчанию - открывает соединение сразу
        public DB() {
            OpenConnection();
            //утанавливаю кодировку принудительно
            var query = "SET NAMES utf8; " +
                        "SET character_set_server=`utf8`;";
            MySqlCommand command = new MySqlCommand(query, connection);
            command.ExecuteNonQuery();
            //сохраняю ссылку на себя
            _db = this;
        }
        //деструктор - закрывает соединение на всякий случай
        ~DB() {
            CloseConnection();
        }
        //открыть соединение
        public void OpenConnection() {
            if (connection.State == ConnectionState.Closed)
                connection.Open();
        }
        //закрыть соединение
        public void CloseConnection() {
            if (connection.State == ConnectionState.Open)
                connection.Close();
        }
        //ссылка на текущее соединение
        public MySqlConnection GetConnection() {
            return connection;
        }
        //исполнить sql строку с возвратом данных в виде таблицы
        public DataTable SqlQuery(string query) {
            MySqlCommand command = new MySqlCommand(query, connection);
            return ExecuteCommandQuery(command);
        }
        //вызов комманды с получением данных в виде таблицы
        private DataTable ExecuteCommandQuery(MySqlCommand query) {
            DataTable table = new DataTable();
            MySqlDataAdapter adapter = new MySqlDataAdapter();
            adapter.SelectCommand = query;
            lock (_lock) {
                OpenConnection();
                adapter.Fill(table);
            }
            return table;
        }
        //вызов комманды без получения данных, возвращает количество затронутых строк
        private int ExecuteCommandNonQuery(MySqlCommand command) {
            int result = 0;
            lock (_lock) {
                OpenConnection();
                result = command.ExecuteNonQuery();
            }
            return result;
        }
        //изменение названия параметра в таблице settings
        public int UpdateParamName(string nameOld, string nameNew) {
            var query = "UPDATE settings SET name = '" + nameNew + "' WHERE name = '" + nameOld + "';";
            //создаем команду
            MySqlCommand command = new MySqlCommand(query, connection);
            //отправляем запрос, возвращаем результат
            return ExecuteCommandNonQuery(command);
        }
        //добавление либо обновление параметра в таблице settings
        public int SetParam(string name, string value) {
            //запрос для сохранения настроек
            string query = "INSERT INTO `settings` (`name`, `value`) " +
                           "VALUES (@name, @value) " +
                           "ON DUPLICATE KEY UPDATE `value` = @value;";
            //создаем команду
            MySqlCommand command = new MySqlCommand(query, connection);
            command.Parameters.Add("@name", MySqlDbType.VarChar).Value = name;
            command.Parameters.Add("@value", MySqlDbType.VarChar).Value = value;
            //отправляем запрос, возвращаем результат
            return ExecuteCommandNonQuery(command);
        }
        //получаем настройки как строку
        public string GetParamStr(string key) {
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
        public int GetParamInt(string key) {
            //перевызываем метод получения строки
            var result = GetParamStr(key);
            //приводим к числовому типу
            int i;
            if (int.TryParse(result, out i))
                return i;
            return -1;
        }
        //получаем параметр как дату-время
        public DateTime GetParamDateTime(string key) {
            //перевызываем метод получения строки
            var result = GetParamStr(key);
            //приводим тип к дате
            DateTime i;
            if (DateTime.TryParse(result, out i))
                return i;
            //если парсинг неудачный - возвращаем минимальное значение
            return DateTime.MinValue;
        }
        //получаем параметры
        public DataTable GetParams(string filter = null) {
            //строка запроса
            string query = "SELECT * FROM settings";
            //если параметр не нулевой - добавляем в запрос
            if (!string.IsNullOrEmpty(filter))
                query += " WHERE name LIKE '%" + filter + "%' OR value LIKE '%" + filter + "%' ";
            //возвращаю результат запроса таблицей
            return SqlQuery(query);
        }
        //возвращает первый элемент из таблицы как строку
        private string First(DataTable dataTable) {
            //если строк больше 0, возвращаем значение первого столбца первой строки
            if (dataTable.Rows.Count >0)
                return dataTable.Rows[0].ItemArray[0].ToString();
            //иначе
            return null;
        }
        //метод для записи логов в базу
        public void ToLog(string message, string site = "") {
            //запрос для записи в лог
            var query = "INSERT INTO `logs` (`datetime`, `site`, `text`) " +
                        "VALUES (NOW(), @site, @message);";            
            //создаю команду
            MySqlCommand command = new MySqlCommand(query, connection);
            command.Parameters.Add("@site", MySqlDbType.VarChar).Value = site;
            command.Parameters.Add("@message", MySqlDbType.VarChar).Value = message;
            //выполняю запрос
            ExecuteCommandNonQuery(command);
        }
        //запрос карточки товара из базы данных
        public string GetGood(string arg, string text) {
            //формируем строку запроса sql
            var query = "SELECT json " +
                        "FROM goods " +
                        "WHERE json->'$."+arg+"' LIKE '%"+text+"%';";
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
        public int SetGood(int id, string updated, string json) {
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
