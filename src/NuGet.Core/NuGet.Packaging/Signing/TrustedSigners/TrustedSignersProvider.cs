// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.Common;
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

        public IReadOnlyList<VerificationAllowListEntry> GetAllowListEntries(ILogger logger)
        {
            var trustedSignersSection = _settings.GetSection(ConfigurationConstants.TrustedSigners);
            if (trustedSignersSection == null)
            {
                return Enumerable.Empty<VerificationAllowListEntry>().ToList();
            }

            // We will dedup certificates based on fingerprint and hash algorithm, therefore
            // the key to this lookup will be hashAlgorithm-fingerprint
            var certificateLookup = new Dictionary<string, CertificateEntryLookupEntry>();

            foreach (var item in trustedSignersSection.Items.OfType<TrustedSignerItem>())
            {
                var itemTarget = GetItemTarget(item, out var itemPlacement);

                foreach (var certificate in item.Certificates)
                {
                    if (certificateLookup.TryGetValue($"{certificate.HashAlgorithm.ToString()}-{certificate.Fingerprint}", out var existingEntry))
                    {
                        if (existingEntry.Certificate.AllowUntrustedRoot != certificate.AllowUntrustedRoot)
                        {
                            // warn and take the most restrictive setting
                            logger.Log(new LogMessage(LogLevel.Warning,
                                string.Format(CultureInfo.CurrentCulture, Strings.ConflictingAllowUntrustedRoot, certificate.HashAlgorithm.ToString(), certificate.Fingerprint),
                                NuGetLogCode.NU3040));
                            existingEntry.Certificate.AllowUntrustedRoot = false;
                        }

                        existingEntry.Target |= itemTarget;
                        existingEntry.Placement |= itemPlacement;

                        if (itemTarget == VerificationTarget.Repository)
                        {
                            var owners = (item as RepositoryItem).Owners;
                            if (existingEntry.Owners == null)
                            {
                                existingEntry.Owners = new List<string>(owners);
                            }
                            else
                            {
                                existingEntry.Owners.AddRange(owners);
                            }
                        }
                    }
                    else
                    {
                        certificateLookup.Add($"{certificate.HashAlgorithm.ToString()}-{certificate.Fingerprint}", new CertificateEntryLookupEntry(itemTarget, itemPlacement, certificate));
                    }
                }
            }

            return certificateLookup.Select(e => e.Value.ToAllowListEntry()).ToList();
        }

        private VerificationTarget GetItemTarget(TrustedSignerItem item, out SignaturePlacement placement)
        {
            if (item is RepositoryItem)
            {
                placement = SignaturePlacement.Any;
                return VerificationTarget.Repository;
            }

            placement = SignaturePlacement.PrimarySignature;
            return VerificationTarget.Author;
        }

        private class CertificateEntryLookupEntry
        {
            public VerificationTarget Target { get; set; }

            public SignaturePlacement Placement { get; set; }

            public IList<string> Owners { get; set; }

            public CertificateItem Certificate { get; }

            public CertificateEntryLookupEntry(VerificationTarget target, SignaturePlacement placement, CertificateItem certificate, IList<string> owners = null)
            {
                Target = target;
                Placement = placement;
                Certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
                Owners = owners;
            }

            public CertificateHashAllowListEntry ToAllowListEntry()
            {
                return new TrustedSignerAllowListEntry(Target, Placement, Certificate.Fingerprint, Certificate.HashAlgorithm, Owners?.ToList());
            }
        }
    }
}
