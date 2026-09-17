// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
#if NETFRAMEWORK
using System.Buffers;
#endif

namespace NuGet.Shared
{
    /// <summary>
    /// Normalizes a JSON input stream to UTF-8 while preserving streaming reads.
    /// </summary>
    /// <remarks>
    /// JSON readers in NuGet consume UTF-8, but existing APIs historically accept UTF-8 and
    /// BOM-marked UTF-16 or UTF-32 streams. This helper centralizes BOM detection, prefix replay
    /// for non-seekable streams, and incremental transcoding without buffering the full document.
    ///
    /// The returned stream owns its decoder and temporary buffers and must be disposed by the
    /// caller. The <c>leaveOpen</c> argument to <see cref="Create"/> controls whether disposing
    /// the returned stream also disposes the input stream.
    /// </remarks>
    internal static class Utf8JsonStream
    {
        /// <summary>
        /// Creates a read-only UTF-8 view over <paramref name="stream"/> at its current position.
        /// </summary>
        /// <param name="stream">The JSON stream to normalize.</param>
        /// <param name="leaveOpen">
        /// <see langword="true"/> to leave <paramref name="stream"/> open when the returned stream
        /// is disposed; otherwise, <see langword="false"/>.
        /// </param>
        /// <returns>
        /// A read-only stream that emits BOM-free UTF-8 bytes. The caller must dispose this stream.
        /// </returns>
        internal static Stream Create(Stream stream, bool leaveOpen)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            long initialPosition = stream.CanSeek ? stream.Position : 0;
            byte[] prefix = new byte[4];
            int prefixLength = ReadPrefix(stream, prefix);
            Encoding? sourceEncoding = GetBomEncoding(prefix, prefixLength, out int preambleLength);

            Stream contentStream;
            if (stream.CanSeek)
            {
                stream.Position = initialPosition + preambleLength;
                contentStream = leaveOpen ? new LeaveOpenReadStream(stream) : stream;
            }
            else
            {
                contentStream = new PrefixReplayStream(prefix, preambleLength, prefixLength, stream, leaveOpen);
            }

            if (sourceEncoding is null)
            {
                return contentStream;
            }

#if NETFRAMEWORK
            return new TranscodingReadStream(contentStream, sourceEncoding);
#else
            return Encoding.CreateTranscodingStream(contentStream, sourceEncoding, Encoding.UTF8, leaveOpen: false);
#endif
        }

        private static int ReadPrefix(Stream stream, byte[] prefix)
        {
            int totalRead = 0;
            while (totalRead < prefix.Length)
            {
                int bytesRead = stream.Read(prefix, totalRead, prefix.Length - totalRead);
                if (bytesRead == 0)
                {
                    break;
                }

                totalRead += bytesRead;
            }

            return totalRead;
        }

        private static Encoding? GetBomEncoding(byte[] prefix, int length, out int preambleLength)
        {
            if (length >= 4)
            {
                if (prefix[0] == 0xFF && prefix[1] == 0xFE && prefix[2] == 0x00 && prefix[3] == 0x00)
                {
                    preambleLength = 4;
                    return new UTF32Encoding(bigEndian: false, byteOrderMark: false);
                }
                if (prefix[0] == 0x00 && prefix[1] == 0x00 && prefix[2] == 0xFE && prefix[3] == 0xFF)
                {
                    preambleLength = 4;
                    return new UTF32Encoding(bigEndian: true, byteOrderMark: false);
                }
            }

            if (length >= 3
                && prefix[0] == 0xEF
                && prefix[1] == 0xBB
                && prefix[2] == 0xBF)
            {
                preambleLength = 3;
                return null;
            }

            if (length >= 2)
            {
                if (prefix[0] == 0xFF && prefix[1] == 0xFE)
                {
                    preambleLength = 2;
                    return Encoding.Unicode;
                }
                if (prefix[0] == 0xFE && prefix[1] == 0xFF)
                {
                    preambleLength = 2;
                    return Encoding.BigEndianUnicode;
                }
            }

            preambleLength = 0;
            return null;
        }

