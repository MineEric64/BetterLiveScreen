using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Extensions
{
    public class ZOrderHelper
    {
        private static List<ZOrderInfo> _zOrders = new List<ZOrderInfo>();

        public static ReadOnlyCollection<ZOrderInfo> ZOrders => _zOrders.AsReadOnly();

        /// <summary>
        /// Gets the z-order for one or more windows atomically with respect to each other. In Windows, smaller z-order is higher. If the window is not top level, the z order is returned as -1. 
        /// </summary>
        private static int[] GetZOrder(IntPtr[] hWnds)
        {
            var z = new int[hWnds.Length];
            for (var i = 0; i < hWnds.Length; i++) z[i] = -1;

            var index = 0;
            var numRemaining = hWnds.Length;

            User32.EnumWindows((wnd, param) =>
            {
                var searchIndex = Array.IndexOf(hWnds, wnd);
                if (searchIndex != -1)
                {
                    z[searchIndex] = index;
                    numRemaining--;
                    if (numRemaining == 0) return false;
                }
                index++;
                return true;
            }, 0);

            return z;
        }

        private static int GetZOrder(IntPtr hWnd)
        {
            const uint GW_HWNDPREV = 3;
            //const uint GW_HWNDLAST = 1;

            var z = 0;
            for (var h = hWnd; h != IntPtr.Zero; h = User32.GetWindow(h, GW_HWNDPREV)) z++;

            return z;
        }

        public static void Refresh()
        {
            ProcessSimpleInfo[] processes = ProcessHelper.GetValidProcessesV2();
            IntPtr[] hWnds = processes.Select(x => x.MainWindowHandle).ToArray();
            int[] zOrders = GetZOrder(hWnds);

            _zOrders.Clear();

            for (int i = 0; i < processes.Length; i++)
            {
                bool captured = User32.GetWindowRect(hWnds[i], out var rect);
                if (zOrders[i] == -1) zOrders[i] = GetZOrder(hWnds[i]);

                _zOrders.Add(new ZOrderInfo(processes[i], hWnds[i], zOrders[i], rect));
            }

            _zOrders = _zOrders.OrderBy(x => x.ZOrder).ToList();
        }

        /// <summary>
        /// Better Live Screen보다 Z-Order가 우선인 프로세스들을 가져옵니다.
        /// </summary>
        /// <returns></returns>
        public static ReadOnlyCollection<ZOrderInfo> GetTopZOrders(out ZOrderInfo me)
        {
            me = _zOrders.Where(x => x.Proc.ProcessName == "BetterLiveScreen").FirstOrDefault();

            if (me == default)
            {
                //???
                return default;
            }

            int myZOrder = me.ZOrder;
            return _zOrders.Where(x => x.ZOrder < myZOrder && x.Proc.ProcessName != "BetterLiveScreen").ToList().AsReadOnly();
        }

        /// <summary>
        /// Better Live Screen 프로그램이 다른 프로그램에 의해 겹쳐 있는가?
        /// </summary>
        /// <returns></returns>
        public static bool IsOverlapped()
        {
            var orders = GetTopZOrders(out var me);

            foreach (var info in orders)
            {
                if (me.IsOverlap(info)) return true;
            }

            return false;
        }
    }
}
