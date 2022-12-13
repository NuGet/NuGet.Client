// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using NuGet.PackageManagement.Telemetry;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class PackageManagerUIRefreshTelemetryTests
    {
        [Fact]
        public void PackageManagerUIRefreshEvent_Constructor_NoProjectKindIfIsSolutionLevelSetToTrue()
        {
            NuGetProjectKind kind = NuGetProjectKind.PackageReference;
            var telemetryEvent = new PackageManagerUIRefreshEvent(
                It.IsAny<Guid>(),
                isSolutionLevel: true,
                It.IsAny<RefreshOperationSource>(),
                It.IsAny<RefreshOperationStatus>(),
                tab: It.IsAny<string>(),
                isUIFiltering: It.IsAny<bool>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<double?>(),
                kind);

            Assert.Null(telemetryEvent["ProjectKind"]);
        }

        [Fact]
        public void PackageManagerUIRefreshEvent_Constructor_ProjectKindIfIsSolutionLevelSetToFalse()
        {
            NuGetProjectKind kind = NuGetProjectKind.PackageReference;
            var telemetryEvent = new PackageManagerUIRefreshEvent(
                It.IsAny<Guid>(),
                isSolutionLevel: false,
                It.IsAny<RefreshOperationSource>(),
                It.IsAny<RefreshOperationStatus>(),
                tab: It.IsAny<string>(),
                isUIFiltering: It.IsAny<bool>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<double?>(),
                kind);

            Assert.NotNull(telemetryEvent["ProjectKind"]);
        }
    }
}
