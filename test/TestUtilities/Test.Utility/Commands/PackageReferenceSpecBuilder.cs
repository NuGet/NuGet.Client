// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Commands.Test;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;

namespace Test.Utility.Commands
{
    public class PackageReferenceSpecBuilder
    {
        private IEnumerable<string> _targetFrameworks;
        private IEnumerable<string> _runtimeIdentifiers;
        private IEnumerable<string> _runtimeSupports;

        private string _projectName;
        private string _projectDirectory;
        private bool _isLockedMode;
        private bool _isRestorePackagesWithLockFile;
        private PackageReferenceSpecBuilder()
        {
        }

        public static PackageReferenceSpecBuilder Create(string projectName, string projectDirectory)
        {
            return new PackageReferenceSpecBuilder()
            {
                _projectName = projectName ?? throw new ArgumentNullException(nameof(projectName)),
                _projectDirectory = projectDirectory ?? throw new ArgumentNullException(nameof(projectDirectory))
            };
        }

        public PackageReferenceSpecBuilder WithTargetFrameworks(IEnumerable<string> targetFrameworks)
        {
            _targetFrameworks = targetFrameworks ?? throw new ArgumentNullException(nameof(targetFrameworks));
            return this;
        }

        public PackageReferenceSpecBuilder WithRuntimeIdentifiers(IEnumerable<string> runtimeIdentifiers, IEnumerable<string> runtimeSupports)
        {
            _runtimeIdentifiers = runtimeIdentifiers ?? throw new ArgumentNullException(nameof(runtimeIdentifiers));
            _runtimeSupports = runtimeSupports ?? throw new ArgumentNullException(nameof(runtimeSupports));
            return this;
        }

        public PackageReferenceSpecBuilder WithPackagesLockFile(bool isLockedMode = false)
        {
            _isLockedMode = isLockedMode;
            _isRestorePackagesWithLockFile = true;
            return this;
        }

        public PackageSpec Build()
        {
            var projectPath = Path.Combine(_projectDirectory, _projectName + ".csproj");

            var packageSpec = JsonPackageSpecReader.GetPackageSpec("{ }", _projectName, projectPath);
            packageSpec.RuntimeGraph = ProjectTestHelpers.GetRuntimeGraph(_runtimeIdentifiers, _runtimeSupports);

            IEnumerable<NuGetFramework> targetFrameworks = _targetFrameworks.Select(e => NuGetFramework.Parse(e));
            packageSpec.TargetFrameworks.AddRange(targetFrameworks.Select(e => new TargetFrameworkInformation { FrameworkName = e }));

            packageSpec.RestoreMetadata = new ProjectRestoreMetadata
            {
                CrossTargeting = packageSpec.TargetFrameworks.Count > 0,
                OriginalTargetFrameworks = packageSpec.TargetFrameworks.Select(e => e.FrameworkName.GetShortFolderName()).ToList(),
                OutputPath = _projectDirectory,
                ProjectStyle = ProjectStyle.PackageReference,
                ProjectName = _projectName,
                ProjectUniqueName = _projectName,
                ProjectPath = projectPath,
                ConfigFilePaths = new List<string>(),
                RestoreLockProperties = new RestoreLockProperties(_isRestorePackagesWithLockFile.ToString(), Path.Combine(_projectDirectory, PackagesLockFileFormat.LockFileName), _isLockedMode),
            };
            packageSpec.RestoreMetadata.TargetFrameworks.AddRange(targetFrameworks.Select(e => new ProjectRestoreMetadataFrameworkInfo { FrameworkName = e }));


            return packageSpec;
        }
    }
}
