using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MessagePack;

namespace BetterLiveScreen.Interfaces
{
    [MessagePackObject]
    public class ReceiveInfo
    {
        [Key(0)]
        public SendTypes SendType { get; set; }
        [Key(1)]
        public int Step { get; set; }
        [Key(2)]
        public byte[] Buffer { get; set; }
        [Key(3)]
        public byte[] ExtraBuffer { get; set; }

        [IgnoreMember]
        public static ReceiveInfo Empty => new ReceiveInfo(SendTypes.None, 0, new byte[0]);

        public ReceiveInfo(SendTypes sendType, int step, byte[] buffer, byte[] extraBuffer = null)
        {
            SendType = sendType;
            Step = step;
            Buffer = buffer;
            ExtraBuffer = extraBuffer;
        }
    }
}
