// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Creates and performs cleanup on an <see cref="X509Chain" /> instance.
    /// </summary>
    /// <remarks>
    /// Certificates held by individual X509ChainElement objects should be disposed immediately after use to minimize
    /// finalizer impact.
    /// </remarks>
    public sealed class X509ChainHolder : IDisposable
    {
        private bool _isDisposed;

        public X509Chain Chain { get; }

        public X509ChainHolder()
        {
            Chain = new X509Chain();
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                foreach (var chainElement in Chain.ChainElements)
                {
                    chainElement.Certificate.Dispose();
                }

                Chain.Dispose();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }
    }
}