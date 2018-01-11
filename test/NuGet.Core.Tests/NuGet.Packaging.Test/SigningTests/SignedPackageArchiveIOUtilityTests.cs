// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SignedPackageArchiveIOUtilityTests
    {
        [Fact]
        public void SeekReaderForwardToMatchByteSignature_WhenReaderNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => SignedPackageArchiveIOUtility.SeekReaderForwardToMatchByteSignature(
                    reader: null,
                    byteSignature: new byte[] { 0 }));

            Assert.Equal("reader", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(new byte[] { })]
        public void SeekReaderForwardToMatchByteSignature_WhenByteSignatureNullOrEmpty_Throws(byte[] byteSignature)
        {
            using (var test = new ReadTest())
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => SignedPackageArchiveIOUtility.SeekReaderForwardToMatchByteSignature(
                        test.Reader,
                        byteSignature));

                Assert.Equal("byteSignature", exception.ParamName);
                Assert.Equal(0, test.Reader.BaseStream.Position);
            }
        }

        [Fact]
        public void SeekReaderForwardToMatchByteSignature_WhenByteSignatureNotFound_MessageContainsByteSignature()
        {
            using (var test = new ReadTest())
            {
                var byteSignature = new byte[] { 6, 7 };

                var exception = Assert.Throws<InvalidDataException>(
                    () => SignedPackageArchiveIOUtility.SeekReaderForwardToMatchByteSignature(
                        test.Reader,
                        byteSignature));

                Assert.Contains(BitConverter.ToString(byteSignature).Replace("-", ""), exception.Message);
                Assert.Equal(0, test.Reader.BaseStream.Position);
            }
        }

        [Fact]
        public void SeekReaderForwardToMatchByteSignature_WhenInitialPositionAtStartAndByteSignatureNotFound_Throws()
        {
            using (var test = new ReadTest())
            {
                var byteSignature = new byte[] { 3, 2 };

                Assert.Throws<InvalidDataException>(
                    () => SignedPackageArchiveIOUtility.SeekReaderForwardToMatchByteSignature(
                        test.Reader,
                        byteSignature));

                Assert.Equal(0, test.Reader.BaseStream.Position);
            }
        }

        [Fact]
        public void SeekReaderForwardToMatchByteSignature_WhenInitialPositionAtStartAndByteSignatureAtStart_Seeks()
        {
            using (var test = new ReadTest())
            {
                var byteSignature = new byte[] { 0, 1 };

                test.Reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.Begin);

                SignedPackageArchiveIOUtility.SeekReaderForwardToMatchByteSignature(test.Reader, byteSignature);

                Assert.Equal(0, test.Reader.BaseStream.Position);

                var actual = test.Reader.ReadBytes(byteSignature.Length);

                Assert.Equal(byteSignature, actual);
            }
        }

        [Fact]
        public void SeekReaderForwardToMatchByteSignature_WhenInitialPositionAtStartAndByteSignatureAtEnd_Seeks()
        {
            using (var test = new ReadTest())
            {
                var byteSignature = new byte[] { 4, 5 };

                test.Reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.Begin);

                SignedPackageArchiveIOUtility.SeekReaderForwardToMatchByteSignature(test.Reader, byteSignature);

                Assert.Equal(4, test.Reader.BaseStream.Position);

                var actual = test.Reader.ReadBytes(byteSignature.Length);

                Assert.Equal(byteSignature, actual);
            }
        }

        [Fact]
        public void SeekReaderForwardToMatchByteSignature_WhenInitialPositionAtEnd_Throws()
        {
            using (var test = new ReadTest())
            {
                var byteSignature = new byte[] { 0, 1 };

                test.Reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.End);

                var exception = Assert.Throws<ArgumentOutOfRangeException>(
                    () => SignedPackageArchiveIOUtility.SeekReaderForwardToMatchByteSignature(
                        test.Reader,
                        byteSignature));

                Assert.Equal("byteSignature", exception.ParamName);
                Assert.Equal(test.Reader.BaseStream.Length, test.Reader.BaseStream.Position);
            }
        }

        [Fact]
        public void SeekReaderBackwardToMatchByteSignature_WhenReaderNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => SignedPackageArchiveIOUtility.SeekReaderBackwardToMatchByteSignature(
                    reader: null,
                    byteSignature: new byte[] { 0 }));

            Assert.Equal("reader", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(new byte[] { })]
        public void SeekReaderBackwardToMatchByteSignature_WhenByteSignatureNullOrEmpty_Throws(byte[] byteSignature)
        {
            using (var test = new ReadTest())
            {
                test.Reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.End);

                var exception = Assert.Throws<ArgumentException>(
                    () => SignedPackageArchiveIOUtility.SeekReaderBackwardToMatchByteSignature(
                        test.Reader,
                        byteSignature));

                Assert.Equal("byteSignature", exception.ParamName);
                Assert.Equal(test.Reader.BaseStream.Length, test.Reader.BaseStream.Position);
            }
        }

        [Fact]
        public void SeekReaderBackwardToMatchByteSignature_WhenByteSignatureNotFound_MessageContainsByteSignature()
        {
            using (var test = new ReadTest())
            {
                var byteSignature = new byte[] { 6, 7 };

                test.Reader.BaseStream.Seek(offset: -byteSignature.Length, origin: SeekOrigin.End);

                var exception = Assert.Throws<InvalidDataException>(
                    () => SignedPackageArchiveIOUtility.SeekReaderBackwardToMatchByteSignature(
                        test.Reader,
                        byteSignature));

                Assert.Contains(BitConverter.ToString(byteSignature).Replace("-", ""), exception.Message);
                Assert.Equal(4, test.Reader.BaseStream.Position);
            }
        }

        [Fact]
        public void SeekReaderBackwardToMatchByteSignature_WhenInitialPositionAtEnd_Throws()
        {
            using (var test = new ReadTest())
            {
                var byteSignature = new byte[] { 2, 3 };

                test.Reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.End);

                var exception = Assert.Throws<ArgumentOutOfRangeException>(
                    () => SignedPackageArchiveIOUtility.SeekReaderBackwardToMatchByteSignature(
                        test.Reader,
                        byteSignature));

                Assert.Equal("byteSignature", exception.ParamName);
                Assert.Equal(test.Reader.BaseStream.Length, test.Reader.BaseStream.Position);
            }
        }

        [Fact]
        public void SeekReaderBackwardToMatchByteSignature_WhenInitialPositionNearEndAndByteSignatureNotFound_Throws()
        {
            using (var test = new ReadTest())
            {
                var byteSignature = new byte[] { 3, 2 };

                test.Reader.BaseStream.Seek(offset: -byteSignature.Length, origin: SeekOrigin.End);

                Assert.Throws<InvalidDataException>(
                    () => SignedPackageArchiveIOUtility.SeekReaderBackwardToMatchByteSignature(
                        test.Reader,
                        byteSignature));

                Assert.Equal(4, test.Reader.BaseStream.Position);
            }
        }

        [Fact]
        public void SeekReaderBackwardToMatchByteSignature_WhenInitialPositionNearEndAndByteSignatureAtEnd_Seeks()
        {
            using (var test = new ReadTest())
            {
                var byteSignature = new byte[] { 4, 5 };

                test.Reader.BaseStream.Seek(offset: -byteSignature.Length, origin: SeekOrigin.End);

                SignedPackageArchiveIOUtility.SeekReaderBackwardToMatchByteSignature(test.Reader, byteSignature);

                Assert.Equal(4, test.Reader.BaseStream.Position);

                var actual = test.Reader.ReadBytes(byteSignature.Length);

                Assert.Equal(byteSignature, actual);
            }
        }

        [Fact]
        public void SeekReaderBackwardToMatchByteSignature_WhenInitialPositionNearEndAndByteSignatureAtStart_Seeks()
        {
            using (var test = new ReadTest())
            {
                var byteSignature = new byte[] { 0, 1 };

                test.Reader.BaseStream.Seek(offset: -byteSignature.Length, origin: SeekOrigin.End);

                SignedPackageArchiveIOUtility.SeekReaderBackwardToMatchByteSignature(test.Reader, byteSignature);

                Assert.Equal(0, test.Reader.BaseStream.Position);

                var actual = test.Reader.ReadBytes(byteSignature.Length);

                Assert.Equal(byteSignature, actual);
            }
        }

        [Fact]
        public void SeekReaderBackwardToMatchByteSignature_WhenInitialPositionAtStart_Seeks()
        {
            using (var test = new ReadTest())
            {
                var byteSignature = new byte[] { 0, 1 };

                test.Reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.Begin);

                SignedPackageArchiveIOUtility.SeekReaderBackwardToMatchByteSignature(
                    test.Reader,
                    byteSignature);

                Assert.Equal(0, test.Reader.BaseStream.Position);

                var actual = test.Reader.ReadBytes(byteSignature.Length);

                Assert.Equal(byteSignature, actual);
            }
        }

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
                        hashAlgorithm: test.HashAlgorithm,
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
                        test.HashAlgorithm,
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
                        test.HashAlgorithm,
                        position: 0));

                Assert.Equal("position", exception.ParamName);
                Assert.Equal(1, test.Reader.BaseStream.Position);
            }
        }

