// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;
using NuGet.Packaging.Signing;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Xunit;
using BcCommitmentTypeQualifier = Org.BouncyCastle.Asn1.Esf.CommitmentTypeQualifier;

namespace NuGet.Packaging.Test
{
    public class CommitmentTypeQualifierTests
    {
        [Fact]
        public void Read_WithInvalidAsn1_Throws()
        {
            Assert.Throws<CryptographicException>(
                () => CommitmentTypeQualifier.Read(new byte[] { 0x30, 0x07 }));
        }

        [Fact]
        public void Read_WithOnlyCommitmentTypeId_ReturnsCommitmentTypeQualifier()
        {
            var bcCommitmentTypeQualifier = new BcCommitmentTypeQualifier(PkcsObjectIdentifiers.IdCtiEtsProofOfSender);
            var bytes = bcCommitmentTypeQualifier.GetDerEncoded();

            var commitmentTypeQualifier = CommitmentTypeQualifier.Read(bytes);

            Assert.Equal(
                bcCommitmentTypeQualifier.CommitmentTypeIdentifier.ToString(),
                commitmentTypeQualifier.CommitmentTypeIdentifier.Value);
            Assert.Null(commitmentTypeQualifier.Qualifier);
        }

        [Fact]
        public void Read_WithQualifier_ReturnsCommitmentTypeQualifier()
        {
            var bcCommitmentTypeQualifier = new BcCommitmentTypeQualifier(
                PkcsObjectIdentifiers.IdCtiEtsProofOfReceipt, DerNull.Instance);
            var bytes = bcCommitmentTypeQualifier.GetDerEncoded();

            var commitmentTypeQualifier = CommitmentTypeQualifier.Read(bytes);

            Assert.Equal(
                bcCommitmentTypeQualifier.CommitmentTypeIdentifier.ToString(),
                commitmentTypeQualifier.CommitmentTypeIdentifier.Value);
            Assert.Equal(DerNull.Instance.GetDerEncoded(), commitmentTypeQualifier.Qualifier);
        }
    }
}