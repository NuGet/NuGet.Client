// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
        private readonly ITimestampProvider _timestampProvider;

        public X509SignatureProvider(ITimestampProvider timestampProvider)
        {
            _timestampProvider = timestampProvider;
        }

        /// <summary>
        /// Sign the package stream hash with an X509Certificate2.
        /// </summary>
        public Task<Signature> CreateSignatureAsync(SignPackageRequest request, SignatureManifest signatureManifest, ILogger logger, CancellationToken token)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (signatureManifest == null)
            {
                throw new ArgumentNullException(nameof(signatureManifest));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            var authorSignature = CreateSignature(request, signatureManifest);

            if (_timestampProvider == null)
            {
                return Task.FromResult(authorSignature);
            }
            else
            {
                return TimestampSignature(request, logger, authorSignature, token);
            }
        }

#if IS_DESKTOP
        private Signature CreateSignature(SignPackageRequest request, SignatureManifest signatureManifest)
        {
            var attributes = SigningUtility.GetSignAttributes();

            using (request)
            {
                if (request.PrivateKey != null)
                {
                    return CreateSignature(request.Certificate, signatureManifest, request.PrivateKey, request.SignatureHashAlgorithm, attributes);
                }

                var contentInfo = new ContentInfo(signatureManifest.GetBytes());
                var cmsSigner = new CmsSigner(SubjectIdentifierType.SubjectKeyIdentifier, request.Certificate);

                foreach (var attribute in attributes)
                {
                    cmsSigner.SignedAttributes.Add(attribute);
                }

                cmsSigner.IncludeOption = X509IncludeOption.WholeChain;

                var cms = new SignedCms(contentInfo);
                cms.ComputeSignature(cmsSigner);

                return Signature.Load(cms);
            }
        }

        private Signature CreateSignature(
            X509Certificate2 cert,
            SignatureManifest signatureManifest,
            CngKey privateKey,
            Common.HashAlgorithmName hashAlgorithm,
            CryptographicAttributeObjectCollection attributes
            )
        {
            var cms = NativeUtilities.NativeSign(
                signatureManifest.GetBytes(), cert, privateKey, attributes, hashAlgorithm);

            return Signature.Load(cms);
        }

        private Task<Signature> TimestampSignature(SignPackageRequest request, ILogger logger, Signature signature, CancellationToken token)
        {
            var timestampRequest = new TimestampRequest
            {
                SignatureValue = signature.GetBytes(),
                Certificate = request.Certificate,
                SigningSpec = SigningSpecifications.V1,
                TimestampHashAlgorithm = request.TimestampHashAlgorithm
            };

            return _timestampProvider.TimestampSignatureAsync(timestampRequest, logger, token);
        }
#else
        private Signature CreateSignature(SignPackageRequest request, SignatureManifest signatureManifest)
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