#if !IS_CORECLR
        [Fact]
        public void ReadAndHashUntilPosition_WhenPositionAtStart_ReadsAndHashes()
        {
            using (var test = new ReadHashTest())
            {
                SignedPackageArchiveIOUtility.ReadAndHashUntilPosition(
                    test.Reader,
                    test.HashAlgorithm,
                    test.Reader.BaseStream.Length);

                var actualHash = test.GetHash();

                Assert.Equal("F+iNsYev1iwW5d6/PmUnzQBrwBK8kLUagQzYDC1RH0M=", actualHash);
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
                    test.HashAlgorithm,
                    position: 5);

                var actualHash = test.GetHash();

                Assert.Equal("H1KP/SiVY0wXZTfAVdqlwJcbeRVRmZkzeg41VBDY/Zg=", actualHash);
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
                    test.HashAlgorithm,
                    test.Reader.BaseStream.Length);

                var actualHash = test.GetHash();

                Assert.Equal("47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=", actualHash);
                Assert.Equal(test.Reader.BaseStream.Length, test.Reader.BaseStream.Position);
            }
        }
#endif

        [Fact]
        public void HashBytes_WhenHashAlgorithmNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => SignedPackageArchiveIOUtility.HashBytes(
                    hashAlgorithm: null, bytes: new byte[] { 0 }));

            Assert.Equal("hashAlgorithm", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(new byte[] { })]
        public void HashBytes_WhenBytesNullOrEmpty_Throws(byte[] bytes)
        {
            using (var hashAlgorithm = SHA256.Create())
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => SignedPackageArchiveIOUtility.HashBytes(
                        hashAlgorithm,
                        bytes));

                Assert.Equal("bytes", exception.ParamName);
            }
        }

