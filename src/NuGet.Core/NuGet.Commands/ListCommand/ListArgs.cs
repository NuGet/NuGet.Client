// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.Commands
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

        public string ListCommandListNotSupported { get; }

        public CancellationToken CancellationToken { get; }

        public IList<Configuration.PackageSource> ListEndpoints { get; }

        public ListArgs(IList<string> arguments, IList<Configuration.PackageSource> listEndpoints,
            ISettings settings, ILogger logger, Log printJustified, bool isDetailedl,
            string listCommandNoPackages, string listCommandLicenseUrl, string listCommandListNotSupported,
            bool allVersions, bool includeDelisted, bool prerelease, CancellationToken token)
        {
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
            ListEndpoints = listEndpoints ?? throw new ArgumentNullException(nameof(listEndpoints));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            AllVersions = allVersions;
            IncludeDelisted = includeDelisted;
            Prerelease = prerelease;
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            PrintJustified = printJustified ?? throw new ArgumentNullException(nameof(printJustified));
            IsDetailed = isDetailedl;
            ListCommandNoPackages = listCommandNoPackages ?? throw new ArgumentNullException(nameof(listCommandNoPackages));
            ListCommandLicenseUrl = listCommandLicenseUrl ?? throw new ArgumentNullException(nameof(listCommandLicenseUrl));
            ListCommandListNotSupported = listCommandListNotSupported ?? throw new ArgumentNullException(nameof(listCommandListNotSupported));
            CancellationToken = token;
        }
    }
}
