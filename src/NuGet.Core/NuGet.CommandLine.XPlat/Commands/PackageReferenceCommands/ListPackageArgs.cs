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
        public bool Framework { get; set; }
        public IList<string> Frameworks { get; set; }
        public bool Outdated { get; set; }
        public bool Deprecated { get; set; }
        public bool Transitive { get; set; }
        public PackageSourceProvider SourceProvider { get; set; }
        public CancellationToken CancellationToken { get; set; }
        
        public ListPackageArgs(ILogger logger, string path, PackageSourceProvider sourceProvider, bool framework, IList<string> frameworks, bool outdated, bool deprecated, bool transitive, CancellationToken cancellationToken)
        {
            Logger = logger;
            ValidateArgument(path);
            Path = path;
            SourceProvider = sourceProvider;
            Framework = framework;
            Frameworks = frameworks;
            Outdated = outdated;
            Deprecated = deprecated;
            Transitive = transitive;
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