// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginDiscoveryResultTests
    {
        [Fact]
        public void Constructor_ThrowsForNullPluginFile()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new PluginDiscoveryResult(pluginFile: null, message: "a"));

            Assert.Equal("pluginFile", exception.ParamName);
        }

        [Fact]
        public void Constructor_InitializesProperties()
        {
            var pluginFile = new PluginFile(filePath: "a", state: PluginFileState.InvalidEmbeddedSignature);

            var result = new PluginDiscoveryResult(pluginFile, message: "b");

            Assert.Same(pluginFile, result.PluginFile);
            Assert.Equal("b", result.Message);
        }
    }
}