using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS.Common.Net
{
    internal class InsideNetworkStream : NetworkStream
    {
        public InsideNetworkStream(Socket socket) : base(socket)
        {
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            if (this.Socket.ReceiveTimeout > 0)
            {
                //try
                //{
                using (var cancellation = new CancellationTokenSource(this.Socket.ReceiveTimeout))
                {
                    var ret = await base.ReadAsync(buffer, offset, size, cancellation.Token);
                    return ret;
                }
                //}
                //catch (OperationCanceledException)
                //{
                //    throw new SocketException((int)SocketError.TimedOut);
                //}
            }
            return await base.ReadAsync(buffer, offset, size, cancellationToken);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (this.Socket.ReceiveTimeout > 0)
            {
                //try
                //{
                using (var cancellation = new CancellationTokenSource(this.Socket.ReceiveTimeout))
                {
                    var ret = await base.ReadAsync(buffer, cancellation.Token);
                    return ret;
                }
                //}
                //catch (OperationCanceledException)
                //{
                //    throw new SocketException((int)SocketError.TimedOut);
                //}

            }
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }
}
