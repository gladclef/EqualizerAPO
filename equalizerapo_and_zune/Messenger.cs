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

        #region public methods

        public Messenger(DeferredInvokeDelegate con, DeferredInvokeDelegate dis)
        {
            // set initial connection values
            this.ConnectedSocket = con;
            this.DisconnectedSocket = dis;

            // create a listening connection
            InitConAPI();
        }

        public void MessageReceived(object sender, EventArgs e) {
            Connection.MessageReceivedEventArgs cea =
                (Connection.MessageReceivedEventArgs)e;
            System.Diagnostics.Debugger.Log(1, "", "<< " + cea.message + "\n");
        }

        public void DeferredDisconnectedSocket(object sender, EventArgs e)
        {
            conAPI.Close();
            InitConAPI();
            if (DisconnectedSocket != null)
            {
                Microsoft.Iris.Application.DeferredInvoke(
                    new Microsoft.Iris.DeferredInvokeHandler(DisconnectedSocket),
                    Microsoft.Iris.DeferredInvokePriority.Normal);
            }
        }

        #endregion

        #region private methods

        private void DeferredConnectedSocket(object sender, EventArgs e)
        {
            SocketClient.ConnectedEventArgs cea =
                (SocketClient.ConnectedEventArgs)e;
            System.Diagnostics.Debugger.Log(1, "", "connection in Messenger [" + (cea.newSocket == null ? "null" : "not null") + "]\n");
            conAPI.Connect(cea.newSocket);
            System.Diagnostics.Debugger.Log(1, "", "1");
            conAPI.StartListening();
            System.Diagnostics.Debugger.Log(1, "", "2");
            if (ConnectedSocket != null)
            {
                System.Diagnostics.Debugger.Log(1, "", "3");
                Microsoft.Iris.Application.DeferredInvoke(
                    new Microsoft.Iris.DeferredInvokeHandler(ConnectedSocket),
                    Microsoft.Iris.DeferredInvokePriority.Normal);
            }
            System.Diagnostics.Debugger.Log(1, "", "4");
        }

        private void InitConAPI()
        {
            conAPI = Connection.GetInstance();
            conAPI.ConnectedEvent += new EventHandler(DeferredConnectedSocket);
            conAPI.DisconnectedEvent += new EventHandler(DeferredDisconnectedSocket);
            conAPI.MessageRecievedEvent += new EventHandler(MessageReceived);
            conAPI.ListenForIncomingConnections();
        }

        #endregion

        #region delegates

        public delegate void DeferredInvokeDelegate(object sender);

        #endregion
    }
}
