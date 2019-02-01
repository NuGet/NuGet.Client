// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace Test.Utility.Signing
{
    public sealed class BCIssueCertificateOptions
    {
        public Action<X509V3CertificateGenerator> CustomizeCertificate { get; set; }
        public DateTimeOffset NotAfter { get; set; }
        public DateTimeOffset NotBefore { get; set; }

        public AsymmetricKeyParameter IssuerPrivateKey { get; set; }

        public AsymmetricCipherKeyPair KeyPair { get; set; }

        public X509Name SubjectName { get; set; }

        public string SignatureAlgorithmName { get; set; }

        public BCIssueCertificateOptions()
        {
            NotBefore = DateTimeOffset.UtcNow;
            NotAfter = NotBefore.AddHours(2);
            SignatureAlgorithmName = "SHA256WITHRSA";
        }

        public static BCIssueCertificateOptions CreateDefaultForRootCertificateAuthority()
        {
            var keyPair = CreateKeyPair();
            var id = CertificateUtilities.GenerateRandomId();
            var subjectName = new X509Name($"C=US,ST=WA,L=Redmond,O=NuGet,CN=NuGet Test Root Certificate Authority ({id})");

            return new BCIssueCertificateOptions()
            {
                KeyPair = keyPair,
                IssuerPrivateKey = keyPair.Private,
                SubjectName = subjectName
            };
        }

        public static BCIssueCertificateOptions CreateDefaultForIntermediateCertificateAuthority()
        {
            var keyPair = CreateKeyPair();
            var id = CertificateUtilities.GenerateRandomId();
            var subjectName = new X509Name($"C=US,ST=WA,L=Redmond,O=NuGet,CN=NuGet Test Intermediate Certificate Authority ({id})");

            return new BCIssueCertificateOptions()
            {
                KeyPair = keyPair,
                SubjectName = subjectName
            };
        }

        public static BCIssueCertificateOptions CreateDefaultForEndCertificate()
        {
            var keyPair = CreateKeyPair();
            var id = CertificateUtilities.GenerateRandomId();
            var subjectName = new X509Name($"C=US,ST=WA,L=Redmond,O=NuGet,CN=NuGet Test Certificate ({id})");

            return new BCIssueCertificateOptions()
            {
                KeyPair = keyPair,
                SubjectName = subjectName
            };
        }

        public static BCIssueCertificateOptions CreateDefaultForTimestampService()
        {
            var keyPair = CreateKeyPair();
            var id = Guid.NewGuid().ToString();
            var subjectName = new X509Name($"C=US,L=Redmond,O=NuGet,CN=NuGet Test Timestamp Service ({id})");


            return new BCIssueCertificateOptions()
            {
                KeyPair = keyPair,
                SubjectName = subjectName
            };
        }

        private static AsymmetricCipherKeyPair CreateKeyPair(int strength = 2048)
        {
            var generator = new RsaKeyPairGenerator();

            generator.Init(new KeyGenerationParameters(new SecureRandom(), strength));

            return generator.GenerateKeyPair();
        }
    }
}