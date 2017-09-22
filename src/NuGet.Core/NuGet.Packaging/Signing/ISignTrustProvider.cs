// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Providers signature trust information.
    /// </summary>
    public interface ISignTrustProvider
    {
        /// <summary>
        /// Check if <paramref name="signature" /> is trusted by the provider.
        /// </summary>
        Task<SignatureTrustResult> GetTrustResultAsync(Signature signature, ILogger logger, CancellationToken token);
    }
}
