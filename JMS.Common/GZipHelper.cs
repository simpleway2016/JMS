using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace JMS.Common
{
    public class GZipHelper
    {
        public static byte[] Compress(byte[] data)
        {
            if (data.Length == 0)
                return data;
            using (var compressedStream = new MemoryStream())
            {
                using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress))
                {
                    zipStream.Write(data, 0, data.Length);
                    zipStream.Close();
                    return compressedStream.ToArray();
                }
            }
        }
        public static byte[] Decompress(byte[] data)
        {
            if (data.Length == 0)
                return data;
            using (var compressedStream = new MemoryStream(data))
            {
                using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                {
                    using (var resultStream = new MemoryStream())
                    {
                        zipStream.CopyTo(resultStream);
                        zipStream.Close();
                        return resultStream.ToArray();
                    }
                }
            }
        }
    }
}
