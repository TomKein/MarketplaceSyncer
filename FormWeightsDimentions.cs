using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Cms;
using Selen.Base;
using Selen.Tools;

namespace Selen {
    public partial class FormWeightsDimentions : Form {
        Dictionary<string, string> DimentionsId = new Dictionary<string, string>(){
            { "Width",  "2283757"},
            { "Height", "2283758"},
            { "Length", "2283759"}
        };
        //размер таблицы, строк
        int _tableSize;

        //сохраняю параметры
        string _sku = null;
        string _value = null;
        //ссылка на товары
        List<RootObject> _bus;
        //конструктор
        public FormWeightsDimentions(List<RootObject> bus) {
            InitializeComponent();
            _bus = bus;
            _tableSize = DB._db.GetParamInt("tableWeightsDimentionsSize");

        }
        //метод заполнения таблицы 
        async Task GridFillAsync() {
            //создаю таблицу
            DataTable dt = await GetDataTableAsync(textBox_Search.Text);
            //даю таблицу датагриду
            dataGridView_Settings.DataSource = dt;
            dataGridView_Settings.AllowUserToAddRows = false;
            //первый столбец (sku) ставлю только для чтения
            dataGridView_Settings.Columns[0].MinimumWidth = 40;
            dataGridView_Settings.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dataGridView_Settings.Columns[0].ReadOnly = true;
            //второй столбец (name) ставлю только для чтения
            dataGridView_Settings.Columns[0].MinimumWidth = 310;
            dataGridView_Settings.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dataGridView_Settings.Columns[1].ReadOnly = true;
            //и фиксирую его при горизонтальной прокрутке
            dataGridView_Settings.Columns[1].Frozen = true;
            //выравнивание
            dataGridView_Settings.Columns[2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dataGridView_Settings.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dataGridView_Settings.Columns[4].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        private async Task<DataTable> GetDataTableAsync(string text) => await Task.Factory.StartNew(() => {
            var dt = new DataTable();
            dt.Columns.Add("SKU", typeof(string));
            dt.Columns.Add("Наименование", typeof(string));
            dt.Columns.Add("Вес", typeof(string));
            dt.Columns.Add("Ширина", typeof(string));
            dt.Columns.Add("Высота", typeof(string));
            dt.Columns.Add("Длина", typeof(string));
            var busSearch = _bus.Where(w => w.amount > 0 &&
                                            w.price > 0 &&
                                            w.name.ToLowerInvariant()
                                                  .Contains(text.ToLowerInvariant()))
                                .Take(_tableSize);
            foreach (var item in busSearch) {
                var width = item.attributes?.Find(f => f.Attribute.id == DimentionsId["Width"]);
                var height = item.attributes?.Find(f => f.Attribute.id == DimentionsId["Height"]);
                var length = item.attributes?.Find(f => f.Attribute.id == DimentionsId["Length"]);
                dt.Rows.Add(item.id, item.name, item.weight??0, width?.Value.value ?? "", height?.Value.value ?? "", length?.Value.value ?? "");
            }
            return dt;
        });

        //метод обработки события загрузки формы
        private void FormWeightsDimentions_Load(object sender, EventArgs e) {
            GridFillAsync();
        }
        //метод обработки события начала режима редактирования
        private void dataGridView_Settings_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e) {
            //запоминаю изменяемое значение параметра
            _value = dataGridView_Settings.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString();
        }
        //метод обработки события завершения редактирования
        private async void dataGridView_Settings_CellEndEdit(object sender, DataGridViewCellEventArgs e) {
            //новое значения параметров
            var value = dataGridView_Settings.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString();
            //если значение параметра изменилось
            if (value != _value) {
                var id = dataGridView_Settings.Rows[e.RowIndex].Cells[0].Value.ToString();
                var name = dataGridView_Settings.Rows[e.RowIndex].Cells[1].Value.ToString();
                //определяю, какой именно параметр менялся
                if (e.ColumnIndex == 2) {
                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                                {"id", id},
                                {"name", name},
                                {"weight", value}
                        });
                    _bus.Find(f => f.id == id).weight = float.Parse(value.Replace(".",","));
                    Log.Add("БД: обновлена характеристика Вес = " + value);
                } else if (e.ColumnIndex == 3)
                    await UpdateDimention(value, id, "Width");
                else if (e.ColumnIndex == 4)
                    await UpdateDimention(value, id, "Height");
                else if (e.ColumnIndex == 5) {
                    await UpdateDimention(value, id, "Length");
                }
            } else
                Log.Add("БД: без изменений");
        }

        private async Task UpdateDimention(string value, string id, string attributeName) {
            //если атрибут в карточке существует
            if (_bus.Find(f => f.id == id).attributes?.Find(f => f.Attribute.id == DimentionsId[attributeName]) != null) {
                //получим id привязки атрибута
                var attributeId = _bus.Find(f => f.id == id)
                                      .attributes
                                      .Find(f => f.Attribute.id == DimentionsId[attributeName])
                                      .Attribute
                                      .id;
                var s = await Class365API.RequestAsync("get", "goodsattributes", new Dictionary<string, string>() {
                            {"good_id", id},
                            {"attribute_id", attributeId}
                        });
                var ga = JsonConvert.DeserializeObject<List<Goodsattributes>>(s);
                s = await Class365API.RequestAsync("put", "goodsattributes", new Dictionary<string, string>() {
                            {"id", ga[0].id},
                            {"value", value}
                        });
                if (s != null && s.Contains("updated")) {
                    _bus.Find(f=>f.id==id)
                        .attributes
                        .Find(f => f.Attribute.id == DimentionsId[attributeName])
                        .Value
                        .value=value;
                    Log.Add("БД: обновлена характеристика " + attributeName + " = " + value);
                }
            } else { //если атрибута нет - создаем
                var s = await Class365API.RequestAsync("post", "goodsattributes", new Dictionary<string, string>() {
                            {"good_id", id},
                            {"attribute_id", DimentionsId[attributeName]},
                            {"value", value}
                        });
                if (s != null && s.Contains("updated")) {
                    _bus.Find(f => f.id == id).attributes.Add(
                        new Attributes() { 
                            Attribute = new Attribute() { id = DimentionsId[attributeName] },
                            Value = new Value(){ value = value} });
                    Log.Add("БД: добавлена характеристика " + attributeName + " = " + value);
                }

            }
        }

        private void button_Clear_Click(object sender, EventArgs e) {
            textBox_Search.Text = "";
        }

        private void textBox_Search_TextChanged(object sender, EventArgs e) {
            GridFillAsync();
        }
        private async void FormWeightsDimentions_FormClosed(object sender, FormClosedEventArgs e) {
            //Log.Level = await _db.GetParamIntAsync("logSize");
        }
    }
}
