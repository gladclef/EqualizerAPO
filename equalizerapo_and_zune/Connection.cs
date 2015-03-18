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
        /// Used to queue up messages so that message receiving isn't
        /// blocked by application logic.
        /// </summary>
        private static Queue<string> MessageQueue;
        /// <summary>
        /// Triggers <see cref="GetMessage"/> occasionaly,
        /// checking to see if there's anything in
        /// <see cref="MessageQueue"/>.
        /// </summary>
        private System.Timers.Timer MessageListenerTimer;
        /// <summary>
        /// Like <see cref="MessageListenerTimer"/>, except that
        /// it starts once a message is received, continues until
        /// a message is no longer in the queue, and triggers
        /// significantly more often.
        /// </summary>
        private System.Timers.Timer ShortMessageListenerTimer;
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
        /// <summary>
        /// used to obtain exclusive access to the message queue
        /// </summary>
        private volatile static AutoResetEvent reset_MessageQueue;

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

            // release other objects
            MessageQueue = null;

            // free AutoResetEvents
            if (reset_MessageQueue != null)
            {
                reset_MessageQueue.Close();
                reset_MessageQueue = null;
            }
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
            CurrentSocketClient.HandleIncomingMessages(
                new SocketClient.SocketCallbackDelegate(SocketCallback));

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
                CurrentSocketClient.HandleIncomingMessages(
                    new SocketClient.SocketCallbackDelegate(SocketCallback));
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
            string success = CurrentSocketClient.Send("%" + data);
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

            // start the listener
            if (MessageListenerTimer != null)
            {
                Connection.KillTimer(MessageListenerTimer, "MessageListenerTimer");
            }
            int listenMessageTimout = Convert.ToInt32(
                Connection.LISTEN_MESSAGE_TIMEOUT * 1000);
            MessageListenerTimer = new System.Timers.Timer(listenMessageTimout);
            MessageListenerTimer.Elapsed +=
                new System.Timers.ElapsedEventHandler(GetMessage);
            MessageListenerTimer.Start();

            // start the listener
            if (ShortMessageListenerTimer != null)
            {
                Connection.KillTimer(ShortMessageListenerTimer, "ShortMessageListenerTimer");
            }
            int shortListenMessageTimout = Convert.ToInt32(
                Connection.SHORT_MESSAGE_TIMEOUT * 1000);
            ShortMessageListenerTimer = new System.Timers.Timer(shortListenMessageTimout);
            ShortMessageListenerTimer.Elapsed +=
                new System.Timers.ElapsedEventHandler(GetMessage);

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
            
            // create the message queue if it doesn't exist
            if (MessageQueue == null)
            {
                MessageQueue = new Queue<string>();
            }

            // create AutoResetEvents that don't exist
            if (reset_MessageQueue == null)
            {
                reset_MessageQueue = new AutoResetEvent(true);
            }
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
            if (MessageListenerTimer != null)
            {
                Connection.KillTimer(MessageListenerTimer, "MessageListenerTimer");
                MessageListenerTimer = null;
            }
            if (ShortMessageListenerTimer != null)
            {
                Connection.KillTimer(ShortMessageListenerTimer, "ShortMessageListenerTimer");
                ShortMessageListenerTimer = null;
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
        /// Checks if a message has been received and interprets the basic message.
        /// If a message, calls <see cref="MessageReceivedEvent"/>.
        /// If a disconnection from the socket, calls <see cref="DeferredDisconnected"/>.
        /// Called occasionally by <see cref="MessageListenerTimer"/>
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="args">N/A</param>
        private void GetMessage(object sender, System.Timers.ElapsedEventArgs args)
        {
            // get the message from the message queue
            string message = "";
            try
            {
                reset_MessageQueue.WaitOne(100);
                // release the short listener timer if there aren't any more messages to be processed
                if (MessageQueue.Count == 0)
                {
                    // no more messages, stop listening so quickly
                    ShortMessageListenerTimer.Stop();
                    return;
                }
                message = MessageQueue.Dequeue();
                reset_MessageQueue.Set();
            }
            catch (Exception e)
            {
                // catch thread-syncronization related errors
                if (!(e is NullReferenceException) &&
                    !(e is ObjectDisposedException))
                {
                    throw;
                }
                else
                {
                    DeferredDisconnected();
                    return;
                }
            }

            // parse the message
            if (message == SocketClient.KEEP_ALIVE)
            {
                // continue until a real message is received
                GetMessage(sender, args);
            }
            else if (message == SocketClient.OPERATION_TIMEOUT ||
                message == SocketClient.UNINITIALIZED ||
                message == SocketClient.NO_MESSAGE)
            {
                // do nothing
            }
            else
            {
                // fire message received event!
                if (MessageRecievedEvent != null)
                {
                    MessageRecievedEvent(this, new MessageReceivedEventArgs(message));
                }
                // start listening for messages more often
                try
                {
                    ShortMessageListenerTimer.Start();
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
        /// Adds messages to the <see cref="MessageQueue"/>.
        /// If the socket sends a disconnect message, calls <see cref="DeferredDisconnected"/>.
        /// </summary>
        /// <param name="s">N/A</param>
        /// <param name="args">The message received event args</param>
        private void SocketCallback(object s, System.Net.Sockets.SocketAsyncEventArgs args)
        {
            // check for null values
            if (CurrentSocketClient == null ||
                reset_MessageQueue == null ||
                MessageQueue == null)
            {
                return;
            }

            // prepare for another message to be recieved
            try
            {
                CurrentSocketClient.HandleIncomingMessages(
                    new SocketClient.SocketCallbackDelegate(SocketCallback));
            }
            catch (ObjectDisposedException)
            {
                System.Diagnostics.Debugger.Log(1, "", "** error: can't recieve any more messages\n");
                // do something here?
            }

            // retrieve and parse the message
            string message;
            if (args.SocketError == System.Net.Sockets.SocketError.Success)
            {
                // Retrieve the data from the buffer
                message = Encoding.UTF8.GetString(args.Buffer, args.Offset, args.BytesTransferred);
                message = message.Trim('\0');
            }
            else
            {
                message = args.SocketError.ToString();
            }

            // parse messages
            string[] messages = message.Split(new char[] { '%' });

            foreach (string m in messages)
            {
                // determine if this message is either a disconnect or worthy message
                if (m.Length == 0)
                {
                    continue;
                }
                else if (m == SocketClient.CONNECTION_ABORTED ||
                    m == SocketClient.CONNECTION_RESET)
                {
                    DeferredDisconnected();
                }
                else
                {
                    try
                    {
                        reset_MessageQueue.WaitOne(100);
                        MessageQueue.Enqueue(m);
                        reset_MessageQueue.Set();
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
