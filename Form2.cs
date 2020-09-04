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
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Selen
{
    public partial class Form2 : Form
    {
        int ind;
        private string mode;
        List<int> bus_sel = new List<int>();
        private int time;

        public Form2(string targ, int i, List<int> sel)
        {
            InitializeComponent();
            ind = i;
            bus_sel.AddRange(sel);
            mode = targ;
            time = 120;
        }

        private void LoadImage(int v, string url)
        {
            Form1 main = this.Owner as Form1;
            WebClient cl = new WebClient();
            byte[] im;
            for(int i=0; i<3; i++)
            {
                if (url.Contains("http"))
                {
                    try
                    {
                        im = cl.DownloadData(url);
                        switch (v)
                        {
                            case 1:
                                this.pictureBox1.Image = Bitmap.FromStream(new MemoryStream(im));
                                break;
                            case 2:
                                this.pictureBox2.Image = Bitmap.FromStream(new MemoryStream(im));
                                break;
                        }
                        System.Threading.Thread.Sleep(100);
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Add("ошибка загрузки картинки\n" + ex.Message);
                        System.Threading.Thread.Sleep(1000);
                    }
                }
            }
        }

        private void button_OkClick(object sender, EventArgs e)
        {
            Form1 main = this.Owner as Form1;
            main.BindedName = listBox1.Text;
            this.Close();
            //ActiveForm.Close();
        }

        private void Form2_Shown(object sender, EventArgs e)
        {
            Form1 main = this.Owner as Form1;
            string url_img1;
            switch (mode)
            {
                case "avito":
                    textBox1.Text = main.avito[ind].name;
                    url_img1 = main.avito[ind].image;
                    break;
                case "auto":
                    textBox1.Text = main.auto[ind].name;
                    url_img1 = main.auto[ind].image;
                    break;
                default:
                    textBox1.Text = main.drom[ind].name;
                    url_img1 = main.drom[ind].image;
                    break;
            }

            for (int u = 0; u < bus_sel.Count; u++)
            {
                listBox1.Items.Add(main.bus[bus_sel[u]].name);
            }
            if (listBox1.Items.Count > 0)
            {
                listBox1.SelectedIndex = 0;
                LoadImage(1, url_img1);
            }
            timer_form2.Enabled = true;
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            Form1 main = this.Owner as Form1;
            var b = bus_sel[listBox1.SelectedIndex];
            if (listBox1.SelectedIndex > -1 && listBox1.SelectedIndex < bus_sel.Count && main.bus[b].images.Count>0)
            {
                LoadImage(2, main.bus[b].images[0].url);
            }
            else
            {
                Log.Add("ИНДЕКС ЗА ПРЕДЕЛАМИ !!");
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            Form1 main = this.Owner as Form1;
            //можно сделать живую сортировку в листе, но пока не надо
        }

        private void timer_form2_Tick(object sender, EventArgs e)
        {
            if (time <= 0)
            {
                timer_form2.Enabled = false;
                button2.PerformClick();
            }
            button2.Text = "Пропустить (" + time + ")";
            time--;
        }
    }
}
