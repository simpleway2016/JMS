using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JMS.Common.IO
{
    //未单元测试
    public class ArrayPoolBufferStream : Stream
    {
        byte[] _buffer;
        long _length;
        int _offset;

        public byte[] Buffer => _buffer;

        public ArrayPoolBufferStream(int maxSize)
        {
            _buffer = ArrayPool<byte>.Shared.Rent(maxSize);
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => _length;

        public override long Position { get => _offset; set => _offset = Math.Min(_buffer.Length, (int)value); }

        public override void Flush()
        {

        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_offset + count > _buffer.Length)
                count = _buffer.Length - _offset;
            if (count == 0)
                return 0;

            Array.Copy(_buffer, _offset, buffer, offset, count);
            _offset += count;
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Begin)
            {
                _offset = Math.Min(_buffer.Length, (int)offset);
            }
            else if (origin == SeekOrigin.Current)
            {
                _offset = Math.Min(_buffer.Length, _offset + (int)offset);
            }
            else if (origin == SeekOrigin.End)
            {
                _offset = Math.Max(0, _buffer.Length - (int)offset);
            }
            return _offset;
        }

        public override void SetLength(long value)
        {
            _length = value;
            _offset = Math.Min((int)_length, _offset);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_offset + count > _buffer.Length)
                count = _buffer.Length - _offset;
            Array.Copy(buffer, offset, _buffer, _offset, count);
            _length += count;
            _offset += count;
        }

        protected override void Dispose(bool disposing)
        {
            if (_buffer != null)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = null;
            }
            base.Dispose(disposing);
        }
    }
}
