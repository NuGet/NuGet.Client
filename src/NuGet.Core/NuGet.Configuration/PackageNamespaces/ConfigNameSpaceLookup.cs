// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Configuration
{
    public class ConfigNameSpaceLookup
    {
        public bool PrefixMatch { get; }

        public HashSet<string> PackageSources { get; }

        public ConfigNameSpaceLookup(bool prefixMatch, HashSet<string> packageSources)
        {
            PrefixMatch = prefixMatch;
            PackageSources = packageSources;
        }
    }
}
