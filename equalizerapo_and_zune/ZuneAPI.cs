using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Zune.Shell;
using System.Threading;
using ZuneUI;
using MicrosoftZunePlayback;
using System.ComponentModel;
using Microsoft.Iris;

namespace equalizerapo_and_zune
{
    /// <summary>
    /// Used to communicate with the Zune software.
    /// Based VERY heavily on Alexander Vos' ZuneLCD project. I almost just gutted his project for the parts that I needed.
    /// <see cref="http://zunelcd.codeplex.com/"/>
    /// </summary>
    public class ZuneAPI
    {
        #region fields

        /// <summary>
        /// The thread that handles the initialization of the Zune application.
        /// </summary>
        private Thread zuneThread;

        /// <summary>
        /// The thread that handles connections to the Zune API.
        /// </summary>
        private Thread connectThread;

        #endregion

        #region properties

        /// <summary>
        /// Instance of the this class (singleton pattern)
        /// </summary>
        public static ZuneAPI Instance { get; private set; }
        
        /// <summary>
        /// State of the Zune player.
        /// </summary>
        public static bool IsZuneReady { get; private set; }

        /// <summary>
        /// State of the connection to the Zune API.
        /// </summary>
        public static bool IsConnectReady { get; private set; }

        /// <summary>
        /// Reference to the currently playing track.
        /// </summary>
        public Track CurrentTrack { get; private set; }

        #endregion

        #region event handlers

        /// <summary>
        /// Triggers whenever a track changed event is received from the Zune player.
        /// </summary>
        public EventHandler TrackChanged { get; set; }

        /// <summary>
        /// Triggers whenever the playback status is changed by the Zune player.
        /// </summary>
        public EventHandler PlaybackChanged { get; set; }

        #endregion

        #region public methods

        /// <summary>
        /// Create a new instance of the ZuneAPI class.
        /// </summary>
        public ZuneAPI()
        {
            IsZuneReady = IsConnectReady = false;

            CurrentTrack = new Track();

            ZuneAPI.Instance = this;
        }

        /// <summary>
        /// When the ZuneAPI object is destroyed, close the Zune Appliction threads.
        /// </summary>
        ~ZuneAPI()
        {
            Close();
        }

        /// <summary>
        /// Create threads to <see cref="zuneThread"/> and the <see cref="connectThread"/>,
        /// if they haven't been created already.
        /// </summary>
        /// <returns>True for creation, false if created already.</returns>
        public bool Init() {
            if (IsZuneReady || IsConnectReady)
            {
                return false;
            }
            
            // start the zune thread
            zuneThread = new Thread(new ThreadStart(ZuneThreadStarter));
            zuneThread.Start();

            connectThread = new Thread(new ThreadStart(ConnectThreadStarter));
            connectThread.Start();

            return true;
        }

        /// <summary>
        /// Close the Zune Application threads.
        /// </summary>
        public void Close()
        {
            if (zuneThread != null && zuneThread.IsAlive)
            {
                zuneThread.Abort();
                zuneThread.Interrupt();
            }
            if (connectThread != null && connectThread.IsAlive)
            {
                connectThread.Abort();
                connectThread.Interrupt();
            }
        }

        /// <summary>
        /// Causes the Zune player to start playing the selected track,
        /// which in turn triggers the <see cref="PlaybackChanged"/> event handler.
        /// </summary>
        public void PlayTrack()
        {
            Application.DeferredInvoke(
                new DeferredInvokeHandler(delegate(object sender)
                {
                    if (TransportControls.Instance.Play.Available)
                        TransportControls.Instance.Play.Invoke(InvokePolicy.AsynchronousNormal);
                }),
                DeferredInvokePriority.Normal);
        }

