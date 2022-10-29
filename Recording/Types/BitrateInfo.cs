using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Recording.Types
{
    public class BitrateInfo
    {
        public int MbpsMin { get; private set; }
        public int MbpsAverage { get; private set; }
        public int MbpsMax { get; private set; }

        public BitrateInfo(int mbpsMin, int mbpsMax)
        {
            MbpsMin = mbpsMin;
            MbpsMax = mbpsMax;
            MbpsAverage = (mbpsMin + mbpsMax) / 2;
        }

        public BitrateInfo(int mbpsMin, int mbpsAverage, int mbpsMax)
        {
            MbpsMin = mbpsMin;
            MbpsAverage = mbpsAverage;
            MbpsMax = mbpsMax;
        }

        public static int GetBitrateFromMbps(int mbps)
        {
            return mbps * 1000000;
        }
    }
}
