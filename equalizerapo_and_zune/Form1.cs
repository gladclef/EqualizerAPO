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
    /// <summary>
    /// Handles the GUI logic of the appliction.
    /// 
    /// Also handles the application logic, which it really shouldn't and
    /// I will move at some point to another object. TODO
    /// </summary>
    public partial class form_main : Form
    {
        #region constants

        private const string CONNECTION_ESTABLISHED = "Connected!";
        private const string LISTENING_FOR_CONNECTION = "Listening on: ";

        #endregion

        #region fields

        /// <summary>
        /// The object used to talk to the Zune player.
        /// </summary>
        public ZuneAPI zuneAPI;

        /// <summary>
        /// The object used to talk to Equalizer APO
        /// and keep the state of the equalizer.
        /// </summary>
        public equalizerapo_api eqAPI;

        /// <summary>
        /// Tracks the filter last hovered over by the mouse on top of
        /// the <see cref="chart_filters"/> object.
        /// Used so that mousedrag events can be properly handled.
        /// </summary>
        private int previousFilterIndex;

        /// <summary>
        /// Tracks the state of the mouse left button (pressed or no?)
        /// in relation to <see cref="chart_filters"/>.
        /// </summary>
        private bool mousePressed = false;

        /// <summary>
        /// Set of numeric controls for the filters on the equalizer.
        /// Auto-generated set whenever a filter is added/removed.
        /// </summary>
        private LinkedList<NumericUpDown> NumberInputs;

        /// <summary>
        /// The object used to send and receive messages from any clients (app extensions).
        /// </summary>
        private Messenger messenger;

        /// <summary>
        /// An optimization to prevent feedback during filter gain updates
        /// via the <see cref="chart_filters"/> control.
        /// </summary>
        private bool doHandleNumericValueChanged = true;

        /// <summary>
        /// Reference to the object used to create/parse messages
        /// to/from the app clients.
        /// </summary>
        private MessageParser messageParser;

        /// <summary>
        /// An optimization to prevent feedback to/from the client app
        /// when the client updates or this form updates.
        /// </summary>
        private Dictionary<string, bool> updateMessagesEnabled;

        /// <summary>
        /// An optimization to prevent feedback events when a track is changed.
        /// </summary>
        private Track cachedTrack;

        /// <summary>
        /// An optimization to prevent feedback events when the playback is changed. 
        /// </summary>
        private bool cachedPlayback;

        #endregion

        #region public/initizer methods

        /// <summary>
        /// Create a new instance of this object, including
        /// initializing all of it's constituent parts and fields.
        /// </summary>
        public form_main()
        {
            InitializeComponent();

            // initialize some other objects
            NumberInputs = new LinkedList<NumericUpDown>();

            // set the min/max volumes, because apparently that doesn't work from the form editor
            InitMinMax();

            // set all the enablers to false
            InitEnablers();

            // initialize zune and equalizer api instances
            InitEqAndZune();

            // get a message parser
            messageParser = new MessageParser(eqAPI, zuneAPI);

            // start a messenger to communicate with app
            InitMessenger();

            // tell the user that we're listening, and on what port
            UpdateListenerDescription(false);
        }

        /// <summary>
        /// Sets the minimum and maximum values on the volume slider,
        /// since that is apparently not something the form designer
        /// is any good for.
        /// </summary>
        private void InitMinMax()
        {
            this.trackbar_volume.Maximum = equalizerapo_api.PREAMP_MAX;
            this.trackbar_volume.Minimum = -equalizerapo_api.PREAMP_MAX;
            this.numeric_volume.Maximum = new decimal(new int[] {
                equalizerapo_api.PREAMP_MAX,
                0,
                0,
                0});
            this.numeric_volume.Minimum = new decimal(new int[] {
                -equalizerapo_api.PREAMP_MAX,
                0,
                0,
                -2147483648});
        }

        /// <summary>
        /// Initialize the equalizer and Zune objects,
        /// opening/connecting to the Zune player and
        /// connecting the equalizer.
        /// </summary>
        private void InitEqAndZune()
        {
            // create objects
            zuneAPI = new ZuneAPI();
            eqAPI = new equalizerapo_api();

            // zune events
            zuneAPI.TrackChanged += new EventHandler(TrackChanged);
            zuneAPI.PlaybackChanged += new EventHandler(PlaybackChanged);

            // equalizer events
            eqAPI.EqualizerChanged += new EventHandler(EqualizerChanged);

            // init objects
            zuneAPI.Init();
        }

        /// <summary>
        /// Initialize the enables such that messages received are not accepted.
        /// They are enabled once the track has changed and playback has changed due
        /// to events from the Zune player.
        /// </summary>
        private void InitEnablers()
        {
            updateMessagesEnabled = new Dictionary<string, bool>();
            updateMessagesEnabled.Add("track", false);
            updateMessagesEnabled.Add("playback", false);
        }

        /// <summary>
        /// Create and initialize the Messenger object to enable talking to
        /// the client app.
        /// </summary>
        private void InitMessenger()
        {
            messenger = new Messenger(
                new Messenger.DeferredInvokeDelegate(ConnectedSocket),
                new Messenger.DeferredInvokeDelegate(DisconnectedSocket),
                new Messenger.DeferredInvokeMessageDelegate(MessageReceieved));
        }

        /// <summary>
        /// When this object is destructed, also close the player and messenger.
        /// </summary>
        ~form_main()
        {
            zuneAPI.Close();
            messenger.Close();
        }

        /// <summary>
        /// Change the value of the <see cref="label_artistname_trackname"/>.
        /// </summary>
        /// <param name="newTitle">The new title to use. Likely to be
        ///     gathered from <see cref="Track.GetFullName"/>.</param>
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

        /// <summary>
        /// Handle a track changed event.
        /// Triggered by the <see cref="ZuneAPI.TrackChanged"/> event handler.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">N/A</param>
        /// <seealso cref="DeferredUpdateAll"/>
        private void TrackChanged(object sender, EventArgs e)
        {
            if (cachedTrack != zuneAPI.CurrentTrack)
            {
                cachedTrack = zuneAPI.CurrentTrack;
                updateMessagesEnabled["track"] = true;
            }
            DeferredUpdateAll(sender);
        }

        /// <summary>
        /// Handle a playback changed event.
        /// Triggered by the <see cref="ZuneAPI.PlaybackChanged"/> event handler.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">N/A</param>
        /// <seealso cref="DeferredUpdateAll"/>
        private void PlaybackChanged(object sender, EventArgs e)
        {
            if (cachedPlayback != zuneAPI.IsPlaying())
            {
                cachedPlayback = zuneAPI.IsPlaying();
                updateMessagesEnabled["playback"] = true;
            }
            DeferredUpdateAll(sender);
            if (updateMessagesEnabled["playback"])
            {
                updateMessagesEnabled["playback"] = false;
                messenger.Send(messageParser.CreateMessage(
                    zuneAPI.IsPlaying()
                        ? MessageParser.MESSAGE_TYPE.PLAY
                        : MessageParser.MESSAGE_TYPE.PAUSE),
                    true);
            }
        }

        /// <summary>
        /// Handle a equalizer changed event.
        /// Triggered by the <see cref="equalizerapo_api.EqualizerChanged"/> event handler.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">N/A</param>
        /// <seealso cref="DeferredUpdateAll"/>
        private void EqualizerChanged(object sender, EventArgs e)
        {
            DeferredUpdateAll(null);
        }

        /// <summary>
        /// Calls <see cref="UpdateAll"/> through a deferred invoke handler.
        /// </summary>
        /// <param name="sender">N/A</param>
        private void DeferredUpdateAll(object sender)
        {
            Microsoft.Iris.Application.DeferredInvoke(
                new Microsoft.Iris.DeferredInvokeHandler(UpdateAll),
                Microsoft.Iris.DeferredInvokePriority.Normal);
        }

        /// <summary>
        /// Updates the state of the form to reflect the current state of the
        /// track/playback/filters/messenger.
        /// </summary>
        /// <param name="sender"></param>
        /// <seealso cref="DeferredUpdateAll"/>
        /// <seealso cref="UpdateTrackTitle"/>
        /// <seealso cref="UpdateEqualizer"/>
        /// <seealso cref="UpdatePlaybackButton"/>
        /// <seealso cref="UpdatePreamp"/>
        private void UpdateAll(object sender)
        {
            if (zuneAPI.CurrentTrack == null)
            {
                return;
            }

            // change the track
            eqAPI.UpdateTrack(zuneAPI.CurrentTrack);
            if (updateMessagesEnabled["track"])
            {
                updateMessagesEnabled["track"] = false;
                messenger.Send(messageParser.CreateMessage(
                    MessageParser.MESSAGE_TYPE.TRACK_CHANGED),
                    true);
            }
            
            // update UI
            UpdateTrackTitle(zuneAPI.CurrentTrack.GetFullName());
            UpdateEqualizer(sender);
            UpdatePlaybackButton();

            UpdatePreamp();
        }

        /// <summary>
        /// Updates the <see cref="chart_filters"/> and <see cref="NumberInputs"/>
        /// to reflect the current state of the filters.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <seealso cref="UpdateAll"/>
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

                // force the chart to update to the new values
                chart_filters.DataBind();
            }
        }

        /// <summary>
        /// Update the <see cref="chart_filters"/> to reflect the gains of the filters.
        /// </summary>
        /// <seealso cref="UpdateAll"/>
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

        /// <summary>
        /// Update the <see cref="NumberInputs"/> to reflect the gains of the filters.
        /// </summary>
        /// <seealso cref="UpdateAll"/>
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

        /// <summary>
        /// Update the playback button to be either a play or pause button,
        /// according the the current <see cref="ZuneAPI.IsPlaying"/> state.
        /// </summary>
        private void UpdatePlaybackButton()
        {
            if (button_play_pause.InvokeRequired)
            {
                // thread-safe callback
                DeferredEmptyCallback d = new DeferredEmptyCallback(UpdatePlaybackButton);
                this.Invoke(d, new object[] { });
            }
            else
            {
                if (zuneAPI.IsPlaying())
                {
                    button_play_pause.Text = "Pause";
                }
                else
                {
                    button_play_pause.Text = "Play";
                }
            }
        }

        /// <summary>
        /// Handle a value changed event from one of the <see cref="NumberInputs"/>.
        /// Triggered by a ValueChanged event on one of the NumericUpDowns.
        /// Goes through each numeric input and changes it based on the current value of its related filter.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">N/A</param>
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
                    messenger.Send(messageParser.CreateMessage(
                        MessageParser.MESSAGE_TYPE.FILTERS_GAIN),
                        true);
                }
            }
        }

        /// <summary>
        /// Handle a click event on the <see cref="button_next"/>.
        /// Causes the Zune player to move to the next track and the
        /// "track" enabler (<see cref="updateMessageEnabled"/>) to be set to true.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">N/A</param>
        private void button_next_Click(object sender, EventArgs e)
        {
            updateMessagesEnabled["track"] = true;
            zuneAPI.ToNextTrack();
        }

        /// <summary>
        /// Handle a click event on the <see cref="button_previous"/>.
        /// Causes the Zune player to move to the previous track and the
        /// "track" enabler (<see cref="updateMessageEnabled"/>) to be set to true.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">N/A</param>
        private void button_previous_Click(object sender, EventArgs e)
        {
            updateMessagesEnabled["track"] = true;
            zuneAPI.ToPreviousTrack();
        }

        /// <summary>
        /// Handles a mouse move event on the <see cref="chart_filters"/>.
        /// If the mouse is being pressed, then update the gain of the filter
        /// at the <see cref="previousFilterIndex"/> that was found when the
        /// mouse was first pressed on the chart.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">mouse event</param>
        private void chart_filters_MouseMove(object sender, MouseEventArgs e)
        {
            if (!mousePressed)
            {
                return;
            }

            chart_filters_Click(sender, e, previousFilterIndex);
        }

        /// <summary>
        /// Register that the mouse is no longer being pressed on the
        /// <see cref="chart_filters."/>.
        /// Also FORCE a filter gains update with the client app.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">N/A</param>
        private void chart_filters_MouseUp(object sender, MouseEventArgs e)
        {
            mousePressed = false;
            messenger.Send(messageParser.CreateMessage(
                MessageParser.MESSAGE_TYPE.FILTERS_GAIN),
                true);
        }

        /// <summary>
        /// Calls <see cref="chart_filters_Click"/> with a filterIndex value of -1.
        /// </summary>
        /// <param name="sender">sender object</param>
        /// <param name="e">mouse event</param>
        private void chart_filters_Click(object sender, MouseEventArgs e)
        {
            chart_filters_Click(sender, e, -1);
        }

        /// <summary>
        /// Adjusts the gain based on which filter is clicked.
        /// Also register that the mouse is currently being pressed on the <see cref="chart_filters"/>.
        /// </summary>
        /// <param name="sender">sender object</param>
        /// <param name="e">mouse event</param>
        /// <param name="filterIndex">if -1, then compute filter index</param>
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
                messenger.Send(messageParser.CreateMessage(
                    MessageParser.MESSAGE_TYPE.FILTERS_GAIN),
                    false);
            }

            // register that the mouse is currently pressed on the chart
            mousePressed = true;
        }

        /// <summary>
        /// Handle checking or unchecking of the <see cref="checkbox_apply_equalizer"/>.
        /// Applies or doesn't apply the equalizer as given by the state of the checkbox.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">N/A</param>
        /// <seealso cref="equalizerapo_api.ApplyEqualizer"/>
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
                messenger.Send(messageParser.CreateMessage(
                    MessageParser.MESSAGE_TYPE.FILTER_APPLY),
                    true);
            }
        }

        /// <summary>
        /// Handle the zero'ing out of the filter gains by clicking on the <see cref="link_zero_equalizer"/>.
        /// Zeros out the filter gains.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">N/A</param>
        /// <seealso cref="equalizerapo_api.ZeroOutEqualizer"/>
        private void link_zero_equalizer_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            eqAPI.ZeroOutEqualizer();
            messenger.Send(messageParser.CreateMessage(
                MessageParser.MESSAGE_TYPE.FILTERS_GAIN),
                true);
        }

        /// <summary>
        /// Handles a click on <see cref="button_remove_filter"/>.
        /// Removes a filter.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">N/A</param>
        /// <seealso cref="equalizerapo_api.RemoveFilter"/>
        private void button_remove_filter_Click(object sender, EventArgs e)
        {
            eqAPI.RemoveFilter();
            messenger.Send(messageParser.CreateMessage(
                MessageParser.MESSAGE_TYPE.FILTER_REMOVED),
                true);
        }
        
        /// <summary>
        /// Handles a click on <see cref="button_add_filter"/>.
        /// Removes a filter.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">N/A</param>
        /// <seealso cref="equalizerapo_api.AddFilter"/>
        private void button_add_filter_Click(object sender, EventArgs e)
        {
            eqAPI.AddFilter();
            messenger.Send(messageParser.CreateMessage(
                MessageParser.MESSAGE_TYPE.FILTER_ADDED),
                true);
        }
        
        /// <summary>
        /// Update the value of the volume slider and numeric.
        /// </summary>
        /// <seealso cref="UpdateAll"/>
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
                trackbar_volume.Value = eqAPI.PreAmp;
                numeric_volume.Value = eqAPI.PreAmp;
            }
        }
        
        /// <summary>
        /// Handles a change the <see cref="trackbar_volume"/> value.
        /// Changes the equalizer preAmp (aka volume).
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">N/A</param>
        /// <seealso cref="equalizerapo_api.PreAmp"/>
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
                eqAPI.PreAmp = trackbar_volume.Value;
                messenger.Send(
                    messageParser.CreateMessage(MessageParser.MESSAGE_TYPE.VOLUME_CHANGED),
                    false);
            }
        }

        /// <summary>
        /// Handles a change the <see cref="numeric_volume"/> value.
        /// Changes the equalizer preAmp (aka volume).
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">N/A</param>
        /// <seealso cref="equalizerapo_api.PreAmp"/>
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
                eqAPI.PreAmp = trackbar_volume.Value;
                messenger.Send(
                    messageParser.CreateMessage(MessageParser.MESSAGE_TYPE.VOLUME_CHANGED),
                    true);
            }
        }

        /// <summary>
        /// Handles a change to the <see cref="combobox_listening_port"/> selected value.
        /// Changes the IPv4 address that the client app connects to.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">N/A</param>
        /// <seealso cref="Messenger.ChangeListeningAddress"/>
        private void combobox_listening_port_SelectedValueChanged(object sender, System.EventArgs e)
        {
            if (combobox_listening_port.InvokeRequired)
            {
                // thread-safe callback
                EventInvokeDelegate d = 
                    new EventInvokeDelegate(combobox_listening_port_SelectedValueChanged);
                this.Invoke(d, new object[] { sender, e });
            }
            else
            {
                if (messenger == null)
                {
                    return;
                }

                messenger.ChangeListeningAddress(
                    combobox_listening_port.SelectedValue.ToString());
                UpdateListenerDescription();
            }
        }

        /// <summary>
        /// Handles a click on <see cref="button_play_pause"/>.
        /// Changes the playback state by communicating with the Zune player.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">N/A</param>
        /// <seealso cref="ZuneAPI.PauseTrack"/>
        /// <seealso cref="ZuneAPI.PlayTrack"/>
        private void button_play_pause_Click(object sender, EventArgs e)
        {
            updateMessagesEnabled["playback"] = true;
            if (zuneAPI.IsPlaying())
            {
                zuneAPI.PauseTrack();
            }
            else
            {
                zuneAPI.PlayTrack();
            }
        }

        #endregion

        #region connection methods

        /// <summary>
        /// Calls <see cref="UpdateListenerDescription"/> with the state of the
        /// connection (established or no?).
        /// </summary>
        private void UpdateListenerDescription()
        {
            bool connected = 
                textblock_listening_port.Text == form_main.CONNECTION_ESTABLISHED;
            UpdateListenerDescription(connected);
        }

        /// <summary>
        /// Updates the <see cref="textblock_listening_port"/> and <see cref="combobox_listening_report"/>.
        /// Set the state of these two object to reflect the connection state of the <see cref="Messenger"/>.
        /// </summary>
        /// <param name="connected">True if the client app is connected.</param>
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
                    string[] listeningAddresses = messenger.GetPossibleIPAddresses();
                    textblock_listening_port.Text = form_main.LISTENING_FOR_CONNECTION;
                    combobox_listening_port.DataSource = listeningAddresses;
                    combobox_listening_port.SelectedIndex = 0;
                    combobox_listening_port.Show();
                }
                else
                {
                    textblock_listening_port.Text = form_main.CONNECTION_ESTABLISHED;
                    combobox_listening_port.Hide();
                }
            }
        }

        /// <summary>
        /// Triggered as the <see cref="Messenger.ConnectedSocketCall"/> delegate
        /// when the client app connects.
        /// Updates the <see cref="textblock_listening_port"/> and
        /// sends the current track to the client.
        /// </summary>
        /// <param name="sender">N/A</param>
        private void ConnectedSocket(object sender)
        {
            UpdateListenerDescription(true);
            System.Threading.Thread.Sleep(2000);
            messenger.Send(messageParser.CreateMessage(
                MessageParser.MESSAGE_TYPE.TRACK_CHANGED),
                true);
        }

        /// <summary>
        /// Triggered as the <see cref="Messenger.MessageReceivedCall"/> delegate
        /// when a message is received from the client.
        /// Parses the message with the <see cref="MessageParser"/>.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="args">Contains the message</param>
        private void MessageReceieved(object sender, Messenger.MessageEventArgs args)
        {
            messageParser.ParseMessage(args.message);
        }

        /// <summary>
        /// Triggered as the <see cref="Messenger.DisconnectedSocketCall"/> delegate
        /// when the client app disconnects.
        /// Updates the <see cref="textblock_listening_port"/>.
        /// </summary>
        /// <param name="sender">N/A</param>
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
