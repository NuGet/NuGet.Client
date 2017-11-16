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
    public class PackageSignatureVerifier : IPackageSignatureVerifier
    {
        private readonly List<ISignatureVerificationProvider> _verificationProviders;
        private readonly SignedPackageVerifierSettings _settings;

        public PackageSignatureVerifier(IEnumerable<ISignatureVerificationProvider> verificationProviders, SignedPackageVerifierSettings settings)
        {
            _verificationProviders = verificationProviders?.ToList() ?? throw new ArgumentNullException(nameof(verificationProviders));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<VerifySignaturesResult> VerifySignaturesAsync(ISignedPackageReader package, ILogger logger, CancellationToken token)
        {
            var valid = false;
            var trustResults = new List<PackageVerificationResult>();

            var isSigned = await package.IsSignedAsync(token);

            if (isSigned)
            {
                // Read package signatures
                var signatures = await package.GetSignaturesAsync(token);

                var signaturesAreValid = signatures.Count > 0; // Fail if there are no signatures
                
                // Verify that the signatures are trusted
                foreach (var signature in signatures)
                {
                    var sigTrustResults = await Task.WhenAll(_verificationProviders.Select(e => e.GetTrustResultAsync(package, signature, logger, token)));
                    signaturesAreValid &= IsValid(sigTrustResults, _settings.AllowUntrusted);
                    trustResults.AddRange(sigTrustResults);
                }

                valid = signaturesAreValid;
            }
            else if (_settings.AllowUnsigned)
            {
                // An unsigned package is valid only if unsigned packages are allowed.
                valid = true;
            }
            else
            {
                var issues = new[] { SignatureIssue.InvalidInputError(Strings.ErrorPackageNotSigned) };
                trustResults.Add(new UnsignedPackageVerificationResult(SignatureVerificationStatus.Invalid, issues));
            }

            return new VerifySignaturesResult(valid, trustResults);
        }

        /// <summary>
        /// True if a provider trusts the package signature.
        /// </summary>
        private static bool IsValid(IEnumerable<PackageVerificationResult> trustResults, bool allowUntrusted)
        {
            var hasItems = trustResults.Any();
            var valid = trustResults.All(e => e.Trust == SignatureVerificationStatus.Trusted || (allowUntrusted && SignatureVerificationStatus.Untrusted == e.Trust));

            return valid && hasItems;
        }
    }
}
