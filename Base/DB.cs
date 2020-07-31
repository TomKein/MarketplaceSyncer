using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Selen.Base {
    class DB {
        private static readonly string connectionString =
            "server=31.31.196.233;database=u0573801_business.ru;user=u0573_businessru;password=123abc123;";
        public MySqlConnection conn = new MySqlConnection(connectionString);

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
    }
}
