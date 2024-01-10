// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Test.Utility.Signing
{
    public sealed class X509TrustTestFixture : IDisposable
    {
        public X509TrustTestFixture()
        {
            TestFallbackCertificateBundleX509ChainFactories.SetTryUseAsDefault(tryUseAsDefault: true);
        }

        public void Dispose()
        {
            TestFallbackCertificateBundleX509ChainFactories.SetTryUseAsDefault(tryUseAsDefault: false);
        }
    }
}
