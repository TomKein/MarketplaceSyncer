using Selen.Tools;
using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace Selen {
    public partial class FormEdit : Form {
        GoodObject _b;
        public FormEdit(GoodObject b) {
            _b = b;
            InitializeComponent();
        }

        private void button_Ok_Click(object sender, EventArgs e) {
            try {
                _b.name = textBox1.Text;
                _b.Price = int.Parse(textBox_Price.Text);
                _b.description = richTextBox1.Text;
            } catch (Exception x) {
                Log.Add("FormEdit: ошибка сохранения изменений - " + x.Message);
            }
            Close();
        }

        private void button_Cancel_Click(object sender, EventArgs e) {
            Close();
        }

        private void Form4_Shown(object sender, EventArgs e) {
            //FormMain main = Owner as FormMain;
            textBox1.Text = _b.name;
            textBox_Price.Text = _b.Price.ToString();
            richTextBox1.Text = _b.description;
            WebClient cl = new WebClient();
            for (int i = 0; i < 3; i++) {
                try {
                    var byteArray = cl.DownloadData(_b.images[0].url);
                    var ms = new MemoryStream(byteArray);
                    pictureBox1.Image = Bitmap.FromStream(ms);
                    break;
                } catch (Exception x) {
                    Log.Add("FormEdit: ошибка загрузки фотографии - " + x.Message);
                    Thread.Sleep(500);
                }
            }
            cl.Dispose();
        }
    }
}
