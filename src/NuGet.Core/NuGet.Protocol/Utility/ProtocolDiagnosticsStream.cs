// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;

namespace NuGet.Protocol.Utility
{
    internal class ProtocolDiagnosticsStream : Stream
    {
        private Stream _baseStream;
        private ProtocolDiagnosticEvent _protocolDiagnosticEvent;
        private Stopwatch _stopwatch;
        private long _bytes;

        internal ProtocolDiagnosticsStream(Stream baseStream, ProtocolDiagnosticEvent protocolDiagnosticEvent, Stopwatch stopwatch)
        {
            _baseStream = baseStream;
            _protocolDiagnosticEvent = protocolDiagnosticEvent;
            _stopwatch = stopwatch;
            _bytes = 0;
        }

        public override bool CanRead => _baseStream.CanRead;

        public override bool CanSeek => _baseStream.CanSeek;

        public override bool CanWrite => _baseStream.CanWrite;

        public override long Length => _baseStream.Length;

        public override long Position { get => _baseStream.Position; set => _baseStream.Position = value; }

        public override void Flush()
        {
            _baseStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                var read = _baseStream.Read(buffer, offset, count);
                if (read > 0)
                {
                    _bytes += read;
                }
                else
                {
                    RaiseDiagnosticEvent(isSuccess: true);
                }
                return read;
            }
            catch
            {
                RaiseDiagnosticEvent(isSuccess: false);
                throw;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _baseStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _baseStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _baseStream.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            RaiseDiagnosticEvent(isSuccess: true);

            base.Dispose(disposing);

            _baseStream?.Dispose();
        }

        private void RaiseDiagnosticEvent(bool isSuccess)
        {
            if (_protocolDiagnosticEvent != null)
            {
                var pde = new ProtocolDiagnosticEvent(
                    DateTime.UtcNow,
                    _protocolDiagnosticEvent.Source,
                    _protocolDiagnosticEvent.Url,
                    _protocolDiagnosticEvent.HeaderDuration,
                    _stopwatch.Elapsed,
                    _protocolDiagnosticEvent.HttpStatusCode,
                    _bytes,
                    isSuccess,
                    _protocolDiagnosticEvent.IsRetry,
                    _protocolDiagnosticEvent.IsCancelled);
                ProtocolDiagnostics.RaiseEvent(pde);

                _protocolDiagnosticEvent = null;
            }
        }
    }
}
