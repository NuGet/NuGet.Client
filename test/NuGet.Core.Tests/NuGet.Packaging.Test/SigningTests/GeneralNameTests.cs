// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Packaging.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class GeneralNameTests
    {
        [Fact]
        public void Create_WhenDistinguishedNameNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => GeneralName.Create(distinguishedName: null));

            Assert.Equal("distinguishedName", exception.ParamName);
        }

        [Fact]
        public void Create_WithDistinguishedName_ReturnsGeneralName()
        {
            var distinguishedName = new X500DistinguishedName(
                "CN=leaf.test,OU=Test Organizational Unit Name,O=Test Organization Name,L=Redmond,S=WA,C=US",
                X500DistinguishedNameFlags.UseCommas);

            var generalName = GeneralName.Create(distinguishedName);

            Assert.Same(distinguishedName, generalName.DirectoryName);
        }

        [Fact]
        public void Read_WithInvalidAsn1_ReturnsNull()
        {
            var generalName = GeneralName.Read(new byte[] { 0x30, 0x07 });

            Assert.Null(generalName);
        }

        [Fact]
        public void Read_WithUnsupportedChoice_ReturnsNull()
        {
            var generalName = GeneralName.Read(
                new byte[] { 0x30, 0x06, 0xa8, 0x04, 0x06, 0x02, 0x2a, 0x03 });

            Assert.Null(generalName);
        }

        [Fact]
        public void Read_WithDistinguishedName_ReturnsGeneralName()
        {
            var generalName = GeneralName.Read(
                new byte[]
                {
                    0xa4, 0x11, 0x30, 0x0f, 0x31, 0x0d, 0x30, 0x0b,
                    0x06, 0x03, 0x55, 0x04, 0x03, 0x13, 0x04, 0x74,
                    0x65, 0x73, 0x74
                });

            Assert.NotNull(generalName);
            Assert.Equal("CN=test", generalName.DirectoryName.Name);
        }
    }
}