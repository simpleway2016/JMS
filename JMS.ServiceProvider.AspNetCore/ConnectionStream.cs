using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JMS.ServiceProvider.AspNetCore
{
    internal class ConnectionStream:Stream
    {
        Stream _input;
        Stream _output;
        public ConnectionStream(HttpContext context)
        {
            //var connectionSocketFeature = context.Features.Get<IConnectionSocketFeature>();
            //var socket = connectionSocketFeature.Socket;
            var connectionTransportFeature = context.Features.Get<IConnectionTransportFeature>();
            this._input = connectionTransportFeature.Transport.Input.AsStream();
            this._output = connectionTransportFeature.Transport.Output.AsStream();

        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
             
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
           return _input.Read(buffer, offset, count);
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
           _output.Write(buffer, offset, count);
        }
    }
}
