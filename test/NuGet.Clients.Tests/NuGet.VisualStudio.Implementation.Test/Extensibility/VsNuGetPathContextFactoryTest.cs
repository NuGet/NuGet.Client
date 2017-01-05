// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Configuration;
using Xunit;

namespace NuGet.VisualStudio.Implementation.Test.Extensibility
{
    public class VsNuGetPathContextFactoryTest
    {
        [Fact]
        public async Task CreateAsync_UsesConfiguredUserPackageFolder()
        {
            // Arrange
            var currentDirectory = Directory.GetCurrentDirectory();
            var settings = new Mock<ISettings>();
            settings
                .Setup(x => x.GetValue("config", "globalPackagesFolder", true))
                .Returns(() => "solution/packages");

            var target = new VsNuGetPathContextFactory(settings.Object);

            // Act
            var actual = await target.CreateAsync(CancellationToken.None);

            // Assert
            Assert.Equal(Path.Combine(currentDirectory, "solution", "packages"), actual.UserPackageFolder);
        }

        [Fact]
        public async Task CreateAsync_UsesConfiguredFallbackPackageFolders()
        {
            // Arrange
            var currentDirectory = Directory.GetCurrentDirectory();
            var settings = new Mock<ISettings>();
            settings
                .Setup(x => x.GetSettingValues("fallbackPackageFolders", true))
                .Returns(() => new List<SettingValue>
                {
                    new SettingValue("a", "solution/packagesA", isMachineWide: false),
                    new SettingValue("b", "solution/packagesB", isMachineWide: false)
                });

            var target = new VsNuGetPathContextFactory(settings.Object);

            // Act
            var actual = await target.CreateAsync(CancellationToken.None);

            // Assert
            Assert.Equal(2, actual.FallbackPackageFolders.Count);
            Assert.Equal(Path.Combine(currentDirectory, "solution", "packagesA"), actual.FallbackPackageFolders[0]);
            Assert.Equal(Path.Combine(currentDirectory, "solution", "packagesB"), actual.FallbackPackageFolders[1]);
        }
    }
}
