using System;
using System.Windows.Forms;
using Selen.Base;
using Selen.Tools;

namespace Selen {
    public partial class FormSettings : Form {
        //ссылка на объект для работы с бд
        DB _db = DB._db;
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
            dataGridView_Settings.Columns[0].ReadOnly = true;
            //и фиксирую его при горизонтальной прокрутке
            dataGridView_Settings.Columns[0].Frozen = true;
        }
        //метод обработки события загрузки формы
        private void FormSettings_Load(object sender, EventArgs e) {
            GridFill();
        }
        //метод обработки события выхода из режима редактирования ячейки
        private void dataGridView_Settings_CellEndEdit(object sender, DataGridViewCellEventArgs e) {
            //определяю, какая строка изменилась
            var rowIndex = e.RowIndex;
            //определяю значение параметра
            var value = dataGridView_Settings.Rows[rowIndex].Cells[1].Value.ToString();
            //определяю имя параметра
            var name = dataGridView_Settings.Rows[rowIndex].Cells[0].Value.ToString();
            //отправляю запрос на изменение значения параметра в таблицу бд
            var res = _db.SetParam(name, value);
            //вывод в лог
            if (res > 0) Log.Add("БД: сохранено " + name + " = " + value);
            else Log.Add("БД: без изменений");
        }

        private void button_Search_Click(object sender, EventArgs e) {
            GridFill();
        }
    }
}
