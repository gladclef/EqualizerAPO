using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace equalizerapo_and_zune
{
    /// <summary>
    /// Represents the file in use by equalizer apo for the given track.
    /// </summary>
    public class File
    {
        #region fields

        /// <summary>
        /// The filesystem path to the EqualizerAPO config.txt file.
        /// </summary>
        private static String FullPathFound = null;
        
        /// <summary>
        /// The preAmp (aka volume) value in the file.
        /// </summary>
        private int preAmp;

        #endregion

        #region properties

        /// <summary>
        /// The Track referenced to by this file.
        /// </summary>
        public Track TrackRef { get; private set; }
        
        /// <summary>
        /// The filesystem path to the track's config file.
        /// </summary>
        public String FullPath { get; private set; }

        /// <summary>
        /// Saves the file, which calls the <see cref="FileSaved"/> event handler.
        /// </summary>
        public int PreAmp
        {
            get { return preAmp; }
            set
            {
                int newVal;
                int preAmpMax = equalizerapo_api.PREAMP_MAX;
                newVal = Math.Max(
                    Math.Min(
                        value,
                        preAmpMax),
                    -preAmpMax);
                if (preAmp == newVal)
                {
                    return;
                }
                preAmp = newVal;
                SaveFile();
            }
        }

        /// <summary>
        /// Determines if the file gets saved, ever.
        /// Set to false when doing bulk edits on a file.
        /// </summary>
        public bool WriteThrough { get; set; }

        /// <summary>
        /// 
        /// </summary>
        private SortedList<double, Filter> CurrentFilters { get; set; }

        #endregion

        #region event handlers

        /// <summary>
        /// Triggers whenever the file is saved.
        /// </summary>
        public EventHandler FileSaved;

        #endregion

        #region public methods

        /// <summary>
        /// Create and initialize the File class object.
        /// Throw a FileNotFoundException if the EqualizerAPO config file can't be found.
        /// </summary>
        public File()
        {
            Init();
        }

        /// <summary>
        /// Create and initialize the File class object, including loading
        /// the file for the given track.
        /// Throw a FileNotFoundException if the EqualizerAPO config file can't be found.
        /// </summary>
        /// <param name="track">A track to load settings for.</param>
        public File(Track track)
        {
            Init();
            TrackRef = track;
            FullPath = GetEqualizerAPOPath() + "config\\" + GetEqualizerFilename();
            ReadFilters();
        }

        /// <summary>
        /// Formats the name of <see cref="Trackref"/>.
        /// </summary>
        /// <returns>The formatted name</returns>
        public String GetEqualizerFilename() {
            return "e2z_" + TrackRef.GetFullName() + ".txt";
        }

        /// <summary>
        /// Reads all of the filters from the file, or creates a new file if the file doesn't exist.
        /// </summary>
        /// <returns>The filters for the current <see cref="Trackref"/>.</returns>
        public SortedList<double, Filter> ReadFilters()
        {
            SortedList<double, Filter> filters = new SortedList<double, Filter>();
            if (TrackRef == null)
            {
                return filters;
            }
            if (CurrentFilters != null)
            {
                // cache the filters to reduce file read/writes
                return CurrentFilters;
            }
            if (!System.IO.File.Exists(FullPath)) {
                // default set of filters, spread evenly over the spectrum
                CurrentFilters = GenerateFilters(5);
                SaveFile();
                return CurrentFilters;
            }

            // parse the file to get the filters
            // example line to read:
            //Filter  1: ON  PK       Fc    50,0 Hz  Gain -10,0 dB  Q  2,50
            Regex getInt = new Regex("-?[0-9]+", RegexOptions.Compiled);
            Regex getDecimal = new Regex("-?[0-9]+,[0-9]+", RegexOptions.Compiled);
            System.Globalization.NumberFormatInfo provider = new System.Globalization.NumberFormatInfo();
            provider.NumberDecimalSeparator = ",";
            provider.NumberGroupSeparator = "";
            foreach (string line in System.IO.File.ReadLines(FullPath)) {
                // preamp: match to beggining of string
                //Preamp: -6 dB
                if (line.Substring(0, "Preamp: ".Length) == "Preamp: ")
                {
                    Match num = getInt.Match(line);
                    preAmp = Convert.ToInt32(num.Value);
                    continue;
                }

                // filters: only match lines that have exactly three decimal-style numbers on them
                MatchCollection nums = getDecimal.Matches(line);
                if (nums.Count == 3)
                {
                    double freq = Convert.ToDouble(nums[0].Value, provider);
                    double gain = Convert.ToDouble(nums[1].Value, provider);
                    double Q = Convert.ToDouble(nums[2].Value, provider);
                    Filter filter = new Filter(freq, gain, Q);
                    filter.FilterChanged += new EventHandler(FilterChanged);
                    filters.Add(freq, filter);
                }
            }
            CurrentFilters = filters;

            return filters;
        }

        /// <summary>
        /// Removes the last filter from the set of filters for this file.
        /// </summary>
        public void RemoveFilter()
        {
            WriteThrough = false;

            // adjust old filters
            for (int i = 0; i < CurrentFilters.Count; i++)
            {
                Filter filter = CurrentFilters.ElementAt(i).Value;
                Dictionary<string, double> filterParameters =
                    Filter.GenerateFilterParameters(CurrentFilters.Count - 1, i);
                filter.Frequency = filterParameters["frequency"];
                filter.Q = filterParameters["Q"];
            }

            // remove last filter
            CurrentFilters.RemoveAt(CurrentFilters.Count - 1);

            // save the new set of filters
            WriteThrough = true;
            SaveFile();
        }

        /// <summary>
        /// Adds a filter to the end of the filters set for this file.
        /// </summary>
        public void AddFilter()
        {
            Filter filter = null;
            Dictionary<string, double> filterParameters = null;
            WriteThrough = false;

            // adjust old filters
            for (int i = 0; i < CurrentFilters.Count; i++)
            {
                filter = CurrentFilters.ElementAt(i).Value;
                filterParameters = Filter.GenerateFilterParameters(CurrentFilters.Count + 1, i);
                filter.Frequency = filterParameters["frequency"];
                filter.Q = filterParameters["Q"];
            }

            // add new filter
            filterParameters = Filter.GenerateFilterParameters(CurrentFilters.Count + 1, CurrentFilters.Count);
            filter = new Filter(
                filterParameters["frequency"],
                filterParameters["gain"],
                filterParameters["Q"]);
            filter.FilterChanged += new EventHandler(FilterChanged);
            CurrentFilters.Add(filter.Frequency, filter);

            // save the new set of filters
            WriteThrough = true;
            SaveFile();
        }

        /// <summary>
        /// Forces a save, even if nothing has changed.
        /// </summary>
        public void ForceSave()
        {
            SaveFile(true);
        }

        #endregion

        #region public static methods

        /// <summary>
        /// Search the filesystem for the path of the "config.txt" file for Equalizer.APO.
        /// Throw a FileNotFoundException if the file can't be found.
        /// </summary>
        /// <returns>The directory path to the "config.txt" file.</returns>
        public static String GetEqualizerAPOPath() {
            if (FullPathFound != null)
            {
                return FullPathFound;
            }

            String pathAppend = "\\EqualizerAPO\\";
            String path1 = Environment.GetEnvironmentVariable("ProgramFiles") + pathAppend;
            String path2 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") + pathAppend;
            if (System.IO.File.Exists(path1 + "Configurator.exe"))
            {
                FullPathFound = path1;
                return path1;
            }
            if (System.IO.File.Exists(path2 + "Configurator.exe"))
            {
                FullPathFound = path2;
                return path2;
            }
            throw new FileNotFoundException("Mising necessary application at " +
                path1 + ". Go to http://sourceforge.net/projects/equalizerapo/ to install the application.",
                path1);
        }

        /// <summary>
        /// Write all lines to a file.
        /// Throws an IOException if writing fails after 1/2 sec.
        /// </summary>
        /// <param name="path">The path to write to.</param>
        /// <param name="lines">The lines to write.</param>
        /// <param name="depth">For internal use.</param>
        public static void WriteAllLines(String path, string[] lines, int depth = 0)
        {
            if (depth > 5)
            {
                // don't catch exception anymore
                System.IO.File.WriteAllLines(path, lines);
                return;
            }

            try
            {
                System.IO.File.WriteAllLines(path, lines);
            }
            catch (System.IO.IOException)
            {
                System.Threading.Thread.Sleep(100);
                // maybe I should do something here?
                File.WriteAllLines(path, lines, depth + 1);
            }
        }

        #endregion

        #region private methods

        /// <summary>
        /// Initialize, including setting up field values.
        /// </summary>
        private void Init() {
            TrackRef = null;
            FullPath = "";
            CurrentFilters = null;
            WriteThrough = true;
            preAmp = 0;
        }

        /// <summary>
        /// Generates a set a filters spread out evenly across the hearing spectrum,
        /// with a gain of 0 and a Q to match the spacing between each filter.
        /// </summary>
        /// <param name="numIntervals">Number of filters</param>
        /// <returns>An evenly spaced generated set of filters</returns>
        private SortedList<double, Filter> GenerateFilters(int numIntervals)
        {
            SortedList<double, Filter> filters = new SortedList<double, Filter>();
            for (int i = 1; i <= numIntervals; i++)
            {
                Dictionary<string,double> filterParameters = 
                    Filter.GenerateFilterParameters(numIntervals, i - 1);
                double freq = filterParameters["frequency"];
                double gain = filterParameters["gain"];
                double Q = filterParameters["Q"];
                Filter filter = new Filter(freq, gain, Q);
                filters.Add(freq, filter);
                filter.FilterChanged += FilterChanged;
            }

            return filters;
        }

        /// <summary>
        /// Triggered by <see cref="Filter.Frequency"/>, <see cref="Filter.Gain"/>, and/or <see cref="Filter.Q"/>.
        /// Save the file with the new values.
        /// Calls the <see cref="FileSaved"/> event handler.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">N/A</param>
        private void FilterChanged(object sender, EventArgs e)
        {
            SaveFile();
        }

        /// <summary>
        /// Save the file, including all of the latest filter values.
        /// Calls the <see cref="FileSaved"/> event handler.
        /// </summary>
        /// <param name="force">Forces a save, even if <see cref="WriteThrough"/> is false.</param>
        private void SaveFile(bool force = false)
        {
            if (!WriteThrough && !force)
            {
                return;
            }

            LinkedList<string> lines = new LinkedList<string>();

            // write the preamp value
            // example:
            //Preamp: -6 dB
            lines.AddLast(String.Format("Preamp: {0} dB", preAmp));

            // write out each filter
            // example filter:
            //Filter  1: ON  PK       Fc    50,0 Hz  Gain -10,0 dB  Q  2,50
            for (int i = 0; i < CurrentFilters.Count; i++)
            {
                Filter filter = CurrentFilters.ElementAt(i).Value;
                lines.AddLast(String.Format("Filter {0}: ON  PK       {1}",
                    i.ToString().PadLeft(2), filter.GetFiletypeString()));
            }

            File.WriteAllLines(FullPath, lines.ToArray<string>());

            if (FileSaved != null)
            {
                FileSaved(this, EventArgs.Empty);
            }
        }

        #endregion
    }
}
