using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Threading.Tasks;

namespace equalizerapo_and_zune
{
    public class SocketClient
    {
        #region constants

        // Define a timeout in milliseconds for each asynchronous call. If a response is not received within this 
        // timeout period, the call is aborted.
        const int TIMEOUT_MILLISECONDS = 4000;

        // The maximum size of the data buffer to use with the asynchronous socket methods
        const int MAX_BUFFER_SIZE = 2048;

        // send/receive message constants
        public const string OPERATION_TIMEOUT = "Operation Timeout";
        public const string UNINITIALIZED = "Socket is not initialized";
        public const string SUCCESS = "Success";
        public const string KEEP_ALIVE = "Keep alive check";
        public const string KEEP_ALIVE_ACK = "Keep alive acknowledged";
        public const string NO_MESSAGE = "No message available";
        public const string DISCONNECTED = "NotConnected";
        public const string CONNECTION_ABORTED = "ConnectionAborted";
        public const string CONNECTION_RESET = "ConnectionReset";

        #endregion

        #region fields

        /// <summary>
        /// Cached Socket object that will be used by each call for the lifetime of this class
        /// </summary>
        Socket _socket = null;

        /// <summary>
        /// Used to listen for incoming connections.
        /// Kept so that it might be closed later.
        /// </summary>
        private Accepter ListenAccepter;

        #endregion

        #region event handlers

        /// <summary>
        /// Triggers when a new client connects.
        /// </summary>
        public EventHandler ConnectedEvent;

        #endregion

        #region public methods

        /// <summary>
        /// Close all connections when the socket is destroyed.
        /// </summary>
        ~SocketClient()
        {
            Close();
        }

        /// <summary>
        /// Creates a SocketClient with an existing socket.
        /// </summary>
        /// <param name="socket">The socket to create with.</param>
        /// <seealso cref="HandleIncomingMessages"/>
        /// <returns></returns>
        public string Connect(Socket socket)
        {
            if (socket != _socket)
            {
                Close();
            }
            _socket = socket;
            return SUCCESS;
        }

        /// <summary>
        /// Attempt a TCP socket connection to the given host over the given port
        /// </summary>
        /// <param name="hostName">The name of the host</param>
        /// <param name="portNumber">The port number to connect</param>
        /// <returns>A string representing the result of this connection attempt</returns>
        /// <seealso cref="HandleIncomingMessages"/>
        public string Connect(string hostName, int portNumber)
        {
            string result = string.Empty;
            // Signaling object used to notify when an asynchronous operation is completed
            ManualResetEvent clientDone = new ManualResetEvent(false);

            // Create DnsEndPoint. The hostName and port are passed in to this method.
            DnsEndPoint hostEntry = new DnsEndPoint(hostName, portNumber);

            // Create a stream-based, TCP socket using the InterNetwork Address Family.
            if (_socket != null)
            {
                Close();
            }
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Create a SocketAsyncEventArgs object to be used in the connection request
            SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
            socketEventArg.RemoteEndPoint = hostEntry;

            // Inline event handler for the Completed event.
            // Note: This event handler was implemented inline in order to make this method self-contained.
            socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate(object s, SocketAsyncEventArgs e)
            {
                // Retrieve the result of this request
                result = e.SocketError.ToString();

                // Signal that the request is complete, unblocking the UI thread
                clientDone.Set();
            });

            // set the send and receive buffer sizes to some unreasonably large number (32k)
            _socket.SendBufferSize = 32768;
            _socket.ReceiveBufferSize = 32768;

            // Sets the state of the event to nonsignaled, causing threads to block
            clientDone.Reset();

            // Make an asynchronous Connect request over the socket
            _socket.ConnectAsync(socketEventArg);

            // Block the UI thread for a maximum of TIMEOUT_MILLISECONDS milliseconds.
            // If no response comes back within this time then proceed
            clientDone.WaitOne(TIMEOUT_MILLISECONDS);

            return result;
        }

        /// <summary>
        /// Creates a listener for incoming connections
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <param name="portNumber"></param>
        public void Listen(IPAddress ipAddress, int portNumber)
        {
            // Create DnsEndPoint. The hostName and port are passed in to this method.
            IPEndPoint hostEntry = new IPEndPoint(ipAddress, portNumber);

            // Create a stream-based, TCP socket using the InterNetwork Address Family.
            if (_socket != null)
            {
                Close();
            }
            _socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // set the send and receive buffer sizes to some unreasonably large number (32k)
            _socket.SendBufferSize = 32768;
            _socket.ReceiveBufferSize = 32768;

            // Bind the socket to the local endpoint, and listen for incoming connections.
            _socket.Bind(hostEntry);
            _socket.Listen(100);

            // get the object ready to continue to receive connections
            ListenAccepter = new Accepter(_socket, ConnectedEvent);
            ListenAccepter.AcceptNextAsync();
        }

        /// <summary>
        /// Send the given data to the server using the established connection
        /// </summary>
        /// <param name="data">The data to send to the server</param>
        /// <returns>The result of the Send request</returns>
        public string Send(string data)
        {
            string response = OPERATION_TIMEOUT;

            // We are re-using the _socket object initialized in the Connect method
            if (_socket == null)
            {
                return UNINITIALIZED;
            }

            try 
            {
                // Signaling object used to notify when an asynchronous operation is completed
                ManualResetEvent clientDone = new ManualResetEvent(false);
                // Create SocketAsyncEventArgs context object
                SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();

                // Set properties on context object
                socketEventArg.RemoteEndPoint = _socket.RemoteEndPoint;
                socketEventArg.UserToken = null;

                // Inline event handler for the Completed event.
                // Note: This event handler was implemented inline in order 
                // to make this method self-contained.
                socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate(object s, SocketAsyncEventArgs e)
                {
                    response = e.SocketError.ToString();

                    // Unblock the UI thread
                    clientDone.Set();
                });

                // Add the data to be sent into the buffer
                byte[] payload = Encoding.UTF8.GetBytes(data);
                socketEventArg.SetBuffer(payload, 0, payload.Length);

                // Sets the state of the event to nonsignaled, causing threads to block
                clientDone.Reset();

                // Make an asynchronous Send request over the socket
                _socket.SendAsync(socketEventArg);

                // Block the UI thread for a maximum of TIMEOUT_MILLISECONDS milliseconds.
                // If no response comes back within this time then proceed
                clientDone.WaitOne(TIMEOUT_MILLISECONDS);
            }
            catch (ObjectDisposedException)
            {
                response = DISCONNECTED;
                Close();
            }

