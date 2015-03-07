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
        const int TIMEOUT_MILLISECONDS = 5000;

        // The maximum size of the data buffer to use with the asynchronous socket methods
        const int MAX_BUFFER_SIZE = 2048;

        // send/receive message constants
        public const string OPERATION_TIMEOUT = "Operation Timeout";
        public const string UNINITIALIZED = "Socket is not initialized";
        public const string SUCCESS = "Success";
        public const string KEEP_ALIVE = "Keep alive check";
        public const string KEEP_ALIVE_ACK = "Keep alive acknowledged";
        public const string NO_MESSAGE = "No message available";

        #endregion

        #region fields

        // Cached Socket object that will be used by each call for the lifetime of this class
        Socket _socket = null;

        private Accepter ListenAccepter;

        #endregion

        #region event handlers

        public EventHandler ConnectedEvent;

        #endregion

        #region public methods

        ~SocketClient()
        {
            Close();
        }

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
            if (_socket != null)
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
            else
            {
                response = UNINITIALIZED;
            }

            return response;
        }

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

        public void AcceptReceiveCallback(IAsyncResult ar)
        {
            if (ConnectedEvent != null)
            {
                ConnectedEvent(this, EventArgs.Empty);
            }
        }

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

        private class Accepter {
            private Socket _socket;
            private EventHandler ConnectedEvent;

            public Accepter(Socket _socket, EventHandler ConnectedEvent)
            {
                this._socket = _socket;
                this.ConnectedEvent = ConnectedEvent;
            }

            ~Accepter()
            {
                Close();
            }

            public void Close()
            {
                if (_socket != null) {
                    SocketClient.FreeSocket(_socket);
                    _socket = null;
                }
                ConnectedEvent = null;
            }

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

            private void AcceptConnection(object sender, SocketAsyncEventArgs e)
            {
                // check for success
                if (e.SocketError == SocketError.Success)
                {
                    System.Diagnostics.Debugger.Log(1, "", ".." +
                        "ConnectSocket:" + (e.ConnectSocket == null ? "null" : "not null") +
                        " AcceptSocket:" + (e.AcceptSocket == null ? "null" : "not null") +
                        " AcceptIsSame:" + (e.AcceptSocket == _socket ? "yes" : "no") +
                        "\n");
                    System.Diagnostics.Debugger.Log(1, "", "connection in SocketClient [" + e.SocketError + "]\n");

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
