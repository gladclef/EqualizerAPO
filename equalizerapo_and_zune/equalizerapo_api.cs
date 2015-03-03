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

        private bool applyEqualizer = true;

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
                CurrentFile.FileSaved += new EventHandler(FileUpdated);
                PointConfig();
            }
        }

        public static void UnsetEqualizer()
        {
            equalizerapo_api eqAPI = new equalizerapo_api();
            eqAPI.PointConfig("none.txt");
        }

        public void ApplyEqualizer(bool apply)
        {
            if (apply)
            {
                PointConfig();
            }
            else
            {
                equalizerapo_api.UnsetEqualizer();
            }
            applyEqualizer = apply;
        }

        #endregion

        #region private methods

        private void PointConfig()
        {
            PointConfig(null);
        }

        private void PointConfig(String equalizerFilename)
        {
            // get the equalizer file name
            if (CurrentFile == null && equalizerFilename == null)
            {
                return;
            }
            if (!applyEqualizer)
            {
                return;
            }
            if (equalizerFilename == null)
            {
                equalizerFilename = CurrentFile.GetEqualizerFilename();
            }

            // check that the config file exists and is a file, not a directory
            String configPath = File.GetEqualizerAPOPath() + "config\\config.txt";
            if (!System.IO.File.Exists(configPath) ||
                (System.IO.File.GetAttributes(configPath) & FileAttributes.Directory) == FileAttributes.Directory)
            {
                throw new FileNotFoundException(
                    String.Format("File {0} not found or is directory.", configPath),
                    configPath);
            }

            // write the include to the config file
            File.WriteAllLines(configPath, new string[] { "Include: " + equalizerFilename });
        }

        private void FileUpdated(object sender, EventArgs e)
        {
            PointConfig();
        }

        #endregion
    }
}
