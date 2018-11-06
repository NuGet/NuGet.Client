// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Commands
{
    public sealed class TrustedSignerActionsProvider
    {
        private readonly ITrustedSignersProvider _trustedSignersProvider;

        /// <summary>
        /// Internal SourceRepository usefull for overriding on tests to get
        /// mocked responses from the server
        /// </summary>
        internal SourceRepository ServiceIndexSourceRepository { get; set; }

        public TrustedSignerActionsProvider(ITrustedSignersProvider trustedSignersProvider)
        {
            _trustedSignersProvider = trustedSignersProvider ?? throw new ArgumentNullException(nameof(trustedSignersProvider));
        }

        /// <summary>
        /// Refresh the certificates of a repository item with the ones the server is announcing.
        /// </summary>
        /// <param name="name">Name of the repository item to refresh.</param>
        /// <param name="token">Cancellation token</param>
        /// <exception cref="InvalidOperationException">When a repository item with the given name is not found.</exception>
        public async Task SyncTrustedRepositoryAsync(string name, CancellationToken token)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(name));
            }

            var signers = _trustedSignersProvider.GetTrustedSigners();
            foreach (var existingRepository in signers.OfType<RepositoryItem>())
            {
                if (string.Equals(existingRepository.Name, name, StringComparison.Ordinal))
                {
                    var certificateItems = await GetCertificateItemsFromServiceIndexAsync(existingRepository.ServiceIndex, token);

                    existingRepository.Certificates.Clear();
                    existingRepository.Certificates.AddRange(certificateItems);

                    _trustedSignersProvider.AddOrUpdateTrustedSigner(existingRepository);

                    return;
                }
            }

            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_TrustedRepositoryDoesNotExist, name));
        }

#if IS_DESKTOP
        /// <summary>
        /// Adds a trusted signer item to the settings based a signed package.
        /// </summary>
        /// <param name="name">Name of the trusted signer.</param>
        /// <param name="package">Package to read signature from.</param>
        /// <param name="trustTarget">Signature to trust from package.</param>
        /// <param name="allowUntrustedRoot">Specifies if allowUntrustedRoot should be set to true.</param>
        /// <param name="owners">Trusted owners that should be set when trusting a repository.</param>
        /// <param name="token">Cancellation token for async request</param>
        public async Task AddTrustedSignerAsync(string name, ISignedPackageReader package, VerificationTarget trustTarget, bool allowUntrustedRoot, IEnumerable<string> owners, CancellationToken token)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(name));
            }

            if (!Enum.IsDefined(typeof(VerificationTarget), trustTarget) || trustTarget == VerificationTarget.Unknown)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_UnsupportedTrustTarget, trustTarget.ToString()));
            }

            if (trustTarget == VerificationTarget.Author && owners != null && owners.Any())
            {
                throw new ArgumentException(Strings.Error_TrustedAuthorNoOwners);
            }

            var v3ServiceIndex = string.Empty;
            IRepositorySignature reposig = null;
            var trustingRepository = trustTarget.HasFlag(VerificationTarget.Repository);

            var primarySignature = await package.GetPrimarySignatureAsync(token);

            if (primarySignature == null)
            {
                throw new InvalidOperationException(Strings.Error_PackageNotSigned);
            }

            if (trustingRepository)
            {
                if (primarySignature.Type == SignatureType.Repository)
                {
                    reposig = primarySignature as RepositoryPrimarySignature;
                }
                else
                {
                    var countersignature = RepositoryCountersignature.GetRepositoryCountersignature(primarySignature);
                    reposig = countersignature ?? throw new InvalidOperationException(Strings.Error_RepoTrustExpectedRepoSignature);
                }

                v3ServiceIndex = reposig.V3ServiceIndexUrl.AbsoluteUri;
            }

            var signers = _trustedSignersProvider.GetTrustedSigners();
            foreach (var existingSigner in signers)
            {
                if (string.Equals(existingSigner.Name, name, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_TrustedSignerAlreadyExists, name));
                }

                if (trustingRepository && existingSigner is RepositoryItem repoItem && string.Equals(repoItem.ServiceIndex, v3ServiceIndex, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_TrustedRepoAlreadyExists, v3ServiceIndex));
                }
            }

            if (trustingRepository)
            {
                var certificateItem = GetCertificateItemForSignature(reposig, allowUntrustedRoot);

                _trustedSignersProvider.AddOrUpdateTrustedSigner(new RepositoryItem(name, v3ServiceIndex, CreateOwnersList(owners), certificateItem));
            }

            if (trustTarget.HasFlag(VerificationTarget.Author))
            {
                if (primarySignature.Type != SignatureType.Author)
                {
                    throw new InvalidOperationException(Strings.Error_AuthorTrustExpectedAuthorSignature);
                }

                var certificateItem = GetCertificateItemForSignature(primarySignature, allowUntrustedRoot);

                _trustedSignersProvider.AddOrUpdateTrustedSigner(new AuthorItem(name, certificateItem));
            }
        }

