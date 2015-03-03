using System;
using System.Collections.Generic;
using System.IO;
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
        public static double GainMax { get; private set; }

        #endregion

        #region public methods

        public equalizerapo_api()
        {
            CurrentFile = null;
            GainMax = 30;
        }

        /// <summary>
        /// Get a singular filter for the equalizer.
        /// </summary>
        /// <param name="filterIndex">The index of the filter. Should be 0 <= value < count</param>
        /// <returns>The filter at the given index or null</returns>
        public Filter GetFilter(int filterIndex)
        {
            if (CurrentFile == null ||
                filterIndex < 0 ||
                filterIndex > CurrentFile.ReadFilters().Count - 1)
            {
                return null;
            }
            return CurrentFile.ReadFilters().ElementAt(filterIndex).Value;
        }

        public SortedList<double, Filter> GetFilters()
        {
            if (CurrentFile == null)
            {
                return new SortedList<double, Filter>();
            }
            return CurrentFile.ReadFilters();
        }

        public void UpdateTrack(Track track)
        {
            if (CurrentFile == null ||
                CurrentFile.TrackRef != track &&
                track != null)
            {
                CurrentFile = new File(track);
                PointConfig();
            }
        }

        #endregion

        #region private methods

        private void PointConfig()
        {
            if (CurrentFile == null)
            {
                return;
            }

            String configPath = File.GetEqualizerAPOPath() + "config\\config.txt";
            if (!System.IO.File.Exists(configPath) ||
                (System.IO.File.GetAttributes(configPath) & FileAttributes.Directory) == FileAttributes.Directory)
            {
                throw new FileNotFoundException(
                    String.Format("File {0} not found or is directory.", configPath),
                    configPath);
            }

            String equalizerFilename = CurrentFile.GetEqualizerFilename();
            System.IO.File.WriteAllLines(
                configPath,
                new string[] { "Include: " + equalizerFilename });
        }

        #endregion
    }
}
