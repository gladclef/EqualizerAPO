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
        #endregion

        #region properties

        public double frequency { get; private set; }
        public double gain { get; private set; }
        public double Q { get; private set; }

        #endregion

        #region public methods

        public Filter(double freq, double gain, double Q)
        {
            this.frequency = freq;
            this.gain = gain;
            this.Q = Q;
        }

        public String GetFiletypeString()
        {
            // example line:
            //Filter  1: ON  PK       Fc    50,0 Hz  Gain -10,0 dB  Q  2,50
            return "Fc " + FormatNumber(frequency).PadLeft(7) + " Hz  " +
                "Gain " + FormatNumber(gain).PadLeft(5) + " db  " +
                "Q " + FormatNumber(Q).PadLeft(5);
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
