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
            this.buttonPriceLevelsReport = new System.Windows.Forms.Button();
            this.button_vk_sync = new System.Windows.Forms.Button();
            this.button_tiu_sync = new System.Windows.Forms.Button();
            this.ds = new System.Data.DataSet();
            this.checkBox_sync = new System.Windows.Forms.CheckBox();
            this.label_bus = new System.Windows.Forms.Label();
            this.label_tiu = new System.Windows.Forms.Label();
            this.label_vk = new System.Windows.Forms.Label();
            this.timer_sync = new System.Windows.Forms.Timer(this.components);
            this.label_drom = new System.Windows.Forms.Label();
            this.button_put_desc = new System.Windows.Forms.Button();
            this.buttonTestPartners = new System.Windows.Forms.Button();
            this.button_AutoRuStart = new System.Windows.Forms.Button();
            this.label_auto = new System.Windows.Forms.Label();
            this.label_youla = new System.Windows.Forms.Label();
            this.label_lastSyncTime = new System.Windows.Forms.Label();
            this.dateTimePicker1 = new System.Windows.Forms.DateTimePicker();
            this.buttonSatom = new System.Windows.Forms.Button();
            this.buttonKupiprodai = new System.Windows.Forms.Button();
            this.buttonKupiprodaiAdd = new System.Windows.Forms.Button();
            this.label_KP = new System.Windows.Forms.Label();
            this.button_PricesCheck = new System.Windows.Forms.Button();
            this.button_GdeGet = new System.Windows.Forms.Button();
            this.label_gde = new System.Windows.Forms.Label();
            this.button_cdek = new System.Windows.Forms.Button();
            this.label_cdek = new System.Windows.Forms.Label();
            this.button_SaveCookie = new System.Windows.Forms.Button();
            this.checkBoxCdekSyncActive = new System.Windows.Forms.CheckBox();
            this.numericUpDown_СdekCheckUrls = new System.Windows.Forms.NumericUpDown();
            this.numericUpDown_CdekAddNewCount = new System.Windows.Forms.NumericUpDown();
            this.button_avto_pro = new System.Windows.Forms.Button();
            this.button_SettingsFormOpen = new System.Windows.Forms.Button();
            this.button_EuroAuto = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.button_izap24 = new System.Windows.Forms.Button();
            this.panel2 = new System.Windows.Forms.Panel();
            this.buttonTest = new System.Windows.Forms.Button();
            this.panel3 = new System.Windows.Forms.Panel();
            this.textBox_LogFilter = new System.Windows.Forms.TextBox();
            this.button_LogFilterClear = new System.Windows.Forms.Button();
            this.panel4 = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.ds)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_СdekCheckUrls)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_CdekAddNewCount)).BeginInit();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panel3.SuspendLayout();
            this.panel4.SuspendLayout();
            this.SuspendLayout();
            // 
            // button_drom_get
            // 
            this.button_drom_get.Location = new System.Drawing.Point(5, 138);
            this.button_drom_get.Name = "button_drom_get";
            this.button_drom_get.Size = new System.Drawing.Size(113, 23);
            this.button_drom_get.TabIndex = 4;
            this.button_drom_get.Text = "Drom.ru";
            this.button_drom_get.UseVisualStyleBackColor = true;
            this.button_drom_get.Click += new System.EventHandler(this.DromRu_Click);
            // 
            // button_base_get
            // 
            this.button_base_get.Location = new System.Drawing.Point(5, 60);
            this.button_base_get.Name = "button_base_get";
            this.button_base_get.Size = new System.Drawing.Size(113, 21);
            this.button_base_get.TabIndex = 1;
            this.button_base_get.Text = "Запуск";
            this.button_base_get.UseVisualStyleBackColor = true;
            this.button_base_get.Click += new System.EventHandler(this.BaseGet);
            // 
            // button_avito_get
            // 
            this.button_avito_get.Location = new System.Drawing.Point(5, 110);
            this.button_avito_get.Name = "button_avito_get";
            this.button_avito_get.Size = new System.Drawing.Size(113, 25);
            this.button_avito_get.TabIndex = 3;
            this.button_avito_get.Text = "Avito.ru";
            this.button_avito_get.UseVisualStyleBackColor = true;
            this.button_avito_get.Click += new System.EventHandler(this.AvitoRu_Click);
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
            // buttonPriceLevelsReport
            // 
            this.buttonPriceLevelsReport.Location = new System.Drawing.Point(670, 8);
            this.buttonPriceLevelsReport.Name = "buttonPriceLevelsReport";
            this.buttonPriceLevelsReport.Size = new System.Drawing.Size(107, 23);
            this.buttonPriceLevelsReport.TabIndex = 20;
            this.buttonPriceLevelsReport.Text = "Остатки по ценам";
            this.buttonPriceLevelsReport.UseVisualStyleBackColor = true;
            this.buttonPriceLevelsReport.Click += new System.EventHandler(this.PriceLevelsRemainsReport);
            // 
            // button_vk_sync
            // 
            this.button_vk_sync.Location = new System.Drawing.Point(5, 238);
            this.button_vk_sync.Name = "button_vk_sync";
            this.button_vk_sync.Size = new System.Drawing.Size(113, 23);
            this.button_vk_sync.TabIndex = 7;
            this.button_vk_sync.Text = "Vk.com";
            this.button_vk_sync.UseVisualStyleBackColor = true;
            this.button_vk_sync.Click += new System.EventHandler(this.VkCom_Click);
            // 
            // button_tiu_sync
            // 
            this.button_tiu_sync.Location = new System.Drawing.Point(5, 84);
            this.button_tiu_sync.Name = "button_tiu_sync";
            this.button_tiu_sync.Size = new System.Drawing.Size(113, 23);
            this.button_tiu_sync.TabIndex = 2;
            this.button_tiu_sync.Text = "Tiu.ru";
            this.button_tiu_sync.UseVisualStyleBackColor = true;
            this.button_tiu_sync.Click += new System.EventHandler(this.TiuRu_Click);
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
            // label_bus
            // 
            this.label_bus.AutoSize = true;
            this.label_bus.Location = new System.Drawing.Point(119, 63);
            this.label_bus.Name = "label_bus";
            this.label_bus.Size = new System.Drawing.Size(0, 13);
            this.label_bus.TabIndex = 36;
            // 
            // label_tiu
            // 
            this.label_tiu.AutoSize = true;
            this.label_tiu.Location = new System.Drawing.Point(125, 89);
            this.label_tiu.Name = "label_tiu";
            this.label_tiu.Size = new System.Drawing.Size(10, 13);
            this.label_tiu.TabIndex = 37;
            this.label_tiu.Text = " ";
            // 
            // label_vk
            // 
            this.label_vk.AutoSize = true;
            this.label_vk.Location = new System.Drawing.Point(125, 243);
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
            // label_drom
            // 
            this.label_drom.AutoSize = true;
            this.label_drom.Location = new System.Drawing.Point(125, 143);
            this.label_drom.Name = "label_drom";
            this.label_drom.Size = new System.Drawing.Size(0, 13);
            this.label_drom.TabIndex = 58;
            // 
            // button_put_desc
            // 
            this.button_put_desc.Location = new System.Drawing.Point(185, 7);
            this.button_put_desc.Name = "button_put_desc";
            this.button_put_desc.Size = new System.Drawing.Size(108, 24);
            this.button_put_desc.TabIndex = 59;
            this.button_put_desc.Text = "Описания";
            this.button_put_desc.UseVisualStyleBackColor = true;
            this.button_put_desc.Click += new System.EventHandler(this.button_put_desc_Click);
            // 
            // buttonTestPartners
            // 
            this.buttonTestPartners.Location = new System.Drawing.Point(434, 8);
            this.buttonTestPartners.Name = "buttonTestPartners";
            this.buttonTestPartners.Size = new System.Drawing.Size(152, 23);
            this.buttonTestPartners.TabIndex = 61;
            this.buttonTestPartners.Text = "Задвоения контрагентов";
            this.buttonTestPartners.UseVisualStyleBackColor = true;
            this.buttonTestPartners.Click += new System.EventHandler(this.ButtonTestPartnersClick);
            // 
            // button_AutoRuStart
            // 
            this.button_AutoRuStart.Location = new System.Drawing.Point(5, 264);
            this.button_AutoRuStart.Name = "button_AutoRuStart";
            this.button_AutoRuStart.Size = new System.Drawing.Size(113, 23);
            this.button_AutoRuStart.TabIndex = 6;
            this.button_AutoRuStart.Text = "Auto.ru";
            this.button_AutoRuStart.UseVisualStyleBackColor = true;
            this.button_AutoRuStart.Click += new System.EventHandler(this.AutoRu_Click);
            // 
            // label_auto
            // 
            this.label_auto.AutoSize = true;
            this.label_auto.Location = new System.Drawing.Point(115, 267);
            this.label_auto.Name = "label_auto";
            this.label_auto.Size = new System.Drawing.Size(0, 13);
            this.label_auto.TabIndex = 66;
            // 
            // label_youla
            // 
            this.label_youla.AutoSize = true;
            this.label_youla.Enabled = false;
            this.label_youla.Location = new System.Drawing.Point(118, 537);
            this.label_youla.Name = "label_youla";
            this.label_youla.Size = new System.Drawing.Size(0, 13);
            this.label_youla.TabIndex = 85;
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
            // buttonSatom
            // 
            this.buttonSatom.Enabled = false;
            this.buttonSatom.Location = new System.Drawing.Point(5, 476);
            this.buttonSatom.Name = "buttonSatom";
            this.buttonSatom.Size = new System.Drawing.Size(108, 23);
            this.buttonSatom.TabIndex = 10;
            this.buttonSatom.Text = "Satom.ru";
            this.buttonSatom.UseVisualStyleBackColor = true;
            this.buttonSatom.Click += new System.EventHandler(this.buttonSatom_Click);
            // 
            // buttonKupiprodai
            // 
            this.buttonKupiprodai.Location = new System.Drawing.Point(5, 164);
            this.buttonKupiprodai.Name = "buttonKupiprodai";
            this.buttonKupiprodai.Size = new System.Drawing.Size(113, 23);
            this.buttonKupiprodai.TabIndex = 5;
            this.buttonKupiprodai.Text = "Купипродай";
            this.buttonKupiprodai.UseVisualStyleBackColor = true;
            this.buttonKupiprodai.Click += new System.EventHandler(this.KupiprodaiRu_Click);
            // 
            // buttonKupiprodaiAdd
            // 
            this.buttonKupiprodaiAdd.Location = new System.Drawing.Point(5, 186);
            this.buttonKupiprodaiAdd.Name = "buttonKupiprodaiAdd";
            this.buttonKupiprodaiAdd.Size = new System.Drawing.Size(113, 23);
            this.buttonKupiprodaiAdd.TabIndex = 112;
            this.buttonKupiprodaiAdd.Text = "Выкладывать";
            this.buttonKupiprodaiAdd.UseVisualStyleBackColor = true;
            this.buttonKupiprodaiAdd.Click += new System.EventHandler(this.KupiprodaiRuAdd_Click);
            // 
            // label_KP
            // 
            this.label_KP.AutoSize = true;
            this.label_KP.Location = new System.Drawing.Point(125, 169);
            this.label_KP.Name = "label_KP";
            this.label_KP.Size = new System.Drawing.Size(0, 13);
            this.label_KP.TabIndex = 114;
            // 
            // button_PricesCheck
            // 
            this.button_PricesCheck.Location = new System.Drawing.Point(298, 8);
            this.button_PricesCheck.Name = "button_PricesCheck";
            this.button_PricesCheck.Size = new System.Drawing.Size(131, 23);
            this.button_PricesCheck.TabIndex = 115;
            this.button_PricesCheck.Text = "Корекция цен закупки";
            this.button_PricesCheck.UseVisualStyleBackColor = true;
            this.button_PricesCheck.Click += new System.EventHandler(this.button_PricesCheck_Click);
            // 
            // button_GdeGet
            // 
            this.button_GdeGet.Location = new System.Drawing.Point(5, 290);
            this.button_GdeGet.Name = "button_GdeGet";
            this.button_GdeGet.Size = new System.Drawing.Size(113, 23);
            this.button_GdeGet.TabIndex = 9;
            this.button_GdeGet.Text = "Gde.ru";
            this.button_GdeGet.UseVisualStyleBackColor = true;
            this.button_GdeGet.Click += new System.EventHandler(this.GdeRu_Click);
            // 
            // label_gde
            // 
            this.label_gde.AutoSize = true;
            this.label_gde.Location = new System.Drawing.Point(125, 295);
            this.label_gde.Name = "label_gde";
            this.label_gde.Size = new System.Drawing.Size(0, 13);
            this.label_gde.TabIndex = 118;
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
            // label_cdek
            // 
            this.label_cdek.AutoSize = true;
            this.label_cdek.Location = new System.Drawing.Point(125, 347);
            this.label_cdek.Name = "label_cdek";
            this.label_cdek.Size = new System.Drawing.Size(10, 13);
            this.label_cdek.TabIndex = 122;
            this.label_cdek.Text = " ";
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
            // checkBoxCdekSyncActive
            // 
            this.checkBoxCdekSyncActive.AutoSize = true;
            this.checkBoxCdekSyncActive.Location = new System.Drawing.Point(7, 448);
            this.checkBoxCdekSyncActive.Name = "checkBoxCdekSyncActive";
            this.checkBoxCdekSyncActive.Size = new System.Drawing.Size(44, 17);
            this.checkBoxCdekSyncActive.TabIndex = 125;
            this.checkBoxCdekSyncActive.Text = "вкл";
            this.checkBoxCdekSyncActive.UseVisualStyleBackColor = true;
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
            // button_avto_pro
            // 
            this.button_avto_pro.Location = new System.Drawing.Point(5, 212);
            this.button_avto_pro.Name = "button_avto_pro";
            this.button_avto_pro.Size = new System.Drawing.Size(113, 23);
            this.button_avto_pro.TabIndex = 139;
            this.button_avto_pro.Text = "Avto.pro";
            this.button_avto_pro.UseVisualStyleBackColor = true;
            this.button_avto_pro.Click += new System.EventHandler(this.AvtoPro_Click);
            // 
            // button_SettingsFormOpen
            // 
            this.button_SettingsFormOpen.Location = new System.Drawing.Point(6, 7);
            this.button_SettingsFormOpen.Name = "button_SettingsFormOpen";
            this.button_SettingsFormOpen.Size = new System.Drawing.Size(112, 24);
            this.button_SettingsFormOpen.TabIndex = 59;
            this.button_SettingsFormOpen.Text = "НАСТРОЙКИ";
            this.button_SettingsFormOpen.UseVisualStyleBackColor = true;
            this.button_SettingsFormOpen.Click += new System.EventHandler(this.button_SettingsFormOpen_Click);
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
            // panel1
            // 
            this.panel1.AutoSize = true;
            this.panel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel1.Controls.Add(this.checkBox_sync);
            this.panel1.Controls.Add(this.button_izap24);
            this.panel1.Controls.Add(this.dateTimePicker1);
            this.panel1.Controls.Add(this.button_EuroAuto);
            this.panel1.Controls.Add(this.button_drom_get);
            this.panel1.Controls.Add(this.button_base_get);
            this.panel1.Controls.Add(this.button_avito_get);
            this.panel1.Controls.Add(this.button_avto_pro);
            this.panel1.Controls.Add(this.button_vk_sync);
            this.panel1.Controls.Add(this.button_tiu_sync);
            this.panel1.Controls.Add(this.label_bus);
            this.panel1.Controls.Add(this.label_tiu);
            this.panel1.Controls.Add(this.numericUpDown_CdekAddNewCount);
            this.panel1.Controls.Add(this.label_vk);
            this.panel1.Controls.Add(this.label_drom);
            this.panel1.Controls.Add(this.numericUpDown_СdekCheckUrls);
            this.panel1.Controls.Add(this.button_AutoRuStart);
            this.panel1.Controls.Add(this.checkBoxCdekSyncActive);
            this.panel1.Controls.Add(this.label_auto);
            this.panel1.Controls.Add(this.label_cdek);
            this.panel1.Controls.Add(this.button_cdek);
            this.panel1.Controls.Add(this.label_youla);
            this.panel1.Controls.Add(this.label_gde);
            this.panel1.Controls.Add(this.button_GdeGet);
            this.panel1.Controls.Add(this.label_lastSyncTime);
            this.panel1.Controls.Add(this.label_KP);
            this.panel1.Controls.Add(this.buttonSatom);
            this.panel1.Controls.Add(this.buttonKupiprodaiAdd);
            this.panel1.Controls.Add(this.buttonKupiprodai);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Left;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(162, 417);
            this.panel1.TabIndex = 146;
            // 
            // button_izap24
            // 
            this.button_izap24.Location = new System.Drawing.Point(4, 342);
            this.button_izap24.Name = "button_izap24";
            this.button_izap24.Size = new System.Drawing.Size(114, 23);
            this.button_izap24.TabIndex = 147;
            this.button_izap24.Text = "IZap24.ru";
            this.button_izap24.UseVisualStyleBackColor = true;
            this.button_izap24.Click += new System.EventHandler(this.Izap24_Click);
            // 
            // panel2
            // 
            this.panel2.AutoSize = true;
            this.panel2.Controls.Add(this.buttonTest);
            this.panel2.Controls.Add(this.buttonPriceLevelsReport);
            this.panel2.Controls.Add(this.button_put_desc);
            this.panel2.Controls.Add(this.button_SettingsFormOpen);
            this.panel2.Controls.Add(this.button_SaveCookie);
            this.panel2.Controls.Add(this.buttonTestPartners);
            this.panel2.Controls.Add(this.button_PricesCheck);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel2.Location = new System.Drawing.Point(0, 417);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(589, 34);
            this.panel2.TabIndex = 147;
            // 
            // buttonTest
            // 
            this.buttonTest.Location = new System.Drawing.Point(781, 8);
            this.buttonTest.Name = "buttonTest";
            this.buttonTest.Size = new System.Drawing.Size(69, 23);
            this.buttonTest.TabIndex = 20;
            this.buttonTest.TabStop = false;
            this.buttonTest.Text = "Тест";
            this.buttonTest.UseVisualStyleBackColor = true;
            this.buttonTest.Click += new System.EventHandler(this.buttonTest_Click);
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.textBox_LogFilter);
            this.panel3.Controls.Add(this.button_LogFilterClear);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel3.Location = new System.Drawing.Point(162, 392);
            this.panel3.Name = "panel3";
            this.panel3.Padding = new System.Windows.Forms.Padding(2);
            this.panel3.Size = new System.Drawing.Size(427, 25);
            this.panel3.TabIndex = 148;
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
            this.panel4.Controls.Add(this.panel3);
            this.panel4.Controls.Add(this.panel1);
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
            this.Controls.Add(this.panel2);
            this.MaximizeBox = false;
            this.Name = "FormMain";
            this.Text = "Синхронизация бизнес.ру ";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.FormMain_Load);
            ((System.ComponentModel.ISupportInitialize)(this.ds)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_СdekCheckUrls)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_CdekAddNewCount)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
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
        private System.Windows.Forms.Button buttonPriceLevelsReport;
        private System.Windows.Forms.Button button_vk_sync;
        private System.Windows.Forms.Button button_tiu_sync;
        private System.Data.DataSet ds;
        private System.Windows.Forms.CheckBox checkBox_sync;
        private System.Windows.Forms.Label label_bus;
        private System.Windows.Forms.Label label_tiu;
        private System.Windows.Forms.Label label_vk;
        private System.Windows.Forms.Timer timer_sync;
        private System.Windows.Forms.Label label_drom;
        private System.Windows.Forms.Button button_put_desc;
        private System.Windows.Forms.Button buttonTestPartners;
        private System.Windows.Forms.Button button_AutoRuStart;
        private System.Windows.Forms.Label label_auto;
        private System.Windows.Forms.Label label_youla;
        private System.Windows.Forms.Label label_lastSyncTime;
        private System.Windows.Forms.DateTimePicker dateTimePicker1;
        private System.Windows.Forms.Button buttonSatom;
        private System.Windows.Forms.Button buttonKupiprodai;
        private System.Windows.Forms.Button buttonKupiprodaiAdd;
        private System.Windows.Forms.Label label_KP;
        private System.Windows.Forms.Button button_PricesCheck;
        private System.Windows.Forms.Button button_GdeGet;
        private System.Windows.Forms.Label label_gde;
        private System.Windows.Forms.Button button_cdek;
        private System.Windows.Forms.Label label_cdek;
        private System.Windows.Forms.Button button_SaveCookie;
        private System.Windows.Forms.CheckBox checkBoxCdekSyncActive;
        private System.Windows.Forms.NumericUpDown numericUpDown_СdekCheckUrls;
        private System.Windows.Forms.NumericUpDown numericUpDown_CdekAddNewCount;
        private System.Windows.Forms.Button button_avto_pro;
        private System.Windows.Forms.Button button_SettingsFormOpen;
        private System.Windows.Forms.Button button_EuroAuto;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.TextBox textBox_LogFilter;
        private System.Windows.Forms.Button button_LogFilterClear;
        private System.Windows.Forms.Panel panel4;
        private System.Windows.Forms.Button button_izap24;
        private System.Windows.Forms.Button buttonTest;
    }
}

