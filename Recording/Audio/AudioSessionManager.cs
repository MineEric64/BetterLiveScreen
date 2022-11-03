using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using CoreAudioApi;
using CoreAudioApi.Interfaces;

using NAudio.CoreAudioApi;

using BetterLiveScreen.Extensions;

namespace BetterLiveScreen.Recording.Audio
{
    public class AudioSessionManager
    {
        private static IMMDeviceEnumerator _pDeviceEnumerator;
        private static IMMDevice _pDevice;
        private static IAudioSessionManager2 _pAudioSessionManager2;
        private static IAudioSessionEnumerator _pAudioSessionEnumerator;

        public static void Initialize()
        {
            int result;
            Guid CLSID_MMDeviceEnumerator = typeof(MMDeviceEnumerator).GUID;
            Guid IID_IMMDeviceEnumerator = typeof(IMMDeviceEnumerator).GUID;
            Guid IID_IAudioSessionManager2 = typeof(IAudioSessionManager).GUID;

            result = Ole32.CoInitializeEx(IntPtr.Zero, Ole32.COINIT.COINIT_APARTMENTTHREADED);
            Marshal.ThrowExceptionForHR(result);

            _pDeviceEnumerator = MMDeviceEnumeratorFactory.CreateInstance();
            result = _pDeviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole, out _pDevice);
            Marshal.ThrowExceptionForHR(result);

            result = _pDevice.Activate(ref IID_IAudioSessionManager2, CLSCTX.ALL, IntPtr.Zero, out var ppv2);
            Marshal.ThrowExceptionForHR(result);

            _pAudioSessionManager2 = (IAudioSessionManager2)ppv2;
            result = _pAudioSessionManager2.GetSessionEnumerator(out _pAudioSessionEnumerator);
            Marshal.ThrowExceptionForHR(result);
        }

        public static Process[] GetAllPlayingSessions()
        {
            string[] excludeTitleList = new string[] { "nvcontainer" };
            var sessionList = new List<int>();
            int result;

            result = _pAudioSessionEnumerator.GetCount(out var sessionCount);
            Marshal.ThrowExceptionForHR(result);

            for (int i = 0; i < sessionCount; i++)
            {
                result = _pAudioSessionEnumerator.GetSession(i, out var session);
                if (HRESULT.FAILED(result)) continue;

                IAudioMeterInformation pMeterInformation = (IAudioMeterInformation)session;
                result = pMeterInformation.GetPeakValue(out var peak);
                Marshal.ThrowExceptionForHR(result);

                if (peak > 0)
                {
                    result = session.GetProcessId(out var id);
                    Marshal.ThrowExceptionForHR(result);

                    sessionList.Add(id);
                }
            }

            return sessionList.Select(x => Process.GetProcessById(x)).Where(x => !excludeTitleList.Contains(x.ProcessName)).ToArray();
        }

        public static void Close()
        {
            Ole32.CoUninitialize();
        }
    }
}
