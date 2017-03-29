// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.VisualStudio
{
    // Full functional implementation of IVsPathContext providing support for
    // reference resolving via reference lookup trie structure.
    internal class VsIndexedPathContext : IVsPathContext
    {
        private readonly PathLookupTrie<string> _referenceLookupIndex;

        public string UserPackageFolder { get; }

        public IEnumerable FallbackPackageFolders { get; }

        public VsIndexedPathContext(
            string userPackageFolder,
            IEnumerable<string> fallbackPackageFolders,
            PathLookupTrie<string> index)
        {
            if (userPackageFolder == null)
            {
                throw new ArgumentNullException(nameof(userPackageFolder));
            }

            if (fallbackPackageFolders == null)
            {
                throw new ArgumentNullException(nameof(fallbackPackageFolders));
            }

            if (index == null)
            {
                throw new ArgumentNullException(nameof(index));
            }

            UserPackageFolder = userPackageFolder;
            FallbackPackageFolders = fallbackPackageFolders.ToList();
            _referenceLookupIndex = index;
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
        }
    }
}
