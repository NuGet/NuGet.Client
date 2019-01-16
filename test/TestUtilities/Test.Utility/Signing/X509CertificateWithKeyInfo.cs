// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Test.Utility.Signing
{
    public class X509CertificateWithKeyInfo : IDisposable
    {
        public X509Certificate2 Certificate { get; private set; }

        public RSA KeyPair { get; private set; }

        private bool _isDisposed;

        public X509CertificateWithKeyInfo(X509Certificate2 cert, RSA keyPair)
        {
            Certificate = cert;
            KeyPair = keyPair;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                Certificate?.Dispose();
                KeyPair?.Dispose();
            }

            _isDisposed = true;
        }
    }
}
