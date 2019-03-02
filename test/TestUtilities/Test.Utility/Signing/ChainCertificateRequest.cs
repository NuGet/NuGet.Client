// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;

namespace Test.Utility.Signing
{
    public class ChainCertificateRequest
    {
        public string CrlServerBaseUri { get; set; }

        public string CrlLocalBaseUri { get; set; }

        public bool IsCA { get; set; }

        public bool ConfigureCrl { get; set; } = true;

        public X509Certificate2 Issuer { get; set; }
    }
}