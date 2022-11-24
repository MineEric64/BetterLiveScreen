using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using log4net;

using NAudio;
using NAudio.CoreAudioApi;

using BetterLiveScreen.Recording.Audio.Wasapi;

using NAudioSessionManager = NAudio.CoreAudioApi.AudioSessionManager;
using WasapiCapture = BetterLiveScreen.Recording.Audio.Wasapi.WasapiCapture;

namespace BetterLiveScreen.Recording.Audio
{
    public class AudioSessionManagerV2
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static NAudioSessionManager _sessionManager = WasapiCapture.DefaultMMDevice?.AudioSessionManager;

        public static Process[] GetAllPlayingSessions()
        {
            string[] excludeTitleList = new string[] { "nvcontainer" };
            var sessionList = new List<int>();
            int sessionCount = 0;

            try
            {
                _sessionManager?.RefreshSessions();

                if (_sessionManager?.Sessions == null)
                {
                    return new Process[] { };
                }

                sessionCount = _sessionManager?.Sessions?.Count ?? 0;
            }
            catch (Exception ex)
            {
                log.Error("Session Manager can't refresh the sessions. returned with the empty array.", ex);
            }

            for (int i = 0; i < sessionCount; i++)
            {
                var session = _sessionManager.Sessions?[i];

                if (session == null)
                {
                    break;
                }
                
                if (session.AudioMeterInformation.MasterPeakValue > 0)
                {
                    sessionList.Add((int)session.GetProcessID);
                }
            }

            if (sessionList.Count == 0)
            {
                return new Process[] { };
            }

            return sessionList.Select(x => Process.GetProcessById(x)).Where(x => !excludeTitleList.Contains(x.ProcessName)).ToArray();
        }
    }
}
