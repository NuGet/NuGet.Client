// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System.Security.Cryptography.X509Certificates;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class SignPackageRequest
    {
        public HashAlgorithmName SignatureHashAlgorithm { get; set; } = HashAlgorithmName.SHA256;

        public HashAlgorithmName TimestampHashAlgorithm { get; set; } = HashAlgorithmName.SHA256;

        public X509Certificate2 Certificate { get; set; }

    }
}