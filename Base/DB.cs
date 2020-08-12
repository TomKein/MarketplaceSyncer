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
    /// 2. связь с таблицей goods
    /// 3. связь с таблицей settings
    /// </summary>
    class DB {
        private static readonly string connectionString =
            "server=31.31.196.233;database=u0573801_business.ru;user=u0573_businessru;password=123abc123;";
        public MySqlConnection conn = new MySqlConnection(connectionString);
        private object _lock = new object();

        public DB() {
            OpenConnection();
        }
        ~DB() {
            CloseConnection();
        }
        public void OpenConnection() {
            if (conn.State == System.Data.ConnectionState.Closed)
                conn.Open();
        }
        public void CloseConnection() {
            if (conn.State == System.Data.ConnectionState.Open)
                conn.Close();
        }
        public MySqlConnection GetConnection() {
            return conn;
        }
        public DataTable SqlQuery(string q) {
            DataTable table = new DataTable();
            MySqlDataAdapter adapter = new MySqlDataAdapter();
            MySqlCommand command = new MySqlCommand(q, conn);
            adapter.SelectCommand = command;
            adapter.Fill(table);
            return table;
        }
        //запрос информации о товаре из базы данных
        //передаем в запросе идин id или сразу несколько
        public DataTable GetGoods(int id=-1, int[] ids=null) { 
            //формируем строку запроса sql с указанием данных id
            //передаю запрос и возвращаю данные в табличной форме
            return new DataTable();
        }
        //обновление данных о товаре
        public void UpdateGood(int id) {
            //проверим, есть ли в базе запись о такой карточке
            var dt = GetGoods(id:id);
            if (dt.Rows.Count > 0) {
                //запись есть, поэтому обновляем
            } else {
                //записи нет - добавляем
            }
        }
        //обновление таблицы настроек
        //передаем пару ключ-значение
        public void UpdateSetting(string key, string value) {
            //формируем sql строку запроса для обновления настройки
            //отправляем запрос
        }
        //получаем настройки
        //передаем ключ, или массив ключей
        public DataTable GetSettings(string key = null, string[] keys=null) {
            //формируем sql строку запроса для получения настройки
            //отправляем запрос и возвращаю таблицу значений
            return new DataTable();
        }
        //метод для записи логов в базу
        public void ToLog(string site, string message) {
            //формируем sql строку
            var q = "INSERT INTO `logs` (`datetime`, `site`, `text`) VALUES (NOW(), @site, @message)";
            //создаю команду
            MySqlCommand command = new MySqlCommand(q, conn);
            //добавляю параметры
            command.Parameters.Add("@site", MySqlDbType.VarChar).Value = site;
            command.Parameters.Add("@message", MySqlDbType.VarChar).Value = message;
            //выполняю запрос
            ExecuteCommand(command);
        }
        //метод вызова комманд
        private void ExecuteCommand(MySqlCommand c) {
            int res = 0;
            lock (_lock) {
                OpenConnection();
                res = c.ExecuteNonQuery();
            }
            if (res==0) throw new Exception("БД: ошибка записи в лог");
        }
    }
}
