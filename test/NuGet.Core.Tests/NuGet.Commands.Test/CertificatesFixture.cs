// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using Test.Utility.Signing;

namespace NuGet.Commands.Test
{
    public sealed class CertificatesFixture : IDisposable
    {
        private readonly X509Certificate2 _defaultCertificate;
        private readonly X509Certificate2 _repositoryCertificate;


        private bool _isDisposed;

        public CertificatesFixture()
        {
            _defaultCertificate = SigningTestUtility.GenerateCertificate("test", generator => { });
            _repositoryCertificate = SigningTestUtility.GenerateCertificate("test repo", generator => { });
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _defaultCertificate.Dispose();
                _repositoryCertificate.Dispose();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }

        public X509Certificate2 GetDefaultCertificate()
        {
            return new X509Certificate2(_defaultCertificate.RawData);
        }

        public X509Certificate2 GetRepositoryCertificate()
        {
            return new X509Certificate2(_repositoryCertificate.RawData);
        }

        public X509Certificate2 GetCertificateWithPassword(string password)
        {
            var bytes = _defaultCertificate.Export(X509ContentType.Pkcs12, password);

            return new X509Certificate2(bytes, password, X509KeyStorageFlags.Exportable);
        }
    }
}