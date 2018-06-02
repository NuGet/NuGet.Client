// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Packaging.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class VerificationUtilityTests
    {
        [Theory]
        [InlineData(SignatureVerificationStatusFlags.NoErrors, SignatureVerificationStatus.Valid)]
        [InlineData(SignatureVerificationStatusFlags.NoSignature, SignatureVerificationStatus.Unknown)]
        [InlineData(SignatureVerificationStatusFlags.NoCertificate, SignatureVerificationStatus.Unknown)]
        [InlineData(SignatureVerificationStatusFlags.MultipleSignatures, SignatureVerificationStatus.Unknown)]
        [InlineData(SignatureVerificationStatusFlags.SignatureCheckFailed, SignatureVerificationStatus.Unknown)]
        [InlineData(SignatureVerificationStatusFlags.SignatureAlgorithmUnsupported, SignatureVerificationStatus.Unknown)]
        [InlineData(SignatureVerificationStatusFlags.CertificatePublicKeyInvalid, SignatureVerificationStatus.Unknown)]
        [InlineData(SignatureVerificationStatusFlags.HasLifetimeSigningEku, SignatureVerificationStatus.Unknown)]
        [InlineData(SignatureVerificationStatusFlags.CertificateValidityInTheFuture, SignatureVerificationStatus.Unknown)]
        [InlineData(SignatureVerificationStatusFlags.CertificateExpired, SignatureVerificationStatus.Unknown)]
        [InlineData(SignatureVerificationStatusFlags.HashAlgorithmUnsupported, SignatureVerificationStatus.Unknown)]
        [InlineData(SignatureVerificationStatusFlags.MessageImprintUnsupportedAlgorithm, SignatureVerificationStatus.Unknown)]
        [InlineData(SignatureVerificationStatusFlags.IntegrityCheckFailed, SignatureVerificationStatus.Suspect)]
        [InlineData(SignatureVerificationStatusFlags.ChainBuildingFailure, SignatureVerificationStatus.Unknown)]
        [InlineData(SignatureVerificationStatusFlags.UnknownRevocation, SignatureVerificationStatus.Unknown)]
        [InlineData(SignatureVerificationStatusFlags.CertificateRevoked, SignatureVerificationStatus.Suspect)]
        [InlineData(SignatureVerificationStatusFlags.UntrustedRoot, SignatureVerificationStatus.Unknown)]
        [InlineData(SignatureVerificationStatusFlags.GeneralizedTimeOutsideValidity, SignatureVerificationStatus.Unknown)]
        [InlineData(SignatureVerificationStatusFlags.NoValidTimestamp, SignatureVerificationStatus.Unknown)]
        [InlineData(SignatureVerificationStatusFlags.MultipleTimestamps, SignatureVerificationStatus.Unknown)]
        [InlineData(SignatureVerificationStatusFlags.Suspect, SignatureVerificationStatus.Suspect)]
        [InlineData(SignatureVerificationStatusFlags.Illegal, SignatureVerificationStatus.Unknown)]
        [InlineData(SignatureVerificationStatusFlags.Untrusted, SignatureVerificationStatus.Unknown)]
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

        [Fact]
        public void IsVerificationTarget_WhenSignatureTypeIsUnhandled_Throws()
        {
            Assert.Throws<NotImplementedException>(
                () => VerificationUtility.IsVerificationTarget((SignatureType)int.MaxValue, VerificationTarget.All));
        }

        [Theory]
        [InlineData(SignatureType.Unknown, VerificationTarget.Unknown)]
        [InlineData(SignatureType.Unknown, VerificationTarget.All)]
        [InlineData(SignatureType.Author, VerificationTarget.Author)]
        [InlineData(SignatureType.Author, VerificationTarget.All)]
        [InlineData(SignatureType.Repository, VerificationTarget.Repository)]
        [InlineData(SignatureType.Repository, VerificationTarget.All)]
        public void IsVerificationTarget_WhenSignatureTypeMatches_ReturnsTrue(
            SignatureType signatureType,
            VerificationTarget target)
        {
            var isVerificationTarget = VerificationUtility.IsVerificationTarget(signatureType, target);

            Assert.True(isVerificationTarget);
        }

        [Theory]
        [InlineData(SignatureType.Unknown, VerificationTarget.Author)]
        [InlineData(SignatureType.Unknown, VerificationTarget.Repository)]
        [InlineData(SignatureType.Author, VerificationTarget.Unknown)]
        [InlineData(SignatureType.Author, VerificationTarget.Repository)]
        [InlineData(SignatureType.Repository, VerificationTarget.Unknown)]
        [InlineData(SignatureType.Repository, VerificationTarget.Author)]
        public void IsVerificationTarget_WhenSignatureTypeDoesNotMatch_ReturnsFalse(
            SignatureType signatureType,
            VerificationTarget target)
        {
            var isVerificationTarget = VerificationUtility.IsVerificationTarget(signatureType, target);

            Assert.False(isVerificationTarget);
        }
    }
}