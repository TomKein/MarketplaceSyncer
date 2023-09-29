namespace Selen {
    partial class FormWeightsDimentions {
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
            this.panel_DataGrid = new System.Windows.Forms.Panel();
            this.dataGridView = new System.Windows.Forms.DataGridView();
            this.panel_Search = new System.Windows.Forms.Panel();
            this.labelRowCount = new System.Windows.Forms.Label();
            this.checkBox_onlyHaveImage = new System.Windows.Forms.CheckBox();
            this.checkBox_EmptyOnly = new System.Windows.Forms.CheckBox();
            this.textBox_Search = new System.Windows.Forms.TextBox();
            this.button_Clear = new System.Windows.Forms.Button();
            this.panel_Common.SuspendLayout();
            this.panel_DataGrid.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            this.panel_Search.SuspendLayout();
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
            // panel_DataGrid
            // 
            this.panel_DataGrid.Controls.Add(this.dataGridView);
            this.panel_DataGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel_DataGrid.Location = new System.Drawing.Point(0, 46);
            this.panel_DataGrid.Name = "panel_DataGrid";
            this.panel_DataGrid.Padding = new System.Windows.Forms.Padding(10);
            this.panel_DataGrid.Size = new System.Drawing.Size(728, 498);
            this.panel_DataGrid.TabIndex = 5;
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToResizeRows = false;
            this.dataGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView.Location = new System.Drawing.Point(10, 10);
            this.dataGridView.MultiSelect = false;
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.RowTemplate.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridView.Size = new System.Drawing.Size(708, 478);
            this.dataGridView.TabIndex = 3;
            this.dataGridView.CellBeginEdit += new System.Windows.Forms.DataGridViewCellCancelEventHandler(this.dataGridView_CellBeginEdit);
            this.dataGridView.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView_CellDoubleClick);
            this.dataGridView.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView_CellEndEdit);
            this.dataGridView.CellMouseDown += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dataGridView_CellMouseDown);
            this.dataGridView.CellMouseEnter += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView_CellMouseEnter);
            this.dataGridView.CellMouseUp += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dataGridView_CellMouseUp);
            // 
            // panel_Search
            // 
            this.panel_Search.Controls.Add(this.labelRowCount);
            this.panel_Search.Controls.Add(this.checkBox_onlyHaveImage);
            this.panel_Search.Controls.Add(this.checkBox_EmptyOnly);
            this.panel_Search.Controls.Add(this.textBox_Search);
            this.panel_Search.Controls.Add(this.button_Clear);
            this.panel_Search.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel_Search.Location = new System.Drawing.Point(0, 0);
            this.panel_Search.Name = "panel_Search";
            this.panel_Search.Padding = new System.Windows.Forms.Padding(10);
            this.panel_Search.Size = new System.Drawing.Size(728, 46);
            this.panel_Search.TabIndex = 4;
            // 
            // labelRowCount
            // 
            this.labelRowCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelRowCount.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.labelRowCount.Location = new System.Drawing.Point(618, 34);
            this.labelRowCount.Name = "labelRowCount";
            this.labelRowCount.Size = new System.Drawing.Size(100, 12);
            this.labelRowCount.TabIndex = 5;
            this.labelRowCount.Text = "0";
            this.labelRowCount.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // checkBox_onlyHaveImage
            // 
            this.checkBox_onlyHaveImage.AutoSize = true;
            this.checkBox_onlyHaveImage.Checked = true;
            this.checkBox_onlyHaveImage.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_onlyHaveImage.Location = new System.Drawing.Point(650, 16);
            this.checkBox_onlyHaveImage.Name = "checkBox_onlyHaveImage";
            this.checkBox_onlyHaveImage.Size = new System.Drawing.Size(60, 17);
            this.checkBox_onlyHaveImage.TabIndex = 4;
            this.checkBox_onlyHaveImage.Text = "с фото";
            this.checkBox_onlyHaveImage.UseVisualStyleBackColor = true;
            this.checkBox_onlyHaveImage.CheckedChanged += new System.EventHandler(this.textBox_Search_TextChanged);
            // 
            // checkBox_EmptyOnly
            // 
            this.checkBox_EmptyOnly.AutoSize = true;
            this.checkBox_EmptyOnly.Checked = true;
            this.checkBox_EmptyOnly.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_EmptyOnly.Location = new System.Drawing.Point(580, 16);
            this.checkBox_EmptyOnly.Name = "checkBox_EmptyOnly";
            this.checkBox_EmptyOnly.Size = new System.Drawing.Size(62, 17);
            this.checkBox_EmptyOnly.TabIndex = 3;
            this.checkBox_EmptyOnly.Text = "пустые";
            this.checkBox_EmptyOnly.UseVisualStyleBackColor = true;
            this.checkBox_EmptyOnly.CheckedChanged += new System.EventHandler(this.textBox_Search_TextChanged);
            // 
            // textBox_Search
            // 
            this.textBox_Search.Dock = System.Windows.Forms.DockStyle.Left;
            this.textBox_Search.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.textBox_Search.Location = new System.Drawing.Point(10, 10);
            this.textBox_Search.Margin = new System.Windows.Forms.Padding(5);
            this.textBox_Search.Name = "textBox_Search";
            this.textBox_Search.Size = new System.Drawing.Size(480, 26);
            this.textBox_Search.TabIndex = 1;
            this.textBox_Search.TextChanged += new System.EventHandler(this.textBox_Search_TextChanged);
            // 
            // button_Clear
            // 
            this.button_Clear.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.button_Clear.Location = new System.Drawing.Point(493, 10);
            this.button_Clear.Margin = new System.Windows.Forms.Padding(10);
            this.button_Clear.Name = "button_Clear";
            this.button_Clear.Size = new System.Drawing.Size(74, 26);
            this.button_Clear.TabIndex = 2;
            this.button_Clear.Text = "Очистить";
            this.button_Clear.UseVisualStyleBackColor = true;
            this.button_Clear.Click += new System.EventHandler(this.button_Clear_Click);
            // 
            // FormWeightsDimentions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(728, 544);
            this.Controls.Add(this.panel_Common);
            this.Name = "FormWeightsDimentions";
            this.Text = "Веса и размеры";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.FormWeightsDimentions_FormClosed);
            this.Load += new System.EventHandler(this.FormWeightsDimentions_Load);
            this.panel_Common.ResumeLayout(false);
            this.panel_DataGrid.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            this.panel_Search.ResumeLayout(false);
            this.panel_Search.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel_Common;
        private System.Windows.Forms.Panel panel_DataGrid;
        private System.Windows.Forms.DataGridView dataGridView;
        private System.Windows.Forms.Panel panel_Search;
        private System.Windows.Forms.TextBox textBox_Search;
        private System.Windows.Forms.Button button_Clear;
        private System.Windows.Forms.CheckBox checkBox_onlyHaveImage;
        private System.Windows.Forms.CheckBox checkBox_EmptyOnly;
        private System.Windows.Forms.Label labelRowCount;
    }


}