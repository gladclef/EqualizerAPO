using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZuneUI;

namespace equalizerapo_and_zune
{
    public class Track
    {
        #region fields
        #endregion

        #region properties

        public PlaybackTrack TrackRef { get; private set; }
        public String Title { get; private set; }

        #endregion

        #region public methods

        public Track()
        {
            Title = "No Song Currently Playing";
        }

        public Track(PlaybackTrack track)
        {
            TrackRef = track;
            Title = TrackRef.Title;
        }

        #endregion
    }
}
