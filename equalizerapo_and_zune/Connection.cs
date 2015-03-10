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
        public const int LISTEN_MESSAGE_TIMEOUT = 1;

        #endregion

        #region fields

        private static Connection Instance;
        private SocketClient CurrentSocketClient;
        private SocketClient ListeningSocket;
        private Queue<string> MessageQueue;
        private static System.Timers.Timer MessageListenerTimer;
        private static System.Timers.Timer KeepAliveTimer;
        private Thread ListenerThread;

        #endregion

        #region properties

        public IPAddress ListeningAddress { get; private set; }

        #endregion

        #region event handlers

        public EventHandler ConnectedEvent;
        public EventHandler DisconnectedEvent;
        public EventHandler MessageRecievedEvent;

        #endregion

        #region public methods

        public Connection()
        {
            Init();
        }

        public Connection(String hostname, int port)
        {
            Init();
            Connect(hostname, port);
        }

        ~Connection()
        {
            Close();
        }

        public void Close()
        {
            EndConnection();
            Connection.Instance = null;
        }

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

        public void ListenForIncomingConnections()
        {
            ListenerThread = new Thread(new ParameterizedThreadStart(StartConnectionListener));
            ListenerThread.Start();
        }

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

        public string Send(string data)
        {
            // check preconditions
            if (CurrentSocketClient == null)
            {
                throw new InvalidOperationException("no connection established to SocketClient");
            }

            // try to send, get success status
            string success = CurrentSocketClient.Send(data);

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

        public string Receive()
        {
            return Receive(true);
        }

        public string Receive(bool waitForTimeout)
        {
            if (CurrentSocketClient == null)
            {
                throw new InvalidOperationException("no connection established to SocketClient");
            }
            return CurrentSocketClient.Receive(waitForTimeout);
        }

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
            int listenMessageTimout = Connection.LISTEN_MESSAGE_TIMEOUT * 1000;
            MessageListenerTimer = new System.Timers.Timer(listenMessageTimout);
            MessageListenerTimer.Elapsed +=
                new System.Timers.ElapsedEventHandler(GetMessage);
            MessageListenerTimer.Start();

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

        public static Connection GetInstance()
        {
            if (Instance == null)
            {
                Instance = new Connection();
            }
            return Instance;
        }

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

        public static IPAddress[] ListeningAddresses()
        {
            return Array.FindAll(
                Dns.GetHostEntry(string.Empty).AddressList,
                a => a.AddressFamily == AddressFamily.InterNetwork);
        }

        #endregion

        #region private methods

        private void Init()
        {
            ListeningAddress = Connection.ListeningAddresses().Last();
            MessageQueue = new Queue<string>();
        }

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

        private void Disconnected()
        {
            Disconnected(null);
        }

        private void Disconnected(object sender)
        {
            EndConnection();
            if (DisconnectedEvent != null)
            {
                DisconnectedEvent(this, EventArgs.Empty);
            }
        }

        private void DeferredDisconnected()
        {
            Application.DeferredInvoke(
                new DeferredInvokeHandler(Disconnected),
                DeferredInvokePriority.Normal);
        }

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

        private void ConnectedSocket(object sender, EventArgs args)
        {
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

        private void GetMessage(object sender, System.Timers.ElapsedEventArgs args)
        {
            // get the message
            if (MessageQueue.Count == 0)
            {
                return;
            }
            string message = MessageQueue.Dequeue();

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
            }
        }
        
        private void KeepAliveChecker(object sender, System.Timers.ElapsedEventArgs args)
        {
            Send(SocketClient.KEEP_ALIVE);
        }

        private void SocketCallback(object s, System.Net.Sockets.SocketAsyncEventArgs args)
        {
            // prepare for another message to be recieved
            if (CurrentSocketClient != null)
            {
                try
                {
                    CurrentSocketClient.HandleIncomingMessages(
                        new SocketClient.SocketCallbackDelegate(SocketCallback));
                }
                catch (ObjectDisposedException)
                {
                    // do something here?
                }
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

            // determine if this message is either a disconnect or worthy message
            if (message == SocketClient.CONNECTION_ABORTED ||
                message == SocketClient.CONNECTION_RESET)
            {
                DeferredDisconnected();
            }
            else if (message.Length > 0)
            {
                MessageQueue.Enqueue(message);
            }
        }

        #endregion

        #region classes

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
