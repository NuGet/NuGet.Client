// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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

        public async Task<VerifySignaturesResult> VerifySignaturesAsync(ISignedPackageReader package, CancellationToken token)
        {
            var valid = false;
            var trustResults = new List<PackageVerificationResult>();

            var isSigned = await package.IsSignedAsync(token);
            if (isSigned)
            {
                try
                {
                    // Read package signatures
                    var signatures = await package.GetSignaturesAsync(token);
                    var signaturesAreValid = signatures.Count > 0; // Fail if there are no signatures

                    // Verify that the signatures are trusted
                    foreach (var signature in signatures)
                    {
                        var sigTrustResults = await Task.WhenAll(_verificationProviders.Select(e => e.GetTrustResultAsync(package, signature, token)));
                        signaturesAreValid &= IsValid(sigTrustResults, _settings.AllowUntrusted);
                        trustResults.AddRange(sigTrustResults);
                    }

                    valid = signaturesAreValid;
                }
                catch(CryptographicException e)
                {
                    // CryptographicException generated while parsing the SignedCms object
                    var issues = new[] {
                        SignatureLog.InvalidInputError(Strings.ErrorPackageSignatureInvalid),
                        SignatureLog.DebugLog($"VerifySignature failed with exception: {e.Message}")
                    };
                    trustResults.Add(new InvalidSignaturePackageVerificationResult(SignatureVerificationStatus.Invalid, issues));
                }
            }
            else if (_settings.AllowUnsigned)
            {
                // An unsigned package is valid only if unsigned packages are allowed.
                valid = true;
            }
            else
            {
                var issues = new[] { SignatureLog.InvalidInputError(Strings.ErrorPackageNotSigned) };
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

            var timestampResult = trustResults.Where(tr => tr is TimestampedPackageVerificationResult).FirstOrDefault();
            var timestampIsTrusted = timestampResult != null && timestampResult.Trust == SignatureVerificationStatus.Trusted;

            foreach (var result in trustResults)
            {
                var resultIsValidByTrusted = result.Trust == SignatureVerificationStatus.Trusted;
                var resultIsValidByUntrusted = allowUntrusted && result.Trust == SignatureVerificationStatus.Untrusted;
                var resultIsValidByRevoked = timestampIsTrusted && result.Trust == SignatureVerificationStatus.Revoked;

                if (!resultIsValidByTrusted && !resultIsValidByUntrusted && !resultIsValidByRevoked)
                {
                    return false;
                }
            }

            return hasItems;
        }
    }
}
