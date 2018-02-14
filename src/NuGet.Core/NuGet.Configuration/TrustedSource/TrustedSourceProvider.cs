// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;
using NuGet.Shared;

namespace NuGet.Configuration
{
    public class TrustedSourceProvider : ITrustedSourceProvider
    {
        private ISettings _settings;

        public TrustedSourceProvider(ISettings settings)
        {
            _settings = settings;
        }

        public IEnumerable<TrustedSource> LoadTrustedSources()
        {
            var trustedSources = new List<TrustedSource>();
            var trustedSourceNames = new HashSet<string>();
            _settings.GetAllSubsections(ConfigurationConstants.TrustedSources)
                .ForEach(s => trustedSourceNames.Add(s));

            foreach (var trustedSourceName in trustedSourceNames)
            {
                var trustedSource = LoadTrustedSource(trustedSourceName);

                if (trustedSource != null)
                {
                    trustedSources.Add(trustedSource);
                }
            }

            return trustedSources;
        }

        public TrustedSource LoadTrustedSource(string packageSourceName)
        {
            TrustedSource trustedSource = null;
            var settingValues = _settings.GetNestedSettingValues(ConfigurationConstants.TrustedSources, packageSourceName);

            if (settingValues?.Count > 0)
            {
                trustedSource = new TrustedSource(packageSourceName);
                foreach (var settingValue in settingValues)
                {
                    if (string.Equals(settingValue.Key, ConfigurationConstants.ServiceIndex, StringComparison.OrdinalIgnoreCase))
                    {
                        trustedSource.ServiceIndex = new ServiceIndexTrustEntry(settingValue.Value, settingValue.Priority);
                    }
                    else
                    {
                        var fingerprint = settingValue.Key;
                        var subjectName = settingValue.Value;
                        var algorithm = HashAlgorithmName.SHA256;

                        if (settingValue.AdditionalData.TryGetValue(ConfigurationConstants.FingerprintAlgorithm, out var algorithmString) &&
                            CryptoHashUtility.GetHashAlgorithmName(algorithmString) != HashAlgorithmName.Unknown)
                        {
                            algorithm = CryptoHashUtility.GetHashAlgorithmName(algorithmString);
                        }

                        trustedSource.Certificates.Add(new CertificateTrustEntry(fingerprint, subjectName, algorithm, settingValue.Priority));
                    }
                }
            }

            return trustedSource;
        }

        public void SaveTrustedSources(IEnumerable<TrustedSource> sources)
        {
            var existingSources = LoadTrustedSources();
            foreach (var source in sources)
            {
                SaveTrustedSource(source, existingSources);
            }
        }

        public void SaveTrustedSource(TrustedSource source)
        {
            var existingSources = LoadTrustedSources();
            SaveTrustedSource(source, existingSources);
        }

        private void SaveTrustedSource(TrustedSource source, IEnumerable<TrustedSource> existingSources)
        {
            var matchingSource = existingSources
                .Where(s => string.Equals(s.SourceName, source.SourceName, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            var settingValues = new List<SettingValue>();

            if (source.ServiceIndex != null)
            {
                // use existing priority if present
                var priority = matchingSource?.ServiceIndex?.Priority ?? source.ServiceIndex.Priority ?? 0;

                var settingValue = new SettingValue(ConfigurationConstants.ServiceIndex, source.ServiceIndex.Value, isMachineWide: false, priority: priority);
                settingValues.Add(settingValue);
            }

            foreach (var cert in source.Certificates)
            {
                // use existing priority if present
                var priority = matchingSource?.Certificates.FirstOrDefault(c => c.Fingerprint == cert.Fingerprint)?.Priority ?? cert.Priority ?? 0;

                // cant save to machine wide settings
                var settingValue = new SettingValue(cert.Fingerprint, cert.SubjectName, isMachineWide: false, priority: priority);

                settingValue.AdditionalData.Add(ConfigurationConstants.FingerprintAlgorithm, cert.FingerprintAlgorithm.ToString());
                settingValues.Add(settingValue);
            }

            if (matchingSource != null)
            {
                _settings.UpdateSubsections(ConfigurationConstants.TrustedSources, source.SourceName, settingValues);
            }
            else
            {
                _settings.SetNestedSettingValues(ConfigurationConstants.TrustedSources, source.SourceName, settingValues);
            }
        }

        public void DeleteTrustedSource(string sourceName)
        {
            // Passing an empty list of values will clear the existing sections
            _settings.UpdateSubsections(ConfigurationConstants.TrustedSources, sourceName, Enumerable.Empty<SettingValue>().AsList());
        }
    }
}
