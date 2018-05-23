// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class ExtensionsTests
    {
        [Fact]
        public void TestExtensionsFromProgramDirLoaded()
        {
            var nugetexe = Util.GetNuGetExePath();
            using (var randomTestFolder = TestDirectory.Create())
            {
                var result = CommandRunner.Run(
                    nugetexe,
                    randomTestFolder,
                    "hello",
                    true);

                Assert.Equal("Hello!" + Environment.NewLine, Util.TrimMSBuildDiscoveryAutoDetectionMessage(result.Item2));
            }
        }
    }
}