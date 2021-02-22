// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.VisualStudio
{
    // Implementation of IVsPathContext without support of reference resolving.
    // Used when project is not managed by NuGet or given project doesn't have any packages installed.
    internal class VsPathContext : IVsPathContext2
    {
        public string UserPackageFolder { get; }

        public IEnumerable FallbackPackageFolders { get; }

        public string SolutionPackageFolder { get; }

        private INuGetTelemetryProvider _telemetryProvider;

        public VsPathContext(NuGetPathContext pathContext, INuGetTelemetryProvider telemetryProvider, string solutionPackageFolder = null)
        {
            if (pathContext == null)
            {
                throw new ArgumentNullException(nameof(pathContext));
            }

            _telemetryProvider = telemetryProvider ?? throw new ArgumentNullException(nameof(telemetryProvider));

            try
            {
                UserPackageFolder = pathContext.UserPackageFolder;
                FallbackPackageFolders = pathContext.FallbackPackageFolders;
                SolutionPackageFolder = solutionPackageFolder;
            }
            catch (Exception exception)
            {
                _telemetryProvider.PostFault(exception, typeof(VsPathContext).FullName);
                throw;
            }
        }

        public VsPathContext(string userPackageFolder, IEnumerable<string> fallbackPackageFolders, INuGetTelemetryProvider telemetryProvider)
        {
            if (userPackageFolder == null)
            {
                throw new ArgumentNullException(nameof(userPackageFolder));
            }

            if (fallbackPackageFolders == null)
            {
                throw new ArgumentNullException(nameof(fallbackPackageFolders));
            }

            _telemetryProvider = telemetryProvider ?? throw new ArgumentNullException(nameof(telemetryProvider));

            try
            {
                UserPackageFolder = userPackageFolder;
                FallbackPackageFolders = fallbackPackageFolders.ToList();
            }
            catch (Exception exception)
            {
                _telemetryProvider.PostFault(exception, typeof(VsPathContext).FullName);
                throw;
            }
        }

        public bool TryResolvePackageAsset(string packageAssetPath, out string packageDirectoryPath)
        {
            // unable to resolve the reference file path without the index
            packageDirectoryPath = null;
            return false;
        }
    }
}
