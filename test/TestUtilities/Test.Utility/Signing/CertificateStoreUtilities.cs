// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using NuGet.Common;

namespace Test.Utility.Signing
{
    public static class CertificateStoreUtilities
    {
        public static StoreLocation GetTrustedCertificateStoreLocation()
        {
            // According to https://github.com/dotnet/runtime/blob/master/docs/design/features/cross-platform-cryptography.md#x509store   
            // use different approaches for Windows, Mac and Linux.
            return (RuntimeEnvironmentHelper.IsWindows || RuntimeEnvironmentHelper.IsMacOSX) ? StoreLocation.LocalMachine : StoreLocation.CurrentUser;
        }
    }
}
