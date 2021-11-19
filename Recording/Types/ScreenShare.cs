using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Recording.Types
{
    public class ScreenShare
    {
        public byte[] Screen { get; }
        public byte[] Audio { get; }
        public string FullUserName { get; }

        public ScreenShare(byte[] screen, byte[] audio, string fullUserName)
        {
            Screen = screen;
            Audio = audio;
            FullUserName = fullUserName;
        }
    }
}
