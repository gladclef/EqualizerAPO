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
        LinkedList<Connection> allConnections;

        #endregion

        #region properties

        public DeferredInvokeDelegate ConnectedSocketCall { get; set; }
        public DeferredInvokeDelegate DisconnectedSocketCall { get; set; }
        public DeferredInvokeMessageDelegate MessageReceivedCall { get; set; }

        #endregion

        #region public methods

        public Messenger(
            DeferredInvokeDelegate connectedSocketCall,
            DeferredInvokeDelegate disconnectedSocketCall,
            DeferredInvokeMessageDelegate messageReceivedCall)
        {
            // set initial connection values
            this.ConnectedSocketCall = connectedSocketCall;
            this.DisconnectedSocketCall = disconnectedSocketCall;
            this.MessageReceivedCall = messageReceivedCall;
            this.allConnections = new LinkedList<Connection>();

            // create a listening connection
            InitConAPI();
        }

        ~Messenger()
        {
            Close();
        }

        public void Close()
        {
            CloseAllConnections();
        }

        private void CloseAllConnections()
        {
            conAPI.Close();
            conAPI = null;
            foreach (Connection connection in allConnections)
            {
                connection.Close();
            }
            allConnections.Clear();
        }

        public void MessageReceived(object sender, EventArgs e) {
            Connection.MessageReceivedEventArgs cea =
                (Connection.MessageReceivedEventArgs)e;
            if (MessageReceivedCall != null &&
                cea.message != null)
            {
                MessageReceivedCall(this,
                    new MessageEventArgs(cea.message));
            }
        }

        public void DeferredDisconnectedSocket(object sender, EventArgs e)
        {
            CloseAllConnections();
            InitConAPI();
            if (DisconnectedSocketCall != null)
            {
                DisconnectedSocketCall(this);
            }
        }

        public string[] GetPossibleIPAddresses()
        {
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
            if (newAddress == conAPI.ListeningAddress.ToString())
            {
                return true;
            }
            foreach (IPAddress address in Connection.ListeningAddresses())
            {
                if (address.ToString() == newAddress)
                {
                    conAPI.ChangeListeningAddress(address);
                    return true;
                }
            }
            return false;
        }

        public void Send(string message, bool important)
        {
            // broadcast to all
            if (conAPI.IsConnected())
            {
                conAPI.Send(message, important);
            }
        }

        #endregion

        #region private methods

        private void ConnectedSocket(object sender, EventArgs e)
        {
            SocketClient.ConnectedEventArgs cea =
                (SocketClient.ConnectedEventArgs)e;

            // set up the new connection to listen for message
            Connection newConnection = new Connection();
            newConnection.Connect(cea.newSocket);
            newConnection.DisconnectedEvent += new EventHandler(DeferredDisconnectedSocket);
            newConnection.MessageRecievedEvent += new EventHandler(MessageReceived);
            allConnections.AddLast(newConnection);

            // pass along the event
            if (ConnectedSocketCall != null)
            {
                ConnectedSocketCall(this);
            }
        }

        private void InitConAPI()
        {
            conAPI = new Connection();
            conAPI.ConnectedEvent += new EventHandler(ConnectedSocket);
            conAPI.DisconnectedEvent += new EventHandler(DeferredDisconnectedSocket);
            conAPI.MessageRecievedEvent += new EventHandler(MessageReceived);
            conAPI.ListenForIncomingConnections();
        }

        #endregion

        #region delegates

        public delegate void DeferredInvokeDelegate(object sender);
        public delegate void DeferredInvokeMessageDelegate(object sender, MessageEventArgs args);
        public class MessageEventArgs : EventArgs
        {
            public string message { get; set; }
            public MessageEventArgs(string m)
            {
                message = m;
            }
        }

        #endregion
    }
}