#endif

        /// <summary>
        /// Adds a new trusted author to the settings.
        /// If a trusted signer already exists with this name, adds a certificate item to it.
        /// </summary>
        /// <param name="name">Name of the trusted author.</param>
        /// <param name="fingerprint">Fingerprint to be added to the certificate entry.</param>
        /// <param name="hashAlgorithm">Hash algorithm used to calculate <paramref name="fingerprint"/>.</param>
        /// <param name="allowUntrustedRoot">Specifies if allowUntrustedRoot should be set to true in the certificate entry.</param>
        public void AddOrUpdateTrustedSigner(string name, string fingerprint, HashAlgorithmName hashAlgorithm, bool allowUntrustedRoot)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(name));
            }

            if (string.IsNullOrEmpty(fingerprint))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(name));
            }

            if (!Enum.IsDefined(typeof(HashAlgorithmName), hashAlgorithm) || hashAlgorithm == HashAlgorithmName.Unknown)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.UnsupportedHashAlgorithm, hashAlgorithm.ToString()));
            }

            var certificateToAdd = new CertificateItem(fingerprint, hashAlgorithm, allowUntrustedRoot);
            TrustedSignerItem signerToAdd = null;

            var signers = _trustedSignersProvider.GetTrustedSigners();
            foreach (var existingSigner in signers)
            {
                if (string.Equals(existingSigner.Name, name, StringComparison.Ordinal))
                {
                    signerToAdd = existingSigner;

                    break;
                }
            }

            if (signerToAdd == null)
            {
                signerToAdd = new AuthorItem(name, certificateToAdd);
            }
            else
            {
                signerToAdd.Certificates.Add(certificateToAdd);
            }

            _trustedSignersProvider.AddOrUpdateTrustedSigner(signerToAdd);
        }

        /// <summary>
        /// Adds a trusted repository with information from <paramref name="serviceIndex"/>
        /// </summary>
        /// <param name="name">Name of the trusted repository.</param>
        /// <param name="serviceIndex">Service index of the trusted repository. Trusted certificates information will be gotten from here.</param>
        /// <param name="owners">Owners to be trusted from the repository.</param>
        /// <param name="token">Cancellation token</param>
        public async Task AddTrustedRepositoryAsync(string name, Uri serviceIndex, IEnumerable<string> owners, CancellationToken token)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(name));
            }

            if (serviceIndex == null)
            {
                throw new ArgumentNullException(nameof(serviceIndex));
            }

            var signers = _trustedSignersProvider.GetTrustedSigners();
            foreach (var existingSigner in signers)
            {
                if (string.Equals(existingSigner.Name, name, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_TrustedSignerAlreadyExists, name));
                }

                if (existingSigner is RepositoryItem repoItem && string.Equals(repoItem.ServiceIndex, serviceIndex.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_TrustedRepoAlreadyExists, serviceIndex.AbsoluteUri));
                }
            }

            var certificateItems = await GetCertificateItemsFromServiceIndexAsync(serviceIndex.AbsoluteUri, token);

            _trustedSignersProvider.AddOrUpdateTrustedSigner(
                new RepositoryItem(name, serviceIndex.AbsoluteUri, CreateOwnersList(owners), certificateItems));
        }
#if IS_DESKTOP
        private CertificateItem GetCertificateItemForSignature(ISignature signature, bool allowUntrustedRoot = false)
        {
            var defaultHashAlgorithm = HashAlgorithmName.SHA256;
            var fingerprint = CertificateUtility.GetHashString(signature.SignerInfo.Certificate, defaultHashAlgorithm);

            return new CertificateItem(fingerprint, defaultHashAlgorithm, allowUntrustedRoot);
        }
#endif
        private async Task<CertificateItem[]> GetCertificateItemsFromServiceIndexAsync(string serviceIndex, CancellationToken token)
        {
            if (string.IsNullOrEmpty(serviceIndex))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(serviceIndex));
            }

            if (ServiceIndexSourceRepository == null)
            {
                var packageSource = new PackageSource(serviceIndex);
                ServiceIndexSourceRepository = new SourceRepository(packageSource, Repository.Provider.GetCoreV3());
            }

            var repositorySignatureResource = await ServiceIndexSourceRepository.GetResourceAsync<RepositorySignatureResource>(token);

            if (repositorySignatureResource == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_InvalidCertificateInformationFromServer, serviceIndex));
            }

            if (repositorySignatureResource.RepositoryCertificateInfos == null || !repositorySignatureResource.RepositoryCertificateInfos.Any())
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_EmptyCertificateListInRepository, serviceIndex));
            }

            var certs = new List<CertificateItem>();
            foreach (var certInfo in repositorySignatureResource.RepositoryCertificateInfos)
            {
                foreach (var hashAlgorithm in SigningSpecifications.V1.AllowedHashAlgorithms)
                {
                    var fingerprint = certInfo.Fingerprints[hashAlgorithm.ConvertToOidString()];

                    if (!string.IsNullOrEmpty(fingerprint))
                    {
                        certs.Add(new CertificateItem(fingerprint, hashAlgorithm));
                    }
                }
            }

            return certs.ToArray();
        }

        private string CreateOwnersList(IEnumerable<string> owners)
        {
            if (owners != null && owners.Any())
            {
                return string.Join(OwnersItem.OwnersListSeparator.ToString(), owners);
            }

            return null;
        }
    }
}
