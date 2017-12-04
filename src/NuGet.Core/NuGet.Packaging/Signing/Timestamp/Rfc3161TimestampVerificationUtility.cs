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

        internal static double _milliesecondsPerMicrosecond = 0.001;
#if IS_DESKTOP

        internal static bool ValidateSignerCertificateAgainstTimestamp(
            X509Certificate2 signerCertificate,
            Rfc3161TimestampTokenInfo tstInfo)
        {
            var tstInfoGenTime = tstInfo.Timestamp;
            double accuracyInMilliseconds;

            if (!tstInfo.AccuracyInMicroseconds.HasValue)
            {
                if (string.Equals(tstInfo.PolicyId, Oids.BaselineTimestampPolicyOid))
                {
                    accuracyInMilliseconds = 1000;
                }
                else
                {
                    accuracyInMilliseconds = 0;
                }
            }
            else
            {
                accuracyInMilliseconds = tstInfo.AccuracyInMicroseconds.Value * _milliesecondsPerMicrosecond;
            }

            // everything to UTC
            var timestampUpperGenTimeUtcTicks = tstInfoGenTime.AddMilliseconds(accuracyInMilliseconds);
            var timestampLowerGenTimeUtcTicks = tstInfoGenTime.Subtract(TimeSpan.FromMilliseconds(accuracyInMilliseconds));

            var signerCertExpiryUtcTicks = signerCertificate.NotAfter.ToUniversalTime();
            var signerCertBeginUtcTicks = signerCertificate.NotBefore.ToUniversalTime();

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
