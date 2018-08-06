// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.Configuration;
using System.Threading;

namespace NuGet.CommandLine.XPlat
{
    public class ListPackageArgs
    {
        public ILogger Logger { get; }
        public string Path { get; set; }
        public List<PackageSource> PackageSources { get; set; }
        public IList<string> Frameworks { get; set; }
        public bool IncludeOutdated { get; set; }
        public bool IncludeDeprecated { get; set; }
        public bool IncludeTransitive { get; set; }
        public bool Prerelease { get; set; }
        public bool HighestPatch { get; set; }
        public bool HighestMinor { get; set; }
        
        public CancellationToken CancellationToken { get; set; }
        
        public ListPackageArgs(
            string path,
            List<PackageSource> packageSources,
            IList<string> frameworks,
            bool includeOutdated,
            bool includeDeprecated,
            bool includeTransitive,
            bool prerelease,
            bool highestPatch,
            bool highestMinor,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            PackageSources = packageSources;
            Frameworks = frameworks;
            IncludeOutdated = includeOutdated;
            IncludeDeprecated = includeDeprecated;
            IncludeTransitive = includeTransitive;
            Prerelease = prerelease;
            HighestPatch = highestPatch;
            HighestMinor = highestMinor;
            Logger = logger;
            CancellationToken = cancellationToken;
        }

    }
}