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
        Timeout,

        //Room
        RoomInfoRequested,
        RoomConnectRequested,

        RoomDisconnected,
        RoomDeleted,

        StreamStarted,
        StreamEnded,
        WatchStarted,
        WatchEnded,
        Video,
        Audio,
    }
}
