// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Loads trust providers and verifies package signatures.
    /// </summary>
    public class SignVerifier
    {
        private readonly List<ISignTrustProvider> _trustProviders;
        private readonly SignVerifierSettings _settings;

        public SignVerifier(IEnumerable<ISignTrustProvider> trustProviders, SignVerifierSettings settings)
        {
            _trustProviders = trustProviders?.ToList() ?? throw new ArgumentNullException(nameof(trustProviders));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<VerifySignaturesResult> VerifySignaturesAsync(ISignPackageReader package, ILogger logger, CancellationToken token)
        {
            var valid = false;
            var trustResults = new List<SignatureTrustResult>();

            var isSigned = await package.IsSignedAsync(token);

            if (isSigned)
            {
                // Read package signatures
                var signatures = await package.GetSignaturesAsync(token);
                var signaturesAreValid = signatures.Count > 0; // Fail if there are no signatures
                var signatureResults = new Dictionary<Signature, List<SignatureTrustResult>>();

                // Verify that the signatures are trusted
                foreach (var signature in signatures)
                {
                    var sigTrustResults = await Task.WhenAll(_trustProviders.Select(e => e.GetTrustResultAsync(signature, logger, token)));
                    signaturesAreValid &= IsValid(sigTrustResults);
                    trustResults.AddRange(sigTrustResults);
                }

                valid = signaturesAreValid;
            }
            else if (_settings.AllowUnsigned)
            {
                // An unsigned package is valid only if unsigned packages are allowed.
                valid = true;
            }

            return new VerifySignaturesResult(valid, trustResults);
        }

        /// <summary>
        /// True if a provider trusts the package signature.
        /// </summary>
        private static bool IsValid(IEnumerable<SignatureTrustResult> trustResults)
        {
            var hasItems = trustResults.Any();
            var valid = trustResults.Any(e => e.Trust == SignatureTrust.Trusted);

            return valid && hasItems;
        }
    }
}
