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

        //Room
        RoomInfoRequested,
        RoomConnectRequested,

        RoomDeleted,

        //User
        UserConnected,
        UserDisconnected,

        //Streaming
        StreamStarted,
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
