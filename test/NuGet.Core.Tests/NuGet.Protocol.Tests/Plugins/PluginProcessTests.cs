// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginProcessTests
    {
        [Fact]
        public void Constructor_ThrowsForNullProcess()
        {
            Assert.Throws<ArgumentNullException>(() => new PluginProcess(process: null));
        }

        // Cannot test other members since System.Diagnostics.Process is not mockable.
    }
}