// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using Moq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.PackageManagement.VisualStudio.Projects;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using VSLangProj150;

namespace Test.Utility
{
    public class TestProjectSystemServices : ILegacyPackageReferenceProjectServices
    {
        public TestProjectSystemServices()
        {
            Mock.Get(ReferencesReader)
                .Setup(x => x.GetProjectReferencesAsync(
                    It.IsAny<NuGet.Common.ILogger>(), CancellationToken.None))
                .ReturnsAsync(() => new ProjectRestoreReference[] { });

            Mock.Get(ReferencesReader)
                .Setup(x => x.GetPackageReferencesAsync(
                    It.IsAny<NuGetFramework>(), CancellationToken.None))
                .ReturnsAsync(() => new LibraryDependency[] { });
        }

        public IProjectSystemCapabilities Capabilities { get; } = Mock.Of<IProjectSystemCapabilities>();

        public IProjectSystemReferencesReader ReferencesReader { get; } = Mock.Of<IProjectSystemReferencesReader>();

        public IProjectSystemService ProjectSystem { get; } = Mock.Of<IProjectSystemService>();

        public IProjectSystemReferencesService References { get; } = Mock.Of<IProjectSystemReferencesService>();

        public IProjectScriptHostService ScriptService { get; } = Mock.Of<IProjectScriptHostService>();

        public VSProject4 Project4 { get; } = Mock.Of<VSProject4>();

        public T GetGlobalService<T>() where T : class
        {
            throw new NotImplementedException();
        }

        public void SetupInstalledPackages(NuGetFramework targetFramework, params LibraryDependency[] dependencies)
        {
            Mock.Get(ReferencesReader)
                .Setup(x => x.GetPackageReferencesAsync(targetFramework, CancellationToken.None))
                .ReturnsAsync(dependencies.ToList());
        }

        public void SetupProjectDependencies(params ProjectRestoreReference[] dependencies)
        {
            Mock.Get(ReferencesReader)
                .Setup(x => x.GetProjectReferencesAsync(It.IsAny<NuGet.Common.ILogger>(), CancellationToken.None))
                .ReturnsAsync(dependencies.ToList());

        }
    }
}
