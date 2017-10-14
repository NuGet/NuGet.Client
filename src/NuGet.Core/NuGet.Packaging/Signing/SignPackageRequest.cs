// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class SignPackageRequest
    {
        // Signature info
        public Signature Signature { get; set; }

        public HashAlgorithmName HashAlgorithm { get; set; } = HashAlgorithmName.SHA256;
    }
}
