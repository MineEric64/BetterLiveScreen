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
        public int Step { get; set; } = 0;
        [Key(2)]
        public byte[] Buffer { get; set; } = new byte[0];
        [Key(3)]
        public byte[] ExtraBuffer { get; set; } = new byte[0];

        [IgnoreMember]
        public static ReceiveInfo Empty => new ReceiveInfo(SendTypes.None);

        [IgnoreMember]
        public static ReceiveInfo OK => new ReceiveInfo(SendTypes.OK);

        /// <summary>
        /// MessagePack을 위한 LZ4 압축 옵션
        /// </summary>
        [IgnoreMember]
        internal static MessagePackSerializerOptions LZ4_OPTIONS => MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

        public ReceiveInfo(SendTypes sendType)
        {
            SendType = sendType;
        }

        public ReceiveInfo(SendTypes sendType, int step) : this(sendType)
        {
            Step = step;
        }

        public ReceiveInfo(SendTypes sendType, byte[] buffer) : this(sendType)
        {
            Buffer = buffer;
        }

        public ReceiveInfo(SendTypes sendType, byte[] buffer, byte[] extraBuffer) : this(sendType)
        {
            Buffer = buffer;
            ExtraBuffer = extraBuffer;
        }

        public ReceiveInfo(SendTypes sendType, int step, byte[] buffer)
        {
            SendType = sendType;
            Step = step;
            Buffer = buffer;
        }

        public ReceiveInfo(SendTypes sendType, int step, byte[] buffer, byte[] extraBuffer) : this(sendType, step, buffer)
        {
            ExtraBuffer = extraBuffer;
        }

        public static ReceiveInfo GetFailed(string message = "")
        {
            byte[] buffer = !string.IsNullOrEmpty(message) ? MessagePackSerializer.Serialize(message, LZ4_OPTIONS) : new byte[0];
            return new ReceiveInfo(SendTypes.Failed, 0, buffer);
        }
    }
}
