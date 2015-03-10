﻿using System;
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

        public const int PREAMP_MAX = 30;
        public const double GAIN_MAX = 15;

        #endregion

        #region fields

        private bool applyEqualizer = true;

        #endregion

        #region properties

        public static equalizerapo_api Instance { get; private set; }
        public File CurrentFile { get; private set; }

        #endregion

        #region event handlers

        public EventHandler EqualizerChanged;

        #endregion

        #region public methods

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
                if (EqualizerChanged != null)
                {
                    EqualizerChanged(this, EventArgs.Empty);
                }
            }
        }

        public static void UnsetEqualizer()
        {
            equalizerapo_api eqAPI = new equalizerapo_api();
            eqAPI.PointConfig("none.txt");
        }

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

        public void RemoveFilter()
        {
            if (CurrentFile == null)
            {
                return;
            }
            CurrentFile.RemoveFilter();
        }

        public void AddFilter()
        {
            if (CurrentFile == null)
            {
                return;
            }
            CurrentFile.AddFilter();
        }

        public void ChangePreamp(int preAmp)
        {
            if (CurrentFile == null)
            {
                return;
            }
            CurrentFile.PreAmp = preAmp;
        }

        public int GetPreAmp()
        {
            if (CurrentFile == null)
            {
                return 0;
            }
            return CurrentFile.PreAmp;
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
            if (EqualizerChanged != null)
            {
                EqualizerChanged(this, EventArgs.Empty);
            }
        }

        #endregion
    }
}
