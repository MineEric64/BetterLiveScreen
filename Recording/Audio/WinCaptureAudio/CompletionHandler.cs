using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wasapi.CoreAudioApi.Interfaces;

using BetterLiveScreen.Extensions;
using System.Threading;

namespace BetterLiveScreen.Recording.Audio.WinCaptureAudio
{
    [StructLayout(LayoutKind.Sequential)]
    public class CompletionHandler : IActivateAudioInterfaceCompletionHandler, IAgileObject
    {
        public IAudioClient client;

        public int hr = HRESULT.E_FAIL;
        public IntPtr eventFinished;

        public CompletionHandler()
        {
            eventFinished = Kernel32.CreateEvent(IntPtr.Zero, false, false, null);
        }

        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation)
        {
            operation.GetActivateResult(out hr, out var obj);
            client = (IAudioClient)obj;
            Kernel32.SetEvent(eventFinished);
        }
    }
}
