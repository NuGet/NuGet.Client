// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Common.Test
{
    public class RuntimeEnvironmentHelperTests
    {
        [Fact]
        public void RuntimeEnvironmentHelper_OperatingSystemChecks_OnlyOneReturnsTrue()
        {
            var results = new[] { RuntimeEnvironmentHelper.IsWindows, RuntimeEnvironmentHelper.IsMacOSX, RuntimeEnvironmentHelper.IsLinux };

            Assert.Equal(1, results.Count(result => result));
        }
    }
}
