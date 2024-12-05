using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

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
        public static byte[] Compress(byte[] data, int offset, int count)
        {
            if (data.Length == 0)
                return data;
            using (var compressedStream = new MemoryStream())
            {
                using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress))
                {
                    zipStream.Write(data, offset, count);
                    zipStream.Close();
                    return compressedStream.ToArray();
                }
            }
        }
        public static void Compress(byte[] data, int offset, int count, Stream outputStream)
        {
            if (data.Length == 0)
                return;

            using (var zipStream = new GZipStream(outputStream, CompressionMode.Compress , true))
            {
                zipStream.Write(data, offset, count);
                zipStream.Close();
            }
        }
        public static async Task CompressAsync(byte[] data,int offset,int count,Stream outputStream)
        {
            if (data.Length == 0)
                return;

            using (var zipStream = new GZipStream(outputStream, CompressionMode.Compress, true))
            {
                await zipStream.WriteAsync(data, offset, count);
                zipStream.Close();
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
        public static byte[] Decompress(byte[] data,int offset,int count)
        {
            if (data.Length == 0)
                return data;
            using (var compressedStream = new MemoryStream(data,offset,count))
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
