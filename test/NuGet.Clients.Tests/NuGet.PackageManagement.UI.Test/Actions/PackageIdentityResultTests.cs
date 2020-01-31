// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Versioning;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class PackageIdentityResultTests
    {
        [Fact]
        public void AccessibleName_ReturnsMessage_CultureNeutral()
        {
            PackageIdentityResult result = new PackageIdentityResult("test.package", new NuGetVersion(0, 0, 1));

            var msg = result.AutomationName;

            Assert.Equal("test.package version 0.0.1", msg);
        }
    }
}
