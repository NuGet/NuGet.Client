// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;
using NuGet.Packaging.Signing;

namespace Test.Utility.Signing
{
    public sealed class TimestampServiceOptions
    {
        public int AccuracyInSeconds { get; set; }
        public bool ReturnFailure { get; set; }
        public bool ReturnSigningCertificate { get; set; }
        public Oid SignatureHashAlgorithm { get; set; }

        public TimestampServiceOptions()
        {
            AccuracyInSeconds = 1;
            ReturnSigningCertificate = true;
            SignatureHashAlgorithm = new Oid(Oids.Sha256);
        }
    }
}