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

        Connection conAPI;

        #endregion

        #region properties

        public DeferredInvokeDelegate ConnectedSocket { get; set; }
        public DeferredInvokeDelegate DisconnectedSocket { get; set; }

        #endregion

        public Messenger()
        {
            // create a listening connection
            conAPI = Connection.GetInstance();
            conAPI.ConnectedEvent += new EventHandler(DeferredConnectedSocket);
            conAPI.DisconnectedEvent += new EventHandler(DeferredDisconnectedSocket);
            conAPI.MessageRecievedEvent += new EventHandler(MessageReceived);
            conAPI.ListenForIncomingConnections();
        }

        public void MessageReceived(object sender, EventArgs e) {
            Connection.MessageReceivedEventArgs cea =
                (Connection.MessageReceivedEventArgs)e;
            System.Diagnostics.Debugger.Log(1, "", "<<" + cea.message + "\n");
        }

        private void DeferredConnectedSocket(object sender, EventArgs e)
        {
            SocketClient.ConnectedEventArgs cea =
                (SocketClient.ConnectedEventArgs)e;
            conAPI.Connect(cea.newSocket);
            conAPI.StartListening();
            if (ConnectedSocket != null)
            {
                Microsoft.Iris.Application.DeferredInvoke(
                    new Microsoft.Iris.DeferredInvokeHandler(ConnectedSocket),
                    Microsoft.Iris.DeferredInvokePriority.Normal);
            }
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
