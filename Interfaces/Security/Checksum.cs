using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Interfaces.Security
{
    public class Checksum
    {
        public static byte ComputeAddition(byte[] data)
        {
            long longSum = data.Sum(x => (long)x);
            return unchecked((byte)longSum);
        }

        public static string ComputeMD5(byte[] inputData)
        {
            //convert byte array to stream
            MemoryStream stream = new MemoryStream();
            stream.Write(inputData, 0, inputData.Length);

            //important: get back to start of stream
            stream.Seek(0, SeekOrigin.Begin);

            //get a string value for the MD5 hash.
            using (var md5Instance = MD5.Create())
            {
                var hashResult = md5Instance.ComputeHash(stream);

                //***I did some formatting here, you may not want to remove the dashes, or use lower case depending on your application
                return BitConverter.ToString(hashResult).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
