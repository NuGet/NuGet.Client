// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace Test.Utility.Signing
{
    internal static class CertificateUtilities
    {
        internal static AsymmetricCipherKeyPair CreateKeyPair()
        {
            var generator = new RsaKeyPairGenerator();

            generator.Init(new KeyGenerationParameters(new SecureRandom(), strength: 2048));

            return generator.GenerateKeyPair();
        }

        internal static string GenerateFingerprint(X509Certificate certificate)
        {
            using (var hashAlgorithm = CryptoHashUtility.GetSha1HashProvider())
            {
                var hash = hashAlgorithm.ComputeHash(certificate.GetEncoded());

                return BitConverter.ToString(hash).Replace("-", "");
            }
        }
    }
}