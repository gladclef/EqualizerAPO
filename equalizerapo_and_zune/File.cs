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

        private static String FullPathFound = null;

        #endregion

        #region properties

        public Track TrackRef { get; private set; }
        public String FullPath { get; private set; }
        private SortedList<double, Filter> CurrentFilters { get; set; }

        #endregion

        #region event handlers

        public EventHandler FileSaved;

        #endregion

        #region public methods

        public File()
        {
            TrackRef = null;
            FullPath = "";
            CurrentFilters = null;
        }

        public File(Track track)
        {
            TrackRef = track;
            FullPath = GetEqualizerAPOPath() + "config\\" + GetEqualizerFilename();
            ReadFilters();
        }

        public String GetEqualizerFilename() {
            return "e2z_" + TrackRef.GetFullName() + ".txt";
        }

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
            Regex getNums = new Regex("[0-9]+,[0-9]+", RegexOptions.Compiled);
            System.Globalization.NumberFormatInfo provider = new System.Globalization.NumberFormatInfo();
            provider.NumberDecimalSeparator = ",";
            provider.NumberGroupSeparator = "";
            foreach (string line in System.IO.File.ReadLines(FullPath)) {
                MatchCollection nums = getNums.Matches(line);
                // only match lines that have exactly three decimal-style numbers on them
                if (nums.Count == 3)
                {
                    double freq = Convert.ToDouble(nums[0].Value, provider);
                    double gain = Convert.ToDouble(nums[1].Value, provider);
                    double Q = Convert.ToDouble(nums[1].Value, provider);
                    Filter filter = new Filter(freq, gain, Q);
                    filter.FilterChanged += FilterChanged;
                    filters.Add(freq, filter);
                }
            }
            CurrentFilters = filters;

            return filters;
        }

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

        #endregion

        #region private methods

        /// <summary>
        /// Generates a set a filters spread out evenly across the hearing spectrum,
        /// with a gain of 0 and a Q to match the spacing between each filter.
        /// </summary>
        /// <param name="numIntervals">Number of filters</param>
        /// <returns>An evenly spaced generated set of filters</returns>
        private SortedList<double, Filter> GenerateFilters(int numIntervals)
        {
            double lowN = Math.Log(20, 2);
            double highN = Math.Log(20000, 2);
            double totalOctaves = highN - lowN;
            double octaveRange = totalOctaves / numIntervals;
            double Q = octaveRange * 1.2;
            
            SortedList<double, Filter> filters = new SortedList<double, Filter>();
            for (int i = 1; i <= numIntervals; i++)
            {
                double pow = lowN + (highN - lowN) / (numIntervals + 1) * i;
                double freq = Math.Pow(2, pow);
                double gain = 0;
                Filter filter = new Filter(freq, gain, Q);
                filters.Add(freq, filter);
                filter.FilterChanged += FilterChanged;
            }

            return filters;
        }

        private void FilterChanged(object sender, EventArgs e)
        {
            SaveFile();
        }

        private void SaveFile()
        {
            LinkedList<string> lines = new LinkedList<string>();

            // write out each filter
            // example filter:
            //Filter  1: ON  PK       Fc    50,0 Hz  Gain -10,0 dB  Q  2,50
            System.Diagnostics.Debugger.Log(1, "", String.Format("Saving file {0} with filters:\n", FullPath));
            for (int i = 0; i < CurrentFilters.Count; i++)
            {
                Filter filter = CurrentFilters.ElementAt(i).Value;
                lines.AddLast(String.Format("Filter {0}: ON  PK       {1}",
                    i.ToString().PadLeft(2), filter.GetFiletypeString()));
                System.Diagnostics.Debugger.Log(1, "", lines.Last.Value + "\n");
            }

            System.IO.File.WriteAllLines(FullPath, lines.ToArray<string>());

            if (FileSaved != null)
            {
                FileSaved(this, EventArgs.Empty);
            }
        }

        #endregion
    }
}
