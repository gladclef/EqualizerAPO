namespace equalizerapo_and_zune
{
    partial class form_main
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea2 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend2 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series2 = new System.Windows.Forms.DataVisualization.Charting.Series();
            this.label_current_track = new System.Windows.Forms.Label();
            this.artistname_trackname_link = new System.Windows.Forms.LinkLabel();
            this.linkLabel2 = new System.Windows.Forms.LinkLabel();
            this.button_next = new System.Windows.Forms.Button();
            this.button_previous = new System.Windows.Forms.Button();
            this.chart_filters = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.checkbox_apply_equalizer = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.chart_filters)).BeginInit();
            this.SuspendLayout();
            // 
            // label_current_track
            // 
            this.label_current_track.AutoSize = true;
            this.label_current_track.Location = new System.Drawing.Point(9, 0);
            this.label_current_track.Name = "label_current_track";
            this.label_current_track.Size = new System.Drawing.Size(95, 17);
            this.label_current_track.TabIndex = 0;
            this.label_current_track.Text = "Current Track";
            // 
            // artistname_trackname_link
            // 
            this.artistname_trackname_link.AutoSize = true;
            this.artistname_trackname_link.Location = new System.Drawing.Point(110, 0);
            this.artistname_trackname_link.Name = "artistname_trackname_link";
            this.artistname_trackname_link.Size = new System.Drawing.Size(158, 17);
            this.artistname_trackname_link.TabIndex = 1;
            this.artistname_trackname_link.TabStop = true;
            this.artistname_trackname_link.Text = "Artist Name - Song Title";
            // 
            // linkLabel2
            // 
            this.linkLabel2.AutoSize = true;
            this.linkLabel2.Location = new System.Drawing.Point(9, 230);
            this.linkLabel2.Name = "linkLabel2";
            this.linkLabel2.Size = new System.Drawing.Size(93, 17);
            this.linkLabel2.TabIndex = 2;
            this.linkLabel2.TabStop = true;
            this.linkLabel2.Text = "edit equalizer";
            // 
            // button_next
            // 
            this.button_next.Location = new System.Drawing.Point(472, 227);
            this.button_next.Name = "button_next";
            this.button_next.Size = new System.Drawing.Size(75, 23);
            this.button_next.TabIndex = 4;
            this.button_next.Text = "Next";
            this.button_next.UseVisualStyleBackColor = true;
            this.button_next.Click += new System.EventHandler(this.button_next_Click);
            // 
            // button_previous
            // 
            this.button_previous.Location = new System.Drawing.Point(391, 227);
            this.button_previous.Name = "button_previous";
            this.button_previous.Size = new System.Drawing.Size(75, 23);
            this.button_previous.TabIndex = 5;
            this.button_previous.Text = "Previous";
            this.button_previous.UseVisualStyleBackColor = true;
            this.button_previous.Click += new System.EventHandler(this.button_previous_Click);
            // 
            // chart_filters
            // 
            this.chart_filters.BackImageTransparentColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.chart_filters.BackSecondaryColor = System.Drawing.Color.Red;
            chartArea2.AxisX.TitleForeColor = System.Drawing.Color.Silver;
            chartArea2.BorderColor = System.Drawing.Color.Silver;
            chartArea2.Name = "ChartArea1";
            this.chart_filters.ChartAreas.Add(chartArea2);
            legend2.Enabled = false;
            legend2.ForeColor = System.Drawing.Color.Silver;
            legend2.Name = "Legend1";
            this.chart_filters.Legends.Add(legend2);
            this.chart_filters.Location = new System.Drawing.Point(12, 20);
            this.chart_filters.Name = "chart_filters";
            series2.ChartArea = "ChartArea1";
            series2.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series2.LabelForeColor = System.Drawing.Color.Red;
            series2.Legend = "Legend1";
            series2.Name = "Series1";
            this.chart_filters.Series.Add(series2);
            this.chart_filters.Size = new System.Drawing.Size(535, 204);
            this.chart_filters.TabIndex = 6;
            this.chart_filters.Text = "chart1";
            this.chart_filters.MouseDown += new System.Windows.Forms.MouseEventHandler(this.chart_filters_Click);
            this.chart_filters.MouseMove += new System.Windows.Forms.MouseEventHandler(this.chart_filters_MouseMove);
            this.chart_filters.MouseUp += new System.Windows.Forms.MouseEventHandler(this.chart_filters_MouseUp);
            // 
            // checkbox_apply_equalizer
            // 
            this.checkbox_apply_equalizer.AutoSize = true;
            this.checkbox_apply_equalizer.Checked = true;
            this.checkbox_apply_equalizer.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkbox_apply_equalizer.Location = new System.Drawing.Point(188, 229);
            this.checkbox_apply_equalizer.Name = "checkbox_apply_equalizer";
            this.checkbox_apply_equalizer.Size = new System.Drawing.Size(128, 21);
            this.checkbox_apply_equalizer.TabIndex = 7;
            this.checkbox_apply_equalizer.Text = "Apply Equalizer";
            this.checkbox_apply_equalizer.UseVisualStyleBackColor = true;
            this.checkbox_apply_equalizer.CheckedChanged += checkbox_apply_equalizer_CheckedChanged;
            // 
            // form_main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(559, 253);
            this.Controls.Add(this.checkbox_apply_equalizer);
            this.Controls.Add(this.chart_filters);
            this.Controls.Add(this.button_previous);
            this.Controls.Add(this.button_next);
            this.Controls.Add(this.linkLabel2);
            this.Controls.Add(this.artistname_trackname_link);
            this.Controls.Add(this.label_current_track);
            this.Name = "form_main";
            this.Text = "EqualizerAPO and Zune";
            ((System.ComponentModel.ISupportInitialize)(this.chart_filters)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label_current_track;
        private System.Windows.Forms.LinkLabel artistname_trackname_link;
        private System.Windows.Forms.LinkLabel linkLabel2;
        private System.Windows.Forms.Button button_next;
        private System.Windows.Forms.Button button_previous;
        private System.Windows.Forms.DataVisualization.Charting.Chart chart_filters;
        private System.Windows.Forms.CheckBox checkbox_apply_equalizer;
    }
}

