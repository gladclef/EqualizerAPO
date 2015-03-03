using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace equalizerapo_and_zune
{
    public partial class form_main : Form
    {
        #region fields

        public ZuneAPI zuneAPI;
        public equalizerapo_api eqAPI;
        private int previousFilterIndex;
        private bool mousePressed = false;

        #endregion

        #region public methods

        public form_main()
        {
            InitializeComponent();

            zuneAPI = new ZuneAPI();
            eqAPI = new equalizerapo_api();
            zuneAPI.TrackChanged += TrackChanged;
            zuneAPI.Init();
        }

        public void UpdateTrackTitle(String newTitle)
        {
            if (newTitle == null)
            {
                return;
            }

            if (label_artistname_trackname.InvokeRequired)
            {
                // thread-safe callback
                SetTextCallback d = new SetTextCallback(UpdateTrackTitle);
                this.Invoke(d, new object[] { newTitle });
            }
            else
            {
                label_artistname_trackname.Text = newTitle;
            }
        }

        #endregion

        #region private methods

        private void TrackChanged(object sender, EventArgs e)
        {
            DeferredUpdateAll(sender);
        }

        private void DeferredUpdateAll(object sender)
        {
            Microsoft.Iris.Application.DeferredInvoke(
                new Microsoft.Iris.DeferredInvokeHandler(UpdateAll),
                Microsoft.Iris.DeferredInvokePriority.Normal);
        }

        private void UpdateAll(object sender)
        {
            if (zuneAPI.CurrentTrack == null)
            {
                return;
            }

            eqAPI.UpdateTrack(zuneAPI.CurrentTrack);
            UpdateTrackTitle(zuneAPI.CurrentTrack.GetFullName());
            UpdateEqualizer(sender);
        }

        private void UpdateEqualizer(object sender)
        {
            if (chart_filters.InvokeRequired)
            {
                // thread-safe method
                DeferredInvokeDelegate d = new DeferredInvokeDelegate(UpdateEqualizer);
                this.Invoke(d, new object[] { sender });
            }
            else
            {
                // get the list of filters
                SortedList<double, Filter> filters = eqAPI.GetFilters();

                // update the equalizer series
                chart_filters.Series.Clear();
                System.Windows.Forms.DataVisualization.Charting.Series series =
                    chart_filters.Series.Add("frequencies");
                foreach (System.Collections.Generic.KeyValuePair<double, Filter> pair in filters)
                {
                    Filter filter = pair.Value;
                    series.Points.Add(filter.Gain);
                }

                // change the equalizer graph visually
                // make it a line graph instead of bar graph
                series.ChartType =
                    System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
                // set the range of the axis
                double gainMax = equalizerapo_api.GainMax;
                System.Windows.Forms.DataVisualization.Charting.Axis yaxis =
                    chart_filters.ChartAreas["ChartArea1"].AxisY;
                System.Windows.Forms.DataVisualization.Charting.Axis xaxis =
                    chart_filters.ChartAreas["ChartArea1"].AxisX;
                yaxis.Interval = gainMax / 3;
                yaxis.Minimum = -gainMax;
                yaxis.Maximum = gainMax;
                xaxis.Minimum = 1;
                xaxis.Maximum = filters.Count;
                // remove grid lines
                xaxis.MajorGrid.LineDashStyle =
                    System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.NotSet;
                yaxis.MajorGrid.LineDashStyle =
                    System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.NotSet;
                // make graph easier to see
                series.BorderWidth = 3;

                chart_filters.DataBind();
            }
        }

        private void button_next_Click(object sender, EventArgs e)
        {
            zuneAPI.ToNextTrack();
        }

        private void button_previous_Click(object sender, EventArgs e)
        {
            zuneAPI.ToPreviousTrack();
        }

        private void chart_filters_MouseMove(object sender, MouseEventArgs e)
        {
            if (!mousePressed)
            {
                return;
            }

            chart_filters_Click(sender, e, previousFilterIndex);
        }

        private void chart_filters_MouseUp(object sender, MouseEventArgs e)
        {
            System.Diagnostics.Debugger.Log(1, "", "released!\n");
            mousePressed = false;
        }

        private void chart_filters_Click(object sender, MouseEventArgs e)
        {
            chart_filters_Click(sender, e, -1);
            System.Diagnostics.Debugger.Log(1, "", "pressed!\n");
            mousePressed = true;
        }

        /// <summary>
        /// Adjusts the gain based on which filter is clicked.
        /// </summary>
        /// <param name="sender">sender object</param>
        /// <param name="e">mouse event</param>
        /// <param name="filterIndex">if -1, then computer filter index</param>
        private void chart_filters_Click(object sender, MouseEventArgs e, int filterIndex)
        {
            double gainMax = equalizerapo_api.GainMax;
            double filterCount = eqAPI.GetFilters().Count;

            // bounding box constant, determined experimentally
            double[,] bb = { { 50, 15 }, { 380, 133 } };
            double xRange = bb[1, 0] - bb[0, 0];
            double yRange = bb[1, 1] - bb[0, 1];

            // {X,Y}, relative to the series area
            double x = e.X - bb[0, 0];
            double y = e.Y - bb[0, 1];

            // determine the gain
            double xRatio =
                Math.Max(
                    Math.Min(
                        x / xRange,
                        1),
                    0);
            double yRatio =
                Math.Max(
                    Math.Min(
                        y / yRange,
                        1),
                    0);
            double gain = gainMax - (2 * gainMax) * (yRatio);

            // determine the filter index
            if (filterIndex == -1)
            {
                filterIndex = Convert.ToInt32(xRatio * filterCount);
                if (filterCount >= 3)
                {
                    double numRegions = filterCount * 2 - 2;
                    filterIndex = Convert.ToInt32(Math.Floor(xRatio * numRegions));
                    if (filterIndex % 2 == 1)
                    {
                        filterIndex += 1;
                    }
                    filterIndex /= 2;
                }
            }
            previousFilterIndex = filterIndex;

            // update the filter
            Filter filter = eqAPI.GetFilter(filterIndex);
            if (filter != null)
            {
                filter.Gain = gain;
                DeferredUpdateAll(this);
            }
        }

        private void checkbox_apply_equalizer_CheckedChanged(object sender, EventArgs e)
        {
            if (checkbox_apply_equalizer.InvokeRequired)
            {
                EventInvokeDelegate d = new EventInvokeDelegate(checkbox_apply_equalizer_CheckedChanged);
                this.Invoke(d, new object[] { sender, e });
            }
            else
            {
                eqAPI.ApplyEqualizer(checkbox_apply_equalizer.Checked);
            }
        }

        #endregion

        #region delegate classes

        delegate void SetTextCallback(String text);
        delegate void ButtonAdjustCallback();
        delegate void DeferredInvokeDelegate(object sender);
        delegate void EventInvokeDelegate(object sender, EventArgs e);

        #endregion
    }
}
