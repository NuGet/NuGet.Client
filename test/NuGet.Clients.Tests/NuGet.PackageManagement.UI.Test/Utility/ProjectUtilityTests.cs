// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class ProjectUtilityTests
    {
        [Fact]
        public async Task GetSortedProjectIdsAsync_WhenProjectsIsNull_Throws()
        {
            ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => ProjectUtility.GetSortedProjectIdsAsync(projects: null, CancellationToken.None).AsTask());

            Assert.Equal("projects", exception.ParamName);
        }

        [Fact]
        public async Task GetSortedProjectIdsAsync_WhenCancellationTokenIsCancelled_Throws()
        {
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => ProjectUtility.GetSortedProjectIdsAsync(
                    Enumerable.Empty<IProjectContextInfo>(),
                    new CancellationToken(canceled: true)).AsTask());
        }

        [Fact]
        public async Task GetSortedProjectIdsAsync_WhenProjectsUnsorted_ReturnsSortedProjectIds()
        {
            var projectA = new Mock<IProjectContextInfo>();
            var projectB = new Mock<IProjectContextInfo>();
            var projectC = new Mock<IProjectContextInfo>();
            var projectAMetadata = new Mock<IProjectMetadataContextInfo>();
            var projectBMetadata = new Mock<IProjectMetadataContextInfo>();
            var projectCMetadata = new Mock<IProjectMetadataContextInfo>();

            projectA.Setup(x => x.GetMetadataAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(projectAMetadata.Object);
            projectB.Setup(x => x.GetMetadataAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(projectBMetadata.Object);
            projectC.Setup(x => x.GetMetadataAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(projectCMetadata.Object);

            projectAMetadata.SetupGet(x => x.UniqueName)
                .Returns("a");
            projectBMetadata.SetupGet(x => x.UniqueName)
                .Returns("b");
            projectCMetadata.SetupGet(x => x.UniqueName)
                .Returns("c");

            // ProjectId values were picked to help detect if the test method incorrectly sorts on project ID.
            var expectedResults = new List<string>() { "2", "0", "1" };

            projectAMetadata.SetupGet(x => x.ProjectId)
                .Returns(expectedResults[0]);
            projectBMetadata.SetupGet(x => x.ProjectId)
                .Returns(expectedResults[1]);
            projectCMetadata.SetupGet(x => x.ProjectId)
                .Returns(expectedResults[2]);

            IEnumerable<string> actualResults = await ProjectUtility.GetSortedProjectIdsAsync(
                new[] { projectB.Object, projectA.Object, projectC.Object },
                CancellationToken.None);

            Assert.Equal(expectedResults, actualResults.ToArray());
        }
    }
}
