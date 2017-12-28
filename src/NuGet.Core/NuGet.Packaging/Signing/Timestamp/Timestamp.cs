// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

namespace NuGet.Packaging.Signing
{
    public sealed class Timestamp
    {
#if IS_DESKTOP

        /// <summary>
        /// Upper limit of Timestamp.
        /// </summary>
        public DateTimeOffset UpperLimit { get; }

        /// <summary>
        /// Lower limit of Timestamp.
        /// </summary>
        public DateTimeOffset LowerLimit { get; }

        /// <summary>
        /// Time timestamp was created by the Time Stamp Authority.
        /// </summary>
        public DateTimeOffset GeneralizedTime { get; }

        /// <summary>
        /// A SignedCms object holding the timestamp and SignerInfo.
        /// </summary>
        public SignedCms SignedCms { get; }

        /// <summary>
        /// SignerInfo for this timestamp.
        /// </summary>
        public SignerInfo SignerInfo => SignedCms.SignerInfos[0];

        /// <summary>
        /// Timestamp token info for this timestamp.
        /// </summary>
        internal Rfc3161TimestampTokenInfo TstInfo { get; }

        /// <summary>
        /// Default constructor. Limits are set to current time.
        /// </summary>
        public Timestamp()
        {
            GeneralizedTime = DateTimeOffset.Now;
            UpperLimit = GeneralizedTime;
            LowerLimit = GeneralizedTime;
        }

        /// <summary>
        /// SignedCms containing a time stamp authority token reponse
        /// </summary>
        /// <param name="timestampCms">SignedCms from Time Stamp Authority</param>
        public Timestamp(SignedCms timestampCms)
        {
            SignedCms = timestampCms ?? throw new ArgumentNullException(nameof(timestampCms));

            if (Rfc3161TimestampVerificationUtility.TryReadTSTInfoFromSignedCms(timestampCms, out var tstInfo))
            {
                try
                {
                    SignedCms.CheckSignature(verifySignatureOnly: true);
                }
                catch (Exception ex)
                {
                    throw new TimestampException(NuGetLogCode.NU3021, Strings.TimestampSignatureValidationFailed, ex);
                }

                TstInfo = tstInfo;
                GeneralizedTime = tstInfo.Timestamp;

                var accuracyInMilliseconds = Rfc3161TimestampVerificationUtility.GetAccuracyInMilliseconds(tstInfo);
                UpperLimit = tstInfo.Timestamp.AddMilliseconds(accuracyInMilliseconds);
                LowerLimit = tstInfo.Timestamp.AddMilliseconds(-accuracyInMilliseconds);
            }
            else
            {
                throw new TimestampException(NuGetLogCode.NU3021, Strings.TimestampSignatureValidationFailed);
            }
        }
#endif
    }
}