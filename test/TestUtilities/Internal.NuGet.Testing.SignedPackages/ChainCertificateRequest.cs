// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma warning disable CS1591

using System.Security.Cryptography.X509Certificates;

namespace Internal.NuGet.Testing.SignedPackages
{
    public class ChainCertificateRequest
    {
        public string CrlServerBaseUri { get; set; }

        public string CrlLocalBaseUri { get; set; }

        public bool IsCA { get; set; }

        public bool ConfigureCrl { get; set; }

        public X509Certificate2 Issuer { get; set; }
    }
}
