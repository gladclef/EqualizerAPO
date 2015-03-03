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

        public ZuneAPI API;

        #endregion

        #region public methods

        public form_main()
        {
            InitializeComponent();

            API = new ZuneAPI();
            API.TrackChanged += TrackChanged;
            API.Init();
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
            if (API.CurrentTrack == null)
            {
                return;
            }

            UpdateTrackTitle(API.CurrentTrack.GetFullName());
        }

        private void button_next_Click(object sender, EventArgs e)
        {
            API.ToNextTrack();
        }

        private void button_previous_Click(object sender, EventArgs e)
        {
            API.ToPreviousTrack();
        }

        #endregion

        #region delegate classes

        delegate void SetTextCallback(String text);
        delegate void ButtonAdjustCallback();

        #endregion
    }
}
