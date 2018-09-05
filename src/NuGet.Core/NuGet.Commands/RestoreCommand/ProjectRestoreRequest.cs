// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.RuntimeModel;

namespace NuGet.Commands
{
    internal class ProjectRestoreRequest
    {
        public ProjectRestoreRequest(
            RestoreRequest request,
            PackageSpec packageSpec,
            LockFile existingLockFile,
            Dictionary<NuGetFramework, RuntimeGraph> runtimeGraphCache,
            ConcurrentDictionary<PackageIdentity, RuntimeGraph> runtimeGraphCacheByPackage)
        {
            CacheContext = request.CacheContext;
            Log = request.Log;
            PackagesDirectory = request.PackagesDirectory;
            ExistingLockFile = existingLockFile;
            RuntimeGraphCache = runtimeGraphCache;
            RuntimeGraphCacheByPackage = runtimeGraphCacheByPackage;
            MaxDegreeOfConcurrency = request.MaxDegreeOfConcurrency;
            PackageSaveMode = request.PackageSaveMode;
            Project = packageSpec;
            XmlDocFileSaveMode = request.XmlDocFileSaveMode;
        }

        public SourceCacheContext CacheContext { get; }
        public ILogger Log { get; }
        public string PackagesDirectory { get; }
        public int MaxDegreeOfConcurrency { get; }
        public LockFile ExistingLockFile { get; }
        public PackageSpec Project { get; }
        public PackageSaveMode PackageSaveMode { get; }
        public XmlDocFileSaveMode XmlDocFileSaveMode { get; }
        public Dictionary<NuGetFramework, RuntimeGraph> RuntimeGraphCache { get; }
        public ConcurrentDictionary<PackageIdentity, RuntimeGraph> RuntimeGraphCacheByPackage { get; }
    }
}