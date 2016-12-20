// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.Commands.ListCommand
{
    public class ListArgs
    {
        public delegate void Log(int startIndex, string message);

        public bool AllVersions { get; }

        public bool IncludeDelisted { get; }

        public bool Prerelease { get; }

        public IList<string> Arguments { get; }

        public ISettings Settings { get; }

        public ILogger Logger { get; }

        public Log PrintJustified { get; }

        public bool IsDetailed { get; }

        public string ListCommandNoPackages { get; }

        public string ListCommandLicenseUrl { get; }

        public CancellationToken CancellationToken { get; }

        public IList<KeyValuePair<Configuration.PackageSource, string>> ListEndpoints { get; }

        public ListArgs(IList<string> arguments, IList<KeyValuePair<Configuration.PackageSource, string>> listEndpoints,
            ISettings settings, ILogger logger, Log printJustified, bool isDetailedl, string listCommandNoPackages, string listCommandLicenseUrl, bool allVersions, bool includeDelisted, bool prerelease, CancellationToken token)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }
            if (listEndpoints == null)
            {
                throw new ArgumentNullException(nameof(listEndpoints));
            }
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
            if (printJustified == null)
            {
                throw new ArgumentNullException(nameof(printJustified));
            }
            if (listCommandNoPackages == null)
            {
                throw new ArgumentNullException(nameof(listCommandNoPackages));
            }
            if (listCommandLicenseUrl == null)
            {
                throw new ArgumentNullException(nameof(listCommandLicenseUrl));
            }
            Arguments = arguments;
            ListEndpoints = listEndpoints;
            Settings = settings;
            AllVersions = allVersions;
            IncludeDelisted = includeDelisted;
            Prerelease = prerelease;
            Logger = logger;
            PrintJustified = printJustified;
            IsDetailed = isDetailedl;
            ListCommandNoPackages = listCommandNoPackages;
            ListCommandLicenseUrl = listCommandLicenseUrl;
            CancellationToken = token;
        }
    }
}