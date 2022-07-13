// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using System.Text;
using NuGet.Common;
using NuGet.Packaging.Signing;

namespace Test.Utility.Signing
{
    internal sealed class TestFallbackCertificateBundleX509ChainFactory
#if NET5_0_OR_GREATER
        : CertificateBundleX509ChainFactory
#endif
    {
        internal static TestFallbackCertificateBundleX509ChainFactory Instance { get; } = new();

        private TestFallbackCertificateBundleX509ChainFactory()
#if NET5_0_OR_GREATER
            : base(LoadCertificates())
#endif
        {
        }

#if NET5_0_OR_GREATER
        private static X509Certificate2Collection LoadCertificates()
        {
            // Load an extract from the May 2022 Windows CTL update.
            // The file contains root certificates valid for both code signing and timestamping.
            // Similar to Windows' trusted root authority certificates trust store, this file contains
            // both expired and active root certificates.  Tests should not be affected by expiration.
            byte[] bytes = SigningTestUtility.GetResourceBytes("codesignctl.pem");
            string pem = Encoding.UTF8.GetString(bytes);
            X509Certificate2Collection certificates = new();

            certificates.ImportFromPem(pem);

            return certificates;
        }
#endif

        internal static void SetTryUseAsDefault(bool tryUseAsDefault)
        {
            IX509ChainFactory factory = null;

#if NET5_0_OR_GREATER
            if (tryUseAsDefault && !RuntimeEnvironmentHelper.IsWindows)
            {
                factory = Instance;
            }
#endif

            X509TrustStore.SetX509ChainFactory(factory);
        }
    }
}