            return response;
        }

        /// <summary>
        /// Used to handle incoming messages from clients.
        /// Must be triggered before each message.
        /// </summary>
        /// <param name="d">A <see cref="SocketCallbackDelegate"/> that
        ///     is called when a message is received.</param>
        public void HandleIncomingMessages(SocketCallbackDelegate d)
        {
            // We are receiving over an established socket connection
            if (_socket != null)
            {
                // Create SocketAsyncEventArgs context object
                SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
                socketEventArg.RemoteEndPoint = _socket.RemoteEndPoint;

                // Setup the buffer to receive the data
                socketEventArg.SetBuffer(new Byte[MAX_BUFFER_SIZE], 0, MAX_BUFFER_SIZE);

                // Inline event handler for the Completed event.
                // Note: This even handler was implemented inline in order to make 
                // this method self-contained.
                socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(d);

                // Make an asynchronous Receive request over the socket
                _socket.ReceiveAsync(socketEventArg);
            }
        }

        /// <summary>
        /// Receive data from the server using the established socket connection
        /// </summary>
        /// <returns>The data received from the server,
        ///     ""</returns>
        public string Receive(bool waitForTimeout)
        {
            string response = waitForTimeout ? OPERATION_TIMEOUT : NO_MESSAGE;

            // We are receiving over an established socket connection
            if (_socket != null)
            {
                // Signaling object used to notify when an asynchronous operation is completed
                ManualResetEvent clientDone = new ManualResetEvent(false);
                // Create SocketAsyncEventArgs context object
                SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
                socketEventArg.RemoteEndPoint = _socket.RemoteEndPoint;

                // Setup the buffer to receive the data
                socketEventArg.SetBuffer(new Byte[MAX_BUFFER_SIZE], 0, MAX_BUFFER_SIZE);

                // Inline event handler for the Completed event.
                // Note: This even handler was implemented inline in order to make 
                // this method self-contained.
                socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate(object s, SocketAsyncEventArgs e)
                {
                    if (e.SocketError == SocketError.Success)
                    {
                        // Retrieve the data from the buffer
                        response = Encoding.UTF8.GetString(e.Buffer, e.Offset, e.BytesTransferred);
                        response = response.Trim('\0');
                    }
                    else
                    {
                        response = e.SocketError.ToString();
                    }

                    clientDone.Set();
                });

                // Sets the state of the event to nonsignaled, causing threads to block
                clientDone.Reset();

                // Make an asynchronous Receive request over the socket
                _socket.ReceiveAsync(socketEventArg);

                // Block the UI thread for a maximum of TIMEOUT_MILLISECONDS milliseconds.
                // If no response comes back within this time then proceed
                if (waitForTimeout)
                {
                    clientDone.WaitOne(TIMEOUT_MILLISECONDS);
                }
                else
                {
                    clientDone.WaitOne(200);
                }
            }
            else
            {
                response = SocketClient.UNINITIALIZED;
            }

            return response;
        }

        /// <summary>
        /// Closes the Socket connection and releases all associated resources
        /// </summary>
        public void Close()
        {
            if (ListenAccepter != null)
            {
                ListenAccepter.Close();
            }
            if (_socket != null)
            {
                FreeSocket(_socket);
                _socket = null;
            }
            if (ConnectedEvent != null)
            {
                ConnectedEvent = null;
            }
        }

        /// <summary>
        /// Check if there is data available to pull off the socket.
        /// Note: never tested.
        /// </summary>
        /// <returns>True if there is at least one byte available.</returns>
        public bool IsDataReady()
        {
            if (_socket == null ||
                _socket.Available == 0)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region public static methods

        /// <summary>
        /// Accept the connection from a client.
        /// </summary>
        /// <param name="ar">The result of the connection.</param>
        public void AcceptReceiveCallback(IAsyncResult ar)
        {
            if (ConnectedEvent != null)
            {
                ConnectedEvent(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Attempt to release a socket.
        /// </summary>
        /// <param name="_socket">The socket to be released.</param>
        public static void FreeSocket(Socket _socket)
        {
            try
            {
                try
                {
                    _socket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException e)
                {
                    // do something here?
                }
                try
                {
                    _socket.Close();
                }
                catch (SocketException e)
                {
                    // do something here?
                }
                try
                {
                    _socket.Disconnect(false);
                }
                catch (SocketException e)
                {
                    // do something here?
                }
            }
            catch (ObjectDisposedException) { }
            }

        /// <summary>
        /// Print out a stack trace to the debugger.
        /// </summary>
        public static void EchoStackTrace()
            {
                string prepend = "--";
                foreach (System.Diagnostics.StackFrame frame in (new System.Diagnostics.StackTrace()).GetFrames())
                {
                    System.Diagnostics.Debugger.Log(1, "", String.Format(
                        "{0} {1}:{3}:{2}\n", prepend, frame.GetFileLineNumber(), frame.GetMethod().ToString(), frame.GetFileName()));
                    prepend += " ";
                }
            }

        #endregion

        #region custom event args

        /// <summary>
        /// Used for the ConnectedEvent event handler
        /// </summary>
        public class ConnectedEventArgs : EventArgs
        {
            public Socket newSocket;

            public ConnectedEventArgs(Socket newSocket)
            {
                this.newSocket = newSocket;
            }
        }

        #endregion

        #region delegates

        public delegate void SocketCallbackDelegate(object s, System.Net.Sockets.SocketAsyncEventArgs e);

        #endregion

        #region classes

        /// <summary>
        /// Used to listen for incoming connections.
        /// </summary>
        private class Accepter {
            private Socket _socket;
            private EventHandler ConnectedEvent;

            public Accepter(Socket _socket, EventHandler ConnectedEvent)
            {
                this._socket = _socket;
                this.ConnectedEvent = ConnectedEvent;
            }

            /// <summary>
            /// Close the socket when the acceptor is destroyed.
            /// </summary>
            ~Accepter()
            {
                Close();
            }

            /// <summary>
            /// Close the socket.
            /// </summary>
            public void Close()
            {
                if (_socket != null) {
                    SocketClient.FreeSocket(_socket);
                    _socket = null;
                }
                ConnectedEvent = null;
            }

            /// <summary>
            /// Accepts the next incoming socket connection.
            /// </summary>
            public void AcceptNextAsync() {

                // get arguments ready for async accept
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                args.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptConnection);

                // Start an asynchronous socket to listen for connections
                if (_socket != null)
                {
                    try
                    {
                        _socket.AcceptAsync(args);
                    }
                    catch (ObjectDisposedException) { }
                }
            }

            /// <summary>
            /// Second half of AcceptNextAsync()
            /// </summary>
            /// <param name="sender">The sender</param>
            /// <param name="e">the event arguments</param>
            private void AcceptConnection(object sender, SocketAsyncEventArgs e)
            {
                // check for success
                if (e.SocketError == SocketError.Success)
                {
                    // apply connected event handler that was set for the SocketClient
                    if (ConnectedEvent != null)
                    {
                        Socket newSocket = e.AcceptSocket;
                        ConnectedEvent(this, new ConnectedEventArgs(newSocket));
                    }
                }
                else
                {
                    // so what was the error, exactly?
                    System.Diagnostics.Debugger.Log(1, "",
                        "-- error in accepting socket: " + e.SocketError + "\n");
                }

                // accept the next connection
                AcceptNextAsync();
            }
        }

        #endregion
    }
}
