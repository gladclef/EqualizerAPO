using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace equalizerapo_and_zune
{
    public class Filter
    {
        #region fields

        /// <summary>
        /// the gain on the filter
        /// </summary>
        private double gain;

        /// <summary>
        /// the center frequency of the filter
        /// </summary>
        private double frequency;

        /// <summary>
        /// The Q value for the filter.
        /// </summary>
        private double q;

        #endregion

        #region properties

        /// <summary>
        /// The public interface to the private frequency value.
        /// get: straight forward
        /// set: caps the value between 20 and 20,000 and makes
        ///     a call to the <see cref="FilterChanged"/> event
        /// </summary>
        public double Frequency {
            get { return frequency; }
            set
            {
                frequency =
                    Math.Max(
                        Math.Min(
                            value,
                            20000),
                        20);
                if (FilterChanged != null)
                {
                    FilterChanged(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// The public interface to the private frequency value.
        /// get: straight forward
        /// set: caps the value between -15 and 15
        ///     (from the <see cref="equalizerapo_api.GainMax"/>) and
        ///     makes a call to the <see cref="FilterChanged"/> event
        /// </summary>
        public double Gain {
            get { return gain; }
            set {
                gain = 
                    Math.Max(
                        Math.Min(
                            value,
                            equalizerapo_api.GainMax),
                        -equalizerapo_api.GainMax);
                if (FilterChanged != null)
                {
                    FilterChanged(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// The public interface to the private frequency value.
        /// get: straight forward
        /// set: caps the value between 0.5 and 10
        ///     (from the number of octaves) and
        ///     makes a call to the <see cref="FilterChanged"/> event
        /// </summary>
        public double Q
        {
            get { return q; }
            set
            {
                q = Math.Max(
                        Math.Min(
                            value,
                            14),
                        0.5);
                if (FilterChanged != null)
                {
                    FilterChanged(this, EventArgs.Empty);
                }
            }
        }

        #endregion

        #region event handlers

        /// <summary>
        /// An event handler that gets triggered every time the filter
        /// parameters are changed.
        /// </summary>
        public EventHandler FilterChanged;

        #endregion

        #region public methods

        /// <summary>
        /// Create a new filter.
        /// </summary>
        /// <param name="freq">The filter frequency.</param>
        /// <param name="gain">The filter gain.</param>
        /// <param name="Q">The filter Q.</param>
        public Filter(double freq, double gain, double Q)
        {
            this.frequency = freq;
            this.gain = gain;
            this.q = Q;
        }

        /// <summary>
        /// Uses <see cref="FormatNumber"/> to create a string that contains
        /// all of the filter parameters, in a format useful for the
        /// equalizerapo program.
        /// </summary>
        /// <returns>A string with the filter parameters for equalizerapo.</returns>
        public String GetFiletypeString()
        {
            // example line:
            //Filter  1: ON  PK       Fc    50,0 Hz  Gain -10,0 dB  Q  2,50
            return "Fc " + FormatNumber(Frequency).PadLeft(8) + " Hz  " +
                "Gain " + FormatNumber(Gain).PadLeft(6) + " dB  " +
                "Q " + FormatNumber(Q).PadLeft(6);
        }

        #endregion

        #region public static methods

        /// <summary>
        /// Generates the necessary parameters for a filter at the given index (0 based)
        /// when there are going to be a total of numIntervals filters generated.
        /// </summary>
        /// <param name="numIntervals">The total number of filters to be generated.</param>
        /// <param name="filterIndex">Index of filter to be generated (0 based).</param>
        /// <returns>The "frequency", "gain", and "Q" parameters that would
        ///          be used for a new filter.</returns>
        public static Dictionary<string,double> GenerateFilterParameters(int numIntervals, int filterIndex)
        {
            double lowN = Math.Log(20, 2);
            double highN = Math.Log(20000, 2);
            double totalOctaves = highN - lowN;
            double octaveRange = totalOctaves / numIntervals;
            double Q = octaveRange * 1.2;
            double pow = lowN + (highN - lowN) / (numIntervals + 1) * filterIndex;
            double freq = Math.Pow(2, pow);
            double gain = 0;

            Dictionary<string, double> retval = new Dictionary<string, double>();
            retval.Add("frequency", freq);
            retval.Add("gain", gain);
            retval.Add("Q", Q);
            return retval;
        }

        #endregion

        #region private methods

        /// <summary>
        /// Takes a double and formats it as [0-9]+,[0-9]{2}
        /// </summary>
        /// <param name="value">The double to be formated.</param>
        /// <returns>The formated string.</returns>
        private String FormatNumber(double value)
        {
            System.Globalization.CultureInfo us =
                System.Globalization.CultureInfo.CreateSpecificCulture("en-US");
            string[] numval = value.ToString("F", us).Split(new char[] { '.' });
            return numval[0] + "," + numval[1].Substring(0, 2);
        }

        #endregion
    }
}
