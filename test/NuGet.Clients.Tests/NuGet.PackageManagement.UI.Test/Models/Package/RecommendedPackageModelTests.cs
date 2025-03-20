// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Moq;
using NuGet.PackageManagement.UI.Models;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.Models.Package
{
    public class RecommendedPackageModelTests
    {
        [Fact]
        public void Constructor_SetRecommenderVersion_InitializeRecommenderVersion()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var vulnerabilityCapability = new Mock<IVulnerableCapable>();
            var embeddedCapability = new Mock<IEmbeddedResources>();
            var knownOwnersCapability = new Mock<IKnownOwnersCapable>();
            (string modelVersion, string vsixVersion) recommenderVersion = ("1.0.0", "1.0.0");

            // Act
            var model = new RecommendedPackageModel(
                identity,
                vulnerabilityCapability.Object,
                embeddedCapability.Object,
                recommenderVersion);

            // Assert
            Assert.Equal(recommenderVersion, model.RecommenderVersion);
        }
    }
}
