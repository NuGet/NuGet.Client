// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Win32;
using NuGet.Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.CommandLine.Test
{
    public class RegistryKeyUtilityTests
    {
        private readonly TestLogger _logger;

        public RegistryKeyUtilityTests(ITestOutputHelper output)
        {
            _logger = new TestLogger(output);
        }

        [WindowsNTFact]
        public void GetValueFromRegistryKey_UnknownKey_ReturnsNull()
        {
            var guidString = new Guid().ToString();
            var registryKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);

            var value = RegistryKeyUtility.GetValueFromRegistryKey(guidString, "key_local_machine\\test\\folder", registryKey, _logger);

            Assert.Null(value);
            Assert.Empty(_logger.ErrorMessages);
        }

        [WindowsNTFact]
        public void GetValueFromRegistryKey_GetSampleKey_ReturnsNotNull()
        {
            var value = RegistryKeyUtility.GetValueFromRegistryKey("ProductName", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", Registry.LocalMachine, _logger);

            Assert.NotNull(value);
            var productName = value as string;
            Assert.StartsWith("Windows", productName);
            Assert.Empty(_logger.ErrorMessages);
        }
    }
}
