// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Protocol.Core.Types;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginFindPackageByIdResourceTests
    {
        [Fact]
        public void Constructor_ThrowsForNullPluginResource()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new PluginFindPackageByIdResource(pluginResource: null));

            Assert.Equal("pluginResource", exception.ParamName);
        }
    }
}