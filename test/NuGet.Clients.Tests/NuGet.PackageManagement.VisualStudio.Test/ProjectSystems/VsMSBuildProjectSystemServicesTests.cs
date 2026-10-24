// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class VsMSBuildProjectSystemServicesTests
    {
        [Fact]
        public void Constructor_NativeProjectSystem_UsesCpsReferenceReader()
        {
            var projectAdapter = new Mock<IVsProjectAdapter>();
            var projectContext = new Mock<INuGetProjectContext>();
            var threadingService = new Mock<IVsProjectThreadingService>();
            var scriptExecutor = new Lazy<IScriptExecutor>(() => Mock.Of<IScriptExecutor>());
            var projectSystem = new NativeProjectSystem(projectAdapter.Object, projectContext.Object);

            var services = new VsMSBuildProjectSystemServices(
                projectAdapter.Object,
                projectSystem,
                threadingService.Object,
                scriptExecutor);

            Assert.IsType<CpsProjectSystemReferenceReader>(services.ReferencesReader);
        }
    }
}
