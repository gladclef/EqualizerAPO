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

        private double gain;
        private double frequency;

        #endregion

        #region properties

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

        public double Q { get; private set; }

        #endregion

        #region event handlers

        public EventHandler FilterChanged;

        #endregion

        #region public methods

        public Filter(double freq, double gain, double Q)
        {
            this.Frequency = freq;
            this.Gain = gain;
            this.Q = Q;
        }

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
