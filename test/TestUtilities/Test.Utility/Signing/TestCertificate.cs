// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Test.Utility;

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
        /// Public cert.
        /// </summary>
        public X509Certificate2 PublicCertWithPrivateKey => SigningTestUtility.GetPublicCertWithPrivateKey(Cert);

        /// <summary>
        /// Certificate Revocation List associated with a certificate.
        /// This will be null if the certificate was not created as a CA certificate.
        /// </summary>
        public CertificateRevocationList Crl { get; set; }

        /// <summary>
        /// Trust the PublicCert cert for the life of the object.
        /// </summary>
        /// <remarks>Dispose of the object returned!</remarks>
        public TrustedTestCert<TestCertificate> WithTrust(StoreName storeName, StoreLocation storeLocation, TestDirectory dir)
        {
            return new TrustedTestCert<TestCertificate>(this, e => PublicCert, storeName, storeLocation, dir);
        }

        /// <summary>
        /// Trust the PublicCert cert for the life of the object.
        /// </summary>
        /// <remarks>Dispose of the object returned!</remarks>
        public TrustedTestCert<TestCertificate> WithPrivateKeyAndTrust(StoreName storeName, StoreLocation storeLocation, TestDirectory dir)
        {
            return new TrustedTestCert<TestCertificate>(this, e => PublicCertWithPrivateKey, storeName, storeLocation, dir);
        }

        public static string GenerateCertificateName()
        {
            return "NuGetTest-" + Guid.NewGuid().ToString();
        }

        public static TestCertificate Generate(Action<TestCertificateGenerator> modifyGenerator = null, ChainCertificateRequest chainCertificateRequest = null)
        {
            var certName = GenerateCertificateName();
            var cert = SigningTestUtility.GenerateCertificateWithKeyInfo(certName, modifyGenerator, chainCertificateRequest: chainCertificateRequest);
            CertificateRevocationList crl = null;

            // create a crl only if the certificate is part of a chain and it is a CA
            if (chainCertificateRequest != null && chainCertificateRequest.IsCA)
            {
                crl = CertificateRevocationList.CreateCrl(cert, chainCertificateRequest.CrlLocalBaseUri);
            }

            var testCertificate = new TestCertificate
            {
                Cert = cert.Certificate,
                Crl = crl
            };

            return testCertificate;
        }
    }
}
