using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace equalizerapo_and_zune
{
    /// <summary>
    /// Parses incoming messages from clients and
    /// prepares outgoing messages for clients.
    /// </summary>
    public class MessageParser
    {
        #region fields/properties

        /// <summary>
        /// The type of message to be created.
        /// <seealso cref="CreateMessage"/>
        /// </summary>
        public enum MESSAGE_TYPE
        {
            TRACK_CHANGED, FILTERS_GAIN, FILTER_REMOVED,
            FILTER_ADDED, PLAY, PAUSE, VOLUME_CHANGED,
            FILTER_APPLY
        };
        
        /// <summary>
        /// A reference to the equalizer object to call methods upon or get values from.
        /// </summary>
        private equalizerapo_api eqAPI;
        
        /// <summary>
        /// A reference to the zune object to call methods upon or get values from.
        /// </summary>
        private ZuneAPI zuneAPI;

        #endregion

        #region public methods

        /// <summary>
        /// Create a new instance of the MessageParser object with the
        /// given references.
        /// </summary>
        /// <param name="eq">Reference to the <see cref="eqAPI"/> object.</param>
        /// <param name="zune">Reference to the <see cref="zuneAPI"/> object.</param>
        public MessageParser(equalizerapo_api eq, ZuneAPI zune)
        {
            eqAPI = eq;
            zuneAPI = zune;
        }

        /// <summary>
        /// Given a message from the client, parse the message to enact changes
        /// in the equalizer or zune instances.
        /// </summary>
        /// <param name="message">The message to be parsed.</param>
        public void ParseMessage(string message)
        {
            // get the message parts
            string[] messageParts = message.Split(new char[] { ':' }, 2);
            if (messageParts.Length < 2)
            {
                return;
            }
            string messageType = messageParts[0];
            string restOfMessage = messageParts[1];

            // parse the message
            switch (messageType)
            {
                case "apply_filter":
                    eqAPI.ApplyEqualizer((restOfMessage == "true")
                        ? true
                        : false);
                    break;
                case "filter":
                    if (restOfMessage == "added")
                    {
                        eqAPI.AddFilter();
                    }
                    else if (restOfMessage == "removed")
                    {
                        eqAPI.RemoveFilter();
                    }
                    break;
                case "filters":
                    eqAPI.SetNewGainValues(restOfMessage.Split(new char[] { ',' }));
                    break;
                case "playback":
                    switch (restOfMessage)
                    {
                        case "pause":
                            zuneAPI.PauseTrack();
                            break;
                        case "play":
                            zuneAPI.PlayTrack();
                            break;
                        case "previous":
                            zuneAPI.ToPreviousTrack();
                            break;
                        case "next":
                            zuneAPI.ToNextTrack();
                            break;
                    }
                    break;
                case "volume":
                    eqAPI.ChangePreamp(
                        Convert.ToInt32(
                            Convert.ToDouble(restOfMessage)));
                    break;
            }
        }

        /// <summary>
        /// Create a message to be passed to the client.
        /// </summary>
        /// <param name="type">The type of message to be passed.</param>
        /// <returns>Said message.</returns>
        public string CreateMessage(MESSAGE_TYPE type)
        {
            StringBuilder sb = new StringBuilder();

            switch (type)
            {
                case MESSAGE_TYPE.FILTER_APPLY:
                    sb.Append("apply_filter:");
                    sb.Append(eqAPI.IsEqualizerApplied() ? "true" : "false");
                    break;
                case MESSAGE_TYPE.FILTER_REMOVED:
                    sb.Append("filter:removed");
                    break;
                case MESSAGE_TYPE.FILTER_ADDED:
                    sb.Append("filter:added");
                    break;
                case MESSAGE_TYPE.FILTERS_GAIN:
                    sb.Append("filters:");

                    // get the gains on the filters
                    bool first = true;
                    foreach (KeyValuePair<double,Filter> pair in eqAPI.GetFilters())
                    {
                        Filter filter = pair.Value;
                        if (first)
                        {
                            first = false;
                        }
                        else
                        {
                            sb.Append(",");
                        }
                        sb.Append(filter.Gain.ToString());
                    }
                    break;
                case MESSAGE_TYPE.PAUSE:
                    sb.Append("playback:pause");
                    break;
                case MESSAGE_TYPE.PLAY:
                    sb.Append("playback:play");
                    break;
                case MESSAGE_TYPE.VOLUME_CHANGED:
                    sb.Append("volume:");
                    sb.Append(eqAPI.GetPreAmp());
                    break;
                case MESSAGE_TYPE.TRACK_CHANGED:
                    sb.Append("track_changed:artist:");
                    sb.Append(zuneAPI.CurrentTrack.Artist);
                    sb.Append(";trackname:");
                    sb.Append(zuneAPI.CurrentTrack.Title);
                    sb.Append(";");
                    sb.Append(zuneAPI.IsPlaying() ? CreateMessage(MESSAGE_TYPE.PLAY) : CreateMessage(MESSAGE_TYPE.PAUSE));
                    sb.Append(";");
                    sb.Append(CreateMessage(MESSAGE_TYPE.VOLUME_CHANGED));
                    sb.Append(";");
                    sb.Append(CreateMessage(MESSAGE_TYPE.FILTERS_GAIN));
                    break;
            }

            return sb.ToString();
        }

        #endregion
    }
}
