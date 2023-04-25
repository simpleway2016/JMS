#if NETCOREAPP2_1
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Pipelines;
using System.IO;
using System;

namespace JMS.ServiceProvider.AspNetCore
{
    internal sealed class PipeWriterStream : Stream
    {
        private readonly PipeWriter _pipeWriter;

        public PipeWriterStream(PipeWriter pipeWriter, bool leaveOpen)
        {
            Debug.Assert(pipeWriter != null);
            _pipeWriter = pipeWriter;
            LeaveOpen = leaveOpen;
        }

        protected override void Dispose(bool disposing)
        {
            if (!LeaveOpen)
            {
                _pipeWriter.Complete();
            }
        }



        internal bool LeaveOpen { get; set; }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
            FlushAsync().GetAwaiter().GetResult();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public sealed override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
            TaskToAsyncResult.Begin(WriteAsync(buffer, offset, count, default), callback, state);

        public sealed override void EndWrite(IAsyncResult asyncResult) =>
            TaskToAsyncResult.End(asyncResult);

        public override void Write(byte[] buffer, int offset, int count) =>
            WriteAsync(buffer, offset, count).GetAwaiter().GetResult();

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer is null)
            {
                return Task.CompletedTask;
            }

            ValueTask<FlushResult> valueTask = _pipeWriter.WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken);

            return GetFlushResultAsTask(valueTask);
        }

#if (!NETSTANDARD2_0 && !NETFRAMEWORK)
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ValueTask<FlushResult> valueTask = _pipeWriter.WriteAsync(buffer, cancellationToken);

            return new ValueTask(GetFlushResultAsTask(valueTask));
        }
#endif

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            ValueTask<FlushResult> valueTask = _pipeWriter.FlushAsync(cancellationToken);

            return GetFlushResultAsTask(valueTask);
        }

        private static Task GetFlushResultAsTask(ValueTask<FlushResult> valueTask)
        {
            if (valueTask.IsCompletedSuccessfully)
            {
                FlushResult result = valueTask.Result;
                if (result.IsCanceled)
                {
                    throw new TaskCanceledException();
                    //ThrowHelper.ThrowOperationCanceledException_FlushCanceled();
                }

                return Task.CompletedTask;
            }

           

            return AwaitTask(valueTask);
        }

        static async Task AwaitTask(ValueTask<FlushResult> valueTask)
        {
            FlushResult result = await valueTask.ConfigureAwait(false);

            if (result.IsCanceled)
            {
                throw new TaskCanceledException();
                //ThrowHelper.ThrowOperationCanceledException_FlushCanceled();
            }
        }
    }
}
#endif