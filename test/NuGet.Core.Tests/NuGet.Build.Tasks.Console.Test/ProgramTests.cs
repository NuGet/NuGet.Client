// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FluentAssertions;
using Xunit;

namespace NuGet.Build.Tasks.Console.Test
{
    public class ProgramTests
    {
        public static IEnumerable<object[]> GetEncodingsToTest()
        {
            yield return new object[] { System.Console.InputEncoding };

            yield return new object[] { Encoding.ASCII };
            yield return new object[] { Encoding.Default };

            foreach (bool byteOrderMark in new bool[] { true, false })
            {
                yield return new object[] { new UTF8Encoding(encoderShouldEmitUTF8Identifier: byteOrderMark) };

                foreach (bool bigEndian in new bool[] { true, false })
                {
                    yield return new object[] { new UTF32Encoding(bigEndian, byteOrderMark) };

                    yield return new object[] { new UnicodeEncoding(bigEndian, byteOrderMark) };
                }
            }
        }

        /// <summary>
        /// Verifies that <see cref="Program.TryDeserializeGlobalProperties(TextWriter, BinaryReader, out Dictionary{string, string})" /> correctly handles if a stream contains a leading preamble.
        /// </summary>
        /// <param name="encoding">The <see cref="Encoding" /> to use as a preamble to begin a stream with.</param>
        [Theory]
        [MemberData(nameof(GetEncodingsToTest))]
        public void TryDeserializeGlobalProperties_WhenEncodingUsed_PropertiesAreDeserialized(Encoding encoding)
        {
            var expectedGlobalProperties = new Dictionary<string, string>
            {
                ["Property1"] = "Value1",
                ["Property2"] = "  Value2  "
            };

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(encoding.GetPreamble());

            StaticGraphRestoreTaskBase.WriteGlobalProperties(writer, expectedGlobalProperties);

            var errors = new StringBuilder();

            using var errorWriter = new StringWriter(errors);

            stream.Position = 0;

            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            bool result = Program.TryDeserializeGlobalProperties(errorWriter, reader, out Dictionary<string, string> actualGlobalProperties);

            actualGlobalProperties.Should().BeEquivalentTo(expectedGlobalProperties);
        }

