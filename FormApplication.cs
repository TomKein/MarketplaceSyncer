using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Selen.Tools;

namespace Selen {
    public partial class FormApplication : Form {

        List<RootObject> _bus;
        List<string> _generations;
        List<string> _attributes;
        WebClient _wc;
        string _genFile = @"..\avito_generations.txt";
        readonly string _applicationAttribureId = "2543011";
        string _l = "FormApplication: ";
        int _busIndex = -1;
        DataTable _dtSelected = new DataTable();
        //конструктор
        public FormApplication(List<RootObject> bus) {
            InitializeComponent();
            _bus = bus;
            _busIndex = _bus.Count();
            var t = File.ReadAllLines(_genFile);
            _generations = new List<string>(t);
            _attributes = _bus.Where(w=>w.attributes!=null && w.attributes.Any(a=>a.Attribute.id == _applicationAttribureId))
                              .Select(s=>s.attributes.First(f=>f.Attribute.id== _applicationAttribureId).Value.name)
                              .Distinct().ToList();
            _wc = new WebClient();
        }
        //загрузка формы
        void FormApplication_Load(object sender, EventArgs e) {
            GridFillAsync();
            GoNextGood(back: true);
            FillGoodInfo();
            GridSelectedFillAsync();
        }

        //подгрузка в форму информации о товаре
        async Task FillGoodInfo() {
            try {
                textBoxName.Text = _bus[_busIndex].name;
                richTextBoxDesc.Text = _bus[_busIndex].description;
                labelPrice.Text = "Цена: " + _bus[_busIndex].price;
                labelAmount.Text = "Кол-во: " + _bus[_busIndex].amount;
                labelId.Text = "Index: " + _busIndex + "/" + _bus.Count;
                var byteArray = _wc.DownloadData(_bus[_busIndex].images[0].url);
                var ms = new MemoryStream(byteArray);
                pictureBoxImage.Image = Bitmap.FromStream(ms);
            } catch (Exception x) {
                Log.Add(x.Message);
            }
        }
        //получить индекс карточки для обработки
        private void GoNextGood(bool back = false) {
            var adding = back ? -1 : 1;
            for (int b = _busIndex + adding; b < _bus.Count && b>-1; b += adding) {
                if (_bus[b].attributes != null &&
                    _bus[b].attributes.Any(c => c.Attribute.id == _applicationAttribureId) ||
                    _bus[b].images.Count == 0 ||
                    _bus[b].GroupName() == "Автохимия" ||
                    _bus[b].GroupName() == "Масла" ||
                    _bus[b].GroupName().Contains("Инструменты"))
                    continue;
                if (b == 0) { b = _bus.Count(); }
                _busIndex = b;
                return;
            }
            _busIndex = 0;
        }
        //заполняю таблицу применимостей на форме
        async Task GridFillAsync() {
            DataTable dt = await GetDataTableAsync(textBoxSearch.Text);
            dataGridViewAvito.DataSource = dt;
            dataGridViewAvito.AllowUserToAddRows = false;
            dataGridViewAvito.Columns[0].MinimumWidth = 180;
            dataGridViewAvito.Columns[0].Width = 280;
            dataGridViewAvito.Columns[0].ReadOnly = true;
            labelAvito.Text = "Доступные применимости " + dt.Rows.Count + " шт. (поиск)";
        }
        //создаю таблицу применимостей
        async Task<DataTable> GetDataTableAsync(string text) => await Task.Factory.StartNew(() => {
            var dt = new DataTable();
            dt.Columns.Add("Марка / Модель / Поколение", typeof(string));

            var textWords = text.ToLowerInvariant().Split(' ');

            var genSearch = _generations.Where(gen => textWords.Length == textWords.Count(tw => gen.ToLowerInvariant().Contains(tw)));
            foreach (var item in genSearch) {
                dt.Rows.Add(item);
            }
            return dt;
        });
        //создаю таблицу рекомендаций применимостей
        async Task GridSelectedFillAsync() {
            try {
                _dtSelected = await GetDataTableSelectedAsync();
                dataGridViewSelect.DataSource = _dtSelected;
                dataGridViewSelect.AllowUserToAddRows = false;
                dataGridViewSelect.Columns[0].MinimumWidth = 180;
                dataGridViewSelect.Columns[0].Width = 280;
                dataGridViewSelect.Columns[0].ReadOnly = true;
            } catch (Exception x) {
                Log.Add(_l+ "GridSelectedFillAsync - "+x.Message);
            }
        }
        //фильтр для таблицы рекомендаций
        async Task<DataTable> GetDataTableSelectedAsync() => await Task.Factory.StartNew(() => {
            var dt = new DataTable();
            dt.Columns.Add("Марка / Модель / Поколение", typeof(string));

            var textWords = (_bus[_busIndex].name + " " + _bus[_busIndex].HtmlDecodedDescription())
                                                                   .ToLowerInvariant()
                                                                   .Split(' ')
                                                                   .Where(w => w.Length > 1)?
                                                                   .Select(s => s.Trim(new char[] { '/', ',', '.', ':', '-', '!',
                                                                                                    '(', ')', '\\', '[', ']' }));
            for (int i = 10; i > 0; i--) {
                var genSearch = _attributes.Where(gen => textWords.Count(tw => gen.ToLowerInvariant().Contains(tw)) >= i);
                if (genSearch.Any()) {
                    foreach (var gen in genSearch) {
                        if (!dt.AsEnumerable().Any(a => a.ItemArray.Contains(gen))) {
                            dt.Rows.Add(gen);
                        }
                    }
                    //break;
                }
            }
            for (int i = 10; i > 0; i--) {
                var genSearch = _generations.Where(gen => textWords.Count(tw => gen.ToLowerInvariant().Contains(tw)) >= i);
                if (genSearch.Any()) {
                    foreach (var item in genSearch) {
                        //if (!dt.Rows.Contains(item.ToString())) {
                        if (!dt.AsEnumerable().Any(a=>a.ItemArray.Contains(item))) {
                            dt.Rows.Add(item);
                        }
                    }
                    break;
                }
            }
            return dt;
        });
        //обработка строки поиска
        void textBoxSearch_TextChanged(object sender, EventArgs e) {
            GridFillAsync();
        }
        //сохраняю применение в карточки
        async Task ApplyNewApplicationsAsync() {
            //значение применимости
            var applicationValue = dataGridViewSelect.SelectedCells[0].Value.ToString();
            //запрашиваю в бизнесе значение
            var s = await Class365API.RequestAsync("get", "attributesforgoodsvalues", new Dictionary<string, string> {
                    { "attribute_id" , "2543011"},
                    { "name", applicationValue}
                });
            var attributesforgoodsvalues = new List<ApplicationItem>();
            if (s.Contains("name"))
                attributesforgoodsvalues = JsonConvert.DeserializeObject<List<ApplicationItem>>(s);
            else {
                //если значение атрибута не найдено - добавляем
                s = await Class365API.RequestAsync("post", "attributesforgoodsvalues", new Dictionary<string, string> {
                            { "attribute_id", "2543011"},
                            { "name", applicationValue }
                        });
                if (s.Contains("updated")) {
                    Log.Add("UpdateApplicationListAsync: " + applicationValue + " - применимость добавлена успешно");
                }
                //Thread.Sleep(500);
                //запрашиваю значение атрибута, чтобы узнать его id
                s = await Class365API.RequestAsync("get", "attributesforgoodsvalues", new Dictionary<string, string> {
                        { "attribute_id" , "2543011"},
                        { "name", applicationValue}
                    });
                if (s.Contains("name"))
                    attributesforgoodsvalues = JsonConvert.DeserializeObject<List<ApplicationItem>>(s);
            }
            //добавляю атрибут в карточку
            s = await Class365API.RequestAsync("post", "goodsattributes", new Dictionary<string, string>() {
                    {"good_id", _bus[_busIndex].id},
                    {"attribute_id", _applicationAttribureId},
                    {"value_id", attributesforgoodsvalues[0].id.ToString()}
                });
            if (s != null && s.Contains("updated")) {
                _bus[_busIndex].attributes.Add(
                new Attributes() {
                    Attribute = new Attribute() { id = _applicationAttribureId },
                    Value = new Value() { name = applicationValue }
                });
                Log.Add(_l + _bus[_busIndex].name + " - добавлена характеристика применимость - " + applicationValue);
                //_bus[_busIndex].updated = DateTime.Now.ToString();
            }
        }
        private void dataGridViewSelect_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e) {
            labelSelectedApplications.Text = "Выбранные применимости: " + dataGridViewSelect.RowCount;
        }

