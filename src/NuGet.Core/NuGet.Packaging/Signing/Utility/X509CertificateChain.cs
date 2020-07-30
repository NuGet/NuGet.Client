// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Packaging.Signing
{
    public sealed class X509CertificateChain : List<X509Certificate2>, IX509CertificateChain
    {
        private bool _isDisposed;

        public void Dispose()
        {
            if (!_isDisposed)
            {
#if !NET45
                //.NET 4.6 added Dispose method to X509Certificate
                foreach (var item in this)
                {
                    item.Dispose();
                }
#endif

                Clear();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }
    }
}