        /// <summary>
        /// Verifies that <see cref="Program.TryDeserializeGlobalProperties(TextWriter, BinaryReader, out Dictionary{string, string})" /> returns <see langword="false" /> and logs an error if the stream contains an unsupported integer.
        /// </summary>
        /// <param name="bytes">An array of bytes to use as the stream.</param>
        [Theory]
        [InlineData(new byte[] { 0x00, 0x00, 0x00, 0xFF })] // 4 byte integer that is negative
        [InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F })] // 4 byte integer that is too big
        [InlineData(new byte[] { 0xFF, 0xFE, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF })] // Valid UTF-32LE BOM with a 4 byte integer that is negative
        [InlineData(new byte[] { 0xFF, 0xFE, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0x7F })] // Valid UTF-32LE BOM with a 4 byte integer after that is too big
        public void TryDeserializeGlobalProperties_WhenInvalidDictionaryLength_ReturnsFalse(byte[] bytes)
        {
            using var stream = GetStreamWithBytes(bytes);

            int expectedLength = BitConverter.ToInt32(bytes, bytes.Length - 4);

            VerifyTryDeserializeGlobalPropertiesError(stream, Strings.Error_StaticGraphRestoreArgumentsParsingFailedUnexpectedIntegerValue, expectedLength);
        }

        /// <summary>
        /// Verifies that <see cref="Program.TryDeserializeGlobalProperties(TextWriter, BinaryReader, out Dictionary{string, string})" /> returns <see langword="false" /> and logs an error if the stream contains bytes that are unexpected.
        /// </summary>
        /// <param name="bytes">An array of bytes to use as the stream.</param>
        [Theory]
        [InlineData(new byte[] { 0x01, 0x02 })] // Only two bytes
        [InlineData(new byte[] { 0xFF, 0xFE, 0x00, 0x00 })] // Valid UTF-32LE BOM but nothing after that
        [InlineData(new byte[] { 0xFF, 0xFE, 0x00, 0x00, 0x01, 0x00 })] // Valid UTF-32LE BOM but not a 4 byte integer after that
        public void TryDeserializeGlobalProperties_WhenInvalidFirstBytes_ReturnsFalse(byte[] bytes)
        {
            using var stream = GetStreamWithBytes(bytes);

            VerifyTryDeserializeGlobalPropertiesError(stream, Strings.Error_StaticGraphRestoreArgumentsParsingFailedEndOfStream);
        }

        /// <summary>
        /// Verifies that <see cref="Program.TryDeserializeGlobalProperties(TextWriter, BinaryReader, out Dictionary{string, string})" /> returns <see langword="false" /> and logs an error if reading the stream causes an exception to be thrown.
        /// </summary>
        /// <param name="throwOnReadCount">Indicates for which call to Stream.Read() should throw, the first, second, or third.</param>
        /// <param name="bytes">An array of bytes to use as the stream.</param>
        [Theory]
        [InlineData(1, new byte[] { 0x01, 0x00, 0x00, 0x00 })] // No preamble/BOM, throw on first Read()
        [InlineData(2, new byte[] { 0x01, 0x00, 0x00, 0x00 })] // No preamble/BOM, throw on second Read()
        [InlineData(1, new byte[] { 0xEF, 0xBB, 0xBF, 0x01, 0x00, 0x00, 0x00 })] // UTF8 preamble/BOM, throw on first Read()
        [InlineData(2, new byte[] { 0xEF, 0xBB, 0xBF, 0x01, 0x00, 0x00, 0x00 })] // UTF8 preamble/BOM, throw on second Read()
        [InlineData(3, new byte[] { 0xEF, 0xBB, 0xBF, 0x01, 0x00, 0x00, 0x00 })] // UTF8 preamble/BOM, throw on third Read()
        public void TryDeserializeGlobalProperties_WhenStreamReadThrows_ReturnsFalse(int throwOnReadCount, byte[] bytes)
        {
            var expectedGlobalProperties = new Dictionary<string, string>
            {
                ["Property1"] = "Value1",
                ["Property2"] = "  Value2  "
            };

            Exception exception = new InvalidOperationException();

            using var stream = new StreamThatThrowsAnException(bytes, throwOnReadCount, exception);

            VerifyTryDeserializeGlobalPropertiesErrorStartsWith(stream, Strings.Error_StaticGraphRestoreArgumentsParsingFailedExceptionReadingStream, exception.Message, string.Empty);
        }

        private static Stream GetStreamWithBytes(params byte[] bytes)
        {
            MemoryStream stream = new MemoryStream();

            using BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            writer.Write(bytes);

            stream.Flush();

            stream.Position = 0;

            return stream;
        }

        private void VerifyTryDeserializeGlobalPropertiesError(Stream stream, string expectedError, params object[] args)
        {
            var errors = new StringBuilder();

            using var errorWriter = new StringWriter(errors);

            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            bool result = Program.TryDeserializeGlobalProperties(errorWriter, reader, out Dictionary<string, string> actualGlobalProperties);

            result.Should().BeFalse();

            actualGlobalProperties.Should().BeNull();

            errors.ToString().Trim().Should().Be(string.Format(CultureInfo.CurrentCulture, expectedError, args));
        }

        private void VerifyTryDeserializeGlobalPropertiesErrorStartsWith(Stream stream, string expectedError, params object[] args)
        {
            var errors = new StringBuilder();

            using var errorWriter = new StringWriter(errors);

            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            bool result = Program.TryDeserializeGlobalProperties(errorWriter, reader, out Dictionary<string, string> actualGlobalProperties);

            result.Should().BeFalse();

            actualGlobalProperties.Should().BeNull();

            errors.ToString().Trim().Should().StartWith(string.Format(CultureInfo.CurrentCulture, expectedError, args));
        }

        private class StreamThatThrowsAnException : Stream
        {
            private readonly byte[] _buffer;
            private readonly Exception _exceptionToThrow;
            private readonly long _length;
            private readonly int _throwOnReadCount;
            private int _readCount = 0;

            public StreamThatThrowsAnException(byte[] buffer, int throwOnReadCount, Exception exception)
            {
                _throwOnReadCount = throwOnReadCount;
                _buffer = buffer;
                _length = buffer.LongLength;
                _exceptionToThrow = exception;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => _length;

            public override long Position { get; set; }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (++_readCount >= _throwOnReadCount)
                {
                    throw _exceptionToThrow;
                }

                int readCount = 0;

                for (int i = 0; i < count; i++)
                {
                    if (Position >= _length)
                    {
                        break;
                    }

                    buffer[i + offset] = _buffer[Position++];

                    readCount++;
                }

                return readCount;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