        private static void ValidateReadArguments(byte[] buffer, int offset, int count)
        {
            if (buffer is null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            if (buffer.Length - offset < count)
            {
                throw new ArgumentException("Offset and count exceed the buffer length.");
            }
        }

        private sealed class LeaveOpenReadStream : Stream
        {
#pragma warning disable CA2213 // This wrapper intentionally leaves the stream open.
            private readonly Stream _stream;
#pragma warning restore CA2213
            private bool _disposed;

            internal LeaveOpenReadStream(Stream stream)
            {
                _stream = stream;
            }

            public override bool CanRead => !_disposed && _stream.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                ThrowIfDisposed();
                return _stream.Read(buffer, offset, count);
            }

            public override void Flush()
            {
            }

            protected override void Dispose(bool disposing)
            {
                _disposed = true;
                base.Dispose(disposing);
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            private void ThrowIfDisposed()
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(LeaveOpenReadStream));
                }
            }
        }

        private sealed class PrefixReplayStream : Stream
        {
            private readonly byte[] _prefix;
            private readonly int _prefixLength;
            private readonly Stream _stream;
            private readonly bool _leaveOpen;
            private int _prefixPosition;
            private bool _disposed;

            internal PrefixReplayStream(
                byte[] prefix,
                int prefixOffset,
                int prefixLength,
                Stream stream,
                bool leaveOpen)
            {
                _prefix = prefix;
                _prefixLength = prefixLength;
                _stream = stream;
                _leaveOpen = leaveOpen;
                _prefixPosition = prefixOffset;
            }

            public override bool CanRead => !_disposed && _stream.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                ThrowIfDisposed();
                ValidateReadArguments(buffer, offset, count);

                int prefixBytesRead = Math.Min(count, _prefixLength - _prefixPosition);
                if (prefixBytesRead > 0)
                {
                    Array.Copy(_prefix, _prefixPosition, buffer, offset, prefixBytesRead);
                    _prefixPosition += prefixBytesRead;
                }

                int streamBytesRead = prefixBytesRead < count
                    ? _stream.Read(buffer, offset + prefixBytesRead, count - prefixBytesRead)
                    : 0;

                return prefixBytesRead + streamBytesRead;
            }

            public override void Flush()
            {
            }

            protected override void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    _disposed = true;
                    if (disposing && !_leaveOpen)
                    {
                        _stream.Dispose();
                    }
                }

                base.Dispose(disposing);
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            private void ThrowIfDisposed()
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(PrefixReplayStream));
                }
            }
        }

#if NETFRAMEWORK
        private sealed class TranscodingReadStream : Stream
        {
            private const int CharBufferSize = 1024;

            private readonly StreamReader _reader;
            private readonly Encoder _encoder;
            private readonly ArrayPool<char> _charPool;
            private readonly ArrayPool<byte> _bytePool;
            private char[] _chars;
            private byte[] _bytes;
            private int _byteOffset;
            private int _byteCount;
            private bool _completed;
            private bool _disposed;

            internal TranscodingReadStream(Stream stream, Encoding sourceEncoding)
            {
                _reader = new StreamReader(
                    stream,
                    sourceEncoding,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: CharBufferSize,
                    leaveOpen: false);
                _encoder = Encoding.UTF8.GetEncoder();
                _charPool = ArrayPool<char>.Shared;
                _bytePool = ArrayPool<byte>.Shared;
                _chars = _charPool.Rent(CharBufferSize);
                _bytes = _bytePool.Rent(Encoding.UTF8.GetMaxByteCount(CharBufferSize));
            }

            public override bool CanRead => !_disposed;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                ThrowIfDisposed();
                ValidateReadArguments(buffer, offset, count);

                int totalBytesRead = 0;
                while (totalBytesRead < count)
                {
                    if (_byteOffset < _byteCount)
                    {
                        int bytesToCopy = Math.Min(count - totalBytesRead, _byteCount - _byteOffset);
                        Array.Copy(_bytes, _byteOffset, buffer, offset + totalBytesRead, bytesToCopy);
                        _byteOffset += bytesToCopy;
                        totalBytesRead += bytesToCopy;
                        continue;
                    }

                    if (_completed)
                    {
                        break;
                    }

                    FillEncodedBuffer();
                }

                return totalBytesRead;
            }

            public override void Flush()
            {
            }

            protected override void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    _disposed = true;
                    if (disposing)
                    {
                        _reader.Dispose();
                    }

                    char[] chars = _chars;
                    byte[] bytes = _bytes;
                    _chars = null!;
                    _bytes = null!;
                    _charPool.Return(chars, clearArray: true);
                    _bytePool.Return(bytes, clearArray: true);
                }

                base.Dispose(disposing);
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            private void FillEncodedBuffer()
            {
                do
                {
                    int charsRead = _reader.Read(_chars, index: 0, count: CharBufferSize);
                    bool flush = charsRead == 0;
                    _encoder.Convert(
                        _chars,
                        charIndex: 0,
                        charCount: charsRead,
                        _bytes,
                        byteIndex: 0,
                        byteCount: _bytes.Length,
                        flush,
                        out _,
                        out _byteCount,
                        out bool completed);

                    _byteOffset = 0;
                    _completed = flush && completed;
                }
                while (_byteCount == 0 && !_completed);
            }

            private void ThrowIfDisposed()
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(TranscodingReadStream));
                }
            }
        }
#endif
    }
}
