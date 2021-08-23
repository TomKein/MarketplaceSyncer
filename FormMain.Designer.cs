using System;

namespace Selen {
    partial class FormMain
    {
        /// <summary>
        /// Обязательная переменная конструктора.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Освободить все используемые ресурсы.
        /// </summary>
        /// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Код, автоматически созданный конструктором форм Windows

        /// <summary>
        /// Требуемый метод для поддержки конструктора — не изменяйте 
        /// содержимое этого метода с помощью редактора кода.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.button_Drom = new System.Windows.Forms.Button();
            this.button_BaseGet = new System.Windows.Forms.Button();
            this.button_Avito = new System.Windows.Forms.Button();
            this.logBox = new System.Windows.Forms.RichTextBox();
            this.button_PriceLevelsReport = new System.Windows.Forms.Button();
            this.button_Vk = new System.Windows.Forms.Button();
            this.button_Tiu = new System.Windows.Forms.Button();
            this.ds = new System.Data.DataSet();
            this.checkBox_sync = new System.Windows.Forms.CheckBox();
            this.label_Bus = new System.Windows.Forms.Label();
            this.label_Tiu = new System.Windows.Forms.Label();
            this.label_Vk = new System.Windows.Forms.Label();
            this.timer_sync = new System.Windows.Forms.Timer(this.components);
            this.label_Drom = new System.Windows.Forms.Label();
            this.button_Descriptions = new System.Windows.Forms.Button();
            this.button_TestPartners = new System.Windows.Forms.Button();
            this.button_AutoRu = new System.Windows.Forms.Button();
            this.label_Auto = new System.Windows.Forms.Label();
            this.label_Youla = new System.Windows.Forms.Label();
            this.label_lastSyncTime = new System.Windows.Forms.Label();
            this.dateTimePicker1 = new System.Windows.Forms.DateTimePicker();
            this.button_Satom = new System.Windows.Forms.Button();
            this.button_Kupiprodai = new System.Windows.Forms.Button();
            this.button_KupiprodaiAdd = new System.Windows.Forms.Button();
            this.label_Kp = new System.Windows.Forms.Label();
            this.button_PricesCorrection = new System.Windows.Forms.Button();
            this.button_Gde = new System.Windows.Forms.Button();
            this.label_Gde = new System.Windows.Forms.Label();
            this.button_cdek = new System.Windows.Forms.Button();
            this.label_Cdek = new System.Windows.Forms.Label();
            this.button_SaveCookie = new System.Windows.Forms.Button();
            this.checkBox_CdekSyncActive = new System.Windows.Forms.CheckBox();
            this.numericUpDown_СdekCheckUrls = new System.Windows.Forms.NumericUpDown();
            this.numericUpDown_CdekAddNewCount = new System.Windows.Forms.NumericUpDown();
            this.button_AvtoPro = new System.Windows.Forms.Button();
            this.button_Settings = new System.Windows.Forms.Button();
            this.button_EuroAuto = new System.Windows.Forms.Button();
            this.panel_Buttons = new System.Windows.Forms.Panel();
            this.button_Youla = new System.Windows.Forms.Button();
            this.button_Izap24 = new System.Windows.Forms.Button();
            this.panel_bottom = new System.Windows.Forms.Panel();
            this.button_Test = new System.Windows.Forms.Button();
            this.panel_Filter = new System.Windows.Forms.Panel();
            this.textBox_LogFilter = new System.Windows.Forms.TextBox();
            this.button_LogFilterClear = new System.Windows.Forms.Button();
            this.panel4 = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.ds)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_СdekCheckUrls)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_CdekAddNewCount)).BeginInit();
            this.panel_Buttons.SuspendLayout();
            this.panel_bottom.SuspendLayout();
            this.panel_Filter.SuspendLayout();
            this.panel4.SuspendLayout();
            this.SuspendLayout();
            // 
            // button_Drom
            // 
            this.button_Drom.Location = new System.Drawing.Point(5, 138);
            this.button_Drom.Name = "button_Drom";
            this.button_Drom.Size = new System.Drawing.Size(113, 23);
            this.button_Drom.TabIndex = 4;
            this.button_Drom.Text = "Drom.ru";
            this.button_Drom.UseVisualStyleBackColor = true;
            this.button_Drom.Click += new System.EventHandler(this.DromRu_Click);
            // 
            // button_BaseGet
            // 
            this.button_BaseGet.Location = new System.Drawing.Point(5, 60);
            this.button_BaseGet.Name = "button_BaseGet";
            this.button_BaseGet.Size = new System.Drawing.Size(113, 21);
            this.button_BaseGet.TabIndex = 1;
            this.button_BaseGet.Text = "Запуск";
            this.button_BaseGet.UseVisualStyleBackColor = true;
            this.button_BaseGet.Click += new System.EventHandler(this.BaseGet);
            // 
            // button_Avito
            // 
            this.button_Avito.Location = new System.Drawing.Point(5, 110);
            this.button_Avito.Name = "button_Avito";
            this.button_Avito.Size = new System.Drawing.Size(113, 25);
            this.button_Avito.TabIndex = 3;
            this.button_Avito.Text = "Avito.ru";
            this.button_Avito.UseVisualStyleBackColor = true;
            this.button_Avito.Click += new System.EventHandler(this.AvitoRu_Click);
            // 
            // logBox
            // 
            this.logBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.logBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logBox.Font = new System.Drawing.Font("Lucida Console", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.logBox.ForeColor = System.Drawing.Color.LightYellow;
            this.logBox.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.logBox.Location = new System.Drawing.Point(162, 0);
            this.logBox.Name = "logBox";
            this.logBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.logBox.Size = new System.Drawing.Size(427, 392);
            this.logBox.TabIndex = 16;
            this.logBox.Text = "";
            this.logBox.TextChanged += new System.EventHandler(this.richTextBox1_TextChanged);
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
            this.button_Vk.Location = new System.Drawing.Point(5, 238);
            this.button_Vk.Name = "button_Vk";
            this.button_Vk.Size = new System.Drawing.Size(113, 23);
            this.button_Vk.TabIndex = 7;
            this.button_Vk.Text = "Vk.com";
            this.button_Vk.UseVisualStyleBackColor = true;
            this.button_Vk.Click += new System.EventHandler(this.VkCom_Click);
            // 
            // button_Tiu
            // 
            this.button_Tiu.Location = new System.Drawing.Point(5, 84);
            this.button_Tiu.Name = "button_Tiu";
            this.button_Tiu.Size = new System.Drawing.Size(113, 23);
            this.button_Tiu.TabIndex = 2;
            this.button_Tiu.Text = "Tiu.ru";
            this.button_Tiu.UseVisualStyleBackColor = true;
            this.button_Tiu.Click += new System.EventHandler(this.TiuRu_Click);
            // 
            // ds
            // 
            this.ds.DataSetName = "NewDataSet";
            // 
            // checkBox_sync
            // 
            this.checkBox_sync.AutoSize = true;
            this.checkBox_sync.Checked = true;
            this.checkBox_sync.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_sync.Location = new System.Drawing.Point(6, 4);
            this.checkBox_sync.Name = "checkBox_sync";
            this.checkBox_sync.Size = new System.Drawing.Size(124, 17);
            this.checkBox_sync.TabIndex = 35;
            this.checkBox_sync.Text = "синхронизация вкл";
            this.checkBox_sync.UseVisualStyleBackColor = true;
            // 
            // label_Bus
            // 
            this.label_Bus.AutoSize = true;
            this.label_Bus.Location = new System.Drawing.Point(120, 64);
            this.label_Bus.Name = "label_Bus";
            this.label_Bus.Size = new System.Drawing.Size(16, 13);
            this.label_Bus.TabIndex = 36;
            this.label_Bus.Text = "...";
            // 
            // label_Tiu
            // 
            this.label_Tiu.AutoSize = true;
            this.label_Tiu.Location = new System.Drawing.Point(120, 89);
            this.label_Tiu.Name = "label_Tiu";
            this.label_Tiu.Size = new System.Drawing.Size(16, 13);
            this.label_Tiu.TabIndex = 37;
            this.label_Tiu.Text = "...";
            // 
            // label_Vk
            // 
            this.label_Vk.AutoSize = true;
            this.label_Vk.Location = new System.Drawing.Point(120, 243);
            this.label_Vk.Name = "label_Vk";
            this.label_Vk.Size = new System.Drawing.Size(16, 13);
            this.label_Vk.TabIndex = 38;
            this.label_Vk.Text = "...";
            // 
            // timer_sync
            // 
            this.timer_sync.Enabled = true;
            this.timer_sync.Interval = 300000;
            this.timer_sync.Tick += new System.EventHandler(this.timer_sync_Tick);
            // 
            // label_Drom
            // 
            this.label_Drom.AutoSize = true;
            this.label_Drom.Location = new System.Drawing.Point(120, 143);
            this.label_Drom.Name = "label_Drom";
            this.label_Drom.Size = new System.Drawing.Size(16, 13);
            this.label_Drom.TabIndex = 58;
            this.label_Drom.Text = "...";
            // 
            // button_Descriptions
            // 
            this.button_Descriptions.Location = new System.Drawing.Point(185, 7);
            this.button_Descriptions.Name = "button_Descriptions";
            this.button_Descriptions.Size = new System.Drawing.Size(108, 24);
            this.button_Descriptions.TabIndex = 59;
            this.button_Descriptions.Text = "Описания";
            this.button_Descriptions.UseVisualStyleBackColor = true;
            this.button_Descriptions.Click += new System.EventHandler(this.button_put_desc_Click);
            // 
            // button_TestPartners
            // 
            this.button_TestPartners.Location = new System.Drawing.Point(434, 8);
            this.button_TestPartners.Name = "button_TestPartners";
            this.button_TestPartners.Size = new System.Drawing.Size(152, 23);
            this.button_TestPartners.TabIndex = 61;
            this.button_TestPartners.Text = "Задвоения контрагентов";
            this.button_TestPartners.UseVisualStyleBackColor = true;
            this.button_TestPartners.Click += new System.EventHandler(this.ButtonTestPartnersClick);
            // 
            // button_AutoRu
            // 
            this.button_AutoRu.Location = new System.Drawing.Point(5, 264);
            this.button_AutoRu.Name = "button_AutoRu";
            this.button_AutoRu.Size = new System.Drawing.Size(113, 23);
            this.button_AutoRu.TabIndex = 6;
            this.button_AutoRu.Text = "Auto.ru";
            this.button_AutoRu.UseVisualStyleBackColor = true;
            this.button_AutoRu.Click += new System.EventHandler(this.AutoRu_Click);
            // 
            // label_Auto
            // 
            this.label_Auto.AutoSize = true;
            this.label_Auto.Location = new System.Drawing.Point(115, 267);
            this.label_Auto.Name = "label_Auto";
            this.label_Auto.Size = new System.Drawing.Size(0, 13);
            this.label_Auto.TabIndex = 66;
            // 
            // label_Youla
            // 
            this.label_Youla.AutoSize = true;
            this.label_Youla.Enabled = false;
            this.label_Youla.Location = new System.Drawing.Point(120, 372);
            this.label_Youla.Name = "label_Youla";
            this.label_Youla.Size = new System.Drawing.Size(16, 13);
            this.label_Youla.TabIndex = 85;
            this.label_Youla.Text = "...";
            // 
            // label_lastSyncTime
            // 
            this.label_lastSyncTime.AutoSize = true;
            this.label_lastSyncTime.Location = new System.Drawing.Point(3, 22);
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
            this.dateTimePicker1.Location = new System.Drawing.Point(5, 37);
            this.dateTimePicker1.MaxDate = new System.DateTime(2099, 12, 31, 0, 0, 0, 0);
            this.dateTimePicker1.MinDate = new System.DateTime(2017, 1, 1, 0, 0, 0, 0);
            this.dateTimePicker1.Name = "dateTimePicker1";
            this.dateTimePicker1.ShowUpDown = true;
            this.dateTimePicker1.Size = new System.Drawing.Size(113, 20);
            this.dateTimePicker1.TabIndex = 99;
            this.dateTimePicker1.Value = new System.DateTime(2020, 9, 2, 5, 50, 0, 0);
            this.dateTimePicker1.ValueChanged += new System.EventHandler(this.dateTimePicker1_ValueChanged);
            // 
            // button_Satom
            // 
            this.button_Satom.Enabled = false;
            this.button_Satom.Location = new System.Drawing.Point(5, 476);
            this.button_Satom.Name = "button_Satom";
            this.button_Satom.Size = new System.Drawing.Size(108, 23);
            this.button_Satom.TabIndex = 10;
            this.button_Satom.Text = "Satom.ru";
            this.button_Satom.UseVisualStyleBackColor = true;
            this.button_Satom.Click += new System.EventHandler(this.buttonSatom_Click);
            // 
            // button_Kupiprodai
            // 
            this.button_Kupiprodai.Location = new System.Drawing.Point(5, 164);
            this.button_Kupiprodai.Name = "button_Kupiprodai";
            this.button_Kupiprodai.Size = new System.Drawing.Size(113, 23);
            this.button_Kupiprodai.TabIndex = 5;
            this.button_Kupiprodai.Text = "Купипродай";
            this.button_Kupiprodai.UseVisualStyleBackColor = true;
            this.button_Kupiprodai.Click += new System.EventHandler(this.KupiprodaiRu_Click);
            // 
            // button_KupiprodaiAdd
            // 
            this.button_KupiprodaiAdd.Location = new System.Drawing.Point(5, 186);
            this.button_KupiprodaiAdd.Name = "button_KupiprodaiAdd";
            this.button_KupiprodaiAdd.Size = new System.Drawing.Size(113, 23);
            this.button_KupiprodaiAdd.TabIndex = 112;
            this.button_KupiprodaiAdd.Text = "Выкладывать";
            this.button_KupiprodaiAdd.UseVisualStyleBackColor = true;
            this.button_KupiprodaiAdd.Click += new System.EventHandler(this.KupiprodaiRuAdd_Click);
            // 
            // label_Kp
            // 
            this.label_Kp.AutoSize = true;
            this.label_Kp.Location = new System.Drawing.Point(120, 169);
            this.label_Kp.Name = "label_Kp";
            this.label_Kp.Size = new System.Drawing.Size(16, 13);
            this.label_Kp.TabIndex = 114;
            this.label_Kp.Text = "...";
            // 
            // button_PricesCorrection
            // 
            this.button_PricesCorrection.Location = new System.Drawing.Point(298, 8);
            this.button_PricesCorrection.Name = "button_PricesCorrection";
            this.button_PricesCorrection.Size = new System.Drawing.Size(131, 23);
            this.button_PricesCorrection.TabIndex = 115;
            this.button_PricesCorrection.Text = "Корекция цен закупки";
            this.button_PricesCorrection.UseVisualStyleBackColor = true;
            this.button_PricesCorrection.Click += new System.EventHandler(this.button_PricesCheck_Click);
            // 
            // button_Gde
            // 
            this.button_Gde.Location = new System.Drawing.Point(5, 290);
            this.button_Gde.Name = "button_Gde";
            this.button_Gde.Size = new System.Drawing.Size(113, 23);
            this.button_Gde.TabIndex = 9;
            this.button_Gde.Text = "Gde.ru";
            this.button_Gde.UseVisualStyleBackColor = true;
            this.button_Gde.Click += new System.EventHandler(this.GdeRu_Click);
            // 
            // label_Gde
            // 
            this.label_Gde.AutoSize = true;
            this.label_Gde.Location = new System.Drawing.Point(120, 295);
            this.label_Gde.Name = "label_Gde";
            this.label_Gde.Size = new System.Drawing.Size(16, 13);
            this.label_Gde.TabIndex = 118;
            this.label_Gde.Text = "...";
            // 
            // button_cdek
            // 
            this.button_cdek.Location = new System.Drawing.Point(5, 421);
            this.button_cdek.Name = "button_cdek";
            this.button_cdek.Size = new System.Drawing.Size(107, 23);
            this.button_cdek.TabIndex = 8;
            this.button_cdek.Text = "Cdek.market";
            this.button_cdek.UseVisualStyleBackColor = true;
            this.button_cdek.Click += new System.EventHandler(this.Cdek_Click);
            // 
            // label_Cdek
            // 
            this.label_Cdek.AutoSize = true;
            this.label_Cdek.Location = new System.Drawing.Point(120, 347);
            this.label_Cdek.Name = "label_Cdek";
            this.label_Cdek.Size = new System.Drawing.Size(16, 13);
            this.label_Cdek.TabIndex = 122;
            this.label_Cdek.Text = "...";
            // 
            // button_SaveCookie
            // 
            this.button_SaveCookie.Location = new System.Drawing.Point(591, 8);
            this.button_SaveCookie.Name = "button_SaveCookie";
            this.button_SaveCookie.Size = new System.Drawing.Size(76, 23);
            this.button_SaveCookie.TabIndex = 123;
            this.button_SaveCookie.Text = "Сохр. Куки";
            this.button_SaveCookie.UseVisualStyleBackColor = true;
            this.button_SaveCookie.Click += new System.EventHandler(this.button_SaveCookie_Click);
            // 
            // checkBox_CdekSyncActive
            // 
            this.checkBox_CdekSyncActive.AutoSize = true;
            this.checkBox_CdekSyncActive.Location = new System.Drawing.Point(7, 448);
            this.checkBox_CdekSyncActive.Name = "checkBox_CdekSyncActive";
            this.checkBox_CdekSyncActive.Size = new System.Drawing.Size(44, 17);
            this.checkBox_CdekSyncActive.TabIndex = 125;
            this.checkBox_CdekSyncActive.Text = "вкл";
            this.checkBox_CdekSyncActive.UseVisualStyleBackColor = true;
            // 
            // numericUpDown_СdekCheckUrls
            // 
            this.numericUpDown_СdekCheckUrls.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.numericUpDown_СdekCheckUrls.Location = new System.Drawing.Point(116, 445);
            this.numericUpDown_СdekCheckUrls.Maximum = new decimal(new int[] {
            200,
            0,
            0,
            0});
            this.numericUpDown_СdekCheckUrls.Name = "numericUpDown_СdekCheckUrls";
            this.numericUpDown_СdekCheckUrls.Size = new System.Drawing.Size(43, 21);
            this.numericUpDown_СdekCheckUrls.TabIndex = 131;
            this.numericUpDown_СdekCheckUrls.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numericUpDown_СdekCheckUrls.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            // 
            // numericUpDown_CdekAddNewCount
            // 
            this.numericUpDown_CdekAddNewCount.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.numericUpDown_CdekAddNewCount.Location = new System.Drawing.Point(116, 422);
            this.numericUpDown_CdekAddNewCount.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.numericUpDown_CdekAddNewCount.Name = "numericUpDown_CdekAddNewCount";
            this.numericUpDown_CdekAddNewCount.Size = new System.Drawing.Size(43, 21);
            this.numericUpDown_CdekAddNewCount.TabIndex = 133;
            this.numericUpDown_CdekAddNewCount.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // button_AvtoPro
            // 
            this.button_AvtoPro.Location = new System.Drawing.Point(5, 212);
            this.button_AvtoPro.Name = "button_AvtoPro";
            this.button_AvtoPro.Size = new System.Drawing.Size(113, 23);
            this.button_AvtoPro.TabIndex = 139;
            this.button_AvtoPro.Text = "Avto.pro";
            this.button_AvtoPro.UseVisualStyleBackColor = true;
            this.button_AvtoPro.Click += new System.EventHandler(this.AvtoPro_Click);
            // 
            // button_Settings
            // 
            this.button_Settings.Location = new System.Drawing.Point(6, 7);
            this.button_Settings.Name = "button_Settings";
            this.button_Settings.Size = new System.Drawing.Size(112, 24);
            this.button_Settings.TabIndex = 59;
            this.button_Settings.Text = "НАСТРОЙКИ";
            this.button_Settings.UseVisualStyleBackColor = true;
            this.button_Settings.Click += new System.EventHandler(this.button_SettingsFormOpen_Click);
            // 
            // button_EuroAuto
            // 
            this.button_EuroAuto.Location = new System.Drawing.Point(5, 316);
            this.button_EuroAuto.Name = "button_EuroAuto";
            this.button_EuroAuto.Size = new System.Drawing.Size(113, 23);
            this.button_EuroAuto.TabIndex = 145;
            this.button_EuroAuto.Text = "EuroAuto.ru";
            this.button_EuroAuto.UseVisualStyleBackColor = true;
            this.button_EuroAuto.Click += new System.EventHandler(this.EuroAuto_Click);
            // 
            // panel_Buttons
            // 
            this.panel_Buttons.AutoSize = true;
            this.panel_Buttons.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel_Buttons.Controls.Add(this.button_Youla);
            this.panel_Buttons.Controls.Add(this.checkBox_sync);
            this.panel_Buttons.Controls.Add(this.button_Izap24);
            this.panel_Buttons.Controls.Add(this.dateTimePicker1);
            this.panel_Buttons.Controls.Add(this.button_EuroAuto);
            this.panel_Buttons.Controls.Add(this.button_Drom);
            this.panel_Buttons.Controls.Add(this.button_BaseGet);
            this.panel_Buttons.Controls.Add(this.button_Avito);
            this.panel_Buttons.Controls.Add(this.button_AvtoPro);
            this.panel_Buttons.Controls.Add(this.button_Vk);
            this.panel_Buttons.Controls.Add(this.button_Tiu);
            this.panel_Buttons.Controls.Add(this.label_Bus);
            this.panel_Buttons.Controls.Add(this.label_Tiu);
            this.panel_Buttons.Controls.Add(this.numericUpDown_CdekAddNewCount);
            this.panel_Buttons.Controls.Add(this.label_Vk);
            this.panel_Buttons.Controls.Add(this.label_Drom);
            this.panel_Buttons.Controls.Add(this.numericUpDown_СdekCheckUrls);
            this.panel_Buttons.Controls.Add(this.button_AutoRu);
            this.panel_Buttons.Controls.Add(this.checkBox_CdekSyncActive);
            this.panel_Buttons.Controls.Add(this.label_Auto);
            this.panel_Buttons.Controls.Add(this.label_Cdek);
            this.panel_Buttons.Controls.Add(this.button_cdek);
            this.panel_Buttons.Controls.Add(this.label_Youla);
            this.panel_Buttons.Controls.Add(this.label_Gde);
            this.panel_Buttons.Controls.Add(this.button_Gde);
            this.panel_Buttons.Controls.Add(this.label_lastSyncTime);
            this.panel_Buttons.Controls.Add(this.label_Kp);
            this.panel_Buttons.Controls.Add(this.button_Satom);
            this.panel_Buttons.Controls.Add(this.button_KupiprodaiAdd);
            this.panel_Buttons.Controls.Add(this.button_Kupiprodai);
            this.panel_Buttons.Dock = System.Windows.Forms.DockStyle.Left;
            this.panel_Buttons.Location = new System.Drawing.Point(0, 0);
            this.panel_Buttons.Name = "panel_Buttons";
            this.panel_Buttons.Size = new System.Drawing.Size(162, 417);
            this.panel_Buttons.TabIndex = 146;
            // 
            // button_Youla
            // 
            this.button_Youla.Location = new System.Drawing.Point(5, 368);
            this.button_Youla.Name = "button_Youla";
            this.button_Youla.Size = new System.Drawing.Size(113, 23);
            this.button_Youla.TabIndex = 148;
            this.button_Youla.Text = "Youla.ru";
            this.button_Youla.UseVisualStyleBackColor = true;
            this.button_Youla.Click += new System.EventHandler(this.Youla_Click);
            // 
            // button_Izap24
            // 
            this.button_Izap24.Location = new System.Drawing.Point(4, 342);
            this.button_Izap24.Name = "button_Izap24";
            this.button_Izap24.Size = new System.Drawing.Size(114, 23);
            this.button_Izap24.TabIndex = 147;
            this.button_Izap24.Text = "IZap24.ru";
            this.button_Izap24.UseVisualStyleBackColor = true;
            this.button_Izap24.Click += new System.EventHandler(this.Izap24_Click);
            // 
            // panel_bottom
            // 
            this.panel_bottom.AutoSize = true;
            this.panel_bottom.Controls.Add(this.button_Test);
            this.panel_bottom.Controls.Add(this.button_PriceLevelsReport);
            this.panel_bottom.Controls.Add(this.button_Descriptions);
            this.panel_bottom.Controls.Add(this.button_Settings);
            this.panel_bottom.Controls.Add(this.button_SaveCookie);
            this.panel_bottom.Controls.Add(this.button_TestPartners);
            this.panel_bottom.Controls.Add(this.button_PricesCorrection);
            this.panel_bottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel_bottom.Location = new System.Drawing.Point(0, 417);
            this.panel_bottom.Name = "panel_bottom";
            this.panel_bottom.Size = new System.Drawing.Size(589, 34);
            this.panel_bottom.TabIndex = 147;
            // 
            // button_Test
            // 
            this.button_Test.Location = new System.Drawing.Point(781, 8);
            this.button_Test.Name = "button_Test";
            this.button_Test.Size = new System.Drawing.Size(69, 23);
            this.button_Test.TabIndex = 20;
            this.button_Test.TabStop = false;
            this.button_Test.Text = "Тест";
            this.button_Test.UseVisualStyleBackColor = true;
            this.button_Test.Click += new System.EventHandler(this.buttonTest_Click);
            // 
            // panel_Filter
            // 
            this.panel_Filter.Controls.Add(this.textBox_LogFilter);
            this.panel_Filter.Controls.Add(this.button_LogFilterClear);
            this.panel_Filter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel_Filter.Location = new System.Drawing.Point(162, 392);
            this.panel_Filter.Name = "panel_Filter";
            this.panel_Filter.Padding = new System.Windows.Forms.Padding(2);
            this.panel_Filter.Size = new System.Drawing.Size(427, 25);
            this.panel_Filter.TabIndex = 148;
            // 
            // textBox_LogFilter
            // 
            this.textBox_LogFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBox_LogFilter.Font = new System.Drawing.Font("Lucida Console", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.textBox_LogFilter.Location = new System.Drawing.Point(2, 2);
            this.textBox_LogFilter.Name = "textBox_LogFilter";
            this.textBox_LogFilter.Size = new System.Drawing.Size(310, 21);
            this.textBox_LogFilter.TabIndex = 0;
            this.textBox_LogFilter.TextChanged += new System.EventHandler(this.textBox_LogFilter_TextChanged);
            // 
            // button_LogFilterClear
            // 
            this.button_LogFilterClear.Dock = System.Windows.Forms.DockStyle.Right;
            this.button_LogFilterClear.Location = new System.Drawing.Point(312, 2);
            this.button_LogFilterClear.Margin = new System.Windows.Forms.Padding(5);
            this.button_LogFilterClear.Name = "button_LogFilterClear";
            this.button_LogFilterClear.Size = new System.Drawing.Size(113, 21);
            this.button_LogFilterClear.TabIndex = 1;
            this.button_LogFilterClear.Text = "Очистить фильтр";
            this.button_LogFilterClear.UseVisualStyleBackColor = true;
            this.button_LogFilterClear.Click += new System.EventHandler(this.button_LogFilterClear_Click);
            // 
            // panel4
            // 
            this.panel4.Controls.Add(this.logBox);
            this.panel4.Controls.Add(this.panel_Filter);
            this.panel4.Controls.Add(this.panel_Buttons);
            this.panel4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel4.Location = new System.Drawing.Point(0, 0);
            this.panel4.Name = "panel4";
            this.panel4.Size = new System.Drawing.Size(589, 417);
            this.panel4.TabIndex = 149;
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Gray;
            this.ClientSize = new System.Drawing.Size(589, 451);
            this.Controls.Add(this.panel4);
            this.Controls.Add(this.panel_bottom);
            this.MaximizeBox = false;
            this.Name = "FormMain";
            this.Text = "Синхронизация бизнес.ру ";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.FormMain_Load);
            ((System.ComponentModel.ISupportInitialize)(this.ds)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_СdekCheckUrls)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_CdekAddNewCount)).EndInit();
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
        private System.Windows.Forms.Button button_Tiu;
        private System.Data.DataSet ds;
        private System.Windows.Forms.CheckBox checkBox_sync;
        private System.Windows.Forms.Label label_Bus;
        private System.Windows.Forms.Label label_Tiu;
        private System.Windows.Forms.Label label_Vk;
        private System.Windows.Forms.Timer timer_sync;
        private System.Windows.Forms.Label label_Drom;
        private System.Windows.Forms.Button button_Descriptions;
        private System.Windows.Forms.Button button_TestPartners;
        private System.Windows.Forms.Button button_AutoRu;
        private System.Windows.Forms.Label label_Auto;
        private System.Windows.Forms.Label label_Youla;
        private System.Windows.Forms.Label label_lastSyncTime;
        private System.Windows.Forms.DateTimePicker dateTimePicker1;
        private System.Windows.Forms.Button button_Satom;
        private System.Windows.Forms.Button button_Kupiprodai;
        private System.Windows.Forms.Button button_KupiprodaiAdd;
        private System.Windows.Forms.Label label_Kp;
        private System.Windows.Forms.Button button_PricesCorrection;
        private System.Windows.Forms.Button button_Gde;
        private System.Windows.Forms.Label label_Gde;
        private System.Windows.Forms.Button button_cdek;
        private System.Windows.Forms.Label label_Cdek;
        private System.Windows.Forms.Button button_SaveCookie;
        private System.Windows.Forms.CheckBox checkBox_CdekSyncActive;
        private System.Windows.Forms.NumericUpDown numericUpDown_СdekCheckUrls;
        private System.Windows.Forms.NumericUpDown numericUpDown_CdekAddNewCount;
        private System.Windows.Forms.Button button_AvtoPro;
        private System.Windows.Forms.Button button_Settings;
        private System.Windows.Forms.Button button_EuroAuto;
        private System.Windows.Forms.Panel panel_Buttons;
        private System.Windows.Forms.Panel panel_bottom;
        private System.Windows.Forms.Panel panel_Filter;
        private System.Windows.Forms.TextBox textBox_LogFilter;
        private System.Windows.Forms.Button button_LogFilterClear;
        private System.Windows.Forms.Panel panel4;
        private System.Windows.Forms.Button button_Izap24;
        private System.Windows.Forms.Button button_Test;
        private System.Windows.Forms.Button button_Youla;
    }
}

