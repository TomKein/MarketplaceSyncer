namespace Selen {
    partial class FormSettings {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.panel_Common = new System.Windows.Forms.Panel();
            this.panel_Search = new System.Windows.Forms.Panel();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.button_Search = new System.Windows.Forms.Button();
            this.panel_DataGrid = new System.Windows.Forms.Panel();
            this.dataGridView_Settings = new System.Windows.Forms.DataGridView();
            this.panel_Common.SuspendLayout();
            this.panel_Search.SuspendLayout();
            this.panel_DataGrid.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView_Settings)).BeginInit();
            this.SuspendLayout();
            // 
            // panel_Common
            // 
            this.panel_Common.Controls.Add(this.panel_DataGrid);
            this.panel_Common.Controls.Add(this.panel_Search);
            this.panel_Common.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel_Common.Location = new System.Drawing.Point(0, 0);
            this.panel_Common.Name = "panel_Common";
            this.panel_Common.Size = new System.Drawing.Size(728, 544);
            this.panel_Common.TabIndex = 4;
            // 
            // panel_Search
            // 
            this.panel_Search.Controls.Add(this.textBox2);
            this.panel_Search.Controls.Add(this.button_Search);
            this.panel_Search.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel_Search.Location = new System.Drawing.Point(0, 0);
            this.panel_Search.Name = "panel_Search";
            this.panel_Search.Padding = new System.Windows.Forms.Padding(10);
            this.panel_Search.Size = new System.Drawing.Size(728, 46);
            this.panel_Search.TabIndex = 4;
            // 
            // textBox2
            // 
            this.textBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBox2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.textBox2.Location = new System.Drawing.Point(10, 10);
            this.textBox2.Margin = new System.Windows.Forms.Padding(5);
            this.textBox2.Name = "textBox2";
            this.textBox2.Size = new System.Drawing.Size(605, 26);
            this.textBox2.TabIndex = 1;
            // 
            // button_Search
            // 
            this.button_Search.Dock = System.Windows.Forms.DockStyle.Right;
            this.button_Search.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.button_Search.Location = new System.Drawing.Point(615, 10);
            this.button_Search.Margin = new System.Windows.Forms.Padding(10);
            this.button_Search.Name = "button_Search";
            this.button_Search.Size = new System.Drawing.Size(103, 26);
            this.button_Search.TabIndex = 2;
            this.button_Search.Text = "Найти";
            this.button_Search.UseVisualStyleBackColor = true;
            // 
            // panel_DataGrid
            // 
            this.panel_DataGrid.Controls.Add(this.dataGridView_Settings);
            this.panel_DataGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel_DataGrid.Location = new System.Drawing.Point(0, 46);
            this.panel_DataGrid.Name = "panel_DataGrid";
            this.panel_DataGrid.Padding = new System.Windows.Forms.Padding(10);
            this.panel_DataGrid.Size = new System.Drawing.Size(728, 498);
            this.panel_DataGrid.TabIndex = 5;
            // 
            // dataGridView_Settings
            // 
            this.dataGridView_Settings.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridView_Settings.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView_Settings.Location = new System.Drawing.Point(10, 10);
            this.dataGridView_Settings.MultiSelect = false;
            this.dataGridView_Settings.Name = "dataGridView_Settings";
            this.dataGridView_Settings.RowTemplate.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView_Settings.Size = new System.Drawing.Size(708, 478);
            this.dataGridView_Settings.TabIndex = 3;
            this.dataGridView_Settings.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView_Settings_CellEndEdit);
            // 
            // FormSettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(728, 544);
            this.Controls.Add(this.panel_Common);
            this.Name = "FormSettings";
            this.Text = "Настройки";
            this.Load += new System.EventHandler(this.FormSettings_Load);
            this.panel_Common.ResumeLayout(false);
            this.panel_Search.ResumeLayout(false);
            this.panel_Search.PerformLayout();
            this.panel_DataGrid.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView_Settings)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel_Common;
        private System.Windows.Forms.Panel panel_DataGrid;
        private System.Windows.Forms.DataGridView dataGridView_Settings;
        private System.Windows.Forms.Panel panel_Search;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.Button button_Search;
    }
}