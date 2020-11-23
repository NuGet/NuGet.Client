// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Moq;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio.Telemetry;
using Xunit;

namespace NuGet.VisualStudio.Implementation.Test
{
    public class VsGlobalPackagesInitScriptExecutorTests
    {
        // known/expected errors should not be reported to telemetry, hence use MockBehavior.Strict
        private Mock<INuGetTelemetryProvider> _telemetryProvider = new Mock<INuGetTelemetryProvider>(MockBehavior.Strict);

        [Fact]
        public async Task NullPackageId()
        {
            var scriptExecutor = new Mock<IScriptExecutor>();
            var executor = new VsGlobalPackagesInitScriptExecutor(scriptExecutor.Object, _telemetryProvider.Object);
            await Assert.ThrowsAsync<ArgumentException>(async ()
                => await executor.ExecuteInitScriptAsync(packageId: null, packageVersion: "1.0.0"));
        }

        [Fact]
        public async Task NullPackageVersion()
        {
            var scriptExecutor = new Mock<IScriptExecutor>();
            var executor = new VsGlobalPackagesInitScriptExecutor(scriptExecutor.Object, _telemetryProvider.Object);
            await Assert.ThrowsAsync<ArgumentException>(async ()
                => await executor.ExecuteInitScriptAsync("A", packageVersion: null));
        }

        [Fact]
        public async Task EmptyPackageId()
        {
            var scriptExecutor = new Mock<IScriptExecutor>();
            var executor = new VsGlobalPackagesInitScriptExecutor(scriptExecutor.Object, _telemetryProvider.Object);
            await Assert.ThrowsAsync<ArgumentException>(async ()
                => await executor.ExecuteInitScriptAsync(packageId: string.Empty, packageVersion: "1.0.0"));
        }

        [Fact]
        public async Task EmptyPackageVersion()
        {
            var scriptExecutor = new Mock<IScriptExecutor>();
            var executor = new VsGlobalPackagesInitScriptExecutor(scriptExecutor.Object, _telemetryProvider.Object);
            await Assert.ThrowsAsync<ArgumentException>(async ()
                => await executor.ExecuteInitScriptAsync("A", packageVersion: string.Empty));
        }

        [Fact]
        public async Task InvalidPackageVersion()
        {
            var scriptExecutor = new Mock<IScriptExecutor>();
            var executor = new VsGlobalPackagesInitScriptExecutor(scriptExecutor.Object, _telemetryProvider.Object);
            await Assert.ThrowsAsync<ArgumentException>(async ()
                => await executor.ExecuteInitScriptAsync("A", "1.abc.0"));
        }
    }
}
