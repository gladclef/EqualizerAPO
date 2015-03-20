using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace equalizerapo_and_zune
{
    /// <summary>
    /// An intermediary between the <see cref="Connection"/> class and the main form.
    /// This class handles multiple Connections so that the form doesn't have to worry about that aspect.
    /// </summary>
    class Messenger
    {
        #region fields

        /// <summary>
        /// Talks to the <see cref="Connection"/> class to send/receive messages.
        /// </summary>
        Connection conAPI;
        /// <summary>
        /// List of connections that are generated when clients connect.
        /// </summary>
        LinkedList<Connection> allConnections;

        #endregion

        #region properties

        /// <summary>
        /// Used to delegate <see cref="Connection.ConnectedSocket"/> events back to the main form
        /// </summary>
        public DeferredInvokeDelegate ConnectedSocketCall { get; set; }
        /// <summary>
        /// Used to delegate <see cref="Connection.DeferredDisconnected"/> events back to the main form
        /// </summary>
        public DeferredInvokeDelegate DisconnectedSocketCall { get; set; }
        /// <summary>
        /// Used to delegate <see cref="Connection.InterpretMessages"/> messages back to the main form
        /// </summary>
        public DeferredInvokeMessageDelegate MessageReceivedCall { get; set; }

        #endregion

        #region public methods

        /// <summary>
        /// Create a new messenger class with callbacks.
        /// </summary>
        /// <param name="connectedSocketCall">For when a client connects</param>
        /// <param name="disconnectedSocketCall">For when a client disconnects</param>
        /// <param name="messageReceivedCall">For when a message is recieved</param>
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

        /// <summary>
        /// Destroy all connections when a this is destroyed.
        /// </summary>
        ~Messenger()
        {
            Close();
        }

        /// <summary>
        /// Closes all connections.
        /// </summary>
        public void Close()
        {
            CloseAllConnections();
        }

        /// <summary>
        /// Closes all connections and releases resources.
        /// </summary>
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

        /// <summary>
        /// Triggered by <see cref="Connection.InterpretMessages"/>.
        /// Passes the message along via <see cref="MessageReceivedCall"/>
        /// </summary>
        /// <param name="sender"><see cref="Connection"/></param>
        /// <param name="e">A <see cref="Connection.MessageReceivedEventArgs"/> object</param>
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

        /// <summary>
        /// Triggered by <see cref="Connection.DeferredDisconnect"/>.
        /// Closes all connection, releases all resources, and reestablishes the listening socket.
        /// </summary>
        /// <param name="sender"><see cref="Connection"/></param>
        /// <param name="e">N/A</param>
        public void DeferredDisconnectedSocket(object sender, EventArgs e)
        {
            CloseAllConnections();
            InitConAPI();
            if (DisconnectedSocketCall != null)
            {
                DisconnectedSocketCall(this);
            }
        }

        /// <summary>
        /// Get a list of the IPv4 addresses of this computer.
        /// </summary>
        /// <returns>A list of IPv4 addresses to listen on.</returns>
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

        /// <summary>
        /// Changes the listening IPv4 address of the socket.
        /// Also releases the currently listening socket and creates a new one.
        /// </summary>
        /// <param name="newAddress">An IPv4 address.</param>
        /// <returns>True if the listener was created successfully.</returns>
        /// <seealso cref="GetPossibleIPAddresses"/>
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

        /// <summary>
        /// Broadcast a message to all <see cref="Connection"/>s.
        /// </summary>
        /// <param name="message">The message to pass</param>
        /// <param name="important">True if this message MUST be sent,
        ///     false if it can be ignored because the attempt is
        ///     too soon after the last message was sent.</param>
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

        /// <summary>
        /// Triggered by <see cref="Connection.ConnectedSocket"/>.
        /// Adds the <see cref="Connection"/> to <see cref="allConnections"/> and
        /// makes a call to <see cref="connectedSocketCall"/>.
        /// </summary>
        /// <param name="sender"><see cref="Connection"/></param>
        /// <param name="e">A <see cref="SocketClient.ConnectedEventArgs"/> object</param>
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

        /// <summary>
        /// Initialize this object, including initializing resources and
        /// creating a <see cref="Connection"/> to listen for incoming connections.
        /// </summary>
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
