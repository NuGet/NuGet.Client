// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.ProjectManagement;
using Xunit;

namespace NuGet.PackageManagement.Test
{
    public class MSBuildNuGetProjectSystemUtilityTests
    {
        [Fact]
        public async Task TryAddFileAsync_WithCancellationToken_SucceedsAsync()
        {
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await MSBuildNuGetProjectSystemUtility.TryAddFileAsync(
                    It.IsAny<IMSBuildProjectSystem>(),
                    path: "",
                    streamTaskFactory: async () => await Task.FromResult(It.IsAny<Stream>()),
                    new CancellationToken(canceled: true));
            });
        }

        [Fact]
        public async Task DeleteFileSafeAsync_WithCancellationToken_ThrowsAsync()
        {
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await MSBuildNuGetProjectSystemUtility.DeleteFileSafeAsync(
                    path: "",
                    streamFactory: async () => await Task.FromResult(It.IsAny<Stream>()),
                    It.IsAny<IMSBuildProjectSystem>(),
                    new CancellationToken(canceled: true));
            });
        }
    }
}
