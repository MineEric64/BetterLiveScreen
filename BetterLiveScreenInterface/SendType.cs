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
        Created,
        Connected,
        Disconnected,

        StreamStarted,
        StreamEnded,
        WatchStarted,
        WatchEnded,
        Video,
        Audio,

        Requested,

        OK,
        Failed
    }
}
