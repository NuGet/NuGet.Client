// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Test.Utility.Signing
{
    public sealed class IssueCertificateOptions
    {
        public Action<TestCertificateGenerator> CustomizeCertificate { get; set; }

        public DateTimeOffset NotAfter { get; set; }

        public DateTimeOffset NotBefore { get; set; }

        public RSA KeyPair { get; set; }

        public X500DistinguishedName SubjectName { get; set; }

        public HashAlgorithmName SignatureAlgorithmName { get; set; }

        public IssueCertificateOptions()
        {
            NotBefore = DateTimeOffset.UtcNow;
            NotAfter = NotBefore.AddHours(2);
            SignatureAlgorithmName = HashAlgorithmName.SHA256;
        }

        public static IssueCertificateOptions CreateDefaultForRootCertificateAuthority()
        {
            using (var keyPair = RSA.Create(keySizeInBits: 2048))
            {
                var id = CertificateUtilities.GenerateRandomId();
                var subjectName = new X500DistinguishedName($"C=US,L=Redmond,O=NuGet,CN=NuGet Test Root Certificate Authority ({id})");

                return new IssueCertificateOptions()
                {
                    KeyPair = keyPair,
                    SubjectName = subjectName
                };
            }
        }

        public static IssueCertificateOptions CreateDefaultForIntermediateCertificateAuthority()
        {
            using (var keyPair = RSA.Create(keySizeInBits: 2048))
            {
                var id = CertificateUtilities.GenerateRandomId();
                var subjectName = new X500DistinguishedName($"C=US,L=Redmond,O=NuGet,CN=NuGet Test Intermediate Certificate Authority ({id})");

                return new IssueCertificateOptions()
                {
                    KeyPair = keyPair,
                    SubjectName = subjectName
                };
            }
        }

        public static IssueCertificateOptions CreateDefaultForEndCertificate()
        {
            using (var keyPair = RSA.Create(keySizeInBits: 2048))
            {
                var id = CertificateUtilities.GenerateRandomId();
                var subjectName = new X500DistinguishedName($"C=US,L=Redmond,O=NuGet,CN=NuGet Test Certificate ({id})");

                return new IssueCertificateOptions()
                {
                    KeyPair = keyPair,
                    SubjectName = subjectName
                };
            }
        }

        public static IssueCertificateOptions CreateDefaultForTimestampService()
        {
            using (var keyPair = RSA.Create(keySizeInBits: 2048))
            {
                var id = Guid.NewGuid().ToString();
                var subjectName = new X500DistinguishedName($"C=US,L=Redmond,O=NuGet,CN=NuGet Test Timestamp Service ({id})");

                return new IssueCertificateOptions()
                {
                    KeyPair = keyPair,
                    SubjectName = subjectName
                };
            }
        }
    }
}