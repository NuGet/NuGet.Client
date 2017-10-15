// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Signing;

namespace NuGet.Packaging.Test.SigningTests
{
    public class SelfSignedSignatureProvider : ISignatureProvider
    {
        public Task<Signature> CreateSignatureAsync(SignPackageRequest request, string manifestHash, ILogger logger, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
