// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;

namespace Test.Utility.Signing
{
    internal static class X509StoreUtilities
    {
        internal static IRootX509Store RootX509Store { get; set; } =
            RuntimeEnvironmentHelper.IsWindows ? PlatformX509Store.Instance : CustomRootX509Store.Instance;
        internal static IX509Store OtherX509Store { get; set; } = PlatformX509Store.Instance;

        internal static void AddCertificateToStore(StoreLocation storeLocation, StoreName storeName, X509Certificate2 certificate)
        {
            if (certificate is null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            if (storeName == StoreName.Root)
            {
                RootX509Store.Add(storeLocation, certificate);
            }
            else
            {
                OtherX509Store.Add(storeLocation, storeName, certificate);
            }
        }

        internal static void RemoveCertificateFromStore(StoreLocation storeLocation, StoreName storeName, X509Certificate2 certificate)
        {
            if (certificate is null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            if (storeName == StoreName.Root)
            {
                RootX509Store.Remove(storeLocation, certificate);
            }
            else
            {
                OtherX509Store.Remove(storeLocation, storeName, certificate);
            }
        }
    }
}
