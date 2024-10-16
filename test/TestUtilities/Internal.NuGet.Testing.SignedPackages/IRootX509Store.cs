// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma warning disable CS1591

using System.Security.Cryptography.X509Certificates;

namespace Internal.NuGet.Testing.SignedPackages
{
    public interface IRootX509Store
    {
        void Add(StoreLocation storeLocation, X509Certificate2 certificate);
        void Remove(StoreLocation storeLocation, X509Certificate2 certificate);
    }
}
