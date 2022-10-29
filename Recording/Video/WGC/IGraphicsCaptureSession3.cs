using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Recording.Video.WGC
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    [Guid("f2cdd966-22ae-5ea1-9596-3a289344c3be")]
    public interface IGraphicsCaptureSession3
    {
        bool IsBorderRequired { get; set; }
    }
}
