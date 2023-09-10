using Selen.Tools;
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

namespace Selen {
    public partial class FormEdit : Form {
        int _i;
        public FormEdit(int i) {
            _i = i;
            InitializeComponent();
        }

        private void button_Ok_Click(object sender, EventArgs e) {
            try {
                FormMain._bus[_i].name = textBox1.Text;
                FormMain._bus[_i].price = int.Parse(textBox_Price.Text);
                FormMain._bus[_i].description = richTextBox1.Text;
            } catch (Exception x) {
                Log.Add("FormEdit: ошибка сохранения изменений - " + x.Message);
            }
            this.Close();
        }

        private void button_Cancel_Click(object sender, EventArgs e) {
            this.Close();
        }

        private void Form4_Shown(object sender, EventArgs e) {
            FormMain main = this.Owner as FormMain;
            textBox1.Text = FormMain._bus[_i].name;
            textBox_Price.Text = FormMain._bus[_i].price.ToString();
            richTextBox1.Text = FormMain._bus[_i].description;
            WebClient cl = new WebClient();
            for (int i = 0; i < 3; i++) {
                try {
                    var byteArray = cl.DownloadData(FormMain._bus[_i].images[0].url);
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
