// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class DownloadResourcePluginTests
    {
        [Fact]
        public void Constructor_ThrowsForNullPluginResource()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new DownloadResourcePlugin(pluginResource: null));

            Assert.Equal("pluginResource", exception.ParamName);
        }
    }
}