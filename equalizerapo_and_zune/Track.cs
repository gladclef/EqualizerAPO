using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        public String Artist { get; private set; }

        #endregion

        #region public methods

        public Track()
        {
            Artist = "No Track";
            Title = "NA";
        }

        public Track(PlaybackTrack track)
        {
            TrackRef = track;
            Artist = "Unknown Artist";
            Title = TrackRef.Title;
            if (TrackRef is LibraryPlaybackTrack)
            {
                LibraryPlaybackTrack libraryPlaybackTrack = (LibraryPlaybackTrack)TrackRef;
                if (libraryPlaybackTrack.AlbumLibraryId > 0)
                {
                    MicrosoftZuneLibrary.AlbumMetadata album =
                        FindAlbumInfoHelper.GetAlbumMetadata(libraryPlaybackTrack.AlbumLibraryId);
                    Artist = album.AlbumArtist;
                }
            }
        }

        public String GetFullName()
        {
            Regex rgx = new Regex("[^a-zA-Z0-9 ]", RegexOptions.Compiled);
            String retval = (rgx.Replace(Artist, "") + "___" + rgx.Replace(Title, "")).Replace(
                " ", "_");
            return retval;
        }

        #endregion
    }
}
