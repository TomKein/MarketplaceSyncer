using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
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
        int _startRowIndex;
        int _startColumnIndex;
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
            dataGridView.DataSource = dt;
            dataGridView.AllowUserToAddRows = false;
            //первый столбец (sku) ставлю только для чтения
            dataGridView.Columns[0].MinimumWidth = 40;
            dataGridView.Columns[0].Width = 80;
            dataGridView.Columns[0].ReadOnly = true;
            dataGridView.Columns[1].Width = 350;
            dataGridView.Columns[1].Frozen = true;
            //выравнивание
            for (var i = 2; i < dataGridView.Columns.Count; i++) {
                dataGridView.Columns[i].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            dataGridView.MultiSelect = true;
        }

        private async Task<DataTable> GetDataTableAsync(string text) => await Task.Factory.StartNew(() => {
            var dt = new DataTable();
            dt.Columns.Add("SKU", typeof(string));
            dt.Columns.Add("Наименование", typeof(string));
            dt.Columns.Add("Вес", typeof(string));
            dt.Columns.Add("Ширина", typeof(string));
            dt.Columns.Add("Высота", typeof(string));
            dt.Columns.Add("Длина", typeof(string));

            var textWords = text.ToLowerInvariant().Split(' ');

            //выражение для фильтра товаров
            var busSearch = _bus.Where(w => (w.group_id == "2060149" || //если в группе черновики
                                            w.amount > 0 &&            //или остаток положительный
                                            w.price > 0 &&              //цена положительная
                                            (w.images.Count > 0 || !checkBox_onlyHaveImage.Checked)) &&         //с фото или если галка только с фото не стоит
                                            (
                                            !(w.attributes != null &&                                           //нет заполненных атрибутов 
                                            w.attributes.Exists(a => a.Attribute.id == DimentionsId["Width"]) &&
                                            w.attributes.Exists(a => a.Attribute.id == DimentionsId["Height"]) &&
                                            w.attributes.Exists(a => a.Attribute.id == DimentionsId["Length"])) ||
                                            !checkBox_EmptyOnly.Checked                                         //или галка только нет заполненных снята
                                            ) &&
                                            (textWords.Length == textWords.Count(textWord => (w.name + w.id).ToLowerInvariant().Contains(textWord)))        //и тескт содержится в названии или номере
                                            )
                                .Reverse()
                                .Take(_tableSize);
            foreach (var item in busSearch) {
                var width = item.attributes?.Find(f => f.Attribute.id == DimentionsId["Width"]);
                var height = item.attributes?.Find(f => f.Attribute.id == DimentionsId["Height"]);
                var length = item.attributes?.Find(f => f.Attribute.id == DimentionsId["Length"]);
                dt.Rows.Add(item.id, item.name, item.weight ?? 0, width?.Value.value ?? "", height?.Value.value ?? "", length?.Value.value ?? "");
            }
            return dt;
        });

        //метод обработки события загрузки формы
        private void FormWeightsDimentions_Load(object sender, EventArgs e) {
            GridFillAsync();
        }
        //метод обработки события начала режима редактирования
        private void dataGridView_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e) {
            //запоминаю изменяемое значение параметра
            _value = dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString();
            try {

                //подставляю нулевые значения
                if (e.ColumnIndex > 1 && (_value == null || _value == "" || _value == "0")) {
                    var name = dataGridView.Rows[e.RowIndex].Cells[1].Value.ToString();
                    if (!string.IsNullOrEmpty(name)) {
                        var textWords = name.ToLowerInvariant().Split(' ').ToList();
                        List<RootObject> busSearch = null;
                        do {
                            //выражение для фильтра похожих товаров
                            busSearch = _bus.Where(w => w.weight != null && w.weight > 0 &&                  //указан вес
                                                        w.attributes != null &&                              //заполненны атрибуты
                                                        w.attributes.Exists(a => a.Attribute.id == DimentionsId["Width"]) &&
                                                        w.attributes.Exists(a => a.Attribute.id == DimentionsId["Height"]) &&
                                                        w.attributes.Exists(a => a.Attribute.id == DimentionsId["Length"]) &&
                                                        //и тескт содержится в названии
                                                        (textWords.Count == textWords.Count(textWord => w.name.ToLowerInvariant().Contains(textWord)))
                                                    ).ToList();
                            if (!busSearch.Any())
                                textWords.RemoveAt(textWords.Count - 1);
                            else
                                break;
                        } while (textWords.Count > 0);
                        if (busSearch.Any()) {
                            var medianIndex = busSearch.Count / 2;
                            string medianValue = null;
                            List<string> attributes = null;
                            List<float> floats = null;
                            if (e.ColumnIndex == 2) { //заполняем средний вес
                                medianValue = (busSearch.OrderBy(o => o.weight).ElementAt(medianIndex).weight ?? 0).ToString();
                            } else if (e.ColumnIndex == 3) {
                                attributes = busSearch.Select(b => b.attributes.Find(a => a.Attribute.id == DimentionsId["Width"]).Value.value).ToList();
                                floats = attributes.Select(a => float.Parse(a.Replace(".", ","))).ToList();
                                medianValue = floats.OrderBy(o => o).ElementAt(medianIndex).ToString();
                            } else if (e.ColumnIndex == 4) {
                                attributes = busSearch.Select(b => b.attributes.Find(a => a.Attribute.id == DimentionsId["Height"]).Value.value).ToList();
                                floats = attributes.Select(a => float.Parse(a.Replace(".", ","))).ToList();
                                medianValue = floats.OrderBy(o => o).ElementAt(medianIndex).ToString();
                            } else if (e.ColumnIndex == 5) {
                                attributes = busSearch.Select(b => b.attributes.Find(a => a.Attribute.id == DimentionsId["Length"]).Value.value).ToList();
                                floats = attributes.Select(a => float.Parse(a.Replace(".", ","))).ToList();
                                medianValue = floats.OrderBy(o => o).ElementAt(medianIndex).ToString();
                            }
                            dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = medianValue;
                        }
                    }
                }
            } catch (Exception x) {
                Log.Add("веса размеры: ошибка редактирования - " + x.Message + x.InnerException.Message);
            }
        }
        //метод обработки события завершения редактирования
        private async void dataGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e) {
            //новое значения параметров
            var value = dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString().Replace(",", ".");
            //если значение параметра изменилось
            if (value != _value)
                await UpdateBusinessAsync(e.RowIndex, e.ColumnIndex, value);
            else
                Log.Add("БД: без изменений");
        }
        private async Task UpdateBusinessAsync(int rowIndex, int colIndex, string value) {
            var id = dataGridView.Rows[rowIndex].Cells[0].Value.ToString();
            var name = dataGridView.Rows[rowIndex].Cells[1].Value.ToString();
            try {
                //определяю, какой именно параметр менялся
                if (colIndex == 1) {
                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                                {"id", id},
                                {"name", value}
                        });
                    _bus.Find(f => f.id == id).name = value;
                    Log.Add("БД: обновлено наименование = " + value);
                }
                if (colIndex == 2) {
                    await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>{
                                {"id", id},
                                {"name", name},
                                {"weight", value}
                        });
                    float floatWeight;
                    if (!float.TryParse(value?.Replace(".", ","), out floatWeight))
                        floatWeight = 0f;
                    _bus.Find(f => f.id == id).weight = floatWeight;
                    Log.Add("БД: обновлена характеристика Вес = " + value);
                } else if (colIndex == 3)
                    await UpdateDimention(value, id, "Width");
                else if (colIndex == 4)
                    await UpdateDimention(value, id, "Height");
                else if (colIndex == 5) {
                    await UpdateDimention(value, id, "Length");
                }
            } catch (Exception x) {
                Log.Add("веса размеры: ошибка выхода из редактирования - " + x.Message + x.InnerException.Message);
            }
        }
        private async Task UpdateDimention(string value, string id, string attributeName) {
            try {
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
                        _bus.Find(f => f.id == id)
                            .attributes
                            .Find(f => f.Attribute.id == DimentionsId[attributeName])
                            .Value
                            .value = value;
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
                                Value = new Value() { value = value }
                            });
                        Log.Add("БД: добавлена характеристика " + attributeName + " = " + value);
                    }

                }
            } catch (Exception x) {
                Log.Add("веса размеры: ошибка обновления характеристик - " + x.Message + x.InnerException.Message);
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

        private async void dataGridView_CellMouseUp(object sender, DataGridViewCellMouseEventArgs e) {
            if (e.RowIndex == _startRowIndex && e.ColumnIndex == _startColumnIndex)
                return;
            for (int c = _startColumnIndex; c <= e.ColumnIndex; c++) {
                var cellValue = (string) dataGridView.Rows[_startRowIndex].Cells[c].Value;
                if (!string.IsNullOrEmpty(cellValue))
                    for (int r = _startRowIndex; r < e.RowIndex; r++) {
                        dataGridView.Rows[r + 1].Cells[c].Value = cellValue;
                        await UpdateBusinessAsync(r + 1, c, cellValue);
                    }
            }
        }

        private void dataGridView_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e) {
            _startColumnIndex = e.ColumnIndex;
            _startRowIndex = e.RowIndex;
        }

        private void dataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e) {
            if (e.ColumnIndex == 0)
                try {
                    Clipboard.SetText(dataGridView.Rows[e.RowIndex].Cells[0].Value.ToString());
                } catch (Exception x) {
                    Log.Add("ошибка копирования в буфер обмена - "+x.Message);
                }
        }

        private void dataGridView_CellMouseEnter(object sender, DataGridViewCellEventArgs e) {
            if (e.ColumnIndex == 0) {
                var tooltip = new ToolTip();
                tooltip.AutoPopDelay = 5000;
                tooltip.InitialDelay = 1000;
                tooltip.ReshowDelay = 500;

                tooltip.ShowAlways = true;
                tooltip.SetToolTip(this.dataGridView, "Here we are!");

                //MessageBox.Show("Here "+_bus.First(b=>b.id == dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString()).name);
                //Form form = new Form();
                //Bitmap img = new Bitmap(width:100,height:100);
                //img.

                //    form.StartPosition = FormStartPosition.CenterScreen;
                //    form.Size = img.Size;

                //    PictureBox pb = new PictureBox();
                //    pb.Dock = DockStyle.Fill;
                //    pb.Image = img;

                //    form.Controls.Add(pb);
                //    form.ShowDialog();

            }
        }
    }

    //public class CustomToolTip : ToolTip {
    //    public CustomToolTip() { 
    //        this.OwnerDraw= true;
    //        this.Popup += new PopupEventHandler(this.OnPopup);
    //        this.Draw += new DrawToolTipEventHandler(this.OnDraw);
    //    }
    //    private void OnPopup(object sender, PopupEventArgs e) {
    //        e.ToolTipSize = new Size(200, 200);
    //    }
    //    private void OnDraw(object sender, DrawToolTipEventArgs e) {

    //    }
    //    protected override void OnPaint(PaintEventArgs e) {
    //        base.OnPaint(e);
    //        Image img = Image.FromFile("c:\Temp\1.jpg");
    //    }
    //}
}
