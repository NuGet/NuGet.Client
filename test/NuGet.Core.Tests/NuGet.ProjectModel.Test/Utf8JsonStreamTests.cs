// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using NuGet.Shared;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class Utf8JsonStreamTests
    {
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(1, true)]
        [InlineData(2, false)]
        [InlineData(2, true)]
        public void Create_WithChunkedInputAndSingleByteOutput_ProducesUtf8(
            int encodingKind,
            bool bigEndian)
        {
            const string expected = "ASCII \u00E9 \U0001F600 end";
            Encoding sourceEncoding = GetEncoding(encodingKind, bigEndian);
            byte[] preamble = sourceEncoding.GetPreamble();
            byte[] content = sourceEncoding.GetBytes(expected);
            var encoded = new byte[preamble.Length + content.Length];
            Array.Copy(preamble, encoded, preamble.Length);
            Array.Copy(content, 0, encoded, preamble.Length, content.Length);
            using var source = new ChunkedReadStream(new MemoryStream(encoded), maxBytesPerRead: 1);
            using Stream utf8Stream = Utf8JsonStream.Create(source, leaveOpen: false);
            using var output = new MemoryStream();
            var buffer = new byte[1];

            int bytesRead;
            while ((bytesRead = utf8Stream.Read(buffer, offset: 0, count: buffer.Length)) > 0)
            {
                output.Write(buffer, offset: 0, count: bytesRead);
            }

            Assert.Equal(expected, Encoding.UTF8.GetString(output.ToArray()));
            Assert.Equal(0, utf8Stream.Read(buffer, offset: 0, count: buffer.Length));
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        public void Dispose_HonorsLeaveOpen(
            bool useUtf16,
            bool useNonSeekableStream,
            bool leaveOpen)
        {
            byte[] content = useUtf16
                ? Encoding.Unicode.GetPreamble()
                : Encoding.UTF8.GetBytes("{}");
            Stream source = new MemoryStream(content);
            if (useNonSeekableStream)
            {
                source = new ChunkedReadStream(source, maxBytesPerRead: 1);
            }
            Stream utf8Stream = Utf8JsonStream.Create(source, leaveOpen);

            utf8Stream.Dispose();

            Assert.Throws<ObjectDisposedException>(() => utf8Stream.ReadByte());
            Assert.Equal(leaveOpen, source.CanRead);
            source.Dispose();
        }

        [Fact]
        public void Create_WithSeekableStreamAtNonzeroPosition_ReadsFromInitialPosition()
        {
            const string expected = """{"value":true}""";
            byte[] prefix = Encoding.UTF8.GetBytes("ignored");
            byte[] content = Encoding.UTF8.GetBytes(expected);
            var source = new MemoryStream();
            source.Write(prefix, offset: 0, count: prefix.Length);
            source.Write(content, offset: 0, count: content.Length);
            source.Position = prefix.Length;
            using Stream utf8Stream = Utf8JsonStream.Create(source, leaveOpen: false);
            using var reader = new StreamReader(utf8Stream, Encoding.UTF8);

            Assert.Equal(expected, reader.ReadToEnd());
        }

        [Fact]
        public void Read_WithInvalidArgumentsWhilePrefixRemains_ThrowsWithoutConsumingPrefix()
        {
            const string expected = """{"value":true}""";
            using var source = new ChunkedReadStream(
                new MemoryStream(Encoding.UTF8.GetBytes(expected)),
                maxBytesPerRead: 1);
            using Stream utf8Stream = Utf8JsonStream.Create(source, leaveOpen: false);
            var buffer = new byte[1];

            Assert.Equal("buffer", Assert.Throws<ArgumentNullException>(
                () => utf8Stream.Read(null!, offset: 0, count: 1)).ParamName);
            Assert.Equal("offset", Assert.Throws<ArgumentOutOfRangeException>(
                () => utf8Stream.Read(buffer, offset: -1, count: 1)).ParamName);
            Assert.Equal("count", Assert.Throws<ArgumentOutOfRangeException>(
                () => utf8Stream.Read(buffer, offset: 0, count: -1)).ParamName);
            Assert.Throws<ArgumentException>(() => utf8Stream.Read(buffer, offset: 1, count: 1));
            Assert.Throws<ArgumentException>(() => utf8Stream.Read(buffer, offset: 2, count: 0));

            using var reader = new StreamReader(utf8Stream, Encoding.UTF8);
            Assert.Equal(expected, reader.ReadToEnd());
        }

        private static Encoding GetEncoding(int encodingKind, bool bigEndian)
        {
            return encodingKind switch
            {
                0 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
                1 => new UnicodeEncoding(bigEndian, byteOrderMark: true),
                2 => new UTF32Encoding(bigEndian, byteOrderMark: true),
                _ => throw new ArgumentOutOfRangeException(nameof(encodingKind)),
            };
        }

        private sealed class ChunkedReadStream : Stream
        {
            private readonly Stream _stream;
            private readonly int _maxBytesPerRead;

            internal ChunkedReadStream(Stream stream, int maxBytesPerRead)
            {
                _stream = stream;
                _maxBytesPerRead = maxBytesPerRead;
            }

            public override bool CanRead => _stream.CanRead;
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
                return _stream.Read(buffer, offset, Math.Min(count, _maxBytesPerRead));
            }

            public override void Flush()
            {
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _stream.Dispose();
                }

                base.Dispose(disposing);
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
