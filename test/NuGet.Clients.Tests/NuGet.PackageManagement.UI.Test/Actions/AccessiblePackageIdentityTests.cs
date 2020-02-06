// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class AccessiblePackageIdentityTests
    {
        [Fact]
        public void AutomationName_WhenCultureIsNeutral_ReturnsMessage()
        {
            var version = new NuGetVersion(0, 0, 1);
            var result = new AccessiblePackageIdentity(new PackageIdentity("test.package", version));

            Assert.Equal("test.package version 0.0.1", result.AutomationName);
            Assert.Equal($"test.package version {version.ToNormalizedString()}", result.AutomationName);
        }
    }
}
