// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Packaging.Signing;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
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
        private readonly X509Certificate2 _selfIssuedCertificate;
        private readonly X509Certificate2 _rootCertificate;
        private readonly DisposableList<X509Certificate2> _cyclicChain;

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
            _selfIssuedCertificate = SigningTestUtility.GenerateSelfIssuedCertificate(isCa: false);
            _rootCertificate = SigningTestUtility.GenerateSelfIssuedCertificate(isCa: true);

            const string name1 = "NuGet Cyclic Test Certificate 1";
            const string name2 = "NuGet Cyclic Test Certificate 2";

            var keyPair1 = SigningTestUtility.GenerateKeyPair(publicKeyLength: 2048);
            var keyPair2 = SigningTestUtility.GenerateKeyPair(publicKeyLength: 2048);

            _cyclicChain = new DisposableList<X509Certificate2>()
            {
                SigningTestUtility.GenerateCertificate(name1, name2, keyPair1.Private, keyPair2),
                SigningTestUtility.GenerateCertificate(name2, name1, keyPair2.Private, keyPair1)
            };
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
                _selfIssuedCertificate.Dispose();
                _rootCertificate.Dispose();
                _cyclicChain.Dispose();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }

        internal X509Certificate2 GetDefaultCertificate() => Clone(_defaultCertificate);
        internal X509Certificate2 GetExpiredCertificate() => Clone(_expiredCertificate);
        internal X509Certificate2 GetLifetimeSigningCertificate() => Clone(_lifetimeSigningCertificate);
        internal X509Certificate2 GetNonSelfSignedCertificate() => Clone(_nonSelfSignedCertificate);
        internal X509Certificate2 GetNotYetValidCertificate() => Clone(_notYetValidCertificate);
        internal X509Certificate2 GetRootCertificate() => Clone(_rootCertificate);
        internal X509Certificate2 GetRsaSsaPssCertificate() => Clone(_rsaSsaPssCertificate);
        internal X509Certificate2 GetSelfIssuedCertificate() => Clone(_selfIssuedCertificate);

        internal DisposableList<X509Certificate2> GetCyclicCertificateChain()
        {
            var list = new DisposableList<X509Certificate2>();

            foreach (var certificate in _cyclicChain)
            {
                list.Add(Clone(certificate));
            }

            return list;
        }

        private static X509Certificate2 Clone(X509Certificate2 certificate)
        {
            var bytes = certificate.Export(X509ContentType.Pkcs12);

            return new X509Certificate2(bytes);
        }
    }
}