using Microsoft.Iris;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace equalizerapo_and_zune
{
    /// <summary>
    /// An intermediary between the <see cref="Messenger"/> and <see cref="SocketClient"/> classes.
    /// This class handles multiple sockets so that the Messenger doesn't have to worry about that aspect.
    /// </summary>
    public class Connection
    {
        #region constants

        // why 2048? because Arther C. Clark, that's why
        public const int APP_PORT = 2048;
        // used in testing with windows simple TCP/IP services
        public const int ECHO_PORT = 7;
        // used in testing with windows simple TCP/IP services
        public const int QOTD_PORT = 17;
        // measured in seconds
        public const int KEEP_ALIVE_TIMOUT = 1;
        public const double LISTEN_MESSAGE_TIMEOUT = 0.05;
        public const double SHORT_MESSAGE_TIMEOUT = 0.01;
        // indicates that a message was blocked for being a non-important message
        public const string MESSAGE_BLOCKED = "message blocked";

        #endregion

        #region fields

        /// <summary>
        /// The <see cref="SocketClient"/> that this connection uses.
        /// </summary>
        private SocketClient CurrentSocketClient;
        /// <summary>
        /// The <see cref="SocketClient"/> that is used to listen for incoming connections.
        /// </summary>
        private SocketClient ListeningSocket;
        /// <summary>
        /// Check occasionally to see if the
        /// <see cref="CurrentSocketClient"/> is still alive.
        /// </summary>
        private System.Timers.Timer KeepAliveTimer;
        /// <summary>
        /// The thread spawned to listen for incoming connections.
        /// Kept so that the thread might be killed.
        /// </summary>
        private Thread ListenerThread;
        /// <summary>
        /// The last time a message was sent.
        /// Tracked so that unimportant messages can be ignored and
        /// messages aren't sent too often.
        /// </summary>
        private long LastSendTime;

        #endregion

        #region properties

        /// <summary>
        /// The IPv4 address of the listening socket.
        /// </summary>
        public IPAddress ListeningAddress { get; private set; }

        #endregion

        #region event handlers

        public EventHandler ConnectedEvent;
        public EventHandler DisconnectedEvent;
        public EventHandler MessageRecievedEvent;

        #endregion

        #region public methods

        /// <summary>
        /// Create a connection object by don't connect to anything.
        /// </summary>
        public Connection()
        {
            Init();
        }

        /// <summary>
        /// Create a connection object and establish a connection.
        /// </summary>
        /// <param name="hostname">The IPv4 address to connect to.</param>
        /// <param name="port">The port to connect to.</param>
        /// <seealso cref="APP_PORT"/>
        public Connection(String hostname, int port)
        {
            Init();
            Connect(hostname, port);
        }

        /// <summary>
        /// When this is destroyed, close all sockets and release resources.
        /// </summary>
        ~Connection()
        {
            Close();
        }

        /// <summary>
        /// Close all sockets and release resources.
        /// </summary>
        public void Close()
        {
            // release connections and stop timers
            EndConnection();
        }

        /// <summary>
        /// Connect to an already-established Socket.
        /// </summary>
        /// <param name="socket">The socket to connect to.</param>
        /// <returns>One of the string constants of the <see cref="SocketClient"/> class.
        ///     Is successful when returning SocketClient.SUCCESS</returns>
        public string Connect(Socket socket)
        {
            // check pre-conditions
            if (CurrentSocketClient == null)
            {
                CurrentSocketClient = new SocketClient();
            }

            // connect and start listening to messages from this socket
            string success = CurrentSocketClient.Connect(socket);
            if (success == SocketClient.SUCCESS)
            {
                CurrentSocketClient.MessageReceived += MessageReceived;
            }

            return success;
        }

        /// <summary>
        /// Create a socket and start listening for incoming connections.
        /// </summary>
        /// <param name="hostname">The IPv4 address to connect to.</param>
        /// <param name="port">The port to connect to.</param>
        /// <returns>One of the string constants of the <see cref="SocketClient"/> class.
        ///     Is successful when returning SocketClient.SUCCESS</returns>
        /// <seealso cref="APP_PORT"/>
        public string Connect(String hostname, int port)
        {
            // check pre-conditions
            if (CurrentSocketClient != null)
            {
                CurrentSocketClient.Close();
            }
            CurrentSocketClient = new SocketClient();

            // connect
            string success = CurrentSocketClient.Connect(hostname, port);

            // start listening for messages from this socket
            if (success == SocketClient.SUCCESS)
            {
                CurrentSocketClient.MessageReceived += MessageReceived;
            }
            else
            {
                CurrentSocketClient = null;
            }

            return success;
        }

        /// <summary>
        /// Get the status of this connection.
        /// </summary>
        /// <returns>True if a client is connected.</returns>
        public bool IsConnected()
        {
            return (CurrentSocketClient != null);
        }

        /// <summary>
        /// Start listening for incoming connections by calling
        /// <see cref="StartConnectionListener"/> on the
        /// <see cref="ListenerThread"/> thread.
        /// </summary>
        public void ListenForIncomingConnections()
        {
            ListenerThread = new Thread(new ParameterizedThreadStart(StartConnectionListener));
            ListenerThread.Start();
        }

        /// <summary>
        /// Change the listening address, including freeing the previous
        /// <see cref="SocketClient"/> and connecting to a new one.
        /// </summary>
        /// <param name="newAddress">The new IPv4 address to connect to.</param>
        public void ChangeListeningAddress(IPAddress newAddress)
        {
            bool alreadyListening = false;

            // change the address
            ListeningAddress = newAddress;

            // close the current listener
            if (ListeningSocket != null)
            {
                alreadyListening = true;
                ListeningSocket.Close();
            }
            if (ListenerThread != null)
            {
                alreadyListening = true;
                ListenerThread.Abort();
                ListenerThread.Interrupt();
            }

            // open a new one if already open
            if (alreadyListening)
            {
                ListenForIncomingConnections();
            }
        }

        /// <summary>
        /// Send a message to the current socket.
        /// </summary>
        /// <param name="data">The message to send.</param>
        /// <param name="important">True if this message MUST be sent,
        ///     false if it can be ignored because the attempt is
        ///     too soon after the last message was sent.</param>
        /// <returns>One of the string constants of the <see cref="SocketClient"/> class.
        ///     Is successful when returning SocketClient.SUCCESS</returns>
        public string Send(string data, bool important)
        {
            // check preconditions
            if (CurrentSocketClient == null)
            {
                return SocketClient.DISCONNECTED;
                throw new InvalidOperationException("no connection established to SocketClient");
            }

            // check that there is room for a non-important message
            if (!important &&
                (DateTime.Now.Ticks - LastSendTime < SHORT_MESSAGE_TIMEOUT * 1000 * 10000 * 2))
            {
                return MESSAGE_BLOCKED;
            }

            // try to send, get success status
            string success = CurrentSocketClient.Send(data);
            LastSendTime = DateTime.Now.Ticks;

            // message sending was NOT successful?
            if (success != SocketClient.SUCCESS)
            {
                if (success == SocketClient.DISCONNECTED ||
                    success == SocketClient.OPERATION_TIMEOUT ||
                    success == SocketClient.CONNECTION_RESET)
                {
                    DeferredDisconnected();
                }
            }

            return success;
        }

        /// <summary>
        /// Stop all action and wait for a message to be recieved by the current socket.
        /// </summary>
        /// <returns>The message, or one of the <see cref="SocketClient"/> string constants.</returns>
        public string Receive()
        {
            return Receive(true);
        }

        /// <summary>
        /// Try and receive a message from the current socket client.
        /// Recommended to use <see cref="MessageReceivedEvent"/> instead.
        /// </summary>
        /// <returns>The message, or one of the <see cref="SocketClient"/> string constants.</returns>
        public string Receive(bool waitForTimeout)
        {
            if (CurrentSocketClient == null)
            {
                throw new InvalidOperationException("no connection established to SocketClient");
            }
            return CurrentSocketClient.Receive(waitForTimeout);
        }

        /// <summary>
        /// Start listening for incoming messages.
        /// </summary>
        /// <seealso cref="ListenForIncomingConnections"/>
        public void StartListening()
        {
            // check pre-conditions
            if (CurrentSocketClient == null)
            {
                Connect(ListeningAddress.ToString(), Connection.APP_PORT);
            }

            // start the keep-alive checks
            if (KeepAliveTimer != null)
            {
                Connection.KillTimer(KeepAliveTimer, "KeepAliveTimer");
            }
            int keepAliveTimeout = KEEP_ALIVE_TIMOUT * 1000;
            KeepAliveTimer = new System.Timers.Timer(keepAliveTimeout);
            KeepAliveTimer.Elapsed +=
                new System.Timers.ElapsedEventHandler(KeepAliveChecker);
            KeepAliveTimer.Start();
        }

        #endregion

        #region public static methods

        /// <summary>
        /// Kill the given timer.
        /// </summary>
        /// <param name="timer">The timer to kill.</param>
        /// <param name="timerName">Name of the timer (for debugging purposes).</param>
        public static void KillTimer(System.Timers.Timer timer, string timerName)
        {
            try
            {
                timer.Stop();
                timer.Enabled = false;
            }
            catch (ObjectDisposedException e) {
            }
            catch (NullReferenceException e) {
            }
        }

        /// <summary>
        /// Get a list of IPv4 addresses this computer is connected to.
        /// </summary>
        /// <returns>The list of addresses.</returns>
        public static IPAddress[] ListeningAddresses()
        {
            return Array.FindAll(
                Dns.GetHostEntry(string.Empty).AddressList,
                a => a.AddressFamily == AddressFamily.InterNetwork);
        }

        #endregion

        #region private methods

        /// <summary>
        /// Initialize the connection, including initializing collections and retrieving
        /// the listening addresses.
        /// </summary>
        private void Init()
        {
            ListeningAddress = Connection.ListeningAddresses().Last();
        }

        /// <summary>
        /// Closes all sockets and releases resources.
        /// </summary>
        private void EndConnection()
        {
            if (CurrentSocketClient != null)
            {
                CurrentSocketClient.Close();
                CurrentSocketClient = null;
            }
            if (KeepAliveTimer != null)
            {
                Connection.KillTimer(KeepAliveTimer, "KeepAliveTimer");
                KeepAliveTimer = null;
            }
            if (ListeningSocket != null)
            {
                ListeningSocket.Close();
                ListeningSocket = null;
            }
            if (ListenerThread != null)
            {
                try
                {
                    ListenerThread.Abort();
                    ListenerThread.Interrupt();
                }
                catch (NullReferenceException)
                {
                    // do something here?
                }
                ListenerThread = null;
            }
        }

        /// <summary>
        /// Closes all sockets and releases resources.
        /// </summary>
        private void Disconnected()
        {
            Disconnected(null);
        }

        /// <summary>
        /// Closes all sockets and releases resources.
        /// </summary>
        private void Disconnected(object sender)
        {
            System.Diagnostics.Debugger.Log(1, "", "disconnected\n");
            Close();
            if (DisconnectedEvent != null)
            {
                DisconnectedEvent(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Calls <see cref="Disconnected"/> via a deferred invoke handler.
        /// </summary>
        private void DeferredDisconnected()
        {
            Application.DeferredInvoke(
                new DeferredInvokeHandler(Disconnected),
                DeferredInvokePriority.Normal);
        }

        /// <summary>
        /// Start listening for incoming connections.
        /// Used to start listening on a new thread by <see cref="ListenForIncomingConnections"/>.
        /// </summary>
        /// <param name="sender"><see cref="Connection"/></param>
        private void StartConnectionListener(object sender)
        {
            if (ListeningSocket != null)
            {
                ListeningSocket.Close();
            }
            ListeningSocket = new SocketClient();
            ListeningSocket.ConnectedEvent += new EventHandler(ConnectedSocket);
            ListeningSocket.Listen(ListeningAddress, Connection.APP_PORT);
        }

        /// <summary>
        /// Triggered by <see cref="SocketClient.ConnectedEvent"/> event.
        /// Starts listening for messages on the received socket and
        /// calls <see cref="ConnectedEvent"/>.
        /// </summary>
        /// <param name="sender"><see cref="SocketClient"/></param>
        /// <param name="args">A <see cref="SocketClient.ConnectedEventArgs"/> object.</param>
        private void ConnectedSocket(object sender, EventArgs args)
        {
            System.Diagnostics.Debugger.Log(1, "", "connected\n");
            SocketClient.ConnectedEventArgs cea =
                args as SocketClient.ConnectedEventArgs;

            // connect to the incoming connection
            if (CurrentSocketClient == null)
            {
                CurrentSocketClient = new SocketClient();
            }
            CurrentSocketClient.Connect(cea.newSocket);

            // start listening for new connections
            StartListening();

            // fire connected event!
            if (ConnectedEvent != null)
            {
                ConnectedEvent(sender, cea);
            }
        }

        /// <summary>
        /// Interprets the basic message.
        /// If a message, calls <see cref="MessageReceivedEvent"/>.
        /// If a disconnection from the socket, calls <see cref="DeferredDisconnected"/>.
        /// </summary>
        /// <param name="message">the message to be interpretted</param>
        private void InterpretMessages(string message)
        {
            // parse the message
            if (message == SocketClient.OPERATION_TIMEOUT ||
                message == SocketClient.UNINITIALIZED ||
                message == SocketClient.NO_MESSAGE ||
                message == SocketClient.KEEP_ALIVE ||
                message == SocketClient.KEEP_ALIVE_ACK ||
                message == SocketClient.CONNECTION_RESET)
            {
                // do nothing
            }
            else if (message == SocketClient.DISCONNECTED ||
                message == SocketClient.CONNECTION_ABORTED)
            {
                // disconnect
                DeferredDisconnected();
            }
            else
            {
                // fire message received event!
                if (MessageRecievedEvent != null)
                {
                    MessageRecievedEvent(this, new MessageReceivedEventArgs(message));
                }
            }
        }
        
        /// <summary>
        /// Checks that the <see cref="CurrentSocket"/> is alive.
        /// Called occasionally by the <see cref="KeepAliveTimer"/>.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="args">N/A</param>
        private void KeepAliveChecker(object sender, System.Timers.ElapsedEventArgs args)
        {
            Send(SocketClient.KEEP_ALIVE, false);
        }

        /// <summary>
        /// Triggered by <see cref="SocketClient.HandleIncomingMessages"/>.
        /// Makes a call to <see cref="InterpretMessages"/>
        /// </summary>
        /// <param name="s">N/A</param>
        /// <param name="args">The message received event args</param>
        private void MessageReceived(object s, EventArgs args)
        {
            // check for null values
            if (CurrentSocketClient == null)
            {
                return;
            }

            // retrieve and parse the message
            string message = (args as SocketClient.MessageEventArgs).message;

            // determine if this message is either a disconnect or worthy message
            if (message.Length == 0)
            {
                // Do nothing
            }
            else
            {
                try
                {
                    InterpretMessages(message);
                }
                catch (Exception e)
                {
                    // catch thread-syncronization related errors
                    if (!(e is NullReferenceException) &&
                        !(e is ObjectDisposedException))
                    {
                        throw;
                    }
                }
            }
        }

        #endregion

        #region classes

        /// <summary>
        /// Used for the <see cref="MessageReceivedEvent"/> event handler.
        /// </summary>
        public class MessageReceivedEventArgs : EventArgs
        {
            public string message;

            public MessageReceivedEventArgs(string message)
            {
                this.message = message;
            }
        }

        #endregion
    }
}
