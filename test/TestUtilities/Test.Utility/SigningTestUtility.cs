// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;

namespace Test.Utility
{
    public static class SigningTestUtility
    {
        /// <summary>
        /// A general purpose certificate that can be shared between tests.
        /// </summary>
        public static readonly Lazy<X509Certificate2> SharedTestCert = new Lazy<X509Certificate2>(() => GenerateCertificate());

        /// <summary>
        /// Create a test certificate with bouncy castle.
        /// </summary>
        public static X509Certificate2 GenerateCertificate()
        {
            return GenerateCertificate(modifyGenerator: null);
        }

        /// <summary>
        /// Create a test certificate with bouncy castle.
        /// </summary>
        public static X509Certificate2 GenerateCertificate(Action<X509V3CertificateGenerator> modifyGenerator)
        {
            var random = new SecureRandom();
            var certCN = new X509Name($"CN=NuGet Test Cert");
            var pairGenerator = new RsaKeyPairGenerator();
            var genParams = new KeyGenerationParameters(random, 1024);
            pairGenerator.Init(genParams);
            var pair = pairGenerator.GenerateKeyPair();

            // Create cert
            var certGen = new X509V3CertificateGenerator();
            certGen.SetSubjectDN(certCN);
            certGen.SetIssuerDN(certCN);

            certGen.SetNotAfter(DateTime.UtcNow.Add(TimeSpan.FromHours(1)));
            certGen.SetNotBefore(DateTime.UtcNow.Subtract(TimeSpan.FromHours(1)));
            certGen.SetPublicKey(pair.Public);

            var serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(long.MaxValue), random);
            certGen.SetSerialNumber(serialNumber);

            // Allow changes
            modifyGenerator?.Invoke(certGen);

            var signatureFactory = new Asn1SignatureFactory("SHA512WITHRSA", pair.Private, random);
            var certificate = certGen.Generate(signatureFactory);
            var certResult = new X509Certificate2(certificate.GetEncoded());

            return certResult;
        }
    }
}
