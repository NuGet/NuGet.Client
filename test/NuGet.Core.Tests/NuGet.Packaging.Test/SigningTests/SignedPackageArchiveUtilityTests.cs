// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SignedPackageArchiveUtilityTests
    {
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
            using (var test = new SignedPackageArchiveUtilityTest(GetEmptyZip()))
            {
                var isZip64 = SignedPackageArchiveUtility.IsZip64(test.Reader);

                Assert.False(isZip64);
            }
        }

        [Fact]
        public void IsZip64_WithNonEmptyZip_ReturnsFalse()
        {
            using (var test = new SignedPackageArchiveUtilityTest(GetNonEmptyZip()))
            {
                var isZip64 = SignedPackageArchiveUtility.IsZip64(test.Reader);

                Assert.False(isZip64);
            }
        }

        [Fact]
        public void IsZip64_WithEmptyZip64_ReturnsTrue()
        {
            using (var test = new SignedPackageArchiveUtilityTest(GetResource("EmptyZip64.zip")))
            {
                var isZip64 = SignedPackageArchiveUtility.IsZip64(test.Reader);

                Assert.True(isZip64);
            }
        }

        [Fact]
        public void IsZip64_WithLocalFileHeaderWithZip64ExtraField_ReturnsTrue()
        {
            using (var test = new SignedPackageArchiveUtilityTest(GetResource("LocalFileHeaderWithZip64ExtraField.zip")))
            {
                var isZip64 = SignedPackageArchiveUtility.IsZip64(test.Reader);

                Assert.True(isZip64);
            }
        }

        [Fact]
        public void IsZip64_WithCentralDirectoryHeaderWithZip64ExtraField_ReturnsTrue()
        {
            using (var test = new SignedPackageArchiveUtilityTest(GetResource("CentralDirectoryHeaderWithZip64ExtraField.zip")))
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

        private sealed class SignedPackageArchiveUtilityTest : IDisposable
        {
            private readonly MemoryStream _stream;
            private bool _isDisposed;

            internal BinaryReader Reader { get; }

            internal SignedPackageArchiveUtilityTest(byte[] bytes)
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