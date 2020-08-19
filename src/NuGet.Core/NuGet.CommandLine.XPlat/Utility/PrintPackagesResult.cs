// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.CommandLine.XPlat.Utility
{
    /// <summary>
    /// Provides meta information found when printing packages.
    /// </summary>
    internal readonly struct PrintPackagesResult
    {
        public PrintPackagesResult(bool autoReferenceFound, bool outdatedFound, bool deprecatedFound, bool vulnerableFound)
        {
            AutoReferenceFound = autoReferenceFound;
            OutdatedFound = outdatedFound;
            DeprecatedFound = deprecatedFound;
            VulnerableFound = vulnerableFound;
        }

        /// <summary>
        /// <c>True</c> when an auto-referenced package was found; otherwise <c>False</c>.
        /// </summary>
        public bool AutoReferenceFound { get; }

        /// <summary>
        /// <c>True</c> when an outdated package was found; otherwise <c>False</c>.
        /// </summary>
        public bool OutdatedFound { get; }

        /// <summary>
        /// <c>True</c> when a deprecated package was found; otherwise <c>False</c>.
        /// </summary>
        public bool DeprecatedFound { get; }

        /// <summary>
        /// <c>True</c> when a vulnerable package was found; otherwise <c>False</c>.
        /// </summary>
        public bool VulnerableFound { get; }
    }
}
