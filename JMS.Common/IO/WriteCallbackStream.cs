using Newtonsoft.Json.Linq;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace JMS.Common.IO
{
    public class WriteCallbackStream : Stream
    {
        public WriteCallback Callback { get; set; }

        public delegate void WriteCallback(byte[] buffer, int offset, int count);
        public WriteCallbackStream( )
        {
 
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => 0;

        public override long Position { get => 0; set { } }

      

        public override void Flush()
        {

        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Callback?.Invoke(buffer, offset, count);
        }

       
    }
}
