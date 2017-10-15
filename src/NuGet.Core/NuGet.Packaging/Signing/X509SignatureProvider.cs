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

#if NET46
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
        public Task<Signature> CreateSignatureAsync(SignPackageRequest request, string manifestHash, ILogger logger, CancellationToken token)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (manifestHash == null)
            {
                throw new ArgumentNullException(nameof(manifestHash));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            var signature = CreateSignature(request.Certificate, manifestHash);
            return Task.FromResult(signature);
        }

#if NET46
        private Signature CreateSignature(X509Certificate2 cert, string manifestHash)
        {
            var manfiestHashBytes = ASCIIEncoding.ASCII.GetBytes(manifestHash);
            var contentInfo = new ContentInfo(manfiestHashBytes);

            var cmsSigner = new CmsSigner(SubjectIdentifierType.SubjectKeyIdentifier, cert);
            var signingTime = new Pkcs9SigningTime();

            cmsSigner.SignedAttributes.Add(
                new CryptographicAttributeObject(
                    signingTime.Oid,
                    new AsnEncodedDataCollection(signingTime)));

            cmsSigner.IncludeOption = X509IncludeOption.WholeChain;

            var cms = new SignedCms(contentInfo);
            cms.ComputeSignature(cmsSigner);

            var signature = new Signature()
            {
                Type = SignatureType.Author,
            };

            return signature;
        }
#else
        private Signature CreateSignature(X509Certificate2 cert, string manifestHash)
        {
            throw new NotSupportedException();
        }
#endif
    }
}
