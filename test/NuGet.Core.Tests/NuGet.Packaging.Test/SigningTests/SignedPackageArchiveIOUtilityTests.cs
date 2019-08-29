// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Org.BouncyCastle.Crypto.Tls;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SignedPackageArchiveIOUtilityTests
    {
        [Fact]
        public void ReadAndWriteUntilPosition_WhenReaderNull_Throws()
        {
            using (var test = new ReadWriteTest())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(
                        reader: null,
                        writer: test.Writer,
                        position: 0));

                Assert.Equal("reader", exception.ParamName);
            }
        }

        [Fact]
        public void ReadAndWriteUntilPosition_WhenPositionTooBig_Throws()
        {
            using (var test = new ReadWriteTest())
            {
                var exception = Assert.Throws<ArgumentOutOfRangeException>(
                    () => SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(
                        test.Reader,
                        test.Writer,
                        test.Reader.BaseStream.Length + 1));

                Assert.Equal("position", exception.ParamName);
                Assert.Equal(0, test.Reader.BaseStream.Position);
            }
        }

        [Fact]
        public void ReadAndWriteUntilPosition_WhenPositionBeforeCurrentReadPosition_Throws()
        {
            using (var test = new ReadWriteTest())
            {
                test.Reader.BaseStream.Seek(offset: 1, origin: SeekOrigin.Begin);

                var exception = Assert.Throws<ArgumentOutOfRangeException>(
                    () => SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(
                        test.Reader,
                        test.Writer,
                        position: 0));

                Assert.Equal("position", exception.ParamName);
                Assert.Equal(1, test.Reader.BaseStream.Position);
            }
        }

        [Fact]
        public void ReadAndWriteUntilPosition_WhenPositionAtStart_ReadsAndWrites()
        {
            using (var test = new ReadWriteTest())
            {
                SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(
                    test.Reader,
                    test.Writer,
                    test.Reader.BaseStream.Length);

                var actual = test.GetWrittenBytes();

                Assert.Equal(test.Bytes, actual);
                Assert.Equal(test.Reader.BaseStream.Length, test.Reader.BaseStream.Position);
            }
        }

        [Fact]
        public void ReadAndWriteUntilPosition_WhenPositionInMiddle_ReadsAndWrites()
        {
            using (var test = new ReadWriteTest())
            {
                test.Reader.BaseStream.Seek(offset: 2, origin: SeekOrigin.Begin);

                SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(test.Reader, test.Writer, position: 5);

                var actual = test.GetWrittenBytes();

                Assert.Equal(new byte[] { 2, 3, 4 }, actual);
                Assert.Equal(5, test.Reader.BaseStream.Position);
            }
        }

        [Fact]
        public void ReadAndWriteUntilPosition_WhenPositionAtEnd_ReadsAndWrites()
        {
            using (var test = new ReadWriteTest())
            {
                test.Reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.End);

                SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(
                    test.Reader,
                    test.Writer,
                    test.Reader.BaseStream.Length);

                var actual = test.GetWrittenBytes();

                Assert.Empty(actual);
                Assert.Equal(test.Reader.BaseStream.Length, test.Reader.BaseStream.Position);
            }
        }

        [Fact]
        public void ReadAndHashUntilPosition_WhenReaderNull_Throws()
        {
            using (var test = new ReadHashTest())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(
                        reader: null,
                        hashFunc: test.hashFunc,
                        position: 0));

                Assert.Equal("reader", exception.ParamName);
            }
        }

        [Fact]
        public void ReadAndHashUntilPosition_WhenPositionTooBig_Throws()
        {
            using (var test = new ReadHashTest())
            {
                var exception = Assert.Throws<ArgumentOutOfRangeException>(
                    () => SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(
                        test.Reader,
                        test.hashFunc,
                        test.Reader.BaseStream.Length + 1));

                Assert.Equal("position", exception.ParamName);
                Assert.Equal(0, test.Reader.BaseStream.Position);
            }
        }

        [Fact]
        public void ReadAndHashUntilPosition_WhenPositionBeforeCurrentReadPosition_Throws()
        {
            using (var test = new ReadHashTest())
            {
                test.Reader.BaseStream.Seek(offset: 1, origin: SeekOrigin.Begin);

                var exception = Assert.Throws<ArgumentOutOfRangeException>(
                    () => SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(
                        test.Reader,
                        test.hashFunc,
                        position: 0));

                Assert.Equal("position", exception.ParamName);
                Assert.Equal(1, test.Reader.BaseStream.Position);
            }
        }

        [Fact]
        public void ReadAndHashUntilPosition_WhenPositionAtStart_ReadsAndHashes()
        {
            using (var test = new ReadHashTest())
            {
                SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(
                    test.Reader,
                    test.hashFunc,
                    test.Reader.BaseStream.Length);

                var actualHash = test.GetHash();

                Assert.Equal("LzgxvMyUzwYbz6X4wjwUKdJuO8a3btrZPZAly5HJA69s+ck13DcZPATCxm59neF8NYKEQYIYr+ohYBR6qpEvTA==", actualHash);
                Assert.Equal(test.Reader.BaseStream.Length, test.Reader.BaseStream.Position);
            }
        }

        [Fact]
        public void ReadAndHashUntilPosition_WhenPositionInMiddle_ReadsAndHashes()
        {
            using (var test = new ReadHashTest())
            {
                test.Reader.BaseStream.Seek(offset: 2, origin: SeekOrigin.Begin);

                SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(
                    test.Reader,
                    test.hashFunc,
                    position: 5);

                var actualHash = test.GetHash();

                Assert.Equal("McXqbLUPbfPhEQ4IvD++P14C7pWeKqbSEGxrMEKd0Lbhg9tao1lzsZmKU0lWx4qLEXI5/Tn2PxJW2GfxHLmgcw==", actualHash);
                Assert.Equal(5, test.Reader.BaseStream.Position);
            }
        }

        [Fact]
        public void ReadAndHashUntilPosition_WhenPositionAtEnd_ReadsAndHashes()
        {
            using (var test = new ReadHashTest())
            {
                test.Reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.End);

                SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(
                    test.Reader,
                    test.hashFunc,
                    test.Reader.BaseStream.Length);

                var actualHash = test.GetHash();

                Assert.Equal("z4PhNX7vuL3xVChQ1m2AB9Yg5AULVxXcg/SpIdNs6c5H0NE8XYXysP+DGNKHfuwvY7kxvUdBeoGlODJ6+SfaPg==", actualHash);
                Assert.Equal(test.Reader.BaseStream.Length, test.Reader.BaseStream.Position);
            }
        }

        [Fact]
        public void HashBytes_WhenHashAlgorithmNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => SignedPackageArchiveIOUtility.HashBytes(
                    hashFunc: null, bytes: new byte[] { 0 }));

            Assert.Equal("hashFunc", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(new byte[] { })]
        public void HashBytes_WhenBytesNullOrEmpty_Throws(byte[] bytes)
        {
            using (var hashFunc = new Sha512HashFunction())
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => SignedPackageArchiveIOUtility.HashBytes(
                        hashFunc,
                        bytes));

                Assert.Equal("bytes", exception.ParamName);
            }
        }


        [Fact]
        public void HashBytes_WithInputBytes_Hashes()
        {
            using (var hashFunc = new Sha512HashFunction())
            {
                SignedPackageArchiveIOUtility.HashBytes(hashFunc, new byte[] { 0, 1, 2 });

                hashFunc.Update(new byte[0], offset: 0, count: 0);

                var actualHash = hashFunc.GetHash();

                Assert.Equal("gIHaX5wePQ4aoW9gTV5QZFQ8/117rOK7MSJSRh4VGz/g8DTqjcHaz/M2GoktYl++G2FM2iZfh6RzwksPodkd/Q==", actualHash);
            }
        }


        [Fact]
        public void ReadSignedArchiveMetadata_WhenReaderNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader: null));

            Assert.Equal("reader", exception.ParamName);
        }

        [Fact]
        public void ReadSignedArchiveMetadata_WithEmptyZip_Throws()
        {
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                }

                stream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var reader = new BinaryReader(stream))
                {
                    Assert.Throws<InvalidDataException>(
                        () => SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader));
                }
            }
        }

        [Fact]
        public void ReadSignedArchiveMetadata_WithUnsignedPackage_Throws()
        {
            using (var stream = new MemoryStream(GetNonEmptyZip()))
            using (var reader = new BinaryReader(stream))
            {
                var exception = Assert.Throws<SignatureException>(
                    () => SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader));

                Assert.Equal(NuGetLogCode.NU3005, exception.Code);
                Assert.Equal("The package does not contain a valid package signature file.", exception.Message);
            }
        }

        [Fact]
        public void ReadSignedArchiveMetadata_WithSignedPackage_ReturnsMetadata()
        {
            using (var stream = new MemoryStream(SigningTestUtility.GetResourceBytes("SignedPackage.1.0.0.nupkg")))
            using (var reader = new BinaryReader(stream))
            {
                var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);

                Assert.Equal(0, metadata.StartOfLocalFileHeaders);

                Assert.Equal(6, metadata.CentralDirectoryHeaders.Count);
                Assert.Equal(0xd7c, metadata.EndOfCentralDirectory);

                Assert.Equal(5, metadata.SignatureCentralDirectoryHeaderIndex);

                var expectedHeaders = new[]
                {
                    new { ChangeInOffset = 0L, FileEntryTotalSize = 0x136, HeaderSize = 0x39, IndexInHeaders = 0,
                          IsPackageSignatureFile = false, OffsetToFileHeader = 0, Position = 0xbcc },
                    new { ChangeInOffset = 0L, FileEntryTotalSize = 0x110, HeaderSize = 0x42, IndexInHeaders = 1,
                          IsPackageSignatureFile = false, OffsetToFileHeader = 0x136, Position = 0xc05 },
                    new { ChangeInOffset = 0L, FileEntryTotalSize = 0x29, HeaderSize = 0x39, IndexInHeaders = 2,
                          IsPackageSignatureFile = false, OffsetToFileHeader = 0x246, Position = 0xc47 },
                    new { ChangeInOffset = 0L, FileEntryTotalSize = 0xff, HeaderSize = 0x41, IndexInHeaders = 3,
                          IsPackageSignatureFile = false, OffsetToFileHeader = 0x26f, Position = 0xc80 },
                    new { ChangeInOffset = 0L, FileEntryTotalSize = 0x1dd, HeaderSize = 0x7f, IndexInHeaders = 4,
                          IsPackageSignatureFile = false, OffsetToFileHeader = 0x36e, Position = 0xcc1 },
                    new { ChangeInOffset = 0L, FileEntryTotalSize = 0x681, HeaderSize = 0x3c, IndexInHeaders = 5,
                          IsPackageSignatureFile = true, OffsetToFileHeader = 0x54b, Position = 0xd40 },
                };

                Assert.Equal(expectedHeaders.Length, metadata.CentralDirectoryHeaders.Count);

                for (var i = 0; i < expectedHeaders.Length; ++i)
                {
                    var expectedHeader = expectedHeaders[i];
                    var actualHeader = metadata.CentralDirectoryHeaders[i];

                    Assert.Equal(expectedHeader.Position, actualHeader.Position);
                    Assert.Equal(expectedHeader.OffsetToFileHeader, actualHeader.OffsetToLocalFileHeader);
                    Assert.Equal(expectedHeader.FileEntryTotalSize, actualHeader.FileEntryTotalSize);
                    Assert.Equal(expectedHeader.IsPackageSignatureFile, actualHeader.IsPackageSignatureFile);
                    Assert.Equal(expectedHeader.HeaderSize, actualHeader.HeaderSize);
                    Assert.Equal(expectedHeader.ChangeInOffset, actualHeader.ChangeInOffset);
                    Assert.Equal(expectedHeader.IndexInHeaders, actualHeader.IndexInHeaders);
                }
            }
        }

        [Fact]
        public void ReadSignedArchiveMetadata_WithOnlyPackageSignatureEntry_ReturnsMetadata()
        {
            using (var stream = new MemoryStream(GetResource("SignatureFileEntry.zip")))
            using (var reader = new BinaryReader(stream))
            {
                var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);

                Assert.Equal(1, metadata.CentralDirectoryHeaders.Count);
                Assert.Equal(0, metadata.SignatureCentralDirectoryHeaderIndex);

                var header = metadata.CentralDirectoryHeaders[0];

                Assert.True(header.IsPackageSignatureFile);
            }
        }

        private static byte[] GetResource(string name)
        {
            return ResourceTestUtility.GetResourceBytes(
                $"Test.Utility.compiler.resources.{name}",
                typeof(SigningTestUtility));
        }

        private static byte[] GetNonEmptyZip()
        {
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    var entry = zip.CreateEntry("file.txt");

                    using (var entryStream = entry.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        streamWriter.Write("peach");
                    }
                }

                return stream.ToArray();
            }
        }

        private class ReadTest : IDisposable
        {
            protected static readonly byte[] _bytes = new byte[] { 0, 1, 2, 3, 4, 5 };

            private readonly MemoryStream _stream;

            protected bool IsDisposed { get; private set; }

            internal byte[] Bytes => _bytes;
            internal BinaryReader Reader { get; }

            internal ReadTest()
            {
                _stream = new MemoryStream(_bytes);
                Reader = new BinaryReader(_stream);
            }

            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!IsDisposed)
                {
                    _stream.Dispose();
                    Reader.Dispose();

                    IsDisposed = true;
                }
            }
        }

        private sealed class ReadWriteTest : ReadTest
        {
            private readonly MemoryStream _writeStream;

            internal BinaryWriter Writer { get; }

            internal ReadWriteTest()
            {
                _writeStream = new MemoryStream();
                Writer = new BinaryWriter(_writeStream);
            }

            protected override void Dispose(bool disposing)
            {
                if (!IsDisposed)
                {
                    _writeStream.Dispose();
                    Writer.Dispose();
                }

                base.Dispose(disposing);
            }

            internal byte[] GetWrittenBytes()
            {
                Writer.Flush();

                return _writeStream.ToArray();
            }
        }

        private sealed class ReadHashTest : ReadTest
        {
            internal Sha512HashFunction  hashFunc { get; }

            internal ReadHashTest()
            {
                hashFunc = new Sha512HashFunction();
            }

            protected override void Dispose(bool disposing)
            {
                if (!IsDisposed)
                {
                    hashFunc.Dispose();
                }

                base.Dispose(disposing);
            }

            internal string GetHash()
            {
                /*
                #if !IS_CORECLR
                                HashAlgorithm.TransformFinalBlock(new byte[0], inputOffset: 0, inputCount: 0);

                                return Convert.ToBase64String(HashAlgorithm.Hash);
                #else
                                throw new NotImplementedException();
                #endif
                */
#if IS_SIGNING_SUPPORTED
                hashFunc.Update(new byte[0], offset: 0, count: 0);

                return hashFunc.GetHash();
#else
                throw new NotImplementedException();
#endif
            }
        }
    }
}
