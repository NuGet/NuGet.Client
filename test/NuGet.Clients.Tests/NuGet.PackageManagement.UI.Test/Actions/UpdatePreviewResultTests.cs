// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class UpdatePreviewResultTests
    {
        [Fact]
        public void AutomationName_WhenCultureIsNeutral_ReturnsMessage()
        {
            var oldVersion = new NuGetVersion(0, 0, 1);
            var newVersion = new NuGetVersion(0, 0, 2);
            var previewResult = new UpdatePreviewResult(
                new PackageIdentity("updated.package", oldVersion),
                new PackageIdentity("updated.package", newVersion));

            Assert.Equal("updated.package version 0.0.1 to updated.package version 0.0.2",
                previewResult.AutomationName);
            Assert.Equal($"updated.package version {oldVersion.ToNormalizedString()} to updated.package version {newVersion.ToNormalizedString()}",
                previewResult.AutomationName);
        }
    }
}
