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
        public Connection conAPI;
        private int previousFilterIndex;
        private bool mousePressed = false;
        private LinkedList<NumericUpDown> NumberInputs;
        private Messenger messenger;
        private bool doHandleNumericValueChanged = true;

        #endregion

        #region public methods

        public form_main()
        {
            InitializeComponent();

            // initialize zune and equalizer api instances
            NumberInputs = new LinkedList<NumericUpDown>();
            zuneAPI = new ZuneAPI();
            eqAPI = new equalizerapo_api();
            zuneAPI.TrackChanged += new EventHandler(TrackChanged);
            eqAPI.EqualizerChanged += new EventHandler(EqualizerChanged);
            zuneAPI.Init();

            // start a messenger to communicate with app
            messenger = new Messenger(this);
            messenger.DisconnectedSocket = new Messenger.DeferredInvokeDelegate(DisconnectedSocket);

            // create a listening connection
            conAPI = Connection.GetInstance();
            conAPI.ConnectedEvent += new EventHandler(DeferredConnectedSocket);
            conAPI.DisconnectedEvent += new EventHandler(messenger.DeferredDisconnectedSocket);
            conAPI.MessageRecievedEvent += new EventHandler(messenger.MessageReceived);
            conAPI.Listen();

            // tell the user that we're listening, and on what port
            UpdateListenerDescription(false);
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

        private void EqualizerChanged(object sender, EventArgs e)
        {
            DeferredUpdateAll(null);
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

            System.Diagnostics.Debugger.Log(1, "",
                String.Format("trackback min:{0}, max:{1}\n", trackbar_volume.Minimum, trackbar_volume.Maximum));
            System.Diagnostics.Debugger.Log(1, "",
                String.Format("numeric min:{0}, max:{1}\n", numeric_volume.Minimum, numeric_volume.Maximum));

            UpdatePreamp();
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
                // update series and change the graph visually
                UpdateEqualizerGraph();

                // update the filter text inputs
                UpdateEqualizerTextInputs();

                chart_filters.DataBind();
            }
        }

        private void UpdateEqualizerGraph()
        {
            //
            // update the series
            //

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

            //
            // change the equalizer graph visually
            //

            // make it a line graph instead of bar graph
            series.ChartType =
                System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;

            // set the range of the axis
            double gainMax = equalizerapo_api.GAIN_MAX;
            System.Windows.Forms.DataVisualization.Charting.Axis yaxis =
                chart_filters.ChartAreas["ChartArea1"].AxisY;
            System.Windows.Forms.DataVisualization.Charting.Axis xaxis =
                chart_filters.ChartAreas["ChartArea1"].AxisX;
            yaxis.Interval = gainMax / 3;
            yaxis.Minimum = -gainMax;
            yaxis.Maximum = gainMax;
            xaxis.Minimum = 1;
            xaxis.Maximum = filters.Count;

            // make grid lines lighter
            xaxis.MajorGrid.LineColor = Color.LightGray;
            yaxis.MajorGrid.LineColor = Color.LightGray;

            // make graph easier to see
            series.BorderWidth = 3;
        }

        private void UpdateEqualizerTextInputs()
        {
            SortedList<double, Filter> filters = eqAPI.GetFilters();

            // get the position and size of the graph
            int x1 = this.chart_filters.Location.X + 50;
            int y1 = this.chart_filters.Location.Y +
                this.chart_filters.Height + 5;
            int w1 = this.chart_filters.Width - 60;
            int boxHeight = 50;
            int boxSpacing = w1 / filters.Count;

            // number of filters has changed
            if (filters.Count != NumberInputs.Count)
            {
                // delete the old text inputs
                foreach (NumericUpDown numeric in NumberInputs)
                {
                    this.Controls.Remove(numeric);
                }
                NumberInputs.Clear();

                // add the new text inputs
                for (int i = 0; i < filters.Count; i++)
                {
                    NumericUpDown numeric = new NumericUpDown();
                    numeric.Location = new System.Drawing.Point(
                        x1 + i * boxSpacing, y1);
                    numeric.Width = boxSpacing - 5;
                    numeric.Height = boxHeight;
                    numeric.Minimum = -Convert.ToInt32(equalizerapo_api.GAIN_MAX);
                    numeric.Maximum = Convert.ToInt32(equalizerapo_api.GAIN_MAX);
                    NumberInputs.AddLast(numeric);
                    this.Controls.Add(numeric);
                    numeric.ValueChanged += new EventHandler(number_inputs_ValueChanged);
                }
            }

            // change filter values
            doHandleNumericValueChanged = false;
            for (int i = 0; i < filters.Count; i++)
            {
                Filter filter = filters.ElementAt(i).Value;
                NumericUpDown numeric = NumberInputs.ElementAt(i);
                numeric.Value = Convert.ToInt32(filter.Gain);
            }
            doHandleNumericValueChanged = true;
        }

        private void number_inputs_ValueChanged(object sender, EventArgs e)
        {
            if (!doHandleNumericValueChanged)
            {
                return;
            }

            SortedList<double, Filter> filters = eqAPI.GetFilters();
            for (int i = 0; i < NumberInputs.Count; i++)
            {
                NumericUpDown numeric = NumberInputs.ElementAt(i);
                Filter filter = filters.ElementAt(i).Value;
                if (numeric.Value != Convert.ToInt32(filter.Gain))
                {
                    eqAPI.GetFilter(i).Gain = Convert.ToDouble(numeric.Value);
                }
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
            mousePressed = false;
        }

        private void chart_filters_Click(object sender, MouseEventArgs e)
        {
            chart_filters_Click(sender, e, -1);
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
            double gainMax = equalizerapo_api.GAIN_MAX;
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

        private void link_zero_equalizer_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            eqAPI.ZeroOutEqualizer();
        }

        private void button_remove_filter_Click(object sender, EventArgs e)
        {
            eqAPI.RemoveFilter();
        }

        private void button_add_filter_Click(object sender, EventArgs e)
        {
            eqAPI.AddFilter();
        }

        private void UpdatePreamp()
        {
            if (trackbar_volume.InvokeRequired)
            {
                // thread-safe callback
                DeferredEmptyCallback d = new DeferredEmptyCallback(UpdatePreamp);
                this.Invoke(d, new object[] { });
            }
            else
            {
                trackbar_volume.Value = eqAPI.GetPreAmp();
                numeric_volume.Value = eqAPI.GetPreAmp();
            }
        }

        private void trackbar_volume_ValueChanged(object sender, EventArgs e)
        {
            if (trackbar_volume.InvokeRequired)
            {
                // thread-safe callback
                EventInvokeDelegate d = new EventInvokeDelegate(trackbar_volume_ValueChanged);
                this.Invoke(d, new object[] { sender, e });
            }
            else
            {
                eqAPI.ChangePreamp(trackbar_volume.Value);
            }
        }

        private void numeric_volume_ValueChanged(object sender, EventArgs e)
        {
            if (trackbar_volume.InvokeRequired)
            {
                // thread-safe callback
                EventInvokeDelegate d = new EventInvokeDelegate(trackbar_volume_ValueChanged);
                this.Invoke(d, new object[] { sender, e });
            }
            else
            {
                eqAPI.ChangePreamp(trackbar_volume.Value);
            }
        }

        #endregion

        #region connection methods

        private void UpdateListenerDescription(bool connected)
        {
            if (label_artistname_trackname.InvokeRequired)
            {
                // thread-safe callback
                SetBoolCallback d = new SetBoolCallback(UpdateListenerDescription);
                this.Invoke(d, new object[] { connected });
            }
            else
            {
                if (!connected)
                {
                    textblock_listening_port.Text = "Listening on: " + Connection.ListeningAddress;
                }
                else
                {
                    textblock_listening_port.Text = "Connected!";
                }
            }
        }

        private void ConnectedSocket(object sender)
        {
            UpdateListenerDescription(true);
            conAPI.StartListening();
        }

        private void DeferredConnectedSocket(object sender, EventArgs e)
        {
            SocketClient.ConnectedEventArgs cea =
                (SocketClient.ConnectedEventArgs)e;
            conAPI.Connect(cea.newSocket);
            Microsoft.Iris.Application.DeferredInvoke(
                new Microsoft.Iris.DeferredInvokeHandler(ConnectedSocket),
                Microsoft.Iris.DeferredInvokePriority.Normal);
        }

        private void DisconnectedSocket(object sender)
        {
            UpdateListenerDescription(false);
        }

        #endregion

        #region delegate classes

        delegate void SetTextCallback(String text);
        delegate void SetBoolCallback(bool isset);
        delegate void DeferredEmptyCallback();
        delegate void DeferredInvokeDelegate(object sender);
        delegate void EventInvokeDelegate(object sender, EventArgs e);

        #endregion
    }
}
