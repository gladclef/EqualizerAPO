using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace equalizerapo_and_zune
{
    class Messenger
    {
        private form_main form;

        public Messenger(form_main form)
        {
            this.form = form;
        }

        public void MessageReceived(object sender, EventArgs e) {
            Connection.MessageReceivedEventArgs cea =
                (Connection.MessageReceivedEventArgs)e;
            System.Diagnostics.Debugger.Log(1, "", "<<" + cea.message + "\n");
        }
    }
}
