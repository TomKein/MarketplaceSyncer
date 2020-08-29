using System;
using System.Windows.Forms;
using Selen.Base;
using Selen.Tools;

namespace Selen {
    public partial class FormSettings : Form {
        //ссылка на объект для работы с бд
        DB _db = DB._db;
        //сохраняю имя параметра
        string _name = null;
        //сохраняю значение параметра
        string _value = null;
        //конструктор
        public FormSettings() {
            InitializeComponent();
        }
        //метод заполнения таблицы значениями параметров из таблицы settings бд
        void GridFill() {
            //получаю таблицу параметров, в качестве параметра передаю значение поля для поиска
            var dt = _db.GetParams(textBox_Search.Text);
            //даю таблицу датагриду
            dataGridView_Settings.DataSource = dt;
            //первый столбец (ключи) ставлю только для чтения
            //dataGridView_Settings.Columns[0].ReadOnly = true;
            //и фиксирую его при горизонтальной прокрутке
            dataGridView_Settings.Columns[0].Frozen = true;
        }
        //метод обработки события загрузки формы
        private void FormSettings_Load(object sender, EventArgs e) {
            GridFill();
        }
        //метод обработки события начала режима редактирования
        private void dataGridView_Settings_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e) {
            //запоминаю имя параметра
            _name = dataGridView_Settings.Rows[e.RowIndex].Cells[0].Value.ToString();
            //запоминаю значение параметра
            _value = dataGridView_Settings.Rows[e.RowIndex].Cells[1].Value.ToString();
        }
        //метод обработки события завершения редактирования
        private void dataGridView_Settings_CellEndEdit(object sender, DataGridViewCellEventArgs e) {
            //определяю имя параметра
            var name = dataGridView_Settings.Rows[e.RowIndex].Cells[0].Value.ToString();
            //определяю значение параметра
            var value = dataGridView_Settings.Rows[e.RowIndex].Cells[1].Value.ToString();
            //если изменили имя параметра
            if (name != _name) {
                var res1 = _db.UpdateParamName(_name, name);
                if (res1 > 0) Log.Add("БД: обновлено имя параметра " + _name + " => " + name);
            }
            //если изменили значение параметра
            else if (value != _value) {
                //отправляю запрос на изменение значения параметра в таблицу бд
                var res = _db.SetParam(name, value);
                if (res > 0) Log.Add("БД: сохранено " + name + " = " + value);
            }
            else Log.Add("БД: без изменений");
        }

        private void button_Clear_Click(object sender, EventArgs e) {
            textBox_Search.Text = "";
        }

        private void textBox_Search_TextChanged(object sender, EventArgs e) {
            GridFill();
        }
    }
}
