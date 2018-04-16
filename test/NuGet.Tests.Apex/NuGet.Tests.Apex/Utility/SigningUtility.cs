// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Xunit;

namespace NuGet.Tests.Apex
{
    public class SigningUtility
    {
        internal static bool IsCertificateExpired(X509Certificate2 certificate)
        {
            return DateTime.Now > certificate.NotAfter;
        }

        internal static void WaitForCertificateToExpire(X509Certificate2 certificate)
        {
            while (DateTimeOffset.Now < certificate.NotAfter)
            {
                Thread.Sleep(100);
            }

            Assert.True(SigningUtility.IsCertificateExpired(certificate));
        }

    }
}
