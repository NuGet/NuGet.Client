// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Configuration;

namespace NuGet.Commands
{
    public class ListArgs
    {
        public delegate void Log(string message);

        public bool AllVersions { get; }

        public bool IncludeDelisted { get; }

        public bool Prerelease { get; }

        public bool Verbose { get; }

        public IList<string> Arguments { get; }

        public ISettings Settings { get; }

        public Log LogError { get; }

        public Log LogInformation { get; }

        public IList<KeyValuePair<Configuration.PackageSource, string>> ListEndpoints { get; }

        public ListArgs(IList<string> arguments, IList<KeyValuePair<Configuration.PackageSource,string>> listEndpoints, ISettings settings, Log logInformation, Log logError, bool allVersions, bool includeDelisted, bool prerelease, bool verbose)
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
            else if (logInformation == null)
            {
                throw new ArgumentNullException(nameof(logInformation));
            }
            else if (logError == null)
            {
                throw new ArgumentNullException(nameof(logError));
            }

            Arguments = arguments;
            ListEndpoints = listEndpoints;
            Settings = settings;
            AllVersions = allVersions;
            IncludeDelisted = includeDelisted;
            Prerelease = prerelease;
            Verbose = verbose;
            LogError = logError;
            LogInformation = logInformation;
        }
    }
}