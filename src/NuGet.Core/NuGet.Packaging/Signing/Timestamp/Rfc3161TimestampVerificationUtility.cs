// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

using System.Security.Cryptography.X509Certificates;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Provides convinience method for verification of a RFC 3161 Timestamp.
    /// </summary>
    internal static class Rfc3161TimestampVerificationUtility
    {
        private const long _ticksPerMicroSecond = 10;

#if IS_DESKTOP

<<<<<<< HEAD
        /// <summary>
        /// Validates a SignedCms object containing a timestamp response.
        /// </summary>
        /// <param name="timestampCms">SignedCms response from the timestamp authority.</param>
        /// <param name="specifications">SigningSpecifications used to validate allowed hash algorithms.</param>
        /// <param name="signerCertificate">X509Certificate2 used to sign the data that was timestamped.</param>
        /// <param name="data">byte[] data that was signed and timestamped.</param>
        public static void Validate(SignedCms timestampCms, SigningSpecifications specifications, X509Certificate2 signerCertificate, byte[] data)
        {
            if (!ValidateTimestampAlgorithm(timestampCms, specifications))
            {
                throw new TimestampException(LogMessage.CreateError(
                    NuGetLogCode.NU3021,
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.TimestampResponseExceptionGeneral,
                    Strings.TimestampFailureInvalidHashAlgorithmOid)));
            }

            Rfc3161TimestampTokenInfo tstInfo;

            if (!TryReadTSTInfoFromSignedCms(timestampCms, out tstInfo))
            {
                throw new TimestampException(LogMessage.CreateError(
                    NuGetLogCode.NU3021,
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.TimestampResponseExceptionGeneral,
                    Strings.TimestampFailureInvalidContentType)));
            }

            if (!ValidateTimestampedData(tstInfo, data))
            {
                throw new TimestampException(LogMessage.CreateError(
                    NuGetLogCode.NU3021,
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.TimestampResponseExceptionGeneral,
                    Strings.TimestampFailureInvalidHash)));
            }

            if (!ValidateSignerCertificateAgainstTimestamp(signerCertificate, tstInfo))
            {
                throw new TimestampException(LogMessage.CreateError(
                    NuGetLogCode.NU3012,
                    Strings.TimestampFailureAuthorCertNotValid));
            }

            var timestamperCertificate = timestampCms.SignerInfos[0].Certificate;

            if (!ValidateTimestampEnhancedKeyUsage(timestamperCertificate))
            {
                throw new TimestampException(LogMessage.CreateError(
                    NuGetLogCode.NU3021,
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.TimestampResponseExceptionGeneral,
                    Strings.TimestampFailureCertInvalidEku)));
            }

            if (!TryBuildTimestampCertificateChain(timestamperCertificate, out var chain))
            {
                throw new TimestampException(LogMessage.CreateError(
                    NuGetLogCode.NU3011,
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.TimestampCertificateChainBuildFailure,
                    CertificateUtility.X509Certificate2ToString(timestamperCertificate))));
            }
        }

        private static bool ValidateSignerCertificateAgainstTimestamp(
=======
        internal static bool ValidateSignerCertificateAgainstTimestamp(
>>>>>>> Update verify command
            X509Certificate2 signerCertificate,
            Rfc3161TimestampTokenInfo tstInfo)
        {
            var tstInfoGenTime = tstInfo.Timestamp;
            var tstInfoAccuracy = tstInfo.AccuracyInMicroseconds;
            long tstInfoAccuracyInTicks;

            if (!tstInfoAccuracy.HasValue)
            {
                if (string.Equals(tstInfo.PolicyId, Oids.BaselineTimestampPolicyOid))
                {
                    tstInfoAccuracyInTicks = TimeSpan.TicksPerSecond;
                }
                else
                {
                    tstInfoAccuracyInTicks = 0;
                }
            }
            else
            {
                tstInfoAccuracyInTicks = tstInfoAccuracy.Value * _ticksPerMicroSecond;
            }

            // everything to UTC
            var timestampUpperGenTimeUtcTicks = tstInfoGenTime.AddTicks(tstInfoAccuracyInTicks).UtcTicks;
            var timestampLowerGenTimeUtcTicks = tstInfoGenTime.Subtract(TimeSpan.FromTicks(tstInfoAccuracyInTicks)).UtcTicks;
            var signerCertExpiryUtcTicks = signerCertificate.NotAfter.ToUniversalTime().Ticks;
            var signerCertBeginUtcTicks = signerCertificate.NotBefore.ToUniversalTime().Ticks;

            return timestampUpperGenTimeUtcTicks < signerCertExpiryUtcTicks &&
                timestampLowerGenTimeUtcTicks > signerCertBeginUtcTicks;
        }

        internal static bool TryReadTSTInfoFromSignedCms(
            SignedCms timestampCms,
            out Rfc3161TimestampTokenInfo tstInfo)
        {
            tstInfo = null;
            if (timestampCms.ContentInfo.ContentType.Value.Equals(Oids.TSTInfoContentTypeOid))
            {
                tstInfo = new Rfc3161TimestampTokenInfo(timestampCms.ContentInfo.Content);
                return true;
            }
            // return false if the signedCms object does not contain the right ContentType
            return false;
        }

        internal static DateTimeOffset GetUpperLimit(SignedCms timestampCms)
        {
            var result = DateTimeOffset.Now;
            if (TryReadTSTInfoFromSignedCms(timestampCms, out var tstInfo))
            {
                // TODO: Get upper limit
            }
            return result;
        }
#endif
    }
}
