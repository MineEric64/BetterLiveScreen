using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Extensions
{
    public static class DictionaryHelper
    {
        public static T1 GetKeyByValue<T1, T2>(this Dictionary<T1, T2> map, T2 value)
        {
            return map.FirstOrDefault(x => x.Value.Equals(value)).Key;
        }

        public static T1 GetKeyByValue<T1, T2>(this ConcurrentDictionary<T1, T2> map, T2 value)
        {
            return map.FirstOrDefault(x => x.Value.Equals(value)).Key;
        }

        public static bool ContainsValue<T1, T2>(this ConcurrentDictionary<T1, T2> map, T2 value)
        {
            return map.Where(x => x.Value.Equals(value)).Any();
        }

        public static void Swap<T1, T2>(this Dictionary<T1, T2> map, T1 a, T1 b)
        {
            (map[a], map[b]) = (map[b], map[a]);
        }
    }
}
