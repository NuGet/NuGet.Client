// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using NuGet.Common;
using NuGet.Packaging;

namespace NuGet.Protocol
{
    /// <summary>
    /// Allow .nuspec files on disk to be cached across v3 folder readers.
    /// </summary>
    /// <remarks>Nuspecs that do not exist are returned as null. It is expected that the caller has already verified
    /// the that folder and paths are valid.</remarks>
    public class LocalNuspecCache
    {
        // Expanded path -> NuspecReader
        private readonly ConcurrentDictionary<string, Lazy<NuspecReader>> _cache
            = new ConcurrentDictionary<string, Lazy<NuspecReader>>(PathUtility.GetStringComparerBasedOnOS());

        public LocalNuspecCache()
        {
        }

        /// <summary>
        /// Read a nuspec file from disk. The nuspec is expected to exist.
        /// </summary>
        public Lazy<NuspecReader> GetOrAdd(string manifestPath, string expandedPath)
        {
            return _cache.GetOrAdd(expandedPath,
                e => new Lazy<NuspecReader>(() => GetNuspec(manifestPath, expandedPath)));
        }

        /// <summary>
        /// Search for a nuspec using the given path, or by the expanded folder path.
        /// The manifest path here is a shortcut to use the already constructed well
        /// known location, if this doesn't exist the folder reader will find the nuspec
        /// if it exists.
        /// </summary>
        private static NuspecReader GetNuspec(string manifestPath, string expandedPath)
        {
            NuspecReader nuspec = null;

            // Verify that the nuspec has the correct name before opening it
            if (File.Exists(manifestPath))
            {
                nuspec = new NuspecReader(File.OpenRead(manifestPath));
            }
            else
            {
                // Scan the folder for the nuspec
                var folderReader = new PackageFolderReader(expandedPath);

                // This will throw if the nuspec is not found
                nuspec = new NuspecReader(folderReader.GetNuspec());
            }

            return nuspec;
        }
    }
}
