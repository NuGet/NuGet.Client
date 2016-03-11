// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class SlowStream : Stream
    {
        private readonly Stream _innerStream;

        public SlowStream(Stream innerStream)
        {
            _innerStream = innerStream;
        }

        public TimeSpan DelayPerByte { get; set; }
        public Action<byte[], int, int> OnRead { get; set;}

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            OnRead?.Invoke(buffer, offset, count);
            var read = _innerStream.Read(buffer, offset, count);
            Task.Delay(new TimeSpan(DelayPerByte.Ticks * read)).Wait();
            return read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }
    }
}