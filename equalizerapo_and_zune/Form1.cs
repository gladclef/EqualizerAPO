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

            if (artistname_trackname_link.InvokeRequired)
            {
                // thread-safe callback
                SetTextCallback d = new SetTextCallback(UpdateTrackTitle);
                this.Invoke(d, new object[] { newTitle });
            }
            else
            {
                artistname_trackname_link.Text = newTitle;
            }
        }

        #endregion

        #region private methods

        private void TrackChanged(object sender, EventArgs e)
        {
            if (zuneAPI.CurrentTrack == null)
            {
                return;
            }

            eqAPI.UpdateTrack(zuneAPI.CurrentTrack);
            UpdateTrackTitle(zuneAPI.CurrentTrack.GetFullName());
            Microsoft.Iris.Application.DeferredInvoke(
                new Microsoft.Iris.DeferredInvokeHandler(UpdateEqualizer),
                Microsoft.Iris.DeferredInvokePriority.Normal);
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
                    series.Points.Add(filter.gain);
                }

                // change the equalizer graph visually
                // make it a line graph instead of bar graph
                series.ChartType =
                    System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
                // set the range of the axis
                System.Windows.Forms.DataVisualization.Charting.Axis yaxis =
                    chart_filters.ChartAreas["ChartArea1"].AxisY;
                System.Windows.Forms.DataVisualization.Charting.Axis xaxis =
                    chart_filters.ChartAreas["ChartArea1"].AxisX;
                yaxis.Interval = 10;
                yaxis.Minimum = -30;
                yaxis.Maximum = 30;
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

        #endregion

        #region other classes

        public class XY
        {
            public double X { get; set; }
            public double Y { get; set; }

            public XY(double xx, double yy)
            {
                X = xx;
                Y = yy;
            }
        }

        delegate void SetTextCallback(String text);
        delegate void ButtonAdjustCallback();
        delegate void DeferredInvokeDelegate(object sender);

        #endregion
    }
}
