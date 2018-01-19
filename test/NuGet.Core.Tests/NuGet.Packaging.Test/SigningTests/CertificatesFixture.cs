// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Packaging.Signing;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Test.Utility.Signing;

namespace NuGet.Packaging.Test
{
    public sealed class CertificatesFixture : IDisposable
    {
        private readonly X509Certificate2 _defaultCertificate;
        private readonly X509Certificate2 _rsaSsaPssCertificate;
        private readonly X509Certificate2 _lifetimeSigningCertificate;
        private readonly X509Certificate2 _notYetValidCertificate;

        private bool _isDisposed;

        public CertificatesFixture()
        {
            _defaultCertificate = SigningTestUtility.GenerateCertificate("test", generator => { });
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

            _notYetValidCertificate = SigningTestUtility.GenerateCertificate(
                "test",
                SigningTestUtility.CertificateModificationGeneratorNotYetValidCert);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _defaultCertificate.Dispose();
                _lifetimeSigningCertificate.Dispose();
                _notYetValidCertificate.Dispose();
                _rsaSsaPssCertificate.Dispose();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }

        internal X509Certificate2 GetDefaultCertificate() => Clone(_defaultCertificate);
        internal X509Certificate2 GetLifetimeSigningCertificate() => Clone(_lifetimeSigningCertificate);
        internal X509Certificate2 GetNotYetValidCertificate() => Clone(_notYetValidCertificate);
        internal X509Certificate2 GetRsaSsaPssCertificate() => Clone(_rsaSsaPssCertificate);

        private static X509Certificate2 Clone(X509Certificate2 certificate)
        {
            var bytes = certificate.Export(X509ContentType.Pkcs12);

            return new X509Certificate2(bytes);
        }
    }
}