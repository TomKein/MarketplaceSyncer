using Selen.Base;
using Selen.Tools;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Selen {
    public partial class FormEdit : Form {
        const string L = "FormEdit: ";
        int _index;
        const string DICTIONARY_FILE = @"..\data\dict.txt";   //словать рус-англ аналогов
        List<string> _eng, _rus;
        WebClient _wc;

        public FormEdit() {
            InitializeComponent();
        }
        private async void button_Ok_Click(object sender, EventArgs e) {
            try {
                //сохраняем значения полей в карточку
                Class365API._bus[_index].name = textBox1.Text;
                Class365API._bus[_index].Price = int.Parse(textBox_Price.Text);
                Class365API._bus[_index].description = richTextBox1.Text;
                var s = await Class365API.RequestAsync("put", "goods", new Dictionary<string, string>() {
                    {"id", Class365API._bus[_index].id},
                    {"name", Class365API._bus[_index].name},
                    {"description", Class365API._bus[_index].description},
                });
                if (s.Contains("updated"))
                    Log.Add($"{L} карточка обновлена - [{Class365API._bus[_index].id}] " +
                        $"{Class365API._bus[_index].name} -- {Class365API._bus[_index].description}");
                else
                    Log.Add($"{L} ошибка обновления карточки [{Class365API._bus[_index].id}] " +
                        $"{Class365API._bus[_index].name} -- {Class365API._bus[_index].description} -- {s}");
                await UpdateFields();
            } catch (Exception x) {
                Log.Add("FormEdit: ошибка сохранения изменений - " + x.Message);
            }
        }
        private void button_Cancel_Click(object sender, EventArgs e) {
            _wc.Dispose();
            Close();
        }
        async Task UpdateFields() {
            //пробегаемся по индексам
            while (++_index < Class365API._bus.Count) {
                //проверяем карточку
                try {                    
                    if (Class365API.Status == SyncStatus.NeedUpdate || 
                        Class365API.Status == SyncStatus.ActiveFull || 
                        Class365API.IsTimeOver)
                        return;
                    //пропускаем карточки без фото
                    if (Class365API._bus[_index].images.Count == 0)
                        continue;
                    if (UpdateDescDict() || 
                        Class365API._bus[_index].description
                                   .Split('<')
                                   .ToList()
                                   .Skip(1)
                                   .Any(w => !w.StartsWith("p>") &&
                                             !w.StartsWith("br") &&
                                             !w.StartsWith("/p"))) { 
                        textBox1.Text = Class365API._bus[_index].name;
                        textBox_Price.Text = Class365API._bus[_index].Price.ToString();
                        Name = "Редактирование карточки " + _index;
                        await Task.Factory.StartNew(() => { 
                            var byteArray = _wc.DownloadData(Class365API._bus[_index].images[0].url);
                            var ms = new MemoryStream(byteArray);
                            pictureBox1.Image = Bitmap.FromStream(ms);
                        });
                    return;
                    }
                } catch (Exception x) {
                    Log.Add($"{L}ошибка - " + x.Message);
                }
            }
            _wc.Dispose();
            Close();
        }
        bool UpdateDescDict() {
            //для каждого слова из словаря проверим, содержится ли оно в описании
            for (int d = 0; d < _eng.Count; d++) {
                //если содержит английское написание И не содержит такого же на русском ИЛИ содержит
                if (!checkBox1.Checked && 
                    Class365API._bus[_index].description.Contains(_eng[d]) &&
                    !Class365API._bus[_index].description.Contains(_rus[d])) {
                    richTextBox1.Text = Class365API._bus[_index].description.Replace(_eng[d], _eng[d] + " / " + _rus[d]);
                    return true;
                }
                //если отмечен чек бокс, то ищем наоборот
                if (checkBox1.Checked && 
                    Class365API._bus[_index].description.Contains(_eng[d] + " / " + _rus[d])) {
                    richTextBox1.Text = Class365API._bus[_index].description.Replace(_eng[d] + " / " + _rus[d], _eng[d]);
                    return true;
                }
            }
            richTextBox1.Text = Class365API._bus[_index].description;
            return false;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e) {
            if (checkBox1.Checked)
                DB.SetParam("descriptionEditRevese", "1");
            else
                DB.SetParam("descriptionEditRevese", "0");
            _index = -1;
            UpdateFields();   
        }

        private void FormEdit_Shown(object sender, EventArgs e) {
            _wc = new WebClient();
            _index = -1;
            //загрузим словари
            _eng = new List<string>();
            _rus = new List<string>();
            List<string> file = new List<string>(File.ReadAllLines(DICTIONARY_FILE, Encoding.UTF8));
            foreach (var s in file) {
                var ar = s.Split(',');
                _eng.Add(ar[0]);
                _rus.Add(ar[1]);
            }
            if (DB.GetParamBool("descriptionEditRevese")) {
                checkBox1.Checked = true;
            } else {
                checkBox1.Checked = false;
                UpdateFields();
            }
        }
    }
}
