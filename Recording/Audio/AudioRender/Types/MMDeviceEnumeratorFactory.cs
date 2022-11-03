using CoreAudioApi.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CoreAudioApi
{
    internal class MMDeviceEnumeratorFactory
    {
        public static IMMDeviceEnumerator CreateInstance()
        {
            var type = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
            return (IMMDeviceEnumerator)Activator.CreateInstance(type);
        }
    }
}
