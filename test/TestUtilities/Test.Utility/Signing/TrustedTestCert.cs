// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;

namespace Test.Utility.Signing
{
    /// <summary>
    /// Give a certificate full trust for the life of the object.
    /// </summary>
    public class TrustedTestCert<T> : IDisposable
    {
        private readonly X509Store _store;

        public X509Certificate2 TrustedCert { get; }

        public T Source { get; }

        public TrustedTestCert(T source, Func<T, X509Certificate2> getCert)
        {
            Source = source;

            TrustedCert = getCert(source);

#if IS_DESKTOP
            var expiration = DateTimeOffset.Parse(TrustedCert.GetExpirationDateString());

            if (expiration > DateTimeOffset.UtcNow.AddHours(2))
            {
                throw new InvalidOperationException("The cert used is valid for more than two hours.");
            }
#endif

            _store = new X509Store(StoreName.TrustedPeople, StoreLocation.CurrentUser);
            _store.Open(OpenFlags.ReadWrite);
            _store.Add(TrustedCert);
        }

        public void Dispose()
        {
            using (_store)
            {
                _store.Remove(TrustedCert);
            }
        }
    }
}
