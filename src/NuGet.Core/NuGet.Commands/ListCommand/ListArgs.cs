// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.Commands
{
    public class ListArgs
    {
        public bool AllVersions { get; }

        public bool IncludeDelisted { get; }

        public bool Prerelease { get; }

        public bool Verbose { get; }

        public IList<string> Arguments { get; }

        //TODO NK - 
        public ISettings Settings { get; }

        public ILogger Logger { get; } // Is this ok? Since this is the console?

        public IList<KeyValuePair<Configuration.PackageSource, string>> ListEndpoints { get; }
        
        public ListArgs(IList<string> arguments, IList<KeyValuePair<Configuration.PackageSource, string>> listEndpoints,
            ISettings settings, ILogger logger, bool allVersions, bool includeDelisted, bool prerelease, bool verbose)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }
            else if (listEndpoints == null)
            {
                throw new ArgumentNullException(nameof(listEndpoints));
            }
            else if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            else if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
            Arguments = arguments;
            ListEndpoints = listEndpoints;
            Settings = settings;
            AllVersions = allVersions;
            IncludeDelisted = includeDelisted;
            Prerelease = prerelease;
            Verbose = verbose;
            Logger = logger;
        }
    }
}