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

        private Thread zuneThread;
        private Thread connectThread;

        #endregion

        #region properties

        public static ZuneAPI Instance { get; private set; }
        public static bool IsZuneReady { get; private set; }
        public static bool IsConnectReady { get; private set; }
        public Track CurrentTrack { get; private set; }

        #endregion

        #region event handlers

        public EventHandler TrackChanged { get; set; }
        public EventHandler PlaybackChanged { get; set; }

        #endregion

        #region public methods

        public ZuneAPI()
        {
            IsZuneReady = IsConnectReady = false;

            CurrentTrack = new Track();

            ZuneAPI.Instance = this;
        }

        ~ZuneAPI()
        {
            Close();
        }

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

        public string GetTrackArtist()
        {
            if (CurrentTrack == null)
            {
                return "";
            }
            return CurrentTrack.Artist;
        }

        public string GetTrackName()
        {
            if (CurrentTrack == null)
            {
                return "";
            }
            return CurrentTrack.Title;
        }

        public bool IsPlaying()
        {
            return TransportControls.Instance.Playing;
        }

        #endregion

        #region private methods

        private void ZuneThreadStarter()
        {
            ZuneApplication.Launch(null, IntPtr.Zero);
        }

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

        private void BindEvents(object sender)
        {
            TransportControls.Instance.PropertyChanged += new PropertyChangedEventHandler(TransportPropertyChanged);
        }

        #endregion
    }
}
