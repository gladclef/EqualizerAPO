using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace equalizerapo_and_zune
{
    /// <summary>
    /// Represents the file in use by equalizer apo for the given track.
    /// </summary>
    class File
    {
        #region properties

        public Track TrackRef { get; private set; }

        #endregion

        #region public methods

        public File(Track track)
        {
            TrackRef = track;
        }

        #endregion
    }
}
