// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Packaging.Core;

namespace NuGet.Packaging.Signing
{
    public class TimestampedPackageVerificationResult : SignedPackageVerificationResult
    {
        public TimestampedPackageVerificationResult(SignatureVerificationStatus trust, Signature signature, IEnumerable<SignatureLog> issues) :
            base(trust, signature, issues)
        {
        }
    }
}
