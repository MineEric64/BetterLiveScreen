using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Extensions
{
    public class Debugger
    {
        private static Stopwatch _sw = new Stopwatch();

        public static void Start()
        {
            _sw.Start();
        }

        public static void Stop()
        {
            _sw.Stop();
            Debug.WriteLine($"{_sw.ElapsedMilliseconds}ms elapsed.");

            _sw.Reset();
        }
    }
}
