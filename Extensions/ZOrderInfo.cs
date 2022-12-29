using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Extensions
{
    public class ZOrderInfo
    {
        public ProcessSimpleInfo Proc { get; set; }
        public IntPtr Hwnd { get; set; }
        public int ZOrder { get; set; }
        public User32.RECT Rect { get; set; }

        public ZOrderInfo(ProcessSimpleInfo proc, IntPtr hwnd, int zOrder, User32.RECT rect)
        {
            Proc = proc;
            Hwnd = hwnd;
            ZOrder = zOrder;
            Rect = rect;
        }

        public override string ToString()
        {
            return $"Process Name : {Proc.ProcessName}, Main Window Title : {Proc.MainWindowTitle}, Z-Order: {ZOrder}";
        }

        public static bool IsOverlap(ZOrderInfo me, ZOrderInfo compareTo)
        {
            return compareTo.Rect.Left <= me.Rect.Left && compareTo.Rect.Right >= me.Rect.Right && compareTo.Rect.Top <= me.Rect.Top && compareTo.Rect.Bottom >= me.Rect.Bottom;
        }

        public bool IsOverlap(ZOrderInfo compareTo)
        {
            return IsOverlap(this, compareTo);
        }
    }
}
