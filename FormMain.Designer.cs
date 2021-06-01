using System;

namespace Selen
{
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
            this.button_drom_get = new System.Windows.Forms.Button();
            this.button_base_get = new System.Windows.Forms.Button();
            this.button_avito_get = new System.Windows.Forms.Button();
            this.logBox = new System.Windows.Forms.RichTextBox();
            this.buttonTest = new System.Windows.Forms.Button();
            this.button_vk_sync = new System.Windows.Forms.Button();
            this.button_tiu_sync = new System.Windows.Forms.Button();
            this.ds = new System.Data.DataSet();
            this.checkBox_sync = new System.Windows.Forms.CheckBox();
            this.label_bus = new System.Windows.Forms.Label();
            this.label_tiu = new System.Windows.Forms.Label();
            this.label_vk = new System.Windows.Forms.Label();
            this.timer_sync = new System.Windows.Forms.Timer(this.components);
            this.dSet = new System.Data.DataSet();
            this.label_drom = new System.Windows.Forms.Label();
            this.button_put_desc = new System.Windows.Forms.Button();
            this.buttonTestPartners = new System.Windows.Forms.Button();
            this.button_AutoRuStart = new System.Windows.Forms.Button();
            this.label_auto = new System.Windows.Forms.Label();
            this.numericUpDown_AutoRuAddCount = new System.Windows.Forms.NumericUpDown();
            this.checkBox_AutoRuSyncEnable = new System.Windows.Forms.CheckBox();
            this.label_drom_toup = new System.Windows.Forms.Label();
            this.label_youla = new System.Windows.Forms.Label();
            this.checkBox_WriteLog = new System.Windows.Forms.CheckBox();
            this.checkBox_liteSync = new System.Windows.Forms.CheckBox();
            this.label2 = new System.Windows.Forms.Label();
            this.dateTimePicker1 = new System.Windows.Forms.DateTimePicker();
            this.buttonSatom = new System.Windows.Forms.Button();
            this.labelKS = new System.Windows.Forms.Label();
            this.checkBox_photo_clear = new System.Windows.Forms.CheckBox();
            this.buttonKupiprodai = new System.Windows.Forms.Button();
            this.buttonKupiprodaiAdd = new System.Windows.Forms.Button();
            this.numericUpDownKupiprodaiAdd = new System.Windows.Forms.NumericUpDown();
            this.labelKP = new System.Windows.Forms.Label();
            this.button_PricesCheck = new System.Windows.Forms.Button();
            this.button_GdeGet = new System.Windows.Forms.Button();
            this.labelGde = new System.Windows.Forms.Label();
            this.numericUpDownGde = new System.Windows.Forms.NumericUpDown();
            this.button_cdek = new System.Windows.Forms.Button();
            this.label_cdek = new System.Windows.Forms.Label();
            this.button_SaveCookie = new System.Windows.Forms.Button();
            this.checkBoxCdekSyncActive = new System.Windows.Forms.CheckBox();
            this.numericUpDown_СdekCheckUrls = new System.Windows.Forms.NumericUpDown();
            this.label6 = new System.Windows.Forms.Label();
            this.numericUpDown_CdekAddNewCount = new System.Windows.Forms.NumericUpDown();
            this.button_ReadSetXml = new System.Windows.Forms.Button();
            this.checkBox_AvtoProSyncEnable = new System.Windows.Forms.CheckBox();
            this.button_avto_pro = new System.Windows.Forms.Button();
            this.numericUpDown_AvtoProAddCount = new System.Windows.Forms.NumericUpDown();
            this.checkBox_IgnoreUrls = new System.Windows.Forms.CheckBox();
            this.button_SettingsFormOpen = new System.Windows.Forms.Button();
            this.button_EuroAuto = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.button_izap24 = new System.Windows.Forms.Button();
            this.checkBox_GdeRu = new System.Windows.Forms.CheckBox();
            this.panel2 = new System.Windows.Forms.Panel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.textBox_LogFilter = new System.Windows.Forms.TextBox();
            this.button_LogFilterClear = new System.Windows.Forms.Button();
            this.panel4 = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.ds)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dSet)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_AutoRuAddCount)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownKupiprodaiAdd)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownGde)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_СdekCheckUrls)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_CdekAddNewCount)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_AvtoProAddCount)).BeginInit();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panel3.SuspendLayout();
            this.panel4.SuspendLayout();
            this.SuspendLayout();
            // 
            // button_drom_get
            // 
            this.button_drom_get.Location = new System.Drawing.Point(12, 132);
            this.button_drom_get.Name = "button_drom_get";
            this.button_drom_get.Size = new System.Drawing.Size(108, 23);
            this.button_drom_get.TabIndex = 4;
            this.button_drom_get.Text = "Drom.ru старт";
            this.button_drom_get.UseVisualStyleBackColor = true;
            this.button_drom_get.Click += new System.EventHandler(this.DromGetAsync);
            // 
            // button_base_get
            // 
            this.button_base_get.Location = new System.Drawing.Point(12, 52);
            this.button_base_get.Name = "button_base_get";
            this.button_base_get.Size = new System.Drawing.Size(113, 21);
            this.button_base_get.TabIndex = 1;
            this.button_base_get.Text = "Старт";
            this.button_base_get.UseVisualStyleBackColor = true;
            this.button_base_get.Click += new System.EventHandler(this.BaseGet);
            // 
            // button_avito_get
            // 
            this.button_avito_get.Location = new System.Drawing.Point(12, 104);
            this.button_avito_get.Name = "button_avito_get";
            this.button_avito_get.Size = new System.Drawing.Size(108, 25);
            this.button_avito_get.TabIndex = 3;
            this.button_avito_get.Text = "Avito.ru старт";
            this.button_avito_get.UseVisualStyleBackColor = true;
            this.button_avito_get.Click += new System.EventHandler(this.AvitoGetAsync);
            // 
            // logBox
            // 
            this.logBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.logBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logBox.Font = new System.Drawing.Font("Lucida Console", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.logBox.ForeColor = System.Drawing.Color.LightYellow;
            this.logBox.Location = new System.Drawing.Point(185, 0);
            this.logBox.Name = "logBox";
            this.logBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.logBox.Size = new System.Drawing.Size(679, 552);
            this.logBox.TabIndex = 16;
            this.logBox.Text = "";
            this.logBox.TextChanged += new System.EventHandler(this.richTextBox1_TextChanged);
            // 
            // buttonTest
            // 
            this.buttonTest.Location = new System.Drawing.Point(745, 37);
            this.buttonTest.Name = "buttonTest";
            this.buttonTest.Size = new System.Drawing.Size(107, 23);
            this.buttonTest.TabIndex = 20;
            this.buttonTest.Text = "Остатки по ценам";
            this.buttonTest.UseVisualStyleBackColor = true;
            this.buttonTest.Click += new System.EventHandler(this.ButtonTest);
            // 
            // button_vk_sync
            // 
            this.button_vk_sync.Location = new System.Drawing.Point(12, 251);
            this.button_vk_sync.Name = "button_vk_sync";
            this.button_vk_sync.Size = new System.Drawing.Size(107, 23);
            this.button_vk_sync.TabIndex = 7;
            this.button_vk_sync.Text = "Vk.com старт";
            this.button_vk_sync.UseVisualStyleBackColor = true;
            this.button_vk_sync.Click += new System.EventHandler(this.VkSyncAsync);
            // 
            // button_tiu_sync
            // 
            this.button_tiu_sync.Location = new System.Drawing.Point(12, 78);
            this.button_tiu_sync.Name = "button_tiu_sync";
            this.button_tiu_sync.Size = new System.Drawing.Size(108, 23);
            this.button_tiu_sync.TabIndex = 2;
            this.button_tiu_sync.Text = "Tiu.ru старт";
            this.button_tiu_sync.UseVisualStyleBackColor = true;
            this.button_tiu_sync.Click += new System.EventHandler(this.TiuSync);
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
            this.checkBox_sync.Location = new System.Drawing.Point(684, 12);
            this.checkBox_sync.Name = "checkBox_sync";
            this.checkBox_sync.Size = new System.Drawing.Size(168, 17);
            this.checkBox_sync.TabIndex = 35;
            this.checkBox_sync.Text = "запускать цикл каждый час";
            this.checkBox_sync.UseVisualStyleBackColor = true;
            // 
            // label_bus
            // 
            this.label_bus.AutoSize = true;
            this.label_bus.Location = new System.Drawing.Point(126, 55);
            this.label_bus.Name = "label_bus";
            this.label_bus.Size = new System.Drawing.Size(0, 13);
            this.label_bus.TabIndex = 36;
            // 
            // label_tiu
            // 
            this.label_tiu.AutoSize = true;
            this.label_tiu.Location = new System.Drawing.Point(126, 83);
            this.label_tiu.Name = "label_tiu";
            this.label_tiu.Size = new System.Drawing.Size(10, 13);
            this.label_tiu.TabIndex = 37;
            this.label_tiu.Text = " ";
            // 
            // label_vk
            // 
            this.label_vk.AutoSize = true;
            this.label_vk.Location = new System.Drawing.Point(172, 256);
            this.label_vk.Name = "label_vk";
            this.label_vk.Size = new System.Drawing.Size(10, 13);
            this.label_vk.TabIndex = 38;
            this.label_vk.Text = " ";
            // 
            // timer_sync
            // 
            this.timer_sync.Enabled = true;
            this.timer_sync.Interval = 300000;
            this.timer_sync.Tick += new System.EventHandler(this.timer_sync_Tick);
            // 
            // dSet
            // 
            this.dSet.DataSetName = "NewDataSet";
            this.dSet.Initialized += new System.EventHandler(this.dsOptions_Initialized);
            // 
            // label_drom
            // 
            this.label_drom.AutoSize = true;
            this.label_drom.Location = new System.Drawing.Point(124, 137);
            this.label_drom.Name = "label_drom";
            this.label_drom.Size = new System.Drawing.Size(0, 13);
            this.label_drom.TabIndex = 58;
            // 
            // button_put_desc
            // 
            this.button_put_desc.Location = new System.Drawing.Point(12, 37);
            this.button_put_desc.Name = "button_put_desc";
            this.button_put_desc.Size = new System.Drawing.Size(156, 24);
            this.button_put_desc.TabIndex = 59;
            this.button_put_desc.Text = "Редактировать описания";
            this.button_put_desc.UseVisualStyleBackColor = true;
            this.button_put_desc.Click += new System.EventHandler(this.button_put_desc_Click);
            // 
            // buttonTestPartners
            // 
            this.buttonTestPartners.Location = new System.Drawing.Point(355, 37);
            this.buttonTestPartners.Name = "buttonTestPartners";
            this.buttonTestPartners.Size = new System.Drawing.Size(164, 23);
            this.buttonTestPartners.TabIndex = 61;
            this.buttonTestPartners.Text = "Тест задвоения клиентов";
            this.buttonTestPartners.UseVisualStyleBackColor = true;
            this.buttonTestPartners.Click += new System.EventHandler(this.ButtonTestPartnersClick);
            // 
            // button_AutoRuStart
            // 
            this.button_AutoRuStart.Location = new System.Drawing.Point(12, 276);
            this.button_AutoRuStart.Name = "button_AutoRuStart";
            this.button_AutoRuStart.Size = new System.Drawing.Size(107, 23);
            this.button_AutoRuStart.TabIndex = 6;
            this.button_AutoRuStart.Text = "Auto.ru старт";
            this.button_AutoRuStart.UseVisualStyleBackColor = true;
            this.button_AutoRuStart.Click += new System.EventHandler(this.button_AutoRuStart_Click);
            // 
            // label_auto
            // 
            this.label_auto.AutoSize = true;
            this.label_auto.Location = new System.Drawing.Point(122, 279);
            this.label_auto.Name = "label_auto";
            this.label_auto.Size = new System.Drawing.Size(0, 13);
            this.label_auto.TabIndex = 66;
            // 
            // numericUpDown_AutoRuAddCount
            // 
            this.numericUpDown_AutoRuAddCount.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.numericUpDown_AutoRuAddCount.Location = new System.Drawing.Point(123, 277);
            this.numericUpDown_AutoRuAddCount.Maximum = new decimal(new int[] {
            20,
            0,
            0,
            0});
            this.numericUpDown_AutoRuAddCount.Name = "numericUpDown_AutoRuAddCount";
            this.numericUpDown_AutoRuAddCount.Size = new System.Drawing.Size(45, 21);
            this.numericUpDown_AutoRuAddCount.TabIndex = 69;
            this.numericUpDown_AutoRuAddCount.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numericUpDown_AutoRuAddCount.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDown_AutoRuAddCount.ValueChanged += new System.EventHandler(this.numericUpDown_auto_ValueChanged);
            // 
            // checkBox_AutoRuSyncEnable
            // 
            this.checkBox_AutoRuSyncEnable.AutoSize = true;
            this.checkBox_AutoRuSyncEnable.Checked = true;
            this.checkBox_AutoRuSyncEnable.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_AutoRuSyncEnable.Location = new System.Drawing.Point(14, 300);
            this.checkBox_AutoRuSyncEnable.Name = "checkBox_AutoRuSyncEnable";
            this.checkBox_AutoRuSyncEnable.Size = new System.Drawing.Size(44, 17);
            this.checkBox_AutoRuSyncEnable.TabIndex = 74;
            this.checkBox_AutoRuSyncEnable.Text = "вкл";
            this.checkBox_AutoRuSyncEnable.UseVisualStyleBackColor = true;
            // 
            // label_drom_toup
            // 
            this.label_drom_toup.AutoSize = true;
            this.label_drom_toup.Location = new System.Drawing.Point(124, 157);
            this.label_drom_toup.Name = "label_drom_toup";
            this.label_drom_toup.Size = new System.Drawing.Size(0, 13);
            this.label_drom_toup.TabIndex = 77;
            // 
            // label_youla
            // 
            this.label_youla.AutoSize = true;
            this.label_youla.Enabled = false;
            this.label_youla.Location = new System.Drawing.Point(125, 531);
            this.label_youla.Name = "label_youla";
            this.label_youla.Size = new System.Drawing.Size(0, 13);
            this.label_youla.TabIndex = 85;
            // 
            // checkBox_WriteLog
            // 
            this.checkBox_WriteLog.AutoSize = true;
            this.checkBox_WriteLog.Checked = true;
            this.checkBox_WriteLog.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_WriteLog.Location = new System.Drawing.Point(187, 12);
            this.checkBox_WriteLog.Name = "checkBox_WriteLog";
            this.checkBox_WriteLog.Size = new System.Drawing.Size(81, 17);
            this.checkBox_WriteLog.TabIndex = 94;
            this.checkBox_WriteLog.Text = "писать лог";
            this.checkBox_WriteLog.UseVisualStyleBackColor = true;
            // 
            // checkBox_liteSync
            // 
            this.checkBox_liteSync.AutoSize = true;
            this.checkBox_liteSync.Checked = true;
            this.checkBox_liteSync.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_liteSync.Location = new System.Drawing.Point(274, 13);
            this.checkBox_liteSync.Name = "checkBox_liteSync";
            this.checkBox_liteSync.Size = new System.Drawing.Size(174, 17);
            this.checkBox_liteSync.TabIndex = 95;
            this.checkBox_liteSync.Text = "использовать легкий рескан";
            this.checkBox_liteSync.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(10, 9);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(119, 13);
            this.label2.TabIndex = 98;
            this.label2.Text = "Посл. синхронизация:";
            // 
            // dateTimePicker1
            // 
            this.dateTimePicker1.CalendarForeColor = System.Drawing.Color.Red;
            this.dateTimePicker1.CustomFormat = "dd/MM/yyyy HH:mm";
            this.dateTimePicker1.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dateTimePicker1.Location = new System.Drawing.Point(12, 25);
            this.dateTimePicker1.MaxDate = new System.DateTime(2099, 12, 31, 0, 0, 0, 0);
            this.dateTimePicker1.MinDate = new System.DateTime(2017, 1, 1, 0, 0, 0, 0);
            this.dateTimePicker1.Name = "dateTimePicker1";
            this.dateTimePicker1.ShowUpDown = true;
            this.dateTimePicker1.Size = new System.Drawing.Size(113, 20);
            this.dateTimePicker1.TabIndex = 99;
            this.dateTimePicker1.Value = new System.DateTime(2020, 9, 2, 5, 50, 0, 0);
            this.dateTimePicker1.ValueChanged += new System.EventHandler(this.dateTimePicker1_ValueChanged);
            // 
            // buttonSatom
            // 
            this.buttonSatom.Enabled = false;
            this.buttonSatom.Location = new System.Drawing.Point(12, 470);
            this.buttonSatom.Name = "buttonSatom";
            this.buttonSatom.Size = new System.Drawing.Size(108, 23);
            this.buttonSatom.TabIndex = 10;
            this.buttonSatom.Text = "Satom.ru";
            this.buttonSatom.UseVisualStyleBackColor = true;
            this.buttonSatom.Click += new System.EventHandler(this.buttonSatom_Click);
            // 
            // labelKS
            // 
            this.labelKS.AutoSize = true;
            this.labelKS.Enabled = false;
            this.labelKS.Location = new System.Drawing.Point(123, 475);
            this.labelKS.Name = "labelKS";
            this.labelKS.Size = new System.Drawing.Size(44, 13);
            this.labelKS.TabIndex = 104;
            this.labelKS.Text = "=> XML";
            // 
            // checkBox_photo_clear
            // 
            this.checkBox_photo_clear.AutoSize = true;
            this.checkBox_photo_clear.Location = new System.Drawing.Point(597, 13);
            this.checkBox_photo_clear.Name = "checkBox_photo_clear";
            this.checkBox_photo_clear.Size = new System.Drawing.Size(79, 17);
            this.checkBox_photo_clear.TabIndex = 110;
            this.checkBox_photo_clear.Text = "photo clear";
            this.checkBox_photo_clear.UseVisualStyleBackColor = true;
            // 
            // buttonKupiprodai
            // 
            this.buttonKupiprodai.Location = new System.Drawing.Point(12, 158);
            this.buttonKupiprodai.Name = "buttonKupiprodai";
            this.buttonKupiprodai.Size = new System.Drawing.Size(107, 23);
            this.buttonKupiprodai.TabIndex = 5;
            this.buttonKupiprodai.Text = "Купипродай старт";
            this.buttonKupiprodai.UseVisualStyleBackColor = true;
            this.buttonKupiprodai.Click += new System.EventHandler(this.KupiprodaiClickAsync);
            // 
            // buttonKupiprodaiAdd
            // 
            this.buttonKupiprodaiAdd.Location = new System.Drawing.Point(12, 180);
            this.buttonKupiprodaiAdd.Name = "buttonKupiprodaiAdd";
            this.buttonKupiprodaiAdd.Size = new System.Drawing.Size(107, 23);
            this.buttonKupiprodaiAdd.TabIndex = 112;
            this.buttonKupiprodaiAdd.Text = "Выложить";
            this.buttonKupiprodaiAdd.UseVisualStyleBackColor = true;
            this.buttonKupiprodaiAdd.Click += new System.EventHandler(this.buttonKupiprodaiAdd_Click);
            // 
            // numericUpDownKupiprodaiAdd
            // 
            this.numericUpDownKupiprodaiAdd.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.numericUpDownKupiprodaiAdd.Location = new System.Drawing.Point(123, 181);
            this.numericUpDownKupiprodaiAdd.Name = "numericUpDownKupiprodaiAdd";
            this.numericUpDownKupiprodaiAdd.Size = new System.Drawing.Size(45, 21);
            this.numericUpDownKupiprodaiAdd.TabIndex = 113;
            this.numericUpDownKupiprodaiAdd.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numericUpDownKupiprodaiAdd.Value = new decimal(new int[] {
            100,
            0,
            0,
            0});
            // 
            // labelKP
            // 
            this.labelKP.AutoSize = true;
            this.labelKP.Location = new System.Drawing.Point(122, 163);
            this.labelKP.Name = "labelKP";
            this.labelKP.Size = new System.Drawing.Size(0, 13);
            this.labelKP.TabIndex = 114;
            // 
            // button_PricesCheck
            // 
            this.button_PricesCheck.Location = new System.Drawing.Point(185, 37);
            this.button_PricesCheck.Name = "button_PricesCheck";
            this.button_PricesCheck.Size = new System.Drawing.Size(164, 23);
            this.button_PricesCheck.TabIndex = 115;
            this.button_PricesCheck.Text = "Корекция цен закупки";
            this.button_PricesCheck.UseVisualStyleBackColor = true;
            this.button_PricesCheck.Click += new System.EventHandler(this.button_PricesCheck_Click);
            // 
            // button_GdeGet
            // 
            this.button_GdeGet.Location = new System.Drawing.Point(12, 317);
            this.button_GdeGet.Name = "button_GdeGet";
            this.button_GdeGet.Size = new System.Drawing.Size(107, 23);
            this.button_GdeGet.TabIndex = 9;
            this.button_GdeGet.Text = "Gde.ru старт";
            this.button_GdeGet.UseVisualStyleBackColor = true;
            this.button_GdeGet.Click += new System.EventHandler(this.button_GdeGet_Click);
            // 
            // labelGde
            // 
            this.labelGde.AutoSize = true;
            this.labelGde.Location = new System.Drawing.Point(124, 322);
            this.labelGde.Name = "labelGde";
            this.labelGde.Size = new System.Drawing.Size(0, 13);
            this.labelGde.TabIndex = 118;
            // 
            // numericUpDownGde
            // 
            this.numericUpDownGde.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.numericUpDownGde.Location = new System.Drawing.Point(123, 318);
            this.numericUpDownGde.Name = "numericUpDownGde";
            this.numericUpDownGde.Size = new System.Drawing.Size(45, 21);
            this.numericUpDownGde.TabIndex = 119;
            this.numericUpDownGde.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numericUpDownGde.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // button_cdek
            // 
            this.button_cdek.Location = new System.Drawing.Point(12, 415);
            this.button_cdek.Name = "button_cdek";
            this.button_cdek.Size = new System.Drawing.Size(107, 23);
            this.button_cdek.TabIndex = 8;
            this.button_cdek.Text = "Cdek.market старт";
            this.button_cdek.UseVisualStyleBackColor = true;
            this.button_cdek.Click += new System.EventHandler(this.button_cdek_Click);
            // 
            // label_cdek
            // 
            this.label_cdek.AutoSize = true;
            this.label_cdek.Location = new System.Drawing.Point(172, 390);
            this.label_cdek.Name = "label_cdek";
            this.label_cdek.Size = new System.Drawing.Size(10, 13);
            this.label_cdek.TabIndex = 122;
            this.label_cdek.Text = " ";
            // 
            // button_SaveCookie
            // 
            this.button_SaveCookie.Location = new System.Drawing.Point(663, 37);
            this.button_SaveCookie.Name = "button_SaveCookie";
            this.button_SaveCookie.Size = new System.Drawing.Size(76, 23);
            this.button_SaveCookie.TabIndex = 123;
            this.button_SaveCookie.Text = "Сохр. Куки";
            this.button_SaveCookie.UseVisualStyleBackColor = true;
            this.button_SaveCookie.Click += new System.EventHandler(this.button_SaveCookie_Click);
            // 
            // checkBoxCdekSyncActive
            // 
            this.checkBoxCdekSyncActive.AutoSize = true;
            this.checkBoxCdekSyncActive.Location = new System.Drawing.Point(14, 442);
            this.checkBoxCdekSyncActive.Name = "checkBoxCdekSyncActive";
            this.checkBoxCdekSyncActive.Size = new System.Drawing.Size(44, 17);
            this.checkBoxCdekSyncActive.TabIndex = 125;
            this.checkBoxCdekSyncActive.Text = "вкл";
            this.checkBoxCdekSyncActive.UseVisualStyleBackColor = true;
            // 
            // numericUpDown_СdekCheckUrls
            // 
            this.numericUpDown_СdekCheckUrls.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.numericUpDown_СdekCheckUrls.Location = new System.Drawing.Point(123, 439);
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
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(75, 443);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(44, 13);
            this.label6.TabIndex = 132;
            this.label6.Text = "чистка:";
            // 
            // numericUpDown_CdekAddNewCount
            // 
            this.numericUpDown_CdekAddNewCount.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.numericUpDown_CdekAddNewCount.Location = new System.Drawing.Point(123, 416);
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
            // button_ReadSetXml
            // 
            this.button_ReadSetXml.Location = new System.Drawing.Point(525, 37);
            this.button_ReadSetXml.Name = "button_ReadSetXml";
            this.button_ReadSetXml.Size = new System.Drawing.Size(132, 23);
            this.button_ReadSetXml.TabIndex = 137;
            this.button_ReadSetXml.Text = "Перечитать настройки";
            this.button_ReadSetXml.UseVisualStyleBackColor = true;
            this.button_ReadSetXml.Click += new System.EventHandler(this.button_ReadSetXmlClick);
            // 
            // checkBox_AvtoProSyncEnable
            // 
            this.checkBox_AvtoProSyncEnable.AutoSize = true;
            this.checkBox_AvtoProSyncEnable.Checked = true;
            this.checkBox_AvtoProSyncEnable.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_AvtoProSyncEnable.Location = new System.Drawing.Point(14, 233);
            this.checkBox_AvtoProSyncEnable.Name = "checkBox_AvtoProSyncEnable";
            this.checkBox_AvtoProSyncEnable.Size = new System.Drawing.Size(44, 17);
            this.checkBox_AvtoProSyncEnable.TabIndex = 138;
            this.checkBox_AvtoProSyncEnable.Text = "вкл";
            this.checkBox_AvtoProSyncEnable.UseVisualStyleBackColor = true;
            // 
            // button_avto_pro
            // 
            this.button_avto_pro.Location = new System.Drawing.Point(12, 208);
            this.button_avto_pro.Name = "button_avto_pro";
            this.button_avto_pro.Size = new System.Drawing.Size(108, 23);
            this.button_avto_pro.TabIndex = 139;
            this.button_avto_pro.Text = "Avto.pro старт";
            this.button_avto_pro.UseVisualStyleBackColor = true;
            this.button_avto_pro.Click += new System.EventHandler(this.button_avto_pro_Click);
            // 
            // numericUpDown_AvtoProAddCount
            // 
            this.numericUpDown_AvtoProAddCount.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.numericUpDown_AvtoProAddCount.Location = new System.Drawing.Point(125, 209);
            this.numericUpDown_AvtoProAddCount.Maximum = new decimal(new int[] {
            999,
            0,
            0,
            0});
            this.numericUpDown_AvtoProAddCount.Name = "numericUpDown_AvtoProAddCount";
            this.numericUpDown_AvtoProAddCount.Size = new System.Drawing.Size(43, 21);
            this.numericUpDown_AvtoProAddCount.TabIndex = 140;
            this.numericUpDown_AvtoProAddCount.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numericUpDown_AvtoProAddCount.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDown_AvtoProAddCount.ValueChanged += new System.EventHandler(this.numericUpDown_avto_pro_add_ValueChanged);
            // 
            // checkBox_IgnoreUrls
            // 
            this.checkBox_IgnoreUrls.AutoSize = true;
            this.checkBox_IgnoreUrls.Checked = true;
            this.checkBox_IgnoreUrls.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_IgnoreUrls.Location = new System.Drawing.Point(454, 13);
            this.checkBox_IgnoreUrls.Name = "checkBox_IgnoreUrls";
            this.checkBox_IgnoreUrls.Size = new System.Drawing.Size(137, 17);
            this.checkBox_IgnoreUrls.TabIndex = 144;
            this.checkBox_IgnoreUrls.Text = "игнорировать ссылки";
            this.checkBox_IgnoreUrls.UseVisualStyleBackColor = true;
            // 
            // button_SettingsFormOpen
            // 
            this.button_SettingsFormOpen.Location = new System.Drawing.Point(12, 7);
            this.button_SettingsFormOpen.Name = "button_SettingsFormOpen";
            this.button_SettingsFormOpen.Size = new System.Drawing.Size(156, 24);
            this.button_SettingsFormOpen.TabIndex = 59;
            this.button_SettingsFormOpen.Text = "НАСТРОЙКИ";
            this.button_SettingsFormOpen.UseVisualStyleBackColor = true;
            this.button_SettingsFormOpen.Click += new System.EventHandler(this.button_SettingsFormOpen_Click);
            // 
            // button_EuroAuto
            // 
            this.button_EuroAuto.Location = new System.Drawing.Point(12, 358);
            this.button_EuroAuto.Name = "button_EuroAuto";
            this.button_EuroAuto.Size = new System.Drawing.Size(108, 23);
            this.button_EuroAuto.TabIndex = 145;
            this.button_EuroAuto.Text = "EuroAuto.ru";
            this.button_EuroAuto.UseVisualStyleBackColor = true;
            this.button_EuroAuto.Click += new System.EventHandler(this.button_EuroAuto_Click);
            // 
            // panel1
            // 
            this.panel1.AutoSize = true;
            this.panel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel1.Controls.Add(this.button_izap24);
            this.panel1.Controls.Add(this.checkBox_GdeRu);
            this.panel1.Controls.Add(this.dateTimePicker1);
            this.panel1.Controls.Add(this.button_EuroAuto);
            this.panel1.Controls.Add(this.button_drom_get);
            this.panel1.Controls.Add(this.button_base_get);
            this.panel1.Controls.Add(this.numericUpDown_AvtoProAddCount);
            this.panel1.Controls.Add(this.button_avito_get);
            this.panel1.Controls.Add(this.button_avto_pro);
            this.panel1.Controls.Add(this.button_vk_sync);
            this.panel1.Controls.Add(this.checkBox_AvtoProSyncEnable);
            this.panel1.Controls.Add(this.button_tiu_sync);
            this.panel1.Controls.Add(this.label_bus);
            this.panel1.Controls.Add(this.label_tiu);
            this.panel1.Controls.Add(this.numericUpDown_CdekAddNewCount);
            this.panel1.Controls.Add(this.label_vk);
            this.panel1.Controls.Add(this.label6);
            this.panel1.Controls.Add(this.label_drom);
            this.panel1.Controls.Add(this.numericUpDown_СdekCheckUrls);
            this.panel1.Controls.Add(this.button_AutoRuStart);
            this.panel1.Controls.Add(this.checkBoxCdekSyncActive);
            this.panel1.Controls.Add(this.label_auto);
            this.panel1.Controls.Add(this.numericUpDown_AutoRuAddCount);
            this.panel1.Controls.Add(this.checkBox_AutoRuSyncEnable);
            this.panel1.Controls.Add(this.label_cdek);
            this.panel1.Controls.Add(this.label_drom_toup);
            this.panel1.Controls.Add(this.button_cdek);
            this.panel1.Controls.Add(this.numericUpDownGde);
            this.panel1.Controls.Add(this.label_youla);
            this.panel1.Controls.Add(this.labelGde);
            this.panel1.Controls.Add(this.button_GdeGet);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.labelKP);
            this.panel1.Controls.Add(this.buttonSatom);
            this.panel1.Controls.Add(this.numericUpDownKupiprodaiAdd);
            this.panel1.Controls.Add(this.labelKS);
            this.panel1.Controls.Add(this.buttonKupiprodaiAdd);
            this.panel1.Controls.Add(this.buttonKupiprodai);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Left;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(185, 577);
            this.panel1.TabIndex = 146;
            // 
            // button_izap24
            // 
            this.button_izap24.Location = new System.Drawing.Point(11, 386);
            this.button_izap24.Name = "button_izap24";
            this.button_izap24.Size = new System.Drawing.Size(108, 23);
            this.button_izap24.TabIndex = 147;
            this.button_izap24.Text = "IZap24.ru";
            this.button_izap24.UseVisualStyleBackColor = true;
            this.button_izap24.Click += new System.EventHandler(this.button_izap24_Click);
            // 
            // checkBox_GdeRu
            // 
            this.checkBox_GdeRu.AutoSize = true;
            this.checkBox_GdeRu.Checked = true;
            this.checkBox_GdeRu.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_GdeRu.Location = new System.Drawing.Point(13, 341);
            this.checkBox_GdeRu.Name = "checkBox_GdeRu";
            this.checkBox_GdeRu.Size = new System.Drawing.Size(44, 17);
            this.checkBox_GdeRu.TabIndex = 146;
            this.checkBox_GdeRu.Text = "вкл";
            this.checkBox_GdeRu.UseVisualStyleBackColor = true;
            // 
            // panel2
            // 
            this.panel2.AutoSize = true;
            this.panel2.Controls.Add(this.checkBox_sync);
            this.panel2.Controls.Add(this.buttonTest);
            this.panel2.Controls.Add(this.checkBox_IgnoreUrls);
            this.panel2.Controls.Add(this.button_ReadSetXml);
            this.panel2.Controls.Add(this.button_put_desc);
            this.panel2.Controls.Add(this.button_SettingsFormOpen);
            this.panel2.Controls.Add(this.button_SaveCookie);
            this.panel2.Controls.Add(this.buttonTestPartners);
            this.panel2.Controls.Add(this.button_PricesCheck);
            this.panel2.Controls.Add(this.checkBox_photo_clear);
            this.panel2.Controls.Add(this.checkBox_liteSync);
            this.panel2.Controls.Add(this.checkBox_WriteLog);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel2.Location = new System.Drawing.Point(0, 577);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(864, 64);
            this.panel2.TabIndex = 147;
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.textBox_LogFilter);
            this.panel3.Controls.Add(this.button_LogFilterClear);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel3.Location = new System.Drawing.Point(185, 552);
            this.panel3.Name = "panel3";
            this.panel3.Padding = new System.Windows.Forms.Padding(2);
            this.panel3.Size = new System.Drawing.Size(679, 25);
            this.panel3.TabIndex = 148;
            // 
            // textBox_LogFilter
            // 
            this.textBox_LogFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBox_LogFilter.Font = new System.Drawing.Font("Lucida Console", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.textBox_LogFilter.Location = new System.Drawing.Point(2, 2);
            this.textBox_LogFilter.Name = "textBox_LogFilter";
            this.textBox_LogFilter.Size = new System.Drawing.Size(562, 21);
            this.textBox_LogFilter.TabIndex = 0;
            this.textBox_LogFilter.TextChanged += new System.EventHandler(this.textBox_LogFilter_TextChanged);
            // 
            // button_LogFilterClear
            // 
            this.button_LogFilterClear.Dock = System.Windows.Forms.DockStyle.Right;
            this.button_LogFilterClear.Location = new System.Drawing.Point(564, 2);
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
            this.panel4.Controls.Add(this.panel3);
            this.panel4.Controls.Add(this.panel1);
            this.panel4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel4.Location = new System.Drawing.Point(0, 0);
            this.panel4.Name = "panel4";
            this.panel4.Size = new System.Drawing.Size(864, 577);
            this.panel4.TabIndex = 149;
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Gray;
            this.ClientSize = new System.Drawing.Size(864, 641);
            this.Controls.Add(this.panel4);
            this.Controls.Add(this.panel2);
            this.MaximizeBox = false;
            this.Name = "FormMain";
            this.Text = "Синхронизация бизнес.ру ";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.FormMain_Load);
            ((System.ComponentModel.ISupportInitialize)(this.ds)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dSet)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_AutoRuAddCount)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownKupiprodaiAdd)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownGde)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_СdekCheckUrls)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_CdekAddNewCount)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_AvtoProAddCount)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            this.panel4.ResumeLayout(false);
            this.panel4.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }



        #endregion
        private System.Windows.Forms.Button button_drom_get;
        private System.Windows.Forms.Button button_base_get;
        private System.Windows.Forms.Button button_avito_get;
        private System.Windows.Forms.RichTextBox logBox;
        private System.Windows.Forms.Button buttonTest;
        private System.Windows.Forms.Button button_vk_sync;
        private System.Windows.Forms.Button button_tiu_sync;
        private System.Data.DataSet ds;
        private System.Windows.Forms.CheckBox checkBox_sync;
        private System.Windows.Forms.Label label_bus;
        private System.Windows.Forms.Label label_tiu;
        private System.Windows.Forms.Label label_vk;
        private System.Windows.Forms.Timer timer_sync;
        private System.Data.DataSet dSet;
        private System.Windows.Forms.Label label_drom;
        private System.Windows.Forms.Button button_put_desc;
        private System.Windows.Forms.Button buttonTestPartners;
        private System.Windows.Forms.Button button_AutoRuStart;
        private System.Windows.Forms.Label label_auto;
        private System.Windows.Forms.NumericUpDown numericUpDown_AutoRuAddCount;
        private System.Windows.Forms.CheckBox checkBox_AutoRuSyncEnable;
        private System.Windows.Forms.Label label_drom_toup;
        private System.Windows.Forms.Label label_youla;
        private System.Windows.Forms.CheckBox checkBox_WriteLog;
        private System.Windows.Forms.CheckBox checkBox_liteSync;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.DateTimePicker dateTimePicker1;
        private System.Windows.Forms.Button buttonSatom;
        private System.Windows.Forms.Label labelKS;
        private System.Windows.Forms.CheckBox checkBox_photo_clear;
        private System.Windows.Forms.Button buttonKupiprodai;
        private System.Windows.Forms.Button buttonKupiprodaiAdd;
        private System.Windows.Forms.NumericUpDown numericUpDownKupiprodaiAdd;
        private System.Windows.Forms.Label labelKP;
        private System.Windows.Forms.Button button_PricesCheck;
        private System.Windows.Forms.Button button_GdeGet;
        private System.Windows.Forms.Label labelGde;
        private System.Windows.Forms.NumericUpDown numericUpDownGde;
        private System.Windows.Forms.Button button_cdek;
        private System.Windows.Forms.Label label_cdek;
        private System.Windows.Forms.Button button_SaveCookie;
        private System.Windows.Forms.CheckBox checkBoxCdekSyncActive;
        private System.Windows.Forms.NumericUpDown numericUpDown_СdekCheckUrls;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.NumericUpDown numericUpDown_CdekAddNewCount;
        private System.Windows.Forms.Button button_ReadSetXml;
        private System.Windows.Forms.CheckBox checkBox_AvtoProSyncEnable;
        private System.Windows.Forms.Button button_avto_pro;
        private System.Windows.Forms.NumericUpDown numericUpDown_AvtoProAddCount;
        private System.Windows.Forms.CheckBox checkBox_IgnoreUrls;
        private System.Windows.Forms.Button button_SettingsFormOpen;
        private System.Windows.Forms.Button button_EuroAuto;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.TextBox textBox_LogFilter;
        private System.Windows.Forms.Button button_LogFilterClear;
        private System.Windows.Forms.Panel panel4;
        private System.Windows.Forms.CheckBox checkBox_GdeRu;
        private System.Windows.Forms.Button button_izap24;
    }
}

