// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
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
        public Task<PrimarySignature> CreatePrimarySignatureAsync(SignPackageRequest request, SignatureContent signatureContent, ILogger logger, CancellationToken token)
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

            var signature = CreatePrimarySignature(request, signatureContent, logger);

            if (_timestampProvider == null)
            {
                return Task.FromResult(signature);
            }
            else
            {
                return TimestampPrimarySignatureAsync(request, logger, signature, token);
            }
        }

#if IS_DESKTOP
        private PrimarySignature CreatePrimarySignature(SignPackageRequest request, SignatureContent signatureContent, ILogger logger)
        {
            var cmsSigner = SigningUtility.CreateCmsSigner(request, logger);

            if (request.PrivateKey != null)
            {
                return CreatePrimarySignature(cmsSigner, signatureContent, request.PrivateKey);
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

            return PrimarySignature.Load(cms);
        }

        private PrimarySignature CreatePrimarySignature(CmsSigner cmsSigner, SignatureContent signatureContent, CngKey privateKey)
        {
            var cms = NativeUtilities.NativeSign(cmsSigner, signatureContent.GetBytes(), privateKey);

            return PrimarySignature.Load(cms);
        }

        private Task<PrimarySignature> TimestampPrimarySignatureAsync(SignPackageRequest request, ILogger logger, PrimarySignature signature, CancellationToken token)
        {
            var timestampRequest = new TimestampRequest
            {
                Signature = signature.GetBytes(),
                SigningSpec = SigningSpecifications.V1,
                TimestampHashAlgorithm = request.TimestampHashAlgorithm
            };

            return _timestampProvider.TimestampPrimarySignatureAsync(timestampRequest, logger, token);
        }

#else
        private PrimarySignature CreatePrimarySignature(SignPackageRequest request, SignatureContent signatureContent, ILogger logger)
        {
            throw new NotSupportedException();
        }

        private Task<PrimarySignature> TimestampPrimarySignatureAsync(SignPackageRequest request, ILogger logger, PrimarySignature signature, CancellationToken token)
        {
            throw new NotSupportedException();
        }
#endif
    }
}