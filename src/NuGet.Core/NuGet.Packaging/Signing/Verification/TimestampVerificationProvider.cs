// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    public class TimestampVerificationProvider : ISignatureVerificationProvider
    {
        public Task<PackageVerificationResult> GetTrustResultAsync(ISignedPackageReader package, Signature signature, ILogger logger, CancellationToken token)
        {
            var result = VerifySignature(signature, logger);
            return Task.FromResult(result);
        }

#if IS_DESKTOP
        private PackageVerificationResult VerifySignature(Signature signature, ILogger logger)
        {
            var status = SignatureVerificationStatus.Trusted;
            var signatureIssues = new List<SignatureLog>();
            var issues = new List<SignatureLog>();

            var authorUnsignedAttributes = signature.SignerInfo.UnsignedAttributes;
            var timestampCms = new SignedCms();

            try
            {
                foreach (var attribute in authorUnsignedAttributes)
                {
                    if (string.Equals(attribute.Oid.Value, Oids.SignatureTimeStampTokenAttributeOid))
                    {
                        timestampCms.Decode(attribute.Values[0].RawData);

                        using (var authorSignatureNativeCms = NativeCms.Decode(signature.SignedCms.Encode(), detached: false))
                        {
                            var signatureHash = NativeCms.GetSignatureValueHash(signature.SignatureManifest.HashAlgorithm, authorSignatureNativeCms);

                            Rfc3161TimestampVerifier.Validate(timestampCms, SigningSpecifications.V1, signature.SignerInfo.Certificate, signatureHash);

                            logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                                Strings.VerificationTimestamperCertDisplay,
                                $"{Environment.NewLine}{CertificateUtility.X509Certificate2ToString(timestampCms.SignerInfos[0].Certificate)}"));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                status = SignatureVerificationStatus.Invalid;
                issues.Add(SignatureLog.InvalidTimestampInSignatureError("The signature contains an invalid timestamp"));
                issues.Add(SignatureLog.DetailedLog($"Parsing timestamp failed with exception: {e.Message}"));
            }

            return new SignedPackageVerificationResult(status, signature, issues);
        }

#else
        private PackageVerificationResult VerifySignature(Signature signature, ILogger logger)
        {
            throw new NotImplementedException();
        }
#endif
    }
}
