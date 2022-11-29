using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Extensions
{
    public class Timestamp
    {
        /// <summary>
        /// Get current utc timestamp (unix time)
        /// </summary>
        public static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public static long FromDateTime(DateTime date) => new DateTimeOffset(date).ToUnixTimeMilliseconds();
    }
}
