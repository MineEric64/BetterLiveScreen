﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio;
using NAudio.CoreAudioApi;

using BetterLiveScreen.Recording.Audio.Wasapi;

using NAudioSessionManager = NAudio.CoreAudioApi.AudioSessionManager;
using WasapiCapture = BetterLiveScreen.Recording.Audio.Wasapi.WasapiCapture;

namespace BetterLiveScreen.Recording.Audio
{
    public class AudioSessionManagerV2
    {
        private static NAudioSessionManager _sessionManager = WasapiCapture.DefaultMMDevice?.AudioSessionManager;

        public static Process[] GetAllPlayingSessions()
        {
            string[] excludeTitleList = new string[] { "nvcontainer" };
            var sessionList = new List<int>();

            _sessionManager.RefreshSessions();

            if (_sessionManager.Sessions == null)
            {
                return new Process[] { };
            }

            for (int i = 0; i < _sessionManager.Sessions.Count; i++)
            {
                var session = _sessionManager.Sessions[i];
                
                if (session.AudioMeterInformation.MasterPeakValue > 0)
                {
                    sessionList.Add((int)session.GetProcessID);
                }
            }

            return sessionList.Select(x => Process.GetProcessById(x)).Where(x => !excludeTitleList.Contains(x.ProcessName)).ToArray();
        }
    }
}