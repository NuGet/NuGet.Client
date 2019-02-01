// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;

namespace Test.Utility.Signing
{
    public sealed class TestCertificateGenerator
    {
        public DateTimeOffset NotBefore { get; set; }

        public DateTimeOffset NotAfter { get; set; }

        public BigInteger SerialNumber { get; set; }

        public Collection<X509Extension> Extensions { get; }

        public TestCertificateGenerator()
        {
            Extensions = new Collection<X509Extension>();
        }
    }
}
