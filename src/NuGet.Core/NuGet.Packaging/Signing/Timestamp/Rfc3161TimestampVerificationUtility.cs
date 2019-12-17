// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

#if HAS_SIGNING
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
        private const double _millisecondsPerMicrosecond = 0.001;

#if HAS_SIGNING

        internal static bool ValidateSignerCertificateAgainstTimestamp(
            X509Certificate2 signerCertificate,
            Timestamp timestamp)
        {
            DateTimeOffset signerCertExpiry = DateTime.SpecifyKind(signerCertificate.NotAfter, DateTimeKind.Local);
            DateTimeOffset signerCertBegin = DateTime.SpecifyKind(signerCertificate.NotBefore, DateTimeKind.Local);

            return timestamp.UpperLimit < signerCertExpiry &&
                timestamp.LowerLimit > signerCertBegin;
        }

        internal static bool TryReadTSTInfoFromSignedCms(
            SignedCms timestampCms,
            out Rfc3161TimestampTokenInfo tstInfo)
        {
            tstInfo = null;
            if (timestampCms.ContentInfo.ContentType.Value.Equals(Oids.TSTInfoContentType))
            {
                tstInfo = new Rfc3161TimestampTokenInfo(timestampCms.ContentInfo.Content);
                return true;
            }
            // return false if the signedCms object does not contain the right ContentType
            return false;
        }

        internal static double GetAccuracyInMilliseconds(Rfc3161TimestampTokenInfo tstInfo)
        {
            double accuracyInMilliseconds;

            if (!tstInfo.AccuracyInMicroseconds.HasValue)
            {
                if (StringComparer.Ordinal.Equals(tstInfo.PolicyId, Oids.BaselineTimestampPolicy))
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
                accuracyInMilliseconds = tstInfo.AccuracyInMicroseconds.Value * _millisecondsPerMicrosecond;
            }

            if (accuracyInMilliseconds < 0)
            {
                throw new InvalidDataException(Strings.VerifyError_TimestampInvalid);
            }

            return accuracyInMilliseconds;
        }
#endif
    }
}
