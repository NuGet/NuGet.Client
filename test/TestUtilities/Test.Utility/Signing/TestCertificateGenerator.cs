// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Test.Utility.Signing
{
    public class TestCertificateGenerator
    {
        public static readonly Oid IdKPCodeSigning = new Oid("1.3.6.1.5.5.7.3.3");
        public static readonly Oid IdKPClientAuth = new Oid("1.3.6.1.5.5.7.3.2");
        public static readonly Oid IdKPEmailProtection = new Oid("1.3.6.1.5.5.7.3.4");
        public static readonly Oid AnyExtendedKeyUsage = new Oid("2.5.29.37");
        public static readonly Oid AuthorityKeyIdentifier = new Oid("2.5.29.35");
        public static readonly Oid CrlDistributionPoints = new Oid("2.5.29.31");

        public DateTimeOffset NotBefore { get; set; }

        public DateTimeOffset NotAfter { get; set; }

        public byte[] SerialNumber { get; private set; }

        public Collection<X509Extension> Extensions { get; }

        public TestCertificateGenerator()
        {
            Extensions = new Collection<X509Extension>();
        }

        public void SetSerialNumber(byte[] serialNumber)
        {
            SerialNumber = serialNumber ?? throw new ArgumentNullException(nameof(serialNumber));
        }
    }
}
