// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;

namespace Test.Utility.Signing
{
    /// <summary>
    /// Test certificate pair.
    /// </summary>
    public class TestCertificate
    {
        /// <summary>
        /// Cert
        /// </summary>
        public X509Certificate2 Cert { get; set; }

        /// <summary>
        /// Public cert.
        /// </summary>
        public X509Certificate2 PublicCert => SigningTestUtility.GetPublicCert(Cert);

        /// <summary>
        /// Trust the PublicCert cert for the life of the object.
        /// </summary>
        /// <remarks>Dispose of the object returned!</remarks>
        public TrustedTestCert<TestCertificate> WithTrust()
        {
            return new TrustedTestCert<TestCertificate>(this, e => PublicCert);
        }

#if IS_DESKTOP
        public static TestCertificate Generate()
        {
            var certName = "NuGetTest " + Guid.NewGuid().ToString();

            var pair = new TestCertificate
            {
                Cert = SigningTestUtility.GenerateCertificate(certName, modifyGenerator: null)
            };

            return pair;
        }
#endif
    }
}