#if !IS_CORECLR
        [Fact]
        public void HashBytes_WithInputBytes_Hashes()
        {
            using (var hashAlgorithm = SHA256.Create())
            {
                SignedPackageArchiveIOUtility.HashBytes(hashAlgorithm, new byte[] { 0, 1, 2 });

                hashAlgorithm.TransformFinalBlock(new byte[0], inputOffset: 0, inputCount: 0);

                var actualHash = Convert.ToBase64String(hashAlgorithm.Hash);

                Assert.Equal("rksygOVuL6+D9BSm49q+nV++GJdlRMBf7RIazLhbU/w=", actualHash);
            }
        }
#endif

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
        public void ReadSignedArchiveMetadata_WithNonEmptyZip_ReturnsMetadata()
        {
            using (var stream = new MemoryStream(GetResource("3Entries.zip")))
            using (var reader = new BinaryReader(stream))
            {
                var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);

                Assert.Equal(0, metadata.StartOfFileHeaders);
                Assert.Equal(0x84, metadata.EndOfFileHeaders);

                Assert.Equal(3, metadata.CentralDirectoryHeaders.Count);
                Assert.Equal(0x11d, metadata.EndOfCentralDirectory);
                Assert.Equal(0x11d, metadata.EndOfCentralDirectoryRecordPosition);

                Assert.Equal(0, metadata.SignatureCentralDirectoryHeaderIndex);

                var header = metadata.CentralDirectoryHeaders[0];

                Assert.Equal(0x84, header.Position);
                Assert.Equal(0, header.OffsetToFileHeader);
                Assert.Equal(0x2a, header.FileEntryTotalSize);
                Assert.False(header.IsPackageSignatureFile);
                Assert.Equal(0x33, header.HeaderSize);
                Assert.Equal(0, header.ChangeInOffset);
                Assert.Equal(0, header.IndexInHeaders);

                header = metadata.CentralDirectoryHeaders[1];

                Assert.Equal(0xb7, header.Position);
                Assert.Equal(0x2a, header.OffsetToFileHeader);
                Assert.Equal(0x2f, header.FileEntryTotalSize);
                Assert.False(header.IsPackageSignatureFile);
                Assert.Equal(0x33, header.HeaderSize);
                Assert.Equal(0, header.ChangeInOffset);
                Assert.Equal(1, header.IndexInHeaders);

                header = metadata.CentralDirectoryHeaders[2];

                Assert.Equal(0xea, header.Position);
                Assert.Equal(0x59, header.OffsetToFileHeader);
                Assert.Equal(0x2b, header.FileEntryTotalSize);
                Assert.False(header.IsPackageSignatureFile);
                Assert.Equal(0x33, header.HeaderSize);
                Assert.Equal(0, header.ChangeInOffset);
                Assert.Equal(2, header.IndexInHeaders);
            }
        }

        [Fact]
        public void ReadSignedArchiveMetadata_WithPackageSignatureEntry_ReturnsMetadata()
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
                $"NuGet.Packaging.Test.compiler.resources.{name}",
                typeof(SignedPackageArchiveUtilityTests));
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
            internal HashAlgorithm HashAlgorithm { get; }

            internal ReadHashTest()
            {
                HashAlgorithm = SHA256.Create();
            }

            protected override void Dispose(bool disposing)
            {
                if (!IsDisposed)
                {
                    HashAlgorithm.Dispose();
                }

                base.Dispose(disposing);
            }

            internal string GetHash()
            {
#if !IS_CORECLR
                HashAlgorithm.TransformFinalBlock(new byte[0], inputOffset: 0, inputCount: 0);

                return Convert.ToBase64String(HashAlgorithm.Hash);
#else
                throw new NotImplementedException();
#endif
            }
        }
    }
}