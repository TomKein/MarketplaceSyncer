using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Org.BouncyCastle.Asn1.Cms;
using Selen.Base;
using Selen.Tools;

namespace Selen {
    public partial class FormWeightsDimentions : Form {
        //ссылка на объект для работы с бд
        DB _db = DB._db;
        //сохраняю имя параметра
        string _name = null;
        //сохраняю значение параметра
        string _value = null;
        //ссылка на товары
        List<RootObject> _bus;
        //конструктор
        public FormWeightsDimentions(List<RootObject> bus) {
            InitializeComponent();
            _bus = bus;
        }
        //метод заполнения таблицы 
        async Task GridFillAsync() {
            //создаю таблицу
            DataTable dt = await GetDataTableAsync(textBox_Search.Text);
            //даю таблицу датагриду
            dataGridView_Settings.DataSource = dt;
            //первый столбец (sku) ставлю только для чтения
            dataGridView_Settings.Columns[0].ReadOnly = true;
            //второй столбец (name) ставлю только для чтения
            dataGridView_Settings.Columns[1].ReadOnly = true;
            //и фиксирую его при горизонтальной прокрутке
            dataGridView_Settings.Columns[1].Frozen = true;
        }

        private async Task<DataTable> GetDataTableAsync(string text) => await Task.Factory.StartNew(() => {
            var dt = new DataTable();
            dt.Columns.Add("SKU", typeof(int));
            dt.Columns.Add("Наименование", typeof(string));
            dt.Columns.Add("Вес", typeof(string));
            dt.Columns.Add("Длина", typeof(string));
            dt.Columns.Add("Ширина", typeof(string));
            dt.Columns.Add("Высота", typeof(string));
            var busSearch = _bus.Where(w => w.amount > 0 &&
                                            w.price > 0 &&
                                            w.name.ToLowerInvariant()
                                                  .Contains(text.ToLowerInvariant()))
                                .Take(100);
            foreach (var item in busSearch) {
                var width = item.attributes.Find(f => f.Attribute.id == "2283757"); //Ширина
                var heigth = item.attributes.Find(f => f.Attribute.id == "2283758"); //Высота
                var length = item.attributes.Find(f => f.Attribute.id == "2283759"); //Длина
                dt.Rows.Add(item.id, item.name, item.weight, width?.Value.value ?? "", heigth?.Value.value ?? "", length?.Value.value ?? "");
            }
            return dt;
        });

        //метод обработки события загрузки формы
        private void FormWeightsDimentions_Load(object sender, EventArgs e) {
            GridFillAsync();
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
                if (res1 > 0)
                    Log.Add("БД: обновлено имя параметра " + _name + " => " + name);
            }
            //если изменили значение параметра
            else if (value != _value) {
                //отправляю запрос на изменение значения параметра в таблицу бд
                var res = _db.SetParam(name, value);
                if (res > 0)
                    Log.Add("БД: сохранено " + name + " = " + value);
            } else
                Log.Add("БД: без изменений");
        }

        private void button_Clear_Click(object sender, EventArgs e) {
            textBox_Search.Text = "";
        }

        private void textBox_Search_TextChanged(object sender, EventArgs e) {
            GridFillAsync();
        }
        private async void FormWeightsDimentions_FormClosed(object sender, FormClosedEventArgs e) {
            Log.Level = await _db.GetParamIntAsync("logSize");
        }
    }
}
