// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.VisualStudio.OLE.Interop;

namespace NuGetVSExtension
{
    internal class DataStreamFromComStream : Stream, IDisposable
    {
        private IStream _comStream;

        public DataStreamFromComStream(IStream comStream)
        {
            _comStream = comStream;
        }

        ~DataStreamFromComStream()
        {
            // DO NOT CALL Close() since native streams cannot be closed in finalizer thread.
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush()
        {
            if (_comStream != null)
            {
                try
                {
                    _comStream.Commit(0);
                }
                catch (Exception)
                {
                }
            }
        }

        public override long Length
        {
            get
            {
                long currentPos = this.Position;
                long endPos = Seek(0, SeekOrigin.End);
                this.Position = currentPos;
                return endPos - currentPos;
            }
        }

        public override long Position
        {
            get { return Seek(0, SeekOrigin.Current); }
            set { Seek(value, SeekOrigin.Begin); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            uint bytesRead = 0;
            byte[] b = buffer;

            if (offset != 0)
            {
                b = new byte[buffer.Length - offset];
                Array.Copy(buffer, offset, b, 0, buffer.Length - offset);
            }

            _comStream.Read(b, (uint)count, out bytesRead);

            if (offset != 0)
            {
                b.CopyTo(buffer, offset);
            }

            return (int)bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            LARGE_INTEGER li = new LARGE_INTEGER();
            ULARGE_INTEGER[] ul = new ULARGE_INTEGER[1];
            ul[0] = new ULARGE_INTEGER();
            li.QuadPart = offset;
            _comStream.Seek(li, (uint)origin, ul);
            return (long)ul[0].QuadPart;
        }

        public override void SetLength(long value)
        {
            ULARGE_INTEGER ul = new ULARGE_INTEGER();
            ul.QuadPart = (ulong)value;
            _comStream.SetSize(ul);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            uint bytesWritten;
            if (count > 0)
            {
                byte[] b = buffer;
                if (offset != 0)
                {
                    b = new byte[buffer.Length - offset];
                    Array.Copy(buffer, offset, b, 0, buffer.Length - offset);
                }

                _comStream.Write(b, (uint)count, out bytesWritten);
                if (bytesWritten != count)
                {
                    throw new IOException();
                }

                if (offset != 0)
                {
                    b.CopyTo(buffer, offset);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                // Cannot close COM stream from finalizer thread.
                _comStream = null;
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
