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
            _timestampProvider = timestampProvider ?? throw new ArgumentNullException(nameof(timestampProvider));
        }

        /// <summary>
        /// Sign a manifest hash with an X509Certificate2.
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

            var signature = CreateSignature(request.Certificate, signatureManifest);

            TimestampSignature(request, logger, signature, token);

            return Task.FromResult(signature);
        }

#if IS_DESKTOP
        private Signature CreateSignature(X509Certificate2 cert, SignatureManifest signatureManifest)
        {
            var contentInfo = new ContentInfo(signatureManifest.GetBytes());

            var cmsSigner = new CmsSigner(SubjectIdentifierType.SubjectKeyIdentifier, cert);
            var signingTime = new Pkcs9SigningTime();

            cmsSigner.SignedAttributes.Add(
                new CryptographicAttributeObject(
                    signingTime.Oid,
                    new AsnEncodedDataCollection(signingTime)));

            cmsSigner.UnsignedAttributes.Add(
                new CryptographicAttributeObject(
                    signingTime.Oid,
                    new AsnEncodedDataCollection(signingTime)));

            cmsSigner.IncludeOption = X509IncludeOption.WholeChain;

            var cms = new SignedCms(contentInfo);
            cms.ComputeSignature(cmsSigner);

            // 0 - Since we just created this signature and it should contain only 1 signerInfo
            return Signature.Load(cms, signerInfoIndex: 0);
        }

        private Task TimestampSignature(SignPackageRequest request, ILogger logger, Signature signature, CancellationToken token)
        {
            var timestampRequest = new TimestampRequest
            {
                Signature = signature,
                Certificate = request.Certificate,
                SigningSpec = SigningSpecifications.V1,
                TimestampHashAlgorithm = request.TimestampHashAlgorithm
            };

            return _timestampProvider.TimestampSignatureAsync(timestampRequest, logger, token);
        }
#else
        private Signature CreateSignature(X509Certificate2 cert, SignatureManifest signatureManifest)
        {
            throw new NotSupportedException();
        }

        private Task TimestampSignature(SignPackageRequest request, ILogger logger, Signature signature, CancellationToken token)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
