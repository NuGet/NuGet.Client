// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Test.Utility.Signing
{
    public class TrustedCertificateChain : IDisposable
    {
        public IList<TrustedTestCert<TestCertificate>> Certificates { get; set; }

        public TrustedTestCert<TestCertificate> Root => Certificates?.First();

        public TrustedTestCert<TestCertificate> Leaf => Certificates?.Last();

        public void Dispose()
        {
            (Certificates as List<TrustedTestCert<TestCertificate>>)?.ForEach(c => c.Dispose());
        }
    }
}
