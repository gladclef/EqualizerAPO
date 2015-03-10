﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace equalizerapo_and_zune
{
    public class MessageParser
    {
        #region fields/properties

        public enum MESSAGE_TYPE
        {
            TRACK_CHANGED, FILTERS_GAIN, FILTER_REMOVED,
            FILTER_ADDED, PLAY, PAUSE, VOLUME_CHANGED
        };
        private equalizerapo_api eqAPI;
        private ZuneAPI zuneAPI;

        #endregion

        #region public methods

        public MessageParser(equalizerapo_api eq, ZuneAPI zune)
        {
            eqAPI = eq;
            zuneAPI = zune;
        }

        public void ParseMessage(string message)
        {
            // get the message parts
            string[] messageParts = message.Split(new char[] { ':' }, 2);
            string messageType = messageParts[0];
            string restOfMessage = messageParts[1];

            // parse the message
            switch (messageType)
            {
                case "filter":
                    break;
                case "filters":
                    break;
                case "playback":
                    break;
                case "volume":
                    break;
                case "track_changed":
                    foreach (string nextMessage in restOfMessage.Split(new char[] { ';' }))
                    {
                        ParseMessage(nextMessage);
                    }
                    break;
            }
        }

        #endregion

        #region private methods

        public string CreateMessage(MESSAGE_TYPE type)
        {
            StringBuilder sb = new StringBuilder();

            switch (type)
            {
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
                        sb.Append(filter.Gain.ToString());
                        if (first)
                        {
                            first = false;
                        }
                        else
                        {
                            sb.Append(",");
                        }
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
