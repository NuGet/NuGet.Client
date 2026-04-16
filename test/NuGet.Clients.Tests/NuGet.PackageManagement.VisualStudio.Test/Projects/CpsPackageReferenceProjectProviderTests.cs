// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test.Projects
{
    [Collection(MockedVS.Collection)]
    public class CpsPackageReferenceProjectProviderTests : MockedVSCollectionTests
    {
        public CpsPackageReferenceProjectProviderTests(GlobalServiceProvider globalServiceProvider)
            : base(globalServiceProvider)
        {
            globalServiceProvider.Reset();
        }

        // As of October 2020, Service Fabric projects (sfproj) uses CPS, but does not support PackageReference. Make sure non-PR CPS projects do not use this project system.
        [Fact]
        public async Task TryCreateNuGetProject_CpsProjectWithoutPackageReferencesCapability_ReturnsNull()
        {
            // Arrange
            var hierarchy = new Mock<IVsHierarchy>();

            var projectAdapter = new Mock<IVsProjectAdapter>();
            projectAdapter.SetupGet(a => a.VsHierarchy)
                .Returns(hierarchy.Object);
            projectAdapter.Setup(a => a.IsCapabilityMatch(NuGet.VisualStudio.IDE.ProjectCapabilities.Cps))
                .Returns(true);
            projectAdapter.Setup(a => a.IsCapabilityMatch(NuGet.VisualStudio.IDE.ProjectCapabilities.PackageReferences))
                .Returns(false);

            var nugetProjectContext = new Mock<INuGetProjectContext>();

            var ppc = new ProjectProviderContext(nugetProjectContext.Object, packagesPathFactory: () => throw new NotImplementedException());

            var projectSystemCache = new Mock<IProjectSystemCache>();
            var scriptExecutor = new Mock<Lazy<IScriptExecutor>>();
            var target = new CpsPackageReferenceProjectProvider(projectSystemCache.Object, scriptExecutor.Object);

            // Act
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            NuGetProject actual = target.TryCreateNuGetProject(projectAdapter.Object, ppc, forceProjectType: false);

            // Assert
            Assert.Null(actual);
            projectAdapter.Verify(a => a.IsCapabilityMatch(NuGet.VisualStudio.IDE.ProjectCapabilities.Cps), Times.Once);
            projectAdapter.Verify(a => a.IsCapabilityMatch(NuGet.VisualStudio.IDE.ProjectCapabilities.PackageReferences), Times.Once);
        }

        [Fact]
        public async Task TryCreateNuGetProject_CpsPackageReferenceProject_InitializesDteProject()
        {
            // Arrange
            var hierarchy = new Mock<IVsHierarchy>();
            var dteProject = new Mock<EnvDTE.Project>();
            var buildProperties = new Mock<IVsProjectBuildProperties>();
            string? restoreProjectStyle = null;
#pragma warning disable CS0618 // Type or member is obsolete
            buildProperties.Setup(x => x.GetPropertyValueWithDteFallback(ProjectBuildProperties.RestoreProjectStyle))
                .Returns(restoreProjectStyle);
#pragma warning restore CS0618 // Type or member is obsolete

            var projectAdapter = new Mock<IVsProjectAdapter>();
            projectAdapter.SetupGet(a => a.VsHierarchy)
                .Returns(hierarchy.Object);
            projectAdapter.SetupGet(a => a.BuildProperties)
                .Returns(buildProperties.Object);
            projectAdapter.SetupGet(a => a.Project)
                .Returns(dteProject.Object);
            projectAdapter.SetupGet(a => a.FullProjectPath)
                .Returns(@"c:\test\project.csproj");
            projectAdapter.SetupGet(a => a.ProjectName)
                .Returns("TestProject");
            projectAdapter.SetupGet(a => a.CustomUniqueName)
                .Returns("TestProject");
            projectAdapter.SetupGet(a => a.ProjectId)
                .Returns(Guid.NewGuid().ToString());
            projectAdapter.Setup(a => a.IsCapabilityMatch(NuGet.VisualStudio.IDE.ProjectCapabilities.Cps))
                .Returns(true);
            projectAdapter.Setup(a => a.IsCapabilityMatch(NuGet.VisualStudio.IDE.ProjectCapabilities.PackageReferences))
                .Returns(true);

            var nugetProjectContext = new Mock<INuGetProjectContext>();

            var ppc = new ProjectProviderContext(nugetProjectContext.Object, packagesPathFactory: () => throw new NotImplementedException());

            var projectSystemCache = new Mock<IProjectSystemCache>();
            var scriptExecutor = new Mock<Lazy<IScriptExecutor>>();
            var target = new CpsPackageReferenceProjectProvider(projectSystemCache.Object, scriptExecutor.Object);

            // Act
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            NuGetProject actual = target.TryCreateNuGetProject(projectAdapter.Object, ppc, forceProjectType: false);

            // Assert
            Assert.NotNull(actual);
            Assert.IsType<CpsPackageReferenceProject>(actual);
            projectAdapter.VerifyGet(a => a.Project, Times.Once);
        }
    }
}
