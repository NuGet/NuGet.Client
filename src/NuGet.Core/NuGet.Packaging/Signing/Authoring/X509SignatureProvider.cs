// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Sign a manifest hash with an X509Certificate2.
    /// </summary>
    public class X509SignatureProvider : ISignatureProvider
    {
        // Occurs when SignedCms.ComputeSignature cannot read the certificate private key
        // "Invalid provider type specified." (INVALID_PROVIDER_TYPE)
        private const int INVALID_PROVIDER_TYPE_HRESULT = unchecked((int)0x80090014);

        private readonly ITimestampProvider _timestampProvider;

        public X509SignatureProvider(ITimestampProvider timestampProvider)
        {
            _timestampProvider = timestampProvider;
        }

        /// <summary>
        /// Sign the package stream hash with an X509Certificate2.
        /// </summary>
        public Task<Signature> CreateSignatureAsync(SignPackageRequest request, SignatureContent signatureContent, ILogger logger, CancellationToken token)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (signatureContent == null)
            {
                throw new ArgumentNullException(nameof(signatureContent));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            var signature = CreateSignature(request, signatureContent, logger);

            if (_timestampProvider == null)
            {
                return Task.FromResult(signature);
            }
            else
            {
                return TimestampSignature(request, logger, signature, token);
            }
        }

#if IS_DESKTOP
        private Signature CreateSignature(SignPackageRequest request, SignatureContent signatureContent, ILogger logger)
        {
            var cmsSigner = CreateCmsSigner(request, logger);

            if (request.PrivateKey != null)
            {
                return CreateSignature(cmsSigner, signatureContent, request.PrivateKey);
            }

            var contentInfo = new ContentInfo(signatureContent.GetBytes());
            var cms = new SignedCms(contentInfo);

            try
            {
                cms.ComputeSignature(cmsSigner);
            }
            catch (CryptographicException ex) when (ex.HResult == INVALID_PROVIDER_TYPE_HRESULT)
            {
                var exceptionBuilder = new StringBuilder();
                exceptionBuilder.AppendLine(Strings.SignFailureCertificateInvalidProviderType);
                exceptionBuilder.AppendLine(CertificateUtility.X509Certificate2ToString(request.Certificate, Common.HashAlgorithmName.SHA256));

                throw new SignatureException(NuGetLogCode.NU3001, exceptionBuilder.ToString());
            }

            return Signature.Load(cms);
        }

        private static CmsSigner CreateCmsSigner(SignPackageRequest request, ILogger logger)
        {
            // Subject Key Identifier (SKI) is smaller and less prone to accidental matching than issuer and serial
            // number.  However, to ensure cross-platform verification, SKI should only be used if the certificate
            // has the SKI extension attribute.
            CmsSigner signer;

            if (request.Certificate.Extensions[Oids.SubjectKeyIdentifier] == null)
            {
                signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, request.Certificate);
            }
            else
            {
                signer = new CmsSigner(SubjectIdentifierType.SubjectKeyIdentifier, request.Certificate);
            }

            request.BuildSigningCertificateChainOnce(logger);

            var chain = request.Chain;

            foreach (var certificate in chain)
            {
                signer.Certificates.Add(certificate);
            }

            var attributes = SigningUtility.CreateSignedAttributes(request, chain);

            foreach (var attribute in attributes)
            {
                signer.SignedAttributes.Add(attribute);
            }

            // We built the chain ourselves and added certificates.
            // Passing any other value here would trigger another chain build
            // and possibly add duplicate certs to the collection.
            signer.IncludeOption = X509IncludeOption.None;
            signer.DigestAlgorithm = request.SignatureHashAlgorithm.ConvertToOid();

            return signer;
        }

        private Signature CreateSignature(CmsSigner cmsSigner, SignatureContent signatureContent, CngKey privateKey)
        {
            var cms = NativeUtilities.NativeSign(cmsSigner, signatureContent.GetBytes(), privateKey);

            return Signature.Load(cms);
        }

        private Task<Signature> TimestampSignature(SignPackageRequest request, ILogger logger, Signature signature, CancellationToken token)
        {
            var timestampRequest = new TimestampRequest
            {
                SignatureValue = signature.GetBytes(),
                SigningSpec = SigningSpecifications.V1,
                TimestampHashAlgorithm = request.TimestampHashAlgorithm
            };

            return _timestampProvider.TimestampSignatureAsync(timestampRequest, logger, token);
        }

#else
        private Signature CreateSignature(SignPackageRequest request, SignatureContent signatureContent, ILogger logger)
        {
            throw new NotSupportedException();
        }

        private Task<Signature> TimestampSignature(SignPackageRequest request, ILogger logger, Signature signature, CancellationToken token)
        {
            throw new NotSupportedException();
        }
#endif
    }
}