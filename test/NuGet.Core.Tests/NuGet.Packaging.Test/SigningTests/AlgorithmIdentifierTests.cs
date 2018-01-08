// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;
using NuGet.Packaging.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class AlgorithmIdentifierTests
    {
        [Fact]
        public void Read_WithInvalidAsn1_Throws()
        {
            Assert.Throws<CryptographicException>(
                () => AlgorithmIdentifier.Read(new byte[] { 0x30, 0x0b }));
        }

        [Fact]
        public void Read_WithSha1_ReturnsAlgorithmIdentifier()
        {
            var algorithmId = AlgorithmIdentifier.Read(
                new byte[]
                {
                    0x30, 0x07, 0x06, 0x05, 0x2b, 0x0e, 0x03, 0x02,
                    0x1a
                });

            Assert.Equal(Oids.Sha1, algorithmId.Algorithm.Value);
        }

        [Fact]
        public void Read_WithSha256_ReturnsAlgorithmIdentifier()
        {
            var algorithmId = AlgorithmIdentifier.Read(
                new byte[]
                {
                    0x30, 0x0b, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01,
                    0x65, 0x03, 0x04, 0x02, 0x01
                });

            Assert.Equal(Oids.Sha256, algorithmId.Algorithm.Value);
        }

        [Fact]
        public void Read_WithSha384_ReturnsAlgorithmIdentifier()
        {
            var algorithmId = AlgorithmIdentifier.Read(
                new byte[]
                {
                    0x30, 0x0b, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01,
                    0x65, 0x03, 0x04, 0x02, 0x02
                });

            Assert.Equal(Oids.Sha384, algorithmId.Algorithm.Value);
        }

        [Fact]
        public void Read_WithSha512_ReturnsAlgorithmIdentifier()
        {
            var algorithmId = AlgorithmIdentifier.Read(
                new byte[]
                {
                    0x30, 0x0b, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01,
                    0x65, 0x03, 0x04, 0x02, 0x03
                });

            Assert.Equal(Oids.Sha512, algorithmId.Algorithm.Value);
        }
    }
}