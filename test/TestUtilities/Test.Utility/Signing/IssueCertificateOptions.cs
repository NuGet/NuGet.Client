// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;

namespace Test.Utility.Signing
{
    public sealed class IssueCertificateOptions
    {
        public Action<X509V3CertificateGenerator> CustomizeCertificate { get; set; }
        public DateTimeOffset NotAfter { get; set; }
        public DateTimeOffset NotBefore { get; set; }
        public AsymmetricKeyParameter PublicKey { get; }
        public X509Name SubjectName { get; set; }

        public IssueCertificateOptions(AsymmetricKeyParameter publicKey)
        {
            if (publicKey == null)
            {
                throw new ArgumentNullException(nameof(publicKey));
            }

            NotBefore = DateTimeOffset.UtcNow;
            NotAfter = NotBefore.AddHours(2);
            PublicKey = publicKey;

            var id = Guid.NewGuid().ToString();
            SubjectName = new X509Name($"C=US,ST=WA,L=Redmond,O=NuGet,CN=NuGet Test Root Certificate Authority ({id})");
        }
    }
}