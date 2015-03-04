﻿using System;
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
            Regex getNums = new Regex("-?[0-9]+,[0-9]+", RegexOptions.Compiled);
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

        public void RemoveFilter()
        {
            SortedList<double, Filter> filters = CurrentFilters;

            // adjust old filters
            for (int i = 0; i < filters.Count; i++)
            {
                Filter filter = filters.ElementAt(i).Value;
                Dictionary<string, double> filterParameters =
                    Filter.GenerateFilterParameters(filters.Count - 1, i);
                filter.Frequency = filterParameters["frequency"];
            }

            // remove last filter
            filters.RemoveAt(filters.Count - 1);

            // save the new set of filters
            SaveFile();
        }

        public void AddFilter()
        {
            Filter filter = null;
            Dictionary<string, double> filterParameters = null;
            SortedList<double, Filter> filters = CurrentFilters;

            // adjust old filters
            for (int i = 0; i < filters.Count; i++)
            {
                filter = filters.ElementAt(i).Value;
                filterParameters = Filter.GenerateFilterParameters(filters.Count + 1, i);
                filter.Frequency = filterParameters["frequency"];
            }

            // add new filter
            filterParameters = Filter.GenerateFilterParameters(filters.Count + 1, filters.Count);
            filter = new Filter(
                filterParameters["frequency"],
                filterParameters["gain"],
                filterParameters["Q"]);
            filters.Add(filter.Frequency, filter);

            // save the new set of filters
            SaveFile();
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

        public static void WriteAllLines(String path, string[] lines)
        {
            File.WriteAllLines(path, lines, 0);
        }

        public static void WriteAllLines(String path, string[] lines, int depth)
        {
            if (depth > 50)
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
                File.WriteAllLines(path, lines);
            }
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
