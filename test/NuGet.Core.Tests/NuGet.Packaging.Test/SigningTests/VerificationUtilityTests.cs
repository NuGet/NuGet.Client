// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class VerificationUtilityTests
    {
        [Theory]
        [InlineData(SignatureVerificationStatusFlags.NoErrors, SignatureVerificationStatus.Valid)]
        [InlineData(SignatureVerificationStatusFlags.NoSignature, SignatureVerificationStatus.Untrusted)]
        [InlineData(SignatureVerificationStatusFlags.NoCertificate, SignatureVerificationStatus.Illegal)]
        [InlineData(SignatureVerificationStatusFlags.MultipleSignatures, SignatureVerificationStatus.Illegal)]
        [InlineData(SignatureVerificationStatusFlags.SignatureCheckFailed, SignatureVerificationStatus.Illegal)]
        [InlineData(SignatureVerificationStatusFlags.SignatureAlgorithmUnsupported, SignatureVerificationStatus.Illegal)]
        [InlineData(SignatureVerificationStatusFlags.CertificatePublicKeyInvalid, SignatureVerificationStatus.Illegal)]
        [InlineData(SignatureVerificationStatusFlags.HasLifetimeSigningEku, SignatureVerificationStatus.Illegal)]
        [InlineData(SignatureVerificationStatusFlags.CertificateValidityInTheFuture, SignatureVerificationStatus.Illegal)]
        [InlineData(SignatureVerificationStatusFlags.CertificateExpired, SignatureVerificationStatus.Untrusted)]
        [InlineData(SignatureVerificationStatusFlags.HashAlgorithmUnsupported, SignatureVerificationStatus.Illegal)]
        [InlineData(SignatureVerificationStatusFlags.MessageImprintUnsupportedAlgorithm, SignatureVerificationStatus.Illegal)]
        [InlineData(SignatureVerificationStatusFlags.IntegrityCheckFailed, SignatureVerificationStatus.Suspect)]
        [InlineData(SignatureVerificationStatusFlags.ChainBuildingFailure, SignatureVerificationStatus.Untrusted)]
        [InlineData(SignatureVerificationStatusFlags.UnknownRevocation, SignatureVerificationStatus.Untrusted)]
        [InlineData(SignatureVerificationStatusFlags.CertificateRevoked, SignatureVerificationStatus.Suspect)]
        [InlineData(SignatureVerificationStatusFlags.UntrustedRoot, SignatureVerificationStatus.Untrusted)]
        [InlineData(SignatureVerificationStatusFlags.GeneralizedTimeOutsideValidity, SignatureVerificationStatus.Illegal)]
        [InlineData(SignatureVerificationStatusFlags.Suspect, SignatureVerificationStatus.Suspect)]
        [InlineData(SignatureVerificationStatusFlags.Illegal, SignatureVerificationStatus.Illegal)]
        [InlineData(SignatureVerificationStatusFlags.Untrusted, SignatureVerificationStatus.Untrusted)]
        public void GetSignatureVerificationStatus_WithStatusFlag_ReturnsStatus(
            SignatureVerificationStatusFlags flags,
            SignatureVerificationStatus expectedStatus)
        {
            Assert.Equal(expectedStatus, VerificationUtility.GetSignatureVerificationStatus(flags));
        }

        [Fact]
        public void GetSignatureVerificationStatus_WithUnknownStatusFlag_ReturnsUnknownStatus()
        {
            Assert.Equal(
                SignatureVerificationStatus.Unknown,
                VerificationUtility.GetSignatureVerificationStatus((SignatureVerificationStatusFlags)(1 << 31)));
        }
    }
}