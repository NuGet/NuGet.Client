// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;

namespace Test.Utility.Signing
{
    internal static class CertificateUtilities
    {
        internal static string GenerateFingerprint(X509Certificate2 certificate)
        {
            using (var hashAlgorithm = CryptoHashUtility.GetSha1HashProvider())
            {
                var hash = hashAlgorithm.ComputeHash(certificate.RawData);

                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        internal static string GenerateRandomId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}