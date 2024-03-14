using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS.WebApiDocument
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

        public override int Read(Span<byte> buffer)
        {
            return _input.Read(buffer);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _input.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _input.ReadAsync(buffer, cancellationToken);
        }

        public override int ReadByte()
        {
            return _input.ReadByte();
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _input.BeginRead(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return _input.EndRead(asyncResult);
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

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _output.Write(buffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _output.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _output.WriteAsync(buffer, cancellationToken);
        }

        public override void WriteByte(byte value)
        {
            _output.WriteByte(value);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _output.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            _output.EndWrite(asyncResult);
        }
    }
}
