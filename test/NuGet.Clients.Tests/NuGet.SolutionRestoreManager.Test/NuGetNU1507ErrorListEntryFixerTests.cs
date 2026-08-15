// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Shell.TableControl;
using Moq;
using NuGet.SolutionRestoreManager.ErrorListFixers;
using NuGet.VisualStudio;
using Test.Utility.Threading;
using Xunit;

namespace NuGet.SolutionRestoreManager.Test
{
    [Collection(DispatcherThreadCollection.CollectionName)]
    public class NuGetNU1507ErrorListEntryFixerTests
    {
        public NuGetNU1507ErrorListEntryFixerTests(DispatcherThreadFixture fixture)
        {
            Assumes.Present(fixture);

            NuGetUIThreadHelper.SetCustomJoinableTaskFactory(fixture.JoinableTaskFactory);
        }

        [Fact]
        public void CanFix_WithNU1507_ReturnsTrue()
        {
            // Arrange
            NuGetNU1507ErrorListEntryFixer fixer = new();
            ITableEntryHandle entry = ErrorListEntryTestUtility.CreateEntry("NU1507");

            // Act
            bool result = fixer.CanFix(entry);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void TryFix_WithNonMatchingEntry_ReturnsFalse()
        {
            // Arrange
            Mock<IResolveSupplyChainSecurityService> service = new();
            NuGetNU1507ErrorListEntryFixer fixer = new()
            {
                ResolveSupplyChainSecurityService = new Lazy<IResolveSupplyChainSecurityService>(() => service.Object),
            };
            ITableEntryHandle entry = ErrorListEntryTestUtility.CreateEntry("NU1508");

            // Act
            bool result = fixer.TryFix(entry);

            // Assert
            Assert.False(result);
            service.Verify(
                s => s.LaunchResolveAsync(
                    It.IsAny<ResolveSupplyChainSecuritySource>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [Fact]
        public void TryFix_WithNU1507AndMissingService_ReturnsFalse()
        {
            // Arrange
            NuGetNU1507ErrorListEntryFixer fixer = new();
            ITableEntryHandle entry = ErrorListEntryTestUtility.CreateEntry("NU1507");

            // Act
            bool result = fixer.TryFix(entry);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void TryFix_WithNU1507AndAvailableService_LaunchesErrorListResolve()
        {
            // Arrange
            using ManualResetEventSlim serviceInvoked = new();
            Mock<IResolveSupplyChainSecurityService> service = new();
            service
                .Setup(s => s.LaunchResolveAsync(
                    ResolveSupplyChainSecuritySource.NU1507ErrorList,
                    "Resolve NU1507 by reviewing my NuGet supply chain security configuration.",
                    It.IsAny<CancellationToken>()))
                .Callback(() => serviceInvoked.Set())
                .Returns(Task.CompletedTask);
            NuGetNU1507ErrorListEntryFixer fixer = new()
            {
                ResolveSupplyChainSecurityService = new Lazy<IResolveSupplyChainSecurityService>(() => service.Object),
            };
            ITableEntryHandle entry = ErrorListEntryTestUtility.CreateEntry("NU1507");

            // Act
            bool result = fixer.TryFix(entry);

            // Assert
            Assert.True(result);
            Assert.True(serviceInvoked.Wait(TimeSpan.FromSeconds(5)));
            service.Verify(
                s => s.LaunchResolveAsync(
                    ResolveSupplyChainSecuritySource.NU1507ErrorList,
                    "Resolve NU1507 by reviewing my NuGet supply chain security configuration.",
                    It.IsAny<CancellationToken>()),
                Times.Once());
        }
    }
}
