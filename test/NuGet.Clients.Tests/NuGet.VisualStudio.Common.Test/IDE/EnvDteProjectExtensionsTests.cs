// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;
using Moq;
using Xunit;

namespace NuGet.VisualStudio.Common.Test.IDE
{
    [Collection(MockedVS.Collection)]
    public class EnvDteProjectExtensionsTests
    {
        public EnvDteProjectExtensionsTests(GlobalServiceProvider sp)
        {
            sp.Reset();
            NuGetUIThreadHelper.SetCustomJoinableTaskFactory(ThreadHelper.JoinableTaskFactory);
        }

        [Fact]
        public async Task GetCustomUniqueNameAsync_ParentProjectItemThrowsArgumentException_ReturnsProjectName()
        {
            // Arrange
            // Regression: DteMiscProject.ParentProjectItem can throw ArgumentException.
            // https://devdiv.visualstudio.com/DevDiv/_git/VS?path=/src/env/vscore/package/Solutions/Dte/DteMiscProject.cs&version=GC01fad60843e7b3b97d52e6a6a602b0eace04a509&line=419&lineEnd=420&lineStartColumn=1&lineEndColumn=1&lineStyle=plain&_a=contents
            const string projectName = "MiscellaneousFiles";

            var project = new Mock<Project>();
            project.SetupGet(p => p.Name).Returns(projectName);
            project.SetupGet(p => p.Kind).Returns(VsProjectTypes.VsProjectKindMisc);
            project.SetupGet(p => p.ParentProjectItem).Throws(new ArgumentException("Item was not found."));

            // Act
            string customUniqueName = await project.Object.GetCustomUniqueNameAsync();

            // Assert
            Assert.Equal(projectName, customUniqueName);
        }
    }
}
