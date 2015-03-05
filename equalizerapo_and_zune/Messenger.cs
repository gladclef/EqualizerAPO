using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace equalizerapo_and_zune
{
    class Messenger
    {
        #region fields

        private form_main form;

        #endregion

        #region properties

        public DeferredInvokeDelegate DisconnectedSocket {get; set; }

        #endregion

        public Messenger(form_main form)
        {
            this.form = form;
        }

        public void MessageReceived(object sender, EventArgs e) {
            Connection.MessageReceivedEventArgs cea =
                (Connection.MessageReceivedEventArgs)e;
            System.Diagnostics.Debugger.Log(1, "", "<<" + cea.message + "\n");
        }

        public void DeferredDisconnectedSocket(object sender, EventArgs e)
        {
            if (DisconnectedSocket != null)
            {
                Microsoft.Iris.Application.DeferredInvoke(
                    new Microsoft.Iris.DeferredInvokeHandler(DisconnectedSocket),
                    Microsoft.Iris.DeferredInvokePriority.Normal);
            }
        }

        #region delegates

        public delegate void DeferredInvokeDelegate(object sender);

        #endregion
    }
}
