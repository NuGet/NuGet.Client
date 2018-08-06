// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using System.Threading;

namespace NuGet.CommandLine.XPlat
{
    public class ListPackageArgs
    {
        public ILogger Logger { get; }
        public string Path { get; set; }
        public List<PackageSource> PackageSources { get; set; }
        public bool Framework { get; set; }
        public IList<string> Frameworks { get; set; }
        public bool Outdated { get; set; }
        public bool Deprecated { get; set; }
        public bool Transitive { get; set; }
        public bool Prerelease { get; set; }
        public bool HighestPatch { get; set; }
        public bool HighestMinor { get; set; }
        
        public CancellationToken CancellationToken { get; set; }
        
        public ListPackageArgs(
            ILogger logger,
            string path,
            List<PackageSource> packageSources,
            bool framework,
            IList<string> frameworks,
            bool outdated,
            bool deprecated,
            bool transitive,
            bool prerelease,
            bool highestPatch,
            bool highestMinor,
            CancellationToken cancellationToken)
        {
            Logger = logger;
            ValidateArgument(path);
            Path = path;
            PackageSources = packageSources;
            Framework = framework;
            Frameworks = frameworks;
            Outdated = outdated;
            Deprecated = deprecated;
            Transitive = transitive;
            Prerelease = prerelease;
            HighestPatch = highestPatch;
            HighestMinor = highestMinor;
            CancellationToken = cancellationToken;
        }

        private void ValidateArgument(object arg)
        {
            if (arg == null)
            {
                throw new ArgumentNullException(nameof(arg));
            }
        }
    }
}