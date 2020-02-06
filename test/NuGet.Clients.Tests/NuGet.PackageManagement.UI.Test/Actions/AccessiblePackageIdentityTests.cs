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
        public void AutomationName_WhenCultureIsNeutral_IdVersionConstructor_ReturnsMessage()
        {
            var result = new AccessiblePackageIdentity("test.package", new NuGetVersion(0, 0, 1));

            Assert.Equal("test.package version 0.0.1", result.AutomationName);
        }

        [Fact]
        public void AutomationName_WhenCultureIsNeutral_PackageIdentityConstructor_ReturnsMessage()
        {
            var result = new AccessiblePackageIdentity(new PackageIdentity("test.package", new NuGetVersion(0, 0, 1)));

            Assert.Equal("test.package version 0.0.1", result.AutomationName);
        }
    }
}
