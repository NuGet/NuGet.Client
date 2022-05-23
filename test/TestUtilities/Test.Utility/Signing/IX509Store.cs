// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;

namespace Test.Utility.Signing
{
    internal interface IX509Store
    {
        void Add(X509Certificate2 certificate, StoreLocation storeLocation, StoreName storeName);
        void Remove(X509Certificate2 certificate, StoreLocation storeLocation, StoreName storeName);
    }
}
