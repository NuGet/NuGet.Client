// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Packaging.Signing;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Test.Utility.Signing;

namespace NuGet.Packaging.Test
{
    public sealed class CertificatesFixture : IDisposable
    {
        private readonly X509Certificate2 _defaultCertificate;
        private readonly X509Certificate2 _rsaSsaPssCertificate;
        private readonly X509Certificate2 _lifetimeSigningCertificate;
        private readonly X509Certificate2 _expiredCertificate;
        private readonly X509Certificate2 _notYetValidCertificate;
        private readonly X509Certificate2 _nonSelfSignedCertificate;

        private bool _isDisposed;

        internal AsymmetricCipherKeyPair DefaultKeyPair { get; }

        public CertificatesFixture()
        {
            DefaultKeyPair = SigningTestUtility.GenerateKeyPair(publicKeyLength: 2048);
            _defaultCertificate = SigningTestUtility.GenerateCertificate("test", DefaultKeyPair);
            _rsaSsaPssCertificate = SigningTestUtility.GenerateCertificate("test", generator => { }, "SHA256WITHRSAANDMGF1");
            _lifetimeSigningCertificate = SigningTestUtility.GenerateCertificate(
                "test",
                generator =>
                {
                    generator.AddExtension(
                        X509Extensions.ExtendedKeyUsage.Id,
                        critical: true,
                        extensionValue: new DerSequence(new DerObjectIdentifier(Oids.LifetimeSigningEku)));
                });
            _expiredCertificate = SigningTestUtility.GenerateCertificate(
                "test",
                SigningTestUtility.CertificateModificationGeneratorExpiredCert);
            _notYetValidCertificate = SigningTestUtility.GenerateCertificate(
                "test",
                SigningTestUtility.CertificateModificationGeneratorNotYetValidCert);
            _nonSelfSignedCertificate = SigningTestUtility.GenerateCertificate(
                "test non-self-signed certificate", // Must be different than the issuing certificate's subject name.
                generator => { },
                chainCertificateRequest: new ChainCertificateRequest() { Issuer = _defaultCertificate });
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _defaultCertificate.Dispose();
                _lifetimeSigningCertificate.Dispose();
                _expiredCertificate.Dispose();
                _notYetValidCertificate.Dispose();
                _rsaSsaPssCertificate.Dispose();
                _nonSelfSignedCertificate.Dispose();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }

        internal X509Certificate2 GetDefaultCertificate() => Clone(_defaultCertificate);
        internal X509Certificate2 GetExpiredCertificate() => Clone(_expiredCertificate);
        internal X509Certificate2 GetLifetimeSigningCertificate() => Clone(_lifetimeSigningCertificate);
        internal X509Certificate2 GetNonSelfSignedCertificate() => Clone(_nonSelfSignedCertificate);
        internal X509Certificate2 GetNotYetValidCertificate() => Clone(_notYetValidCertificate);
        internal X509Certificate2 GetRsaSsaPssCertificate() => Clone(_rsaSsaPssCertificate);

        private static X509Certificate2 Clone(X509Certificate2 certificate)
        {
            var bytes = certificate.Export(X509ContentType.Pkcs12);

            return new X509Certificate2(bytes);
        }
    }
}