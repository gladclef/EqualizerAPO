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

        #endregion

        #region fields

        private static Connection Instance;
        private static SocketClient CurrentSocketClient;
        private bool keepAliveReceived;
        private Thread keepAliveThread;

        #endregion

        #region properties

        public bool Connected { get; private set; }
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
                Connected = true;
            }
            else
            {
                CurrentSocketClient = null;
            }

            return success;
        }

        public void Listen()
        {
            new Thread(new ParameterizedThreadStart(Listener)).Start();
        }

        public string Send(string data)
        {
            if (!Connected)
            {
                throw new InvalidOperationException("no connection established to SocketClient");
            }
            return CurrentSocketClient.Send(data);
        }

        public String Receive()
        {
            return CurrentSocketClient.Receive();
        }

        public void StartListening()
        {
            if (CurrentSocketClient == null)
            {
                Connect(Connection.ListeningAddress.ToString(), Connection.APP_PORT);
            }
            System.Diagnostics.Debugger.Log(1, "", "started listening\n");
            new Thread(new ParameterizedThreadStart(ContinueListening)).Start();
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
            Connected = false;
            ListeningAddress = Array.FindLast(
                Dns.GetHostEntry(string.Empty).AddressList,
                a => a.AddressFamily == AddressFamily.InterNetwork);
            System.Diagnostics.Debugger.Log(1, "", "ip address: " + ListeningAddress + "\n");
        }

        private void Listener(object sender)
        {
            SocketClient socket = new SocketClient();
            socket.ConnectedEvent += new EventHandler(ConnectedSocket);
            socket.Listen(Connection.ListeningAddress, Connection.APP_PORT);
        }

        private void ConnectedSocket(object sender, EventArgs e)
        {
            // create a keep-alive checker thread
            if (keepAliveThread != null)
            {
                keepAliveThread.Abort();
            }
            keepAliveThread = new Thread(new ParameterizedThreadStart(KeepAliveChecker));

            if (ConnectedEvent != null)
            {
                ConnectedEvent(sender, e);
            }
        }

        private void KeepAliveChecker(object sender)
        {
            Thread.Sleep(5000);

            if (CurrentSocketClient == null)
            {
                Connect(Connection.ListeningAddress.ToString(), Connection.APP_PORT);
            }

            System.Diagnostics.Debugger.Log(1, "", "sending keep-alive check\n");
            keepAliveReceived = false;
            CurrentSocketClient.Send("keep alive check");
            System.Diagnostics.Debugger.Log(1, "", "receiving keep-alive check\n");
            string response = CurrentSocketClient.Receive();
            System.Diagnostics.Debugger.Log(1, "", "response: " + response + "\n");
            if (response == SocketClient.KEEP_ALIVE_ACK ||
                keepAliveReceived)
            {

                // continue to keep alive!
                KeepAliveChecker(sender);
            }
            else
            {

                // no response! must have disconnected
                if (DisconnectedEvent != null)
                {
                    DisconnectedEvent(this, EventArgs.Empty);
                }
                if (keepAliveThread != null)
                {
                    keepAliveThread.Abort();
                    keepAliveThread = null;
                }
            }
        }

        private void ContinueListening(object sender)
        {
            Thread.Sleep(200);

            string message = CurrentSocketClient.Receive();
            if (message == SocketClient.KEEP_ALIVE_ACK)
            {
                keepAliveReceived = true;
            }
            else if (message == SocketClient.OPERATION_TIMEOUT ||
                message == SocketClient.UNINITIALIZED)
            {
                // do nothing
            }
            else
            {
                if (MessageRecievedEvent != null)
                {
                    MessageRecievedEvent(this, EventArgs.Empty);
                }
            }
        }

        #endregion
    }
}
