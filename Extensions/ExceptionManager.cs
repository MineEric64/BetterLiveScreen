using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Extensions
{
    public static class ExceptionManager
    {
        private const string NAMESPACE = "BetterLiveScreen";

        private static string GetClassName(Exception exception) => exception.GetType().ToString();
        private static void GetStackTrace(Exception exception, ref StringBuilder message, bool needFileInfo)
        {
            var st = new StackTrace(exception, true);
            var frames = st.GetFrames();

            foreach (var frame in frames)
            {
                int lineNumber = frame.GetFileLineNumber();
                var method = frame.GetMethod();

                if (lineNumber < 1)
                    continue;

                message.AppendLine();
                message.Append("   at ");
                message.Append(method.ReflectedType.FullName);
                message.Append(".");
                message.Append(method.Name);
                message.Append("()");

                if (needFileInfo)
                {
                    message.Append(" in ");
                    message.Append(frame.GetFileName());
                    message.Append(":line ");
                    message.Append(lineNumber);
                }
            }
        }

        public static string ToCleanString(this Exception exception)
        {
            var message = new StringBuilder();

            message.Append(GetClassName(exception));
            message.Append(": ");
            message.Append(exception.Message);

            if (exception.InnerException != null)
            {
                message.Append(" ---> ");
                message.AppendLine(exception.InnerException.ToString());

                message.Append("--- End of inner exception stack trace ---");
            }

            if (exception.StackTrace != null)
            {
                GetStackTrace(exception, ref message, false);
            }

            return message.ToString();
        }
    }
}
