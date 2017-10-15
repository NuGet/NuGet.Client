// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;

namespace Test.Utility.Signing
{
    /// <summary>
    /// Test certificate pair.
    /// </summary>
    public class TestCertificate
    {
        /// <summary>
        /// Cert signed by the CA
        /// </summary>
        public X509Certificate2 Cert { get; set; }

        /// <summary>
        /// Self signed root CA cert
        /// </summary>
        public X509Certificate2 CA { get; set; }

        /// <summary>
        /// Public CA cert.
        /// </summary>
        public X509Certificate2 PublicCA => SigningTestUtility.GetPublicCert(CA);

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
            return new TrustedTestCert<TestCertificate>(this, e => PublicCA);
        }

#if IS_DESKTOP
        public static TestCertificate Generate()
        {
            var caName = "NuGetTest CA";

            var pair = new TestCertificate
            {
                CA = SigningTestUtility.GenerateCertificate(caName, issuer: null, modifyGenerator: null)
            };

            pair.Cert = SigningTestUtility.GenerateCertificate("NuGetTest Author", pair.CA, modifyGenerator: null);

            return pair;
        }
#endif
    }
}
