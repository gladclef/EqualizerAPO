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
        #region constants

        /// <summary>
        /// Maximum number of decibels the preAmp (aka volume) can be adjusted by
        /// </summary>
        public const int PREAMP_MAX = 30;

        /// <summary>
        /// Maximum number of decibels the filters can be adjusted by
        /// </summary>
        public const double GAIN_MAX = 15;

        /// <summary>
        /// Minimum step size needed to register a change in filter gain/preAmp gain
        /// </summary>
        public const double GAIN_ACCURACY = 0.09;

        /// <summary>
        /// What to pass to <see cref="PointConfig"/> to ensure to filters are applied.
        /// </summary>
        private const string NO_FILTERS = "no filters";

        #endregion

        #region fields

        /// <summary>
        /// For internal use, an optimization value to
        /// limit the feedback events during a mass update to the filters.
        /// While true, the <see cref="EqualizerChanged"/> event doesn't fire.
        /// </summary>
        private bool applyEqualizer = true;

        #endregion

        #region properties

        /// <summary>
        /// Instance of this singleton class.
        /// </summary>
        private static equalizerapo_api Instance { get; set; }

        /// <summary>
        /// The file that contains the current equalization settings.
        /// </summary>
        public File CurrentFile { get; private set; }

        /// <summary>
        /// The preAmp (aka volume) value.
        /// Trimmed to be within -+<see cref="MAX_PREAMP"/>.
        /// Calls the <see cref="EqualizerChanged"/> event handler when changed.
        /// </summary>
        /// <param name="preAmp">The new preAmp value</param>
        public int PreAmp
        {
            get
            {
                if (CurrentFile == null)
                {
                    return 0;
                }
                return CurrentFile.PreAmp;
            }
            set
            {
                if (CurrentFile == null)
                {
                    return;
                }
                CurrentFile.PreAmp = value;
            }
        }

        #endregion

        #region event handlers

        /// <summary>
        /// Fires whenever the equalizer is changed.
        /// Also fires when the <see cref="CurrentFile"/> is modified.
        /// </summary>
        public EventHandler EqualizerChanged;

        #endregion

        #region public methods

        /// <summary>
        /// Create a new instance of this class.
        /// </summary>
        public equalizerapo_api()
        {
            CurrentFile = null;
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

        /// <summary>
        /// Get a list of the filters for the current file.
        /// </summary>
        /// <returns>The filters, or an empty list.</returns>
        public SortedList<double, Filter> GetFilters()
        {
            if (CurrentFile == null)
            {
                return new SortedList<double, Filter>();
            }
            return CurrentFile.ReadFilters();
        }

        /// <summary>
        /// Creates a new <see cref="CurrentFile"/> to point to the new track.
        /// Calls the <see cref="EqualizerChanged"/> event handler.
        /// </summary>
        /// <param name="track">Track to change to</param>
        public void UpdateTrack(Track track)
        {
            if (CurrentFile == null ||
                CurrentFile.TrackRef != track &&
                track != null)
            {
                CurrentFile = new File(track);
                CurrentFile.FileSaved += new EventHandler(FileUpdated);
                PointConfig();
                if (EqualizerChanged != null)
                {
                    EqualizerChanged(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Removes all pointers to the Equalizer APO settings,
        /// effectively making sure no settings are applied.
        /// </summary>
        public static void UnsetEqualizer()
        {
            equalizerapo_api eqAPI = Instance;
            if (eqAPI == null)
            {
                eqAPI = new equalizerapo_api();
            }
            eqAPI.PointConfig(NO_FILTERS);
        }

        /// <summary>
        /// Applies the equalizer by pointing the Equalizer APO config file
        /// to either the <see cref="CurrentFile"/> or not pointing it at all.
        /// Calls the <see cref="EqualizerChanged"/> event handler.
        /// </summary>
        /// <param name="apply">Apply the equalizer or turn it off?</param>
        public void ApplyEqualizer(bool apply)
        {
            applyEqualizer = apply;
            if (apply)
            {
                PointConfig();
            }
            else
            {
                equalizerapo_api.UnsetEqualizer();
            }
            if (EqualizerChanged != null)
            {
                EqualizerChanged(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Set all filters on the equalizer to zero gain.
        /// Calls the <see cref="EqualizerChanged"/> event handler.
        /// </summary>
        public void ZeroOutEqualizer()
        {
            if (CurrentFile == null)
            {
                return;
            }

            // turn of write-through until the last filter has been updated
            CurrentFile.WriteThrough = false;

            // set gain to zero for all filters
            SortedList<double, Filter> filters = CurrentFile.ReadFilters();
            for (int i = 0; i < filters.Count; i++) 
            {
                KeyValuePair<double,Filter> pair = filters.ElementAt(i);
                pair.Value.Gain = 0;
            }

            // enable write-through and save
            CurrentFile.WriteThrough = true;
            CurrentFile.ForceSave();
        }

        /// <summary>
        /// Remove the last filter in the list of filters.
        /// Calls the <see cref="EqualizerChanged"/> event handler.
        /// </summary>
        public void RemoveFilter()
        {
            if (CurrentFile == null)
            {
                return;
            }
            CurrentFile.RemoveFilter();
        }

        /// <summary>
        /// Add a filter to the list of filters at the end, with zero gain.
        /// Calls the <see cref="EqualizerChanged"/> event handler.
        /// </summary>
        public void AddFilter()
        {
            if (CurrentFile == null)
            {
                return;
            }
            CurrentFile.AddFilter();
        }

        /// <summary>
        /// Get the status of the equalizer.
        /// </summary>
        /// <returns>True if the equalizer is being applied.</returns>
        public bool IsEqualizerApplied()
        {
            return applyEqualizer;
        }

        /// <summary>
        /// Set new values for the gains for the filters on the <see cref="CurrentFile"/>.
        /// Adds or removes filters as necessary so that there are as many filters as there are string values.
        /// Calls the <see cref="EqualizerChanged"/> event handler.
        /// </summary>
        /// <param name="newFilterGains">The new gains, as string representations of decimal values</param>
        public void SetNewGainValues(string[] newFilterGains)
        {
            // remove unnecessary filters
            while (CurrentFile.ReadFilters().Count > newFilterGains.Length)
            {
                RemoveFilter();
            }

            // go through existing filters
            int filterIndex = -1;
            foreach (KeyValuePair<double, Filter> pair in CurrentFile.ReadFilters())
            {
                filterIndex++;
                Filter filter = pair.Value;
                double gain = Convert.ToDouble(newFilterGains[filterIndex]);

                // check that the gain will change
                if (Math.Abs(filter.Gain - gain) < GAIN_ACCURACY)
                {
                    continue;
                }

                // change the gain
                filter.Gain = gain;
            }

            // add necessary filters
            for (filterIndex = CurrentFile.ReadFilters().Count; filterIndex < newFilterGains.Length; filterIndex++)
            {
                double gain = Convert.ToDouble(newFilterGains[filterIndex]);
                AddFilter();
                CurrentFile.ReadFilters().Last().Value.Gain = gain;
            }
        }

        #endregion

        #region private methods

        /// <summary>
        /// Points the Equalizer APO configuration file to the specified named file.
        /// If null, the file it points to is the name found by <see cref="File.GetEqualizerFilename"/>().
        /// If <see cref="NO_FILTERS"/>, the configuration is set not to point to any file.
        /// </summary>
        /// <param name="equalizerFilename"></param>
        private void PointConfig(String equalizerFilename = null)
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
            String configPath = File.GetEqualizerAPOPath() + "config\\config.txt";

            // check for none.txt filenames
            if (equalizerFilename == NO_FILTERS)
            {
                File.WriteAllLines(configPath, new string[] { "" });                
            }

            // check that the config file exists and is a file, not a directory
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

        /// <summary>
        /// Event handler callback when the file gets updated.
        /// Triggered by the <see cref="File.FileSaved"/> event handler.
        /// Calls the <see cref="EqualizerChanged"/> event handler.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">N/A</param>
        private void FileUpdated(object sender, EventArgs e)
        {
            PointConfig();
            if (EqualizerChanged != null)
            {
                EqualizerChanged(this, EventArgs.Empty);
            }
        }

        #endregion
    }
}
