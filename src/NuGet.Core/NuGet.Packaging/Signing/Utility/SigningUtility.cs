// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Utility methods for signing.
    /// </summary>
    public static class SigningUtility
    {
        public static void Verify(SignPackageRequest request, ILogger logger)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (!CertificateUtility.IsSignatureAlgorithmSupported(request.Certificate))
            {
                throw new SignatureException(NuGetLogCode.NU3013, Strings.SigningCertificateHasUnsupportedSignatureAlgorithm);
            }

            if (!CertificateUtility.IsCertificatePublicKeyValid(request.Certificate))
            {
                throw new SignatureException(NuGetLogCode.NU3014, Strings.SigningCertificateFailsPublicKeyLengthRequirement);
            }

            if (CertificateUtility.HasExtendedKeyUsage(request.Certificate, Oids.LifetimeSigningEku))
            {
                throw new SignatureException(NuGetLogCode.NU3015, Strings.ErrorCertificateHasLifetimeSigningEKU);
            }

            if (CertificateUtility.IsCertificateValidityPeriodInTheFuture(request.Certificate))
            {
                throw new SignatureException(NuGetLogCode.NU3017, Strings.SignatureNotYetValid);
            }

            request.BuildSigningCertificateChainOnce(logger);
        }

#if IS_DESKTOP
        public static CryptographicAttributeObjectCollection CreateSignedAttributes(
            SignPackageRequest request,
            IReadOnlyList<X509Certificate2> chainList)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (chainList == null || chainList.Count == 0)
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(chainList));
            }

            var attributes = new CryptographicAttributeObjectCollection
            {
                new Pkcs9SigningTime()
            };

            if (request.SignatureType != SignatureType.Unknown)
            {
                // Add signature type if set.
                attributes.Add(AttributeUtility.CreateCommitmentTypeIndication(request.SignatureType));
            }

            attributes.Add(AttributeUtility.CreateSigningCertificateV2(chainList[0], request.SignatureHashAlgorithm));

            return attributes;
        }

        public static CryptographicAttributeObjectCollection CreateSignedAttributes(
            RepositorySignPackageRequest request,
            IReadOnlyList<X509Certificate2> chainList)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (chainList == null || chainList.Count == 0)
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(chainList));
            }

            var attributes = CreateSignedAttributes((SignPackageRequest)request, chainList);

            attributes.Add(AttributeUtility.CreateNuGetV3ServiceIndexUrl(request.V3ServiceIndexUrl));

            if (request.PackageOwners?.Count > 0)
            {
                attributes.Add(AttributeUtility.CreateNuGetPackageOwners(request.PackageOwners));
            }

            return attributes;
        }

        public static CmsSigner CreateCmsSigner(SignPackageRequest request, ILogger logger)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

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

            CryptographicAttributeObjectCollection attributes;

            if (request.SignatureType == SignatureType.Repository)
            {
                attributes = CreateSignedAttributes((RepositorySignPackageRequest)request, chain);
            }
            else
            {
                attributes = CreateSignedAttributes(request, chain);
            }

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

        public static async Task RemoveSignatureAsync(string packageFilePath, string outputFilePath, CancellationToken token)
        {
            using (var packageReadStream = File.OpenRead(packageFilePath))
            using (var packageWriteStream = File.Open(outputFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            using (var package = new SignedPackageArchive(packageReadStream, packageWriteStream))
            {
                await package.RemoveSignatureAsync(token);
            }
        }

        /// <summary>
        /// Add a signature to a package.
        /// </summary>
        public static async Task SignAsync(SigningOptions options, SignPackageRequest signRequest, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            Verify(signRequest, options.Logger);

            var inputPackagePath = options.PackageFilePath;
            var tempPackagePath = Path.GetTempFileName();

            try
            {
                using (var packageReadStream = File.OpenRead(inputPackagePath))
                using (var packageWriteStream = File.Open(tempPackagePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                using (var package = new SignedPackageArchive(packageReadStream, packageWriteStream))
                {
                    var primarySignature = await package.GetPrimarySignatureAsync(token);

                    if (options.Overwrite && primarySignature != null)
                    {
                        await package.RemoveSignatureAsync(token);
                        inputPackagePath = tempPackagePath;
                    }
                }

                using (var packageReadStream = File.OpenRead(inputPackagePath))
                using (var packageWriteStream = File.OpenWrite(options.OutputFilePath))
                using (var package = new SignedPackageArchive(packageReadStream, packageWriteStream))
                {
                    if (await package.IsZip64Async(token))
                    {
                        throw new SignatureException(NuGetLogCode.NU3006, Strings.ErrorZip64NotSupported);
                    }

                    var hashAlgorithm = signRequest.SignatureHashAlgorithm;

                    var zipArchiveHash = await package.GetArchiveHashAsync(hashAlgorithm, token);
                    var signatureContent = GenerateSignatureContent(hashAlgorithm, zipArchiveHash);
                    var signature = await options.SignatureProvider.CreatePrimarySignatureAsync(signRequest, signatureContent, options.Logger, token);

                    using (var stream = new MemoryStream(signature.GetBytes()))
                    {
                        await package.AddSignatureAsync(stream, token);
                    }
                }
            }
            finally
            {
                FileUtility.Delete(tempPackagePath);
            }
        }

        private static SignatureContent GenerateSignatureContent(Common.HashAlgorithmName hashAlgorithmName, byte[] zipArchiveHash)
        {
            var base64ZipArchiveHash = Convert.ToBase64String(zipArchiveHash);

            return new SignatureContent(SigningSpecifications.V1, hashAlgorithmName, base64ZipArchiveHash);
        }
#else
        /// <summary>
        /// Remove a signature from a package.
        /// </summary>
        public static Task RemoveSignatureAsync(string packageFilePath, string outputFilePath, CancellationToken token) => throw new NotImplementedException();


        /// <summary>
        /// Add a signature to a package.
        /// </summary>
        public static Task SignAsync(SigningOptions options, SignPackageRequest signRequest, CancellationToken token) => throw new NotImplementedException();
#endif
    }
}