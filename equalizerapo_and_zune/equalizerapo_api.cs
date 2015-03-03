using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace equalizerapo_and_zune
{
    public class equalizerapo_api
    {
        #region fields
        
        #endregion

        #region properties

        public static equalizerapo_api Instance { get; private set; }
        public File CurrentFile { get; private set; }

        #endregion

        #region public methods

        public equalizerapo_api()
        {
            CurrentFile = null;
        }

        public SortedList<double, Filter> GetFilters()
        {
            return CurrentFile.ReadFilters();
        }

        public void UpdateTrack(Track track)
        {
            if (CurrentFile == null ||
                CurrentFile.TrackRef != track)
            {
                CurrentFile = new File(track);
            }
        }

        #endregion

        #region private methods

        #endregion
    }
}
