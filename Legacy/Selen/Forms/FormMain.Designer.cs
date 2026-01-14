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
            this.button_drom = new System.Windows.Forms.Button();
            this.button_avito = new System.Windows.Forms.Button();
            this.logBox = new System.Windows.Forms.RichTextBox();
            this.button_PriceLevelsReport = new System.Windows.Forms.Button();
            this.button_vk = new System.Windows.Forms.Button();
            this.label_Bus = new System.Windows.Forms.Label();
            this.button_Descriptions = new System.Windows.Forms.Button();
            this.button_TestPartners = new System.Windows.Forms.Button();
            this.label_Auto = new System.Windows.Forms.Label();
            this.label_lastSyncTime = new System.Windows.Forms.Label();
            this.dateTimePicker1 = new System.Windows.Forms.DateTimePicker();
            this.button_PricesIncomeCorrection = new System.Windows.Forms.Button();
            this.button_Settings = new System.Windows.Forms.Button();
            this.panel_Buttons = new System.Windows.Forms.Panel();
            this.button_LogShowAll = new System.Windows.Forms.Button();
            this.button_ShowWarnings = new System.Windows.Forms.Button();
            this.button_ShowErrors = new System.Windows.Forms.Button();
            this.button_wb = new System.Windows.Forms.Button();
            this.button_AvitoCategories = new System.Windows.Forms.Button();
            this.button_mm = new System.Windows.Forms.Button();
            this.button_ozon = new System.Windows.Forms.Button();
            this.button_ym = new System.Windows.Forms.Button();
            this.button_Izap24 = new System.Windows.Forms.Button();
            this.panel_bottom = new System.Windows.Forms.Panel();
            this.button_application = new System.Windows.Forms.Button();
            this.button_WeightsDimensions = new System.Windows.Forms.Button();
            this.button_Test = new System.Windows.Forms.Button();
            this.panel_Filter = new System.Windows.Forms.Panel();
            this.textBox_LogFilter = new System.Windows.Forms.TextBox();
            this.panel4 = new System.Windows.Forms.Panel();
            this.panel_Buttons.SuspendLayout();
            this.panel_bottom.SuspendLayout();
            this.panel_Filter.SuspendLayout();
            this.panel4.SuspendLayout();
            this.SuspendLayout();
            // 
            // button_drom
            // 
            this.button_drom.Location = new System.Drawing.Point(5, 95);
            this.button_drom.Name = "button_drom";
            this.button_drom.Size = new System.Drawing.Size(113, 23);
            this.button_drom.TabIndex = 4;
            this.button_drom.Text = "Drom.ru";
            this.button_drom.UseVisualStyleBackColor = true;
            this.button_drom.Click += new System.EventHandler(this.button_drom_Click);
            // 
            // button_avito
            // 
            this.button_avito.Location = new System.Drawing.Point(33, 67);
            this.button_avito.Name = "button_avito";
            this.button_avito.Size = new System.Drawing.Size(85, 25);
            this.button_avito.TabIndex = 3;
            this.button_avito.Text = "Avito.ru";
            this.button_avito.UseVisualStyleBackColor = true;
            this.button_avito.Click += new System.EventHandler(this.button_avito_Click);
            // 
            // logBox
            // 
            this.logBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.logBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logBox.Font = new System.Drawing.Font("Lucida Console", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.logBox.ForeColor = System.Drawing.Color.LightYellow;
            this.logBox.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.logBox.Location = new System.Drawing.Point(135, 0);
            this.logBox.Name = "logBox";
            this.logBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.logBox.Size = new System.Drawing.Size(649, 352);
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
            // button_vk
            // 
            this.button_vk.Location = new System.Drawing.Point(5, 121);
            this.button_vk.Name = "button_vk";
            this.button_vk.Size = new System.Drawing.Size(113, 23);
            this.button_vk.TabIndex = 7;
            this.button_vk.Text = "Vk.com";
            this.button_vk.UseVisualStyleBackColor = true;
            this.button_vk.Click += new System.EventHandler(this.button_vk_Click);
            // 
            // label_Bus
            // 
            this.label_Bus.AutoSize = true;
            this.label_Bus.Location = new System.Drawing.Point(10, 50);
            this.label_Bus.Name = "label_Bus";
            this.label_Bus.Size = new System.Drawing.Size(16, 13);
            this.label_Bus.TabIndex = 36;
            this.label_Bus.Text = "...";
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
            this.label_Auto.Location = new System.Drawing.Point(120, 155);
            this.label_Auto.Name = "label_Auto";
            this.label_Auto.Size = new System.Drawing.Size(0, 13);
            this.label_Auto.TabIndex = 66;
            // 
            // label_lastSyncTime
            // 
            this.label_lastSyncTime.AutoSize = true;
            this.label_lastSyncTime.Location = new System.Drawing.Point(8, 8);
            this.label_lastSyncTime.Name = "label_lastSyncTime";
            this.label_lastSyncTime.Size = new System.Drawing.Size(119, 13);
            this.label_lastSyncTime.TabIndex = 98;
            this.label_lastSyncTime.Text = "Посл. синхронизация:";
            // 
            // dateTimePicker1
            // 
            this.dateTimePicker1.CalendarForeColor = System.Drawing.Color.Red;
            this.dateTimePicker1.CustomFormat = "dd.MM.yyyy HH:mm";
            this.dateTimePicker1.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dateTimePicker1.Location = new System.Drawing.Point(8, 24);
            this.dateTimePicker1.MaxDate = new System.DateTime(2099, 12, 31, 0, 0, 0, 0);
            this.dateTimePicker1.MinDate = new System.DateTime(2017, 1, 1, 0, 0, 0, 0);
            this.dateTimePicker1.Name = "dateTimePicker1";
            this.dateTimePicker1.ShowUpDown = true;
            this.dateTimePicker1.Size = new System.Drawing.Size(113, 20);
            this.dateTimePicker1.TabIndex = 99;
            this.dateTimePicker1.Value = new System.DateTime(2020, 9, 2, 5, 50, 0, 0);
            this.dateTimePicker1.Validated += new System.EventHandler(this.dateTimePicker1_Validated);
            // 
            // button_PricesIncomeCorrection
            // 
            this.button_PricesIncomeCorrection.Location = new System.Drawing.Point(392, 8);
            this.button_PricesIncomeCorrection.Name = "button_PricesIncomeCorrection";
            this.button_PricesIncomeCorrection.Size = new System.Drawing.Size(116, 23);
            this.button_PricesIncomeCorrection.TabIndex = 115;
            this.button_PricesIncomeCorrection.Text = "Seller24 закупка";
            this.button_PricesIncomeCorrection.UseVisualStyleBackColor = true;
            this.button_PricesIncomeCorrection.Click += new System.EventHandler(this.Button_PricesCheck_Click);
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
            this.panel_Buttons.Controls.Add(this.button_LogShowAll);
            this.panel_Buttons.Controls.Add(this.button_ShowWarnings);
            this.panel_Buttons.Controls.Add(this.button_ShowErrors);
            this.panel_Buttons.Controls.Add(this.button_wb);
            this.panel_Buttons.Controls.Add(this.button_AvitoCategories);
            this.panel_Buttons.Controls.Add(this.button_mm);
            this.panel_Buttons.Controls.Add(this.button_ozon);
            this.panel_Buttons.Controls.Add(this.button_ym);
            this.panel_Buttons.Controls.Add(this.button_Izap24);
            this.panel_Buttons.Controls.Add(this.dateTimePicker1);
            this.panel_Buttons.Controls.Add(this.button_drom);
            this.panel_Buttons.Controls.Add(this.button_avito);
            this.panel_Buttons.Controls.Add(this.button_vk);
            this.panel_Buttons.Controls.Add(this.label_Bus);
            this.panel_Buttons.Controls.Add(this.label_Auto);
            this.panel_Buttons.Controls.Add(this.label_lastSyncTime);
            this.panel_Buttons.Dock = System.Windows.Forms.DockStyle.Left;
            this.panel_Buttons.Location = new System.Drawing.Point(0, 0);
            this.panel_Buttons.Name = "panel_Buttons";
            this.panel_Buttons.Padding = new System.Windows.Forms.Padding(5);
            this.panel_Buttons.Size = new System.Drawing.Size(135, 377);
            this.panel_Buttons.TabIndex = 146;
            // 
            // button_LogShowAll
            // 
            this.button_LogShowAll.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.button_LogShowAll.Location = new System.Drawing.Point(5, 305);
            this.button_LogShowAll.Margin = new System.Windows.Forms.Padding(5);
            this.button_LogShowAll.Name = "button_LogShowAll";
            this.button_LogShowAll.Size = new System.Drawing.Size(125, 21);
            this.button_LogShowAll.TabIndex = 1;
            this.button_LogShowAll.Text = "Весь лог";
            this.button_LogShowAll.UseVisualStyleBackColor = true;
            this.button_LogShowAll.Click += new System.EventHandler(this.Button_LogFilterClear_Click);
            // 
            // button_ShowWarnings
            // 
            this.button_ShowWarnings.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.button_ShowWarnings.Location = new System.Drawing.Point(5, 326);
            this.button_ShowWarnings.Name = "button_ShowWarnings";
            this.button_ShowWarnings.Size = new System.Drawing.Size(125, 23);
            this.button_ShowWarnings.TabIndex = 156;
            this.button_ShowWarnings.Text = "Предупреждения";
            this.button_ShowWarnings.UseVisualStyleBackColor = true;
            this.button_ShowWarnings.Click += new System.EventHandler(this.button_showWarnings_Click);
            // 
            // button_ShowErrors
            // 
            this.button_ShowErrors.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.button_ShowErrors.Location = new System.Drawing.Point(5, 349);
            this.button_ShowErrors.Name = "button_ShowErrors";
            this.button_ShowErrors.Size = new System.Drawing.Size(125, 23);
            this.button_ShowErrors.TabIndex = 155;
            this.button_ShowErrors.Text = "Ошибки";
            this.button_ShowErrors.UseVisualStyleBackColor = true;
            this.button_ShowErrors.Click += new System.EventHandler(this.button_showErrors_Click);
            // 
            // button_wb
            // 
            this.button_wb.Location = new System.Drawing.Point(5, 225);
            this.button_wb.Name = "button_wb";
            this.button_wb.Size = new System.Drawing.Size(113, 23);
            this.button_wb.TabIndex = 154;
            this.button_wb.Text = "Wildberries";
            this.button_wb.UseVisualStyleBackColor = true;
            this.button_wb.Click += new System.EventHandler(this.button_wb_Click);
            // 
            // button_AvitoCategories
            // 
            this.button_AvitoCategories.FlatAppearance.BorderSize = 0;
            this.button_AvitoCategories.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button_AvitoCategories.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.button_AvitoCategories.Location = new System.Drawing.Point(4, 67);
            this.button_AvitoCategories.Name = "button_AvitoCategories";
            this.button_AvitoCategories.Size = new System.Drawing.Size(29, 25);
            this.button_AvitoCategories.TabIndex = 152;
            this.button_AvitoCategories.Text = "⚙";
            this.button_AvitoCategories.UseVisualStyleBackColor = true;
            this.button_AvitoCategories.Click += new System.EventHandler(this.button_AvitoCategories_Click);
            // 
            // button_mm
            // 
            this.button_mm.Location = new System.Drawing.Point(5, 251);
            this.button_mm.Name = "button_mm";
            this.button_mm.Size = new System.Drawing.Size(113, 23);
            this.button_mm.TabIndex = 151;
            this.button_mm.Text = "MegaMarket";
            this.button_mm.UseVisualStyleBackColor = true;
            this.button_mm.Click += new System.EventHandler(this.button_mm_Click);
            // 
            // button_ozon
            // 
            this.button_ozon.Location = new System.Drawing.Point(5, 199);
            this.button_ozon.Name = "button_ozon";
            this.button_ozon.Size = new System.Drawing.Size(113, 23);
            this.button_ozon.TabIndex = 151;
            this.button_ozon.Text = "OZON";
            this.button_ozon.UseVisualStyleBackColor = true;
            this.button_ozon.Click += new System.EventHandler(this.button_ozon_Click);
            // 
            // button_ym
            // 
            this.button_ym.Location = new System.Drawing.Point(5, 173);
            this.button_ym.Name = "button_ym";
            this.button_ym.Size = new System.Drawing.Size(113, 23);
            this.button_ym.TabIndex = 149;
            this.button_ym.Text = "Yandex.Market";
            this.button_ym.UseVisualStyleBackColor = true;
            this.button_ym.Click += new System.EventHandler(this.button_ym_Click);
            // 
            // button_Izap24
            // 
            this.button_Izap24.Location = new System.Drawing.Point(4, 147);
            this.button_Izap24.Name = "button_Izap24";
            this.button_Izap24.Size = new System.Drawing.Size(114, 23);
            this.button_Izap24.TabIndex = 147;
            this.button_Izap24.Text = "IZap24.ru";
            this.button_Izap24.UseVisualStyleBackColor = true;
            this.button_Izap24.Click += new System.EventHandler(this.button_Izap24_Click);
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
            this.panel_bottom.Controls.Add(this.button_TestPartners);
            this.panel_bottom.Controls.Add(this.button_PricesIncomeCorrection);
            this.panel_bottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel_bottom.Location = new System.Drawing.Point(0, 377);
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
            this.panel_Filter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel_Filter.Location = new System.Drawing.Point(135, 352);
            this.panel_Filter.Name = "panel_Filter";
            this.panel_Filter.Padding = new System.Windows.Forms.Padding(2);
            this.panel_Filter.Size = new System.Drawing.Size(649, 25);
            this.panel_Filter.TabIndex = 148;
            // 
            // textBox_LogFilter
            // 
            this.textBox_LogFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBox_LogFilter.Font = new System.Drawing.Font("Lucida Console", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.textBox_LogFilter.Location = new System.Drawing.Point(2, 2);
            this.textBox_LogFilter.Name = "textBox_LogFilter";
            this.textBox_LogFilter.Size = new System.Drawing.Size(645, 21);
            this.textBox_LogFilter.TabIndex = 0;
            this.textBox_LogFilter.TextChanged += new System.EventHandler(this.TextBox_LogFilter_TextChanged);
            // 
            // panel4
            // 
            this.panel4.Controls.Add(this.logBox);
            this.panel4.Controls.Add(this.panel_Filter);
            this.panel4.Controls.Add(this.panel_Buttons);
            this.panel4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel4.Location = new System.Drawing.Point(0, 0);
            this.panel4.Name = "panel4";
            this.panel4.Size = new System.Drawing.Size(784, 377);
            this.panel4.TabIndex = 149;
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Gray;
            this.ClientSize = new System.Drawing.Size(784, 411);
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
        private System.Windows.Forms.Button button_drom;
        private System.Windows.Forms.Button button_avito;
        private System.Windows.Forms.RichTextBox logBox;
        private System.Windows.Forms.Button button_PriceLevelsReport;
        private System.Windows.Forms.Button button_vk;
        private System.Windows.Forms.Label label_Bus;
        private System.Windows.Forms.Button button_Descriptions;
        private System.Windows.Forms.Button button_TestPartners;
        private System.Windows.Forms.Label label_Auto;
        private System.Windows.Forms.Label label_lastSyncTime;
        private System.Windows.Forms.DateTimePicker dateTimePicker1;
        private System.Windows.Forms.Button button_PricesIncomeCorrection;
        private System.Windows.Forms.Button button_Settings;
        private System.Windows.Forms.Panel panel_Buttons;
        private System.Windows.Forms.Panel panel_bottom;
        private System.Windows.Forms.Panel panel_Filter;
        private System.Windows.Forms.TextBox textBox_LogFilter;
        private System.Windows.Forms.Button button_LogShowAll;
        private System.Windows.Forms.Panel panel4;
        private System.Windows.Forms.Button button_Izap24;
        private System.Windows.Forms.Button button_Test;
        private System.Windows.Forms.Button button_ym;
        private System.Windows.Forms.Button button_WeightsDimensions;
        private System.Windows.Forms.Button button_ozon;
        private System.Windows.Forms.Button button_application;
        private System.Windows.Forms.Button button_mm;
        private System.Windows.Forms.Button button_AvitoCategories;
        private System.Windows.Forms.Button button_wb;
        private System.Windows.Forms.Button button_ShowErrors;
        private System.Windows.Forms.Button button_ShowWarnings;
    }
}

