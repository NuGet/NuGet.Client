// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Packaging.Signing
{
    public class UnsignedPackageVerificationResult : PackageVerificationResult
    {
        public UnsignedPackageVerificationResult(SignatureVerificationStatus trust, IEnumerable<SignatureLog> issues) :
            base(trust, issues)
        {
        }
    }
}
