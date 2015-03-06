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
        public const int KEEP_ALIVE_TIMOUT = 3;

        #endregion

        #region fields

        private static Connection Instance;
        private SocketClient CurrentSocketClient;
        private SocketClient ListeningSocket;
        private Queue<string> MessageQueue;
        private Timer MessageListenerTimer;
        private Thread ListenerThread;

        #endregion

        #region properties

        public static IPAddress ListeningAddress { get; private set; }

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
            CurrentSocketClient = new SocketClient();
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
            Connection.Instance = null;
            if (CurrentSocketClient != null)
            {
                CurrentSocketClient.Close();
            }
            if (MessageListenerTimer != null)
            {
                MessageListenerTimer.Dispose();
            }
            if (ListeningSocket != null)
            {
                ListeningSocket.Close();
            }
            if (ListenerThread != null)
            {
                ListenerThread.Abort();
                ListenerThread.Interrupt();
            }
        }

        public string Connect(Socket socket)
        {
            if (CurrentSocketClient == null)
            {
                CurrentSocketClient = new SocketClient();
            }
            string success = CurrentSocketClient.Connect(socket);
            CurrentSocketClient.HandleIncomingMessages(
                new SocketClient.SocketCallbackDelegate(SocketCallback));
            return success;
        }

        public string Connect(String hostname, int port)
        {
            if (CurrentSocketClient != null)
            {
                CurrentSocketClient.Close();
            }
            CurrentSocketClient = new SocketClient();

            string success = CurrentSocketClient.Connect(hostname, port);
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

        public string Send(string data)
        {
            if (CurrentSocketClient == null)
            {
                throw new InvalidOperationException("no connection established to SocketClient");
            }
            return CurrentSocketClient.Send(data);
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
            if (CurrentSocketClient == null)
            {
                Connect(Connection.ListeningAddress.ToString(), Connection.APP_PORT);
            }

            // start the listener
            MessageListenerTimer = new Timer(ContinueListening, null, 0, 500);
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

        #endregion

        #region private methods

        private void Init()
        {
            ListeningAddress = Array.FindLast(
                Dns.GetHostEntry(string.Empty).AddressList,
                a => a.AddressFamily == AddressFamily.InterNetwork);
            MessageQueue = new Queue<string>();
        }

        private void StartConnectionListener(object sender)
        {
            if (ListeningSocket != null)
            {
                ListeningSocket.Close();
            }
            ListeningSocket = new SocketClient();
            ListeningSocket.ConnectedEvent += new EventHandler(ConnectedSocket);
            ListeningSocket.Listen(Connection.ListeningAddress, Connection.APP_PORT);
        }

        private void ConnectedSocket(object sender, EventArgs e)
        {
            SocketClient.ConnectedEventArgs cea =
                (SocketClient.ConnectedEventArgs)e;

            // connect to the incoming connection
            CurrentSocketClient.Connect(cea.newSocket);

            if (ConnectedEvent != null)
            {
                ConnectedEvent(sender, cea);
            }
        }

        private void ContinueListening(object sender)
        {
            if (MessageQueue.Count == 0)
            {
                return;
            }
            string message = MessageQueue.Dequeue();

            if (message == SocketClient.KEEP_ALIVE)
            {
                Send(SocketClient.KEEP_ALIVE_ACK);
            }
            else if (message == SocketClient.KEEP_ALIVE_ACK)
            {
                // do nothing
            }
            else if (message == SocketClient.OPERATION_TIMEOUT ||
                message == SocketClient.UNINITIALIZED ||
                message == SocketClient.NO_MESSAGE)
            {
                // do nothing
            }
            else
            {
                if (MessageRecievedEvent != null)
                {
                    MessageRecievedEvent(this, new MessageReceivedEventArgs(message));
                }
            }
        }

        private void SocketCallback(object s, System.Net.Sockets.SocketAsyncEventArgs e)
        {
            string message;
            if (e.SocketError == System.Net.Sockets.SocketError.Success)
            {
                // Retrieve the data from the buffer
                message = Encoding.UTF8.GetString(e.Buffer, e.Offset, e.BytesTransferred);
                message = message.Trim('\0');
            }
            else
            {
                message = e.SocketError.ToString();
            }
            MessageQueue.Enqueue(message);

            CurrentSocketClient.HandleIncomingMessages(
                new SocketClient.SocketCallbackDelegate(SocketCallback));
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
