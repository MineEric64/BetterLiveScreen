using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Interfaces
{
    public enum SendTypes
    {
        None,

        //Before Connecting Room
        PeerConnected,

        //Room
        RoomInfoRequested,
        RoomConnectRequested,

        //User
        UserConnected,
        UserDisconnected,

        //Streaming
        StreamStarted,
        StreamChanged,
        StreamEnded,

        WatchStarted,
        WatchEnded,

        ThumbnailUpdated,
        ThumbnailUpdateRequested,

        //Buffer
        Video,
        Audio,
    }
}
