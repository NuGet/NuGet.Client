// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
#endif
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public sealed class AuthorPrimarySignature : PrimarySignature
    {
#if IS_DESKTOP

        public AuthorPrimarySignature(SignedCms signedCms)
            : base(signedCms, SignatureType.Author)
        {
        }

        public override SignatureVerificationSummary Verify(
            Timestamp timestamp,
            SignatureVerifySettings settings,
            HashAlgorithmName fingerprintAlgorithm,
            X509Certificate2Collection certificateExtraStore,
            List<SignatureLog> issues)
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            settings = settings ?? SignatureVerifySettings.Default;

            issues.Add(SignatureLog.InformationLog(string.Format(CultureInfo.CurrentCulture, Strings.SignatureType, Type.ToString())));
            return base.Verify(timestamp, settings, fingerprintAlgorithm, certificateExtraStore, issues);
        }
#endif
    }
}
