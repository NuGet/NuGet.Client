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

            return trustedSignersSection.Items.OfType<TrustedSignerItem>().Select(s => ToAllowListEntry(s)).ToList();
        }

        private VerificationAllowListEntry ToAllowListEntry(TrustedSignerItem item)
        {
            return null;
        }
    }
}
