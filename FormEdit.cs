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
        public FormEdit() {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e) {
            FormMain main = this.Owner as FormMain;
            main.bus[main._i].name = textBox1.Text;
            //main.bus[main._i].price = Convert.ToInt32(textBox_Price.Text);
            main.bus[main._i].description = richTextBox1.Text;
            //ActiveForm.Close();
            this.Close();
        }

        private void button2_Click(object sender, EventArgs e) {
            //ActiveForm.Close();
            this.Close();
        }

        private void Form4_Shown(object sender, EventArgs e) {
            FormMain main = this.Owner as FormMain;
            textBox1.Text = main.bus[main._i].name;
            textBox_Price.Text = main.bus[main._i].price.ToString();
            richTextBox1.Text = main.bus[main._i].description;
            WebClient cl = new WebClient();
            for (int i = 0; i < 3; i++) {
                try {
                    pictureBox1.Image = Bitmap.FromStream(
                                            new MemoryStream(
                                                cl.DownloadData(
                                                    main.bus[main._i].images[0].url)));
                    break;
                } catch {
                    Thread.Sleep(1000);
                }
            }
            cl.Dispose();
        }
    }
}
