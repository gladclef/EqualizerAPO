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
                // get the list of frequencies
                SortedList<double, Filter> filters = eqAPI.GetFilters();
                List<double> frequencies = new List<double>();
                foreach (System.Collections.Generic.KeyValuePair<double, Filter> pair in filters)
                {
                    Filter filter = pair.Value;
                    System.Diagnostics.Debugger.Log(1, "", filter.GetFiletypeString() + "\n");
                    frequencies.Add(filter.frequency);
                }

                // update the equalizer graph
                chart_filters.DataSource = frequencies;
                System.Windows.Forms.DataVisualization.Charting.Axis yaxis =
                    chart_filters.ChartAreas["ChartArea1"].AxisY;
                yaxis.Interval = 10;
                yaxis.Minimum = -30;
                yaxis.Maximum = 30;
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

        #region delegate classes

        delegate void SetTextCallback(String text);
        delegate void ButtonAdjustCallback();
        delegate void DeferredInvokeDelegate(object sender);

        #endregion
    }
}
