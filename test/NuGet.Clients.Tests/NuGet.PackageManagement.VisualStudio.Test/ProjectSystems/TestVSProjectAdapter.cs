// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using NuGet.Frameworks;
using NuGet.ProjectManagement;
using NuGet.RuntimeModel;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    internal class TestVSProjectAdapter : IVsProjectAdapter
    {
        private readonly string _targetFrameworkString;
        private readonly string _restorePackagesWithLockFile;
        private readonly string _nuGetLockFilePath;
        private readonly bool _restoreLockedMode;

        public TestVSProjectAdapter(
            string fullProjectPath,
            ProjectNames projectNames,
            string targetFrameworkString,
            string restorePackagesWithLockFile = null,
            string nuGetLockFilePath = null,
            bool restoreLockedMode = false)
        {
            FullProjectPath = fullProjectPath;
            ProjectNames = projectNames;
            _targetFrameworkString = targetFrameworkString;
            _restorePackagesWithLockFile = restorePackagesWithLockFile;
            _nuGetLockFilePath = nuGetLockFilePath;
            _restoreLockedMode = restoreLockedMode;
        }

        public string AssetTargetFallback
        {
            get
            {
                return null;
            }
        }

        public string MSBuildProjectExtensionsPath
        {
            get
            {
                return Path.Combine(ProjectDirectory, "obj");
            }
        }

        public IProjectBuildProperties BuildProperties { get; } = Mock.Of<IProjectBuildProperties>();

        public string CustomUniqueName => ProjectNames.CustomUniqueName;

        public string FullName => ProjectNames.FullName;

        public string FullProjectPath { get; private set; }

        public bool IsDeferred => false;

        public bool IsSupported => true;

        public string NoWarn
        {
            get
            {
                return null;
            }
        }

        public string PackageTargetFallback
        {
            get
            {
                return null;
            }
        }

        public Project Project { get; } = Mock.Of<Project>();

        public string ProjectId
        {
            get
            {
                return Guid.Empty.ToString();
            }
        }

        public string ProjectDirectory
        {
            get
            {
                return Path.GetDirectoryName(FullProjectPath);
            }
        }

        public string ProjectName => ProjectNames.ShortName;

        public ProjectNames ProjectNames { get; private set; }

        public string RestoreAdditionalProjectFallbackFolders
        {
            get
            {
                return null;
            }
        }

        public string RestoreAdditionalProjectSources
        {
            get
            {
                return null;
            }
        }

        public string RestoreFallbackFolders
        {
            get
            {
                return null;
            }
        }

        public string RestorePackagesPath
        {
            get
            {
                return null;
            }
        }

        public string RestoreSources
        {
            get
            {
                return null;
            }
        }

        public string TreatWarningsAsErrors
        {
            get
            {
                return null;
            }
        }

        public string UniqueName => ProjectNames.UniqueName;

        public string Version
        {
            get
            {
                return "1.0.0";
            }
        }

        public IVsHierarchy VsHierarchy { get; } = Mock.Of<IVsHierarchy>();

        public string WarningsAsErrors
        {
            get
            {
                return null;
            }
        }

        public Task<FrameworkName> GetDotNetFrameworkNameAsync()
        {
            return Task.FromResult(new FrameworkName(_targetFrameworkString));
        }

        public Task<string> GetNuGetLockFilePathAsync() => Task.FromResult<string>(_nuGetLockFilePath);

        public Task<string[]> GetProjectTypeGuidsAsync()
        {
            return Task.FromResult(Array.Empty<string>());
        }

        public Task<IEnumerable<string>> GetReferencedProjectsAsync()
        {
            return Task.FromResult(Enumerable.Empty<string>());
        }

        public Task<string> GetRestorePackagesWithLockFileAsync()
        {
            return Task.FromResult<string>(_restorePackagesWithLockFile);
        }

        public Task<IEnumerable<RuntimeDescription>> GetRuntimeIdentifiersAsync()
        {
            return Task.FromResult(Enumerable.Empty<RuntimeDescription>());
        }

        public Task<IEnumerable<CompatibilityProfile>> GetRuntimeSupportsAsync()
        {
            return Task.FromResult(Enumerable.Empty<CompatibilityProfile>());
        }

        public Task<NuGetFramework> GetTargetFrameworkAsync()
        {
            return Task.FromResult(NuGetFramework.Parse(_targetFrameworkString));
        }

        public Task<bool> IsRestoreLockedAsync()
        {
            return Task.FromResult(_restoreLockedMode);
        }
    }
}
