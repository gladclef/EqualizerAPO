using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

        public DeferredInvokeDelegate ConnectedSocketCall { get; set; }
        public DeferredInvokeDelegate DisconnectedSocketCall { get; set; }

        #endregion

        #region public methods

        public Messenger(DeferredInvokeDelegate con, DeferredInvokeDelegate dis)
        {
            // set initial connection values
            this.ConnectedSocketCall = con;
            this.DisconnectedSocketCall = dis;

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
            if (DisconnectedSocketCall != null)
            {
                DisconnectedSocketCall(this);
            }
        }

        public string[] GetPossibleIPAddresses()
        {
            System.Diagnostics.Debugger.Log(1, "", "GetPossibleIPAddresses 1\n");
            IPAddress[] addresses = Connection.ListeningAddresses();

            // get the addresses
            LinkedList<string> retval = new LinkedList<string>(
                Array.ConvertAll(
                    addresses,
                    (IPAddress address) => {
                        return address.ToString();
                    }));

            // move the selected address to the top
            if (addresses.First() != conAPI.ListeningAddress &&
                conAPI.ListeningAddress != null)
            {
                string current = conAPI.ListeningAddress.ToString();
                retval.Remove(current);
                retval.AddFirst(current);
            }

            // return the new values
            return retval.ToArray();
        }

        public bool ChangeListeningAddress(string newAddress)
        {
            System.Diagnostics.Debugger.Log(1, "", "ChangeListeningAddress 1\n");
            if (newAddress == conAPI.ListeningAddress.ToString())
            {
                return true;
            }
            System.Diagnostics.Debugger.Log(1, "", "ChangeListeningAddress 2\n");
            foreach (IPAddress address in Connection.ListeningAddresses())
            {
                if (address.ToString() == newAddress)
                {
                    conAPI.ChangeListeningAddress(address);
                    return true;
                }
            }
            System.Diagnostics.Debugger.Log(1, "", "ChangeListeningAddress 3\n");
            return false;
        }

        #endregion

        #region private methods

        private void ConnectedSocket(object sender, EventArgs e)
        {
            SocketClient.ConnectedEventArgs cea =
                (SocketClient.ConnectedEventArgs)e;
            System.Diagnostics.Debugger.Log(1, "", "connection in Messenger [" + (cea.newSocket == null ? "null" : "not null") + "]\n");
            conAPI.Connect(cea.newSocket);
            conAPI.StartListening();
            if (ConnectedSocketCall != null)
            {
                ConnectedSocketCall(this);
            }
        }

        private void InitConAPI()
        {
            conAPI = Connection.GetInstance();
            conAPI.ConnectedEvent += new EventHandler(ConnectedSocket);
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
