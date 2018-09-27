// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;

namespace NuGet.Packaging.Signing
{
    public class TrustedSignersProvider
    {
        private readonly ISettings _settings;

        public TrustedSignersProvider(ISettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public IReadOnlyList<VerificationAllowListEntry> GetAllowListEntries()
        {
            var trustedSignersSection = _settings.GetSection(ConfigurationConstants.TrustedSigners);
            if (trustedSignersSection == null)
            {
                return Enumerable.Empty<VerificationAllowListEntry>().ToList();
            }

            return trustedSignersSection.Items.OfType<TrustedSignerItem>().SelectMany(s => ToAllowListEntries(s)).ToList();
        }

        private IReadOnlyList<VerificationAllowListEntry> ToAllowListEntries(TrustedSignerItem item)
        {
            var entries = new List<VerificationAllowListEntry>();
            if (item is RepositoryItem repositoryItem)
            {
                foreach(var certificate in repositoryItem.Certificates)
                {
                    entries.Add(new TrustedRepositoryAllowListEntry(certificate.Fingerprint, certificate.HashAlgorithm, repositoryItem.Owners));
                }
            }
            else if (item is AuthorItem authorItem)
            {
                foreach (var certificate in authorItem.Certificates)
                {
                    entries.Add(new CertificateHashAllowListEntry(VerificationTarget.Author, SignaturePlacement.PrimarySignature, certificate.Fingerprint, certificate.HashAlgorithm));
                }
            }

            return entries;
        }
    }
}
