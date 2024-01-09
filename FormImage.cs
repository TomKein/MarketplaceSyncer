using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using Selen.Tools;

namespace Selen {
    public partial class FormImage : Form {
        int _i;
        public FormImage(int i) {
            _i = i;
            InitializeComponent();
        }

        private void ImageForm_Shown(object sender, EventArgs e) {
            FormMain main = this.Owner as FormMain;
            this.Text = Class365API._bus[_i].name;
            WebClient cl = new WebClient();
            for (int i = 0; i < 3; i++) {
                try {
                    var byteArray = cl.DownloadData(Class365API._bus[_i].images[0].url);
                    var ms = new MemoryStream(byteArray);
                    var bm = Bitmap.FromStream(ms);
                    var h = 800;
                    var w = (bm.Width / bm.Height) * h;
                    this.Size = new Size(w, h);
                    pictureBox1.Size = new Size(w, h);
                    pictureBox1.Image = bm;
                    break;
                } catch (Exception x) {
                    Log.Add("FormEdit: ошибка загрузки фотографии - " + x.Message);
                    Thread.Sleep(500);
                }
            }
            cl.Dispose();
        }

        private void pictureBox1_Click(object sender, EventArgs e) {
            this.Close();
        }
    }
}
