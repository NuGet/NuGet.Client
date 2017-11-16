// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using System.Globalization;

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

namespace NuGet.Packaging.Signing
{
    public class X509SignatureVerificationProvider : ISignatureVerificationProvider
    {
        public Task<PackageVerificationResult> GetTrustResultAsync(ISignedPackageReader package, Signature signature, ILogger logger, CancellationToken token)
        {
            //if (signature.Type != SignatureType.Author)
            //{
            //    var issues = new List<SignatureIssue> { SignatureIssue.InvalidPackageError("Unsupported signature type.") };
            //    var verificationResult = new SignedPackageVerificationResult(SignatureVerificationStatus.Invalid, signature, issues);
            //    return Task.FromResult<PackageVerificationResult>(verificationResult);
            //}

            var result = VerifySignature(signature, logger);
            return Task.FromResult(result);
        }

#if IS_DESKTOP
        private PackageVerificationResult VerifySignature(Signature signature, ILogger logger)
        {
            var status = SignatureVerificationStatus.Trusted;
            var signatureIssues = new List<SignatureLog>();
            var issues = new List<SignatureLog>();

            //TODO: Check commitment-type-indication == id-cti-ets-proofOfOrigin
            //if (commitment - type - indication == id - cti - ets - proofOfOrigin)
            //{
            //    status = SignatureVerificationStatus.Untrusted;
            //    issues.Add(SignatureIssue.TrustOfSignatureCannotBeProvenWarning("Commitment-type-indication is not know."));
            //    return new SignedPackageVerificationResult(status, signature, issues);
            //}
            

            try
            {
                signature.SignerInfo.CheckSignature(verifySignatureOnly: true);
            }
            catch (Exception e)
            {
                issues.Add(SignatureLog.InvalidPackageError($"Author signature verification failed. {e.Message}"));
            }

            if (signature.SignerInfo.Certificate == null)
            {
                issues.Add(SignatureLog.InvalidPackageError("Signature does not have a certificate."));
                return new SignedPackageVerificationResult(status, signature, issues);
            }


            logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                Strings.VerificationAuthorCertDisplay,
                $"{Environment.NewLine}{CertificateUtility.X509Certificate2ToString(signature.SignerInfo.Certificate)}"));

            status = GetVerificationStatusFromCertificate(signature.SignerInfo.Certificate, signature.Certificates, issues);

            if (status != SignatureVerificationStatus.Invalid)
            {
                if (!SigningUtility.IsCertificatePublicKeyValid(signature.SignerInfo.Certificate))
                {
                    issues.Add(SignatureLog.InvalidPackageError("Certificate does not meet the public key requirements."));
                    status = SignatureVerificationStatus.Invalid;
                }
            }
           
            return new SignedPackageVerificationResult(status, signature, issues);
        }

        private SignatureVerificationStatus GetVerificationStatusFromCertificate(X509Certificate2 certificate, X509Certificate2Collection additionalCertificates, List<SignatureLog> issues)
        {
            try
            {
                if (SigningUtility.IsCertificateChainValid(certificate, additionalCertificates, allowUntrustedRoot: false))
                {
                    return SignatureVerificationStatus.Trusted;
                }
                else if (SigningUtility.IsCertificateChainValid(certificate, additionalCertificates, allowUntrustedRoot: true))
                {
                    issues.Add(SignatureLog.UntrustedRootWarning("Signing certificate does not chain to a trusted root."));
                    return SignatureVerificationStatus.Untrusted;
                }

                issues.Add(SignatureLog.InvalidPackageError("Unable to validate signer certificate chain."));
                return SignatureVerificationStatus.Invalid;
            }
            catch(SignatureException e)
            {
                issues.Add(SignatureLog.InvalidPackageError(e.Message));
                return SignatureVerificationStatus.Invalid;
            }
        }
#else
        private PackageVerificationResult VerifySignature(Signature signature, ILogger logger)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
