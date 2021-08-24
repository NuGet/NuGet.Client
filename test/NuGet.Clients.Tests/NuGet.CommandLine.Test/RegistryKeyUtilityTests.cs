// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Win32;
using NuGet.Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.CommandLine.Test
{
    public class RegistryKeyUtilityTests
    {
        private readonly ITestOutputHelper _output;
        private readonly TestLogger _logger;

        public RegistryKeyUtilityTests(ITestOutputHelper helper)
        {
            _output = helper;
            _logger = new TestLogger(_output);
        }

        [WindowsNTFact]
        public void GetValueFromRegistry_NETFrameworkVersion_Succeeds()
        {
            // Constants from:
            // https://docs.microsoft.com/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed#detect-net-framework-45-and-later-versions
            var val = RegistryKeyUtility.GetValueFromRegistryKey("Release", @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\", Registry.LocalMachine, _logger);
            var netFxVersion = val as int?;

            Assert.NotNull(val);
            Assert.Empty(_logger.ErrorMessages);
            Assert.NotNull(netFxVersion);
            Assert.True(netFxVersion >= 378389); // Greater than .NET Framework 4.5
        }
    }
}
