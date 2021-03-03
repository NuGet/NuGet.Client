// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.VisualStudio
{
    // Full functional implementation of IVsPathContext providing support for
    // reference resolving via reference lookup trie structure.
    internal class VsIndexedPathContext : IVsPathContext
    {
        private readonly PathLookupTrie<string> _referenceLookupIndex;
        private readonly INuGetTelemetryProvider _telemetryProvider;

        public string UserPackageFolder { get; }

        public IEnumerable FallbackPackageFolders { get; }

        public VsIndexedPathContext(
            string userPackageFolder,
            IEnumerable<string> fallbackPackageFolders,
            PathLookupTrie<string> index,
            INuGetTelemetryProvider telemetryProvider)
        {
            if (index == null)
            {
                throw new ArgumentNullException(nameof(index));
            }

            UserPackageFolder = userPackageFolder ?? throw new ArgumentNullException(nameof(userPackageFolder));
            FallbackPackageFolders = fallbackPackageFolders?.ToList() ?? throw new ArgumentNullException(nameof(fallbackPackageFolders));
            _referenceLookupIndex = index ?? throw new ArgumentNullException(nameof(index));
            _telemetryProvider = telemetryProvider ?? throw new ArgumentNullException(nameof(telemetryProvider));
        }

        public bool TryResolvePackageAsset(string packageAssetPath, out string packageDirectoryPath)
        {
            try
            {
                packageDirectoryPath = _referenceLookupIndex[packageAssetPath];
                return true;
            }
            catch (KeyNotFoundException)
            {
                packageDirectoryPath = null;
                return false;
            }
            catch (Exception exception)
            {
                _telemetryProvider.PostFault(exception, typeof(VsIndexedPathContext).FullName);
                throw;
            }
        }
    }
}
