// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using NuGet.Common;

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
            var trustedSourceNames = _settings.GetAllSubsections(ConfigurationConstants.TrustedSources);

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
                        trustedSource.ServiceIndex = settingValue.Value;
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

                        trustedSource.Certificates.Add(new CertificateTrustEntry(fingerprint, subjectName, algorithm));
                    }
                }
            }

            return trustedSource;
        }

        public void SaveTrustedSources(IEnumerable<TrustedSource> sources)
        {
            throw new NotImplementedException();
        }

        public void SaveTrustedSource(TrustedSource trustedSource)
        {
            throw new NotImplementedException();
        }
    }
}
