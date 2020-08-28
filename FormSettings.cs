using System;
using System.Windows.Forms;
using Selen.Base;

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
            //получаю таблицу параметров
            var dt = _db.GetParamsAll();
            //прикрепляю к датагриду формы
            dataGridView_Settings.DataSource = dt;
            dataGridView_Settings.Columns[0].ReadOnly = true;
            dataGridView_Settings.Columns[0].Frozen = true;
        }
        //метод обработки события загрузки формы
        private void FormSettings_Load(object sender, EventArgs e) {
            GridFill();
        }
        //метод обработки события выхода из режима редактирования ячейки
        private void dataGridView_Settings_CellEndEdit(object sender, DataGridViewCellEventArgs e) {
            //определяю, какой столбец изменился, 
        }
    }
}