        /// <summary>
        /// Causes the Zune player to pause the selected track,
        /// which in turn triggers the <see cref="PlaybackChanged"/> event handler.
        /// </summary>
        public void PauseTrack()
        {
            Application.DeferredInvoke(
                new DeferredInvokeHandler(delegate(object sender)
                {
                    if (TransportControls.Instance.Pause.Available)
                        TransportControls.Instance.Pause.Invoke(InvokePolicy.AsynchronousNormal);
                }),
                DeferredInvokePriority.Normal);
        }

        /// <summary>
        /// Causes the Zune player to skip to the next track,
        /// which in turn triggers the <see cref="TrackChanged"/> event handler.
        /// </summary>
        public void ToNextTrack()
        {
            Application.DeferredInvoke(
                new DeferredInvokeHandler(delegate(object sender)
                {
                    if (TransportControls.Instance.Forward.Available)
                        TransportControls.Instance.Forward.Invoke(InvokePolicy.AsynchronousNormal);
                }),
                DeferredInvokePriority.Normal);
        }

        /// <summary>
        /// Causes the Zune player to skip to the previous track (or start the current track over),
        /// which in turn triggers the <see cref="TrackChanged"/> event handler.
        /// </summary>
        public void ToPreviousTrack()
        {
            Application.DeferredInvoke(
                new DeferredInvokeHandler(delegate(object sender)
                {
                    if (TransportControls.Instance.Back.Available)
                        TransportControls.Instance.Back.Invoke(InvokePolicy.AsynchronousNormal);
                }),
                DeferredInvokePriority.Normal);
        }

        /// <summary>
        /// Get the artist name of the currently playing track.
        /// </summary>
        /// <returns>The artist name.</returns>
        public string GetTrackArtist()
        {
            if (CurrentTrack == null)
            {
                return "";
            }
            return CurrentTrack.Artist;
        }

        /// <summary>
        /// Get the track name of the currently playing track.
        /// </summary>
        /// <returns>The track name.</returns>
        public string GetTrackName()
        {
            if (CurrentTrack == null)
            {
                return "";
            }
            return CurrentTrack.Title;
        }

        /// <summary>
        /// Get the playback status from the Zune Player.
        /// </summary>
        /// <returns>True if playing.</returns>
        public bool IsPlaying()
        {
            return TransportControls.Instance.Playing;
        }

        #endregion

        #region private methods

        /// <summary>
        /// Start the Zune application.
        /// </summary>
        private void ZuneThreadStarter()
        {
            ZuneApplication.Launch(null, IntPtr.Zero);
        }

        /// <summary>
        /// Connect to the Zune application through the API.
        /// </summary>
        private void ConnectThreadStarter()
        {
            // wait for zune to finish loading
            while (ZuneShell.DefaultInstance == null || PlayerInterop.Instance == null)
            {
                Thread.Sleep(100);
            }

            // bind events
            Application.DeferredInvoke(
                new DeferredInvokeHandler(BindEvents),
                DeferredInvokePriority.Normal);
        }

        /// <summary>
        /// Triggered by playback/track changed events in the Zune player.
        /// Calls the <see cref="TrackChanged"/> or <see cref="PlaybackChanged"/> event handlers.
        /// </summary>
        /// <param name="sender">The Zune application</param>
        /// <param name="e">The event arguments.</param>
        private void TransportPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("CurrentTrack")) // the current track has changed
            {
                if (TrackChanged != null)
                {
                    if (TransportControls.Instance.CurrentTrackIndex >= 0)
                    {
                        CurrentTrack = new Track(TransportControls.Instance.CurrentTrack);
                    }
                    else
                    {
                        CurrentTrack = new Track();
                    }
                    TrackChanged(this, EventArgs.Empty);
                }
            }
            else if (e.PropertyName.Equals("Playing"))
            {
                if (PlaybackChanged != null)
                {
                    PlaybackChanged(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Binds the <see cref="TransportPropertyChanged"/> to the Zune application.
        /// </summary>
        /// <param name="sender">this</param>
        private void BindEvents(object sender)
        {
            TransportControls.Instance.PropertyChanged += new PropertyChangedEventHandler(TransportPropertyChanged);
        }

        #endregion
    }
}
