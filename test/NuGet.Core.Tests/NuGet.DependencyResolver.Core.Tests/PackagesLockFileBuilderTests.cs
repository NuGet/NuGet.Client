// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Commands;
using NuGet.Commands.Utility;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.DependencyResolver.Core.Tests
{
    public class PackagesLockFileBuilderTests
    {
        [Fact]
        public async Task ConvertUnsupportedFrameworkToAnyAsync()
        {
            // Arrange
            var packages = new List<PackageReference>()
            {
                new PackageReference(new PackageIdentity("PackageA", new NuGetVersion(1, 0, 0)), NuGetFramework.UnsupportedFramework)
            };

            var contentHashUtility = new Mock<IContentHashUtility>();

            var builder = new PackagesLockFileBuilder();

            // Act
            var result = await builder.CreateNuGetLockFileAsync(packages, contentHashUtility.Object, CancellationToken.None);

            // Assert
            Assert.Equal(1, result.Targets.Count);
            Assert.Equal(NuGetFramework.AnyFramework, result.Targets[0].TargetFramework);
        }
    }
}
