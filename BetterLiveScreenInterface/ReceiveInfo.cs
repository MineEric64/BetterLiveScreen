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
        public ResponseCodes ResponseCode { get; set; } = ResponseCodes.None;
        [Key(2)]
        public BufferTypes BufferType { get; set; } = BufferTypes.None;
        [Key(3)]
        public int Step { get; set; } = 0;
        [Key(4)]
        public int MaxStep { get; set; } = 0;
        [Key(5)]
        public byte[] Buffer { get; set; } = new byte[0];
        [Key(6)]
        public byte[] ExtraBuffer { get; set; } = new byte[0];

        [IgnoreMember]
        public static ReceiveInfo Empty => new ReceiveInfo(SendTypes.None);

        /// <summary>
        /// MessagePack을 위한 LZ4 압축 옵션
        /// </summary>
        [IgnoreMember]
        internal static MessagePackSerializerOptions LZ4_OPTIONS => MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

        public ReceiveInfo(SendTypes sendType)
        {
            SendType = sendType;
        }

        public ReceiveInfo(SendTypes sendType, ResponseCodes responseCode) : this(sendType)
        {
            ResponseCode = responseCode;
        }

        public ReceiveInfo(SendTypes sendType, byte[] buffer, BufferTypes bufferType) : this(sendType)
        {
            Buffer = buffer;
            BufferType = bufferType;
        }

        public ReceiveInfo(SendTypes sendType, ResponseCodes responseCode, byte[] buffer, BufferTypes bufferType) : this(sendType, buffer, bufferType)
        {
            ResponseCode = responseCode;
        }

        public ReceiveInfo(SendTypes sendType, byte[] buffer, BufferTypes bufferType, byte[] extraBuffer) : this(sendType, buffer, bufferType)
        {
            ExtraBuffer = extraBuffer;
        }

        public ReceiveInfo(SendTypes sendType, ResponseCodes responseCodes, byte[] buffer, BufferTypes bufferType, byte[] extraBuffer) : this(sendType, buffer, bufferType, extraBuffer)
        {
            ResponseCode = ResponseCode;
        }

        public ReceiveInfo(SendTypes sendType, int step, int maxStep, byte[] buffer, BufferTypes bufferType) : this(sendType, buffer, bufferType)
        {
            Step = step;
            MaxStep = maxStep;
        }

        public ReceiveInfo(SendTypes sendType, ResponseCodes responseCode, int step, int maxStep, byte[] buffer, BufferTypes bufferType) : this(sendType, step, maxStep, buffer, bufferType)
        {
            ResponseCode = responseCode;
        }

        public ReceiveInfo(SendTypes sendType, int step, int maxStep, byte[] buffer, BufferTypes bufferType, byte[] extraBuffer) : this(sendType, step, maxStep, buffer, bufferType)
        {
            ExtraBuffer = extraBuffer;
        }

        public ReceiveInfo(SendTypes sendType, ResponseCodes responseCode, int step, int maxStep, byte[] buffer, BufferTypes bufferType, byte[] extraBuffer) : this(sendType, step, maxStep, buffer, bufferType, extraBuffer)
        {
            ResponseCode = responseCode;
        }

        public ReceiveInfo GetOK()
        {
            return new ReceiveInfo(SendType, ResponseCodes.OK);
        }

        public ReceiveInfo GetOK(byte[] buffer, BufferTypes bufferType)
        {
            return new ReceiveInfo(SendType, ResponseCodes.OK, buffer, bufferType);
        }

        public ReceiveInfo GetFailed(ResponseCodes responseCode)
        {
            return new ReceiveInfo(SendType, responseCode);
        }
    }
}
