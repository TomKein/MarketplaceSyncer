using System;

namespace Selen {
    partial class FormMain {
        /// <summary>
        /// Обязательная переменная конструктора.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Освободить все используемые ресурсы.
        /// </summary>
        /// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Код, автоматически созданный конструктором форм Windows

        /// <summary>
        /// Требуемый метод для поддержки конструктора — не изменяйте 
        /// содержимое этого метода с помощью редактора кода.
        /// </summary>
        private void InitializeComponent() {
            this.button_Drom = new System.Windows.Forms.Button();
            this.button_BaseGet = new System.Windows.Forms.Button();
            this.button_Avito = new System.Windows.Forms.Button();
            this.logBox = new System.Windows.Forms.RichTextBox();
            this.button_PriceLevelsReport = new System.Windows.Forms.Button();
            this.button_Vk = new System.Windows.Forms.Button();
            this.label_Bus = new System.Windows.Forms.Label();
            this.label_Vk = new System.Windows.Forms.Label();
            this.label_Drom = new System.Windows.Forms.Label();
            this.button_Descriptions = new System.Windows.Forms.Button();
            this.button_TestPartners = new System.Windows.Forms.Button();
            this.label_Auto = new System.Windows.Forms.Label();
            this.label_lastSyncTime = new System.Windows.Forms.Label();
            this.dateTimePicker1 = new System.Windows.Forms.DateTimePicker();
            this.button_PricesIncomeCorrection = new System.Windows.Forms.Button();
            this.label_Cdek = new System.Windows.Forms.Label();
            this.button_SaveCookie = new System.Windows.Forms.Button();
            this.button_Settings = new System.Windows.Forms.Button();
            this.panel_Buttons = new System.Windows.Forms.Panel();
            this.button_ozon = new System.Windows.Forms.Button();
            this.label_YandexMarket = new System.Windows.Forms.Label();
            this.button_YandexMarket = new System.Windows.Forms.Button();
            this.button_Izap24 = new System.Windows.Forms.Button();
            this.panel_bottom = new System.Windows.Forms.Panel();
            this.button_application = new System.Windows.Forms.Button();
            this.button_WeightsDimensions = new System.Windows.Forms.Button();
            this.button_Test = new System.Windows.Forms.Button();
            this.panel_Filter = new System.Windows.Forms.Panel();
            this.textBox_LogFilter = new System.Windows.Forms.TextBox();
            this.button_LogFilterClear = new System.Windows.Forms.Button();
            this.panel4 = new System.Windows.Forms.Panel();
            this.panel_Buttons.SuspendLayout();
            this.panel_bottom.SuspendLayout();
            this.panel_Filter.SuspendLayout();
            this.panel4.SuspendLayout();
            this.SuspendLayout();
            // 
            // button_Drom
            // 
            this.button_Drom.Location = new System.Drawing.Point(5, 93);
            this.button_Drom.Name = "button_Drom";
            this.button_Drom.Size = new System.Drawing.Size(113, 23);
            this.button_Drom.TabIndex = 4;
            this.button_Drom.Text = "Drom.ru";
            this.button_Drom.UseVisualStyleBackColor = true;
            this.button_Drom.Click += new System.EventHandler(this.ButtonDromRu_Click);
            // 
            // button_BaseGet
            // 
            this.button_BaseGet.Location = new System.Drawing.Point(5, 41);
            this.button_BaseGet.Name = "button_BaseGet";
            this.button_BaseGet.Size = new System.Drawing.Size(113, 21);
            this.button_BaseGet.TabIndex = 1;
            this.button_BaseGet.Text = "Запуск";
            this.button_BaseGet.UseVisualStyleBackColor = true;
            this.button_BaseGet.Click += new System.EventHandler(this.BaseGet);
            // 
            // button_Avito
            // 
            this.button_Avito.Location = new System.Drawing.Point(5, 65);
            this.button_Avito.Name = "button_Avito";
            this.button_Avito.Size = new System.Drawing.Size(113, 25);
            this.button_Avito.TabIndex = 3;
            this.button_Avito.Text = "Avito.ru";
            this.button_Avito.UseVisualStyleBackColor = true;
            this.button_Avito.Click += new System.EventHandler(this.ButtonAvitoRu_Click);
            // 
            // logBox
            // 
            this.logBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.logBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logBox.Font = new System.Drawing.Font("Lucida Console", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.logBox.ForeColor = System.Drawing.Color.LightYellow;
            this.logBox.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.logBox.Location = new System.Drawing.Point(139, 0);
            this.logBox.Name = "logBox";
            this.logBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.logBox.Size = new System.Drawing.Size(645, 282);
            this.logBox.TabIndex = 16;
            this.logBox.Text = "";
            this.logBox.TextChanged += new System.EventHandler(this.RichTextBox1_TextChanged);
            // 
            // button_PriceLevelsReport
            // 
            this.button_PriceLevelsReport.Location = new System.Drawing.Point(670, 8);
            this.button_PriceLevelsReport.Name = "button_PriceLevelsReport";
            this.button_PriceLevelsReport.Size = new System.Drawing.Size(107, 23);
            this.button_PriceLevelsReport.TabIndex = 20;
            this.button_PriceLevelsReport.Text = "Остатки по ценам";
            this.button_PriceLevelsReport.UseVisualStyleBackColor = true;
            this.button_PriceLevelsReport.Click += new System.EventHandler(this.PriceLevelsRemainsReport);
            // 
            // button_Vk
            // 
            this.button_Vk.Location = new System.Drawing.Point(5, 119);
            this.button_Vk.Name = "button_Vk";
            this.button_Vk.Size = new System.Drawing.Size(113, 23);
            this.button_Vk.TabIndex = 7;
            this.button_Vk.Text = "Vk.com";
            this.button_Vk.UseVisualStyleBackColor = true;
            this.button_Vk.Click += new System.EventHandler(this.ButtonVkCom_Click);
            // 
            // label_Bus
            // 
            this.label_Bus.AutoSize = true;
            this.label_Bus.Location = new System.Drawing.Point(120, 45);
            this.label_Bus.Name = "label_Bus";
            this.label_Bus.Size = new System.Drawing.Size(16, 13);
            this.label_Bus.TabIndex = 36;
            this.label_Bus.Text = "...";
            // 
            // label_Vk
            // 
            this.label_Vk.AutoSize = true;
            this.label_Vk.Location = new System.Drawing.Point(120, 124);
            this.label_Vk.Name = "label_Vk";
            this.label_Vk.Size = new System.Drawing.Size(16, 13);
            this.label_Vk.TabIndex = 38;
            this.label_Vk.Text = "...";
            // 
            // label_Drom
            // 
            this.label_Drom.AutoSize = true;
            this.label_Drom.Location = new System.Drawing.Point(120, 98);
            this.label_Drom.Name = "label_Drom";
            this.label_Drom.Size = new System.Drawing.Size(16, 13);
            this.label_Drom.TabIndex = 58;
            this.label_Drom.Text = "...";
            // 
            // button_Descriptions
            // 
            this.button_Descriptions.Location = new System.Drawing.Point(141, 7);
            this.button_Descriptions.Name = "button_Descriptions";
            this.button_Descriptions.Size = new System.Drawing.Size(68, 24);
            this.button_Descriptions.TabIndex = 59;
            this.button_Descriptions.Text = "Описания";
            this.button_Descriptions.UseVisualStyleBackColor = true;
            this.button_Descriptions.Click += new System.EventHandler(this.ButtonDescriptionsEdit_Click);
            // 
            // button_TestPartners
            // 
            this.button_TestPartners.Location = new System.Drawing.Point(513, 8);
            this.button_TestPartners.Name = "button_TestPartners";
            this.button_TestPartners.Size = new System.Drawing.Size(73, 23);
            this.button_TestPartners.TabIndex = 61;
            this.button_TestPartners.Text = "Задвоения контрагентов";
            this.button_TestPartners.UseVisualStyleBackColor = true;
            this.button_TestPartners.Click += new System.EventHandler(this.ButtonTestPartnersClick);
            // 
            // label_Auto
            // 
            this.label_Auto.AutoSize = true;
            this.label_Auto.Location = new System.Drawing.Point(115, 148);
            this.label_Auto.Name = "label_Auto";
            this.label_Auto.Size = new System.Drawing.Size(0, 13);
            this.label_Auto.TabIndex = 66;
            // 
            // label_lastSyncTime
            // 
            this.label_lastSyncTime.AutoSize = true;
            this.label_lastSyncTime.Location = new System.Drawing.Point(3, 3);
            this.label_lastSyncTime.Name = "label_lastSyncTime";
            this.label_lastSyncTime.Size = new System.Drawing.Size(119, 13);
            this.label_lastSyncTime.TabIndex = 98;
            this.label_lastSyncTime.Text = "Посл. синхронизация:";
            // 
            // dateTimePicker1
            // 
            this.dateTimePicker1.CalendarForeColor = System.Drawing.Color.Red;
            this.dateTimePicker1.CustomFormat = "dd/MM/yyyy HH:mm";
            this.dateTimePicker1.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dateTimePicker1.Location = new System.Drawing.Point(5, 18);
            this.dateTimePicker1.MaxDate = new System.DateTime(2099, 12, 31, 0, 0, 0, 0);
            this.dateTimePicker1.MinDate = new System.DateTime(2017, 1, 1, 0, 0, 0, 0);
            this.dateTimePicker1.Name = "dateTimePicker1";
            this.dateTimePicker1.ShowUpDown = true;
            this.dateTimePicker1.Size = new System.Drawing.Size(113, 20);
            this.dateTimePicker1.TabIndex = 99;
            this.dateTimePicker1.Value = new System.DateTime(2020, 9, 2, 5, 50, 0, 0);
            this.dateTimePicker1.ValueChanged += new System.EventHandler(this.dateTimePicker1_ValueChanged);
            // 
            // button_PricesIncomeCorrection
            // 
            this.button_PricesIncomeCorrection.Location = new System.Drawing.Point(416, 8);
            this.button_PricesIncomeCorrection.Name = "button_PricesIncomeCorrection";
            this.button_PricesIncomeCorrection.Size = new System.Drawing.Size(92, 23);
            this.button_PricesIncomeCorrection.TabIndex = 115;
            this.button_PricesIncomeCorrection.Text = "Корекция цен закупки";
            this.button_PricesIncomeCorrection.UseVisualStyleBackColor = true;
            this.button_PricesIncomeCorrection.Click += new System.EventHandler(this.Button_PricesCheck_Click);
            // 
            // label_Cdek
            // 
            this.label_Cdek.AutoSize = true;
            this.label_Cdek.Location = new System.Drawing.Point(120, 150);
            this.label_Cdek.Name = "label_Cdek";
            this.label_Cdek.Size = new System.Drawing.Size(16, 13);
            this.label_Cdek.TabIndex = 122;
            this.label_Cdek.Text = "...";
            // 
            // button_SaveCookie
            // 
            this.button_SaveCookie.Location = new System.Drawing.Point(590, 8);
            this.button_SaveCookie.Name = "button_SaveCookie";
            this.button_SaveCookie.Size = new System.Drawing.Size(76, 23);
            this.button_SaveCookie.TabIndex = 123;
            this.button_SaveCookie.Text = "Сохр. Куки";
            this.button_SaveCookie.UseVisualStyleBackColor = true;
            this.button_SaveCookie.Click += new System.EventHandler(this.Button_SaveCookie_Click);
            // 
            // button_Settings
            // 
            this.button_Settings.Location = new System.Drawing.Point(6, 7);
            this.button_Settings.Name = "button_Settings";
            this.button_Settings.Size = new System.Drawing.Size(112, 24);
            this.button_Settings.TabIndex = 59;
            this.button_Settings.Text = "НАСТРОЙКИ";
            this.button_Settings.UseVisualStyleBackColor = true;
            this.button_Settings.Click += new System.EventHandler(this.Button_SettingsFormOpen_Click);
            // 
            // panel_Buttons
            // 
            this.panel_Buttons.AutoSize = true;
            this.panel_Buttons.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel_Buttons.Controls.Add(this.button_ozon);
            this.panel_Buttons.Controls.Add(this.label_YandexMarket);
            this.panel_Buttons.Controls.Add(this.button_YandexMarket);
            this.panel_Buttons.Controls.Add(this.button_Izap24);
            this.panel_Buttons.Controls.Add(this.dateTimePicker1);
            this.panel_Buttons.Controls.Add(this.button_Drom);
            this.panel_Buttons.Controls.Add(this.button_BaseGet);
            this.panel_Buttons.Controls.Add(this.button_Avito);
            this.panel_Buttons.Controls.Add(this.button_Vk);
            this.panel_Buttons.Controls.Add(this.label_Bus);
            this.panel_Buttons.Controls.Add(this.label_Vk);
            this.panel_Buttons.Controls.Add(this.label_Drom);
            this.panel_Buttons.Controls.Add(this.label_Auto);
            this.panel_Buttons.Controls.Add(this.label_Cdek);
            this.panel_Buttons.Controls.Add(this.label_lastSyncTime);
            this.panel_Buttons.Dock = System.Windows.Forms.DockStyle.Left;
            this.panel_Buttons.Location = new System.Drawing.Point(0, 0);
            this.panel_Buttons.Name = "panel_Buttons";
            this.panel_Buttons.Size = new System.Drawing.Size(139, 307);
            this.panel_Buttons.TabIndex = 146;
            // 
            // button_ozon
            // 
            this.button_ozon.Location = new System.Drawing.Point(5, 197);
            this.button_ozon.Name = "button_ozon";
            this.button_ozon.Size = new System.Drawing.Size(113, 23);
            this.button_ozon.TabIndex = 151;
            this.button_ozon.Text = "OZON";
            this.button_ozon.UseVisualStyleBackColor = true;
            this.button_ozon.Click += new System.EventHandler(this.button_ozon_ClickAsync);
            // 
            // label_YandexMarket
            // 
            this.label_YandexMarket.AutoSize = true;
            this.label_YandexMarket.Enabled = false;
            this.label_YandexMarket.Location = new System.Drawing.Point(120, 176);
            this.label_YandexMarket.Name = "label_YandexMarket";
            this.label_YandexMarket.Size = new System.Drawing.Size(16, 13);
            this.label_YandexMarket.TabIndex = 150;
            this.label_YandexMarket.Text = "...";
            // 
            // button_YandexMarket
            // 
            this.button_YandexMarket.Location = new System.Drawing.Point(5, 171);
            this.button_YandexMarket.Name = "button_YandexMarket";
            this.button_YandexMarket.Size = new System.Drawing.Size(113, 23);
            this.button_YandexMarket.TabIndex = 149;
            this.button_YandexMarket.Text = "Yandex.Market";
            this.button_YandexMarket.UseVisualStyleBackColor = true;
            this.button_YandexMarket.Click += new System.EventHandler(this.ButtonYandexMarket_Click);
            // 
            // button_Izap24
            // 
            this.button_Izap24.Location = new System.Drawing.Point(4, 145);
            this.button_Izap24.Name = "button_Izap24";
            this.button_Izap24.Size = new System.Drawing.Size(114, 23);
            this.button_Izap24.TabIndex = 147;
            this.button_Izap24.Text = "IZap24.ru";
            this.button_Izap24.UseVisualStyleBackColor = true;
            this.button_Izap24.Click += new System.EventHandler(this.ButtonIzap24_Click);
            // 
            // panel_bottom
            // 
            this.panel_bottom.AutoSize = true;
            this.panel_bottom.Controls.Add(this.button_application);
            this.panel_bottom.Controls.Add(this.button_WeightsDimensions);
            this.panel_bottom.Controls.Add(this.button_Test);
            this.panel_bottom.Controls.Add(this.button_PriceLevelsReport);
            this.panel_bottom.Controls.Add(this.button_Descriptions);
            this.panel_bottom.Controls.Add(this.button_Settings);
            this.panel_bottom.Controls.Add(this.button_SaveCookie);
            this.panel_bottom.Controls.Add(this.button_TestPartners);
            this.panel_bottom.Controls.Add(this.button_PricesIncomeCorrection);
            this.panel_bottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel_bottom.Location = new System.Drawing.Point(0, 307);
            this.panel_bottom.Name = "panel_bottom";
            this.panel_bottom.Size = new System.Drawing.Size(784, 34);
            this.panel_bottom.TabIndex = 147;
            // 
            // button_application
            // 
            this.button_application.Location = new System.Drawing.Point(294, 7);
            this.button_application.Name = "button_application";
            this.button_application.Size = new System.Drawing.Size(92, 24);
            this.button_application.TabIndex = 125;
            this.button_application.Text = "Применимость";
            this.button_application.UseVisualStyleBackColor = true;
            this.button_application.Click += new System.EventHandler(this.button_application_Click);
            // 
            // button_WeightsDimensions
            // 
            this.button_WeightsDimensions.Location = new System.Drawing.Point(213, 7);
            this.button_WeightsDimensions.Name = "button_WeightsDimensions";
            this.button_WeightsDimensions.Size = new System.Drawing.Size(77, 24);
            this.button_WeightsDimensions.TabIndex = 124;
            this.button_WeightsDimensions.Text = "Вес размер";
            this.button_WeightsDimensions.UseVisualStyleBackColor = true;
            this.button_WeightsDimensions.Click += new System.EventHandler(this.Button_WeightsDimensions_ClickAsync);
            // 
            // button_Test
            // 
            this.button_Test.Location = new System.Drawing.Point(790, 8);
            this.button_Test.Name = "button_Test";
            this.button_Test.Size = new System.Drawing.Size(69, 23);
            this.button_Test.TabIndex = 20;
            this.button_Test.TabStop = false;
            this.button_Test.Text = "Тест";
            this.button_Test.UseVisualStyleBackColor = true;
            this.button_Test.Click += new System.EventHandler(this.ButtonTest_Click);
            // 
            // panel_Filter
            // 
            this.panel_Filter.Controls.Add(this.textBox_LogFilter);
            this.panel_Filter.Controls.Add(this.button_LogFilterClear);
            this.panel_Filter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel_Filter.Location = new System.Drawing.Point(139, 282);
            this.panel_Filter.Name = "panel_Filter";
            this.panel_Filter.Padding = new System.Windows.Forms.Padding(2);
            this.panel_Filter.Size = new System.Drawing.Size(645, 25);
            this.panel_Filter.TabIndex = 148;
            // 
            // textBox_LogFilter
            // 
            this.textBox_LogFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBox_LogFilter.Font = new System.Drawing.Font("Lucida Console", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.textBox_LogFilter.Location = new System.Drawing.Point(2, 2);
            this.textBox_LogFilter.Name = "textBox_LogFilter";
            this.textBox_LogFilter.Size = new System.Drawing.Size(528, 21);
            this.textBox_LogFilter.TabIndex = 0;
            this.textBox_LogFilter.TextChanged += new System.EventHandler(this.TextBox_LogFilter_TextChanged);
            // 
            // button_LogFilterClear
            // 
            this.button_LogFilterClear.Dock = System.Windows.Forms.DockStyle.Right;
            this.button_LogFilterClear.Location = new System.Drawing.Point(530, 2);
            this.button_LogFilterClear.Margin = new System.Windows.Forms.Padding(5);
            this.button_LogFilterClear.Name = "button_LogFilterClear";
            this.button_LogFilterClear.Size = new System.Drawing.Size(113, 21);
            this.button_LogFilterClear.TabIndex = 1;
            this.button_LogFilterClear.Text = "Очистить фильтр";
            this.button_LogFilterClear.UseVisualStyleBackColor = true;
            this.button_LogFilterClear.Click += new System.EventHandler(this.Button_LogFilterClear_Click);
            // 
            // panel4
            // 
            this.panel4.Controls.Add(this.logBox);
            this.panel4.Controls.Add(this.panel_Filter);
            this.panel4.Controls.Add(this.panel_Buttons);
            this.panel4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel4.Location = new System.Drawing.Point(0, 0);
            this.panel4.Name = "panel4";
            this.panel4.Size = new System.Drawing.Size(784, 307);
            this.panel4.TabIndex = 149;
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Gray;
            this.ClientSize = new System.Drawing.Size(784, 341);
            this.Controls.Add(this.panel4);
            this.Controls.Add(this.panel_bottom);
            this.MaximizeBox = false;
            this.Name = "FormMain";
            this.Text = "Синхронизация бизнес.ру ";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.FormMain_Load);
            this.panel_Buttons.ResumeLayout(false);
            this.panel_Buttons.PerformLayout();
            this.panel_bottom.ResumeLayout(false);
            this.panel_Filter.ResumeLayout(false);
            this.panel_Filter.PerformLayout();
            this.panel4.ResumeLayout(false);
            this.panel4.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }



        #endregion
        private System.Windows.Forms.Button button_Drom;
        private System.Windows.Forms.Button button_BaseGet;
        private System.Windows.Forms.Button button_Avito;
        private System.Windows.Forms.RichTextBox logBox;
        private System.Windows.Forms.Button button_PriceLevelsReport;
        private System.Windows.Forms.Button button_Vk;
        private System.Windows.Forms.Label label_Bus;
        private System.Windows.Forms.Label label_Vk;
        private System.Windows.Forms.Label label_Drom;
        private System.Windows.Forms.Button button_Descriptions;
        private System.Windows.Forms.Button button_TestPartners;
        private System.Windows.Forms.Label label_Auto;
        private System.Windows.Forms.Label label_lastSyncTime;
        private System.Windows.Forms.DateTimePicker dateTimePicker1;
        private System.Windows.Forms.Button button_PricesIncomeCorrection;
        private System.Windows.Forms.Label label_Cdek;
        private System.Windows.Forms.Button button_SaveCookie;
        private System.Windows.Forms.Button button_Settings;
        private System.Windows.Forms.Panel panel_Buttons;
        private System.Windows.Forms.Panel panel_bottom;
        private System.Windows.Forms.Panel panel_Filter;
        private System.Windows.Forms.TextBox textBox_LogFilter;
        private System.Windows.Forms.Button button_LogFilterClear;
        private System.Windows.Forms.Panel panel4;
        private System.Windows.Forms.Button button_Izap24;
        private System.Windows.Forms.Button button_Test;
        private System.Windows.Forms.Label label_YandexMarket;
        private System.Windows.Forms.Button button_YandexMarket;
        private System.Windows.Forms.Button button_WeightsDimensions;
        private System.Windows.Forms.Button button_ozon;
        private System.Windows.Forms.Button button_application;
    }
}

