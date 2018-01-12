// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SignedPackageArchiveUtilityTests
    {
        [Fact]
        public void IsSigned_WhenReaderIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => SignedPackageArchiveUtility.IsSigned(reader: null));

            Assert.Equal("reader", exception.ParamName);
        }

        [Fact]
        public void IsSigned_WithEmptyZip_ReturnsFalse()
        {
            using (var test = new Test(GetEmptyZip()))
            {
                var isSigned = SignedPackageArchiveUtility.IsSigned(test.Reader);

                Assert.False(isSigned);
            }
        }

        [Fact]
        public void IsSigned_WithCorrectSignatureFileEntry_ReturnsTrue()
        {
            using (var test = new Test(GetResource("SignatureFileEntry.zip")))
            {
                var isSigned = SignedPackageArchiveUtility.IsSigned(test.Reader);

                Assert.True(isSigned);
            }
        }

        [Fact]
        public void IsSigned_WithLocalFileHeaderUsingUtf8_ReturnsFalse()
        {
            var zipBytes = GetResource("SignatureFileEntry.zip");

            zipBytes[7] = 0x08;

            using (var test = new Test(zipBytes))
            {
                var isSigned = SignedPackageArchiveUtility.IsSigned(test.Reader);

                Assert.False(isSigned);
            }
        }

        [Fact]
        public void IsSigned_WithCentralDirectoryHeaderUsingUtf8_ReturnsFalse()
        {
            var zipBytes = GetResource("SignatureFileEntry.zip");

            zipBytes[35] = 0x08;

            using (var test = new Test(zipBytes))
            {
                var isSigned = SignedPackageArchiveUtility.IsSigned(test.Reader);

                Assert.False(isSigned);
            }
        }

        [Fact]
        public void IsSigned_WithIncorrectSignatureFileNameInLocalFileHeader_ReturnsFalse()
        {
            var zipBytes = GetResource("SignatureFileEntry.zip");
            var fileName = Encoding.ASCII.GetBytes(SigningSpecifications.V1.SignaturePath.ToUpper());

            Array.Copy(fileName, sourceIndex: 0, destinationArray: zipBytes, destinationIndex: 0x1e, length: fileName.Length);

            using (var test = new Test(zipBytes))
            {
                var isSigned = SignedPackageArchiveUtility.IsSigned(test.Reader);

                Assert.False(isSigned);
            }
        }

        [Fact]
        public void IsSigned_WithIncorrectSignatureFileNameInCentralDirectoryHeader_ReturnsFalse()
        {
            var zipBytes = GetResource("SignatureFileEntry.zip");
            var fileName = Encoding.ASCII.GetBytes(SigningSpecifications.V1.SignaturePath.ToUpper());

            Array.Copy(fileName, sourceIndex: 0, destinationArray: zipBytes, destinationIndex: 0x5a, length: fileName.Length);

            using (var test = new Test(zipBytes))
            {
                var isSigned = SignedPackageArchiveUtility.IsSigned(test.Reader);

                Assert.False(isSigned);
            }
        }

        [Fact]
        public void IsZip64_WhenReaderIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => SignedPackageArchiveUtility.IsZip64(reader: null));

            Assert.Equal("reader", exception.ParamName);
        }

        [Fact]
        public void IsZip64_WithEmptyZip_ReturnsFalse()
        {
            using (var test = new Test(GetEmptyZip()))
            {
                var isZip64 = SignedPackageArchiveUtility.IsZip64(test.Reader);

                Assert.False(isZip64);
            }
        }

        [Fact]
        public void IsZip64_WithNonEmptyZip_ReturnsFalse()
        {
            using (var test = new Test(GetNonEmptyZip()))
            {
                var isZip64 = SignedPackageArchiveUtility.IsZip64(test.Reader);

                Assert.False(isZip64);
            }
        }

        [Fact]
        public void IsZip64_WithEmptyZip64_ReturnsTrue()
        {
            using (var test = new Test(GetResource("EmptyZip64.zip")))
            {
                var isZip64 = SignedPackageArchiveUtility.IsZip64(test.Reader);

                Assert.True(isZip64);
            }
        }

        [Fact]
        public void IsZip64_WithLocalFileHeaderWithZip64ExtraField_ReturnsTrue()
        {
            using (var test = new Test(GetResource("LocalFileHeaderWithZip64ExtraField.zip")))
            {
                var isZip64 = SignedPackageArchiveUtility.IsZip64(test.Reader);

                Assert.True(isZip64);
            }
        }

        [Fact]
        public void IsZip64_WithCentralDirectoryHeaderWithZip64ExtraField_ReturnsTrue()
        {
            using (var test = new Test(GetResource("CentralDirectoryHeaderWithZip64ExtraField.zip")))
            {
                var isZip64 = SignedPackageArchiveUtility.IsZip64(test.Reader);

                Assert.True(isZip64);
            }
        }

        private static byte[] GetEmptyZip()
        {
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                }

                return stream.ToArray();
            }
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

        private static byte[] GetResource(string name)
        {
            return ResourceTestUtility.GetResourceBytes(
                $"NuGet.Packaging.Test.compiler.resources.{name}",
                typeof(SignedPackageArchiveUtilityTests));
        }

        private sealed class Test : IDisposable
        {
            private readonly MemoryStream _stream;
            private bool _isDisposed;

            internal BinaryReader Reader { get; }

            internal Test(byte[] bytes)
            {
                _stream = new MemoryStream(bytes);
                Reader = new BinaryReader(_stream);
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    Reader.Dispose();
                    _stream.Dispose();

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }
        }
    }
}