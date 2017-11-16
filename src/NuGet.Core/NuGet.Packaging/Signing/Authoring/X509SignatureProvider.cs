// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using System.IO;

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

            var authorSignature = CreateSignature(request.Certificate, signatureManifest);

            if (_timestampProvider == null)
            {
                return authorSignature;
            }
            else
            {
                return TimestampSignature(request, logger, authorSignature.Result, token);
            }
        }

#if IS_DESKTOP
        private Task<Signature> CreateSignature(X509Certificate2 cert, SignatureManifest signatureManifest)
        {
            var contentInfo = new ContentInfo(signatureManifest.GetBytes());
            var cmsSigner = new CmsSigner(SubjectIdentifierType.SubjectKeyIdentifier, cert);
            var signingTime = new Pkcs9SigningTime();

            cmsSigner.SignedAttributes.Add(
                new CryptographicAttributeObject(
                    signingTime.Oid,
                    new AsnEncodedDataCollection(signingTime)));

            cmsSigner.IncludeOption = X509IncludeOption.WholeChain;

            var cms = new SignedCms(contentInfo);
            cms.ComputeSignature(cmsSigner);

            // 0 since this is the first signerInfo that we just created.
            return Task.FromResult(Signature.Load(cms));
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
        private Task<Signature> CreateSignature(X509Certificate2 cert, SignatureManifest signatureManifest)
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
