using System;
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
            if (!VerifyRequisiteProgramsInstalled())
            {
                return;
            }

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

        private static bool VerifyRequisiteProgramsInstalled()
        {
            try
            {
                File.GetEqualizerAPOPath();
            }
            catch (System.IO.FileNotFoundException e)
            {
                string message = "The application can't be opened. Requisite applications must first be installed.\n\n" +
                    "\"" + e.Message + "\"" +
                    "\n\nWould you like to go to this website now?";
                string caption = "Unable to start EqualizerAPO and Zune";
                DialogResult result =
                    MessageBox.Show(message, caption, MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                if (result == DialogResult.OK)
                {
                    System.Diagnostics.Process.Start(
                        "http://sourceforge.net/projects/equalizerapo/");
                }
                return false;
            }
            return true;
        }
    }
}
