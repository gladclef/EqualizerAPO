﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace equalizerapo_and_zune
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // check that the Equalizer APO program is installed
            File.GetEqualizerAPOPath();

            // reset the filter upon exiting the program
            Application.ApplicationExit += ApplicationEnd;

            // start the program
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new form_main());
        }

        private static void ApplicationEnd(object sender, EventArgs e)
        {
            equalizerapo_api.UnsetEqualizer();
        }
    }
}