        private void dataGridViewSelect_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e) {
            labelSelectedApplications.Text = "Выбранные применимости: " + dataGridViewSelect.RowCount;
        }
        //сохраняю значение по двойному клику на нем
        private void dataGridViewSelect_CellDoubleClick(object sender, DataGridViewCellEventArgs e) {
            //_dtSelected.Rows[e.RowIndex].Delete();
            buttonOk_ClickAsync(sender, e);
        }
        //копирую строку по двойному клику
        private void dataGridViewAvito_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e) {
            _dtSelected.Rows.Add(dataGridViewAvito.Rows[e.RowIndex].Cells[0].Value);
        }
        async void richTextBoxDesc_TextChanged(object sender, EventArgs e) {
            if (String.IsNullOrEmpty(textBoxName.Text) || String.IsNullOrEmpty(richTextBoxDesc.Text))
                return;
            if (_bus[_busIndex].name == textBoxName.Text && _bus[_busIndex].description == richTextBoxDesc.Text)
                return;
            _bus[_busIndex].name = textBoxName.Text;
            _bus[_busIndex].description = richTextBoxDesc.Text;
            var s = await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>() {
                                {"id", _bus[_busIndex].id},
                                {"name", _bus[_busIndex].name},
                                {"description", _bus[_busIndex].description},
                            });
            if (s.Contains("updated"))
                Log.Add("business.ru: " + _bus[_busIndex].name + " - описание карточки обновлено - " + _bus[_busIndex].description);
            else
                Log.Add("business.ru: ошибка сохранения изменений " + _bus[_busIndex].name + " - " + s);
        }

        //сохранить значения и перейти к следующей карточке
        async void buttonOk_ClickAsync(object sender, EventArgs e) {
            Application.UseWaitCursor = true;
            buttonOk.Enabled = false;
            await ApplyNewApplicationsAsync();
            GoNextGood(back: true);
            await FillGoodInfo();
            await GridSelectedFillAsync();
            buttonOk.Enabled = true;
            Application.UseWaitCursor = false;
        }
        //пропустить
        async void buttonSkip_Click(object sender, EventArgs e) {
            GoNextGood(back: true);
            await FillGoodInfo();
            await GridSelectedFillAsync();
        }
        //вернуться к предыдущей карточке
        async void buttonBack_Click(object sender, EventArgs e) {
            GoNextGood();
            await FillGoodInfo();
            await GridSelectedFillAsync();
        }
    }
}
