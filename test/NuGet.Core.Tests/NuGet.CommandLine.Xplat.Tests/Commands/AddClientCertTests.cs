// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.CommandLine;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.CommandLine.XPlat;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class AddClientCertTests
    {
        [Fact]
        public void AddClientCert_RunSameCommandInBothCommandLineInterfaces_CommandOutputEqual()
        {
            using var certData = new ClientCertificateTestInfo();

            // Arrange
            var currentCli = new CommandLineApplication();
            var testLoggerCurrent = new TestLogger();
            AddVerbParser.Register(currentCli, () => testLoggerCurrent);

            var newCli = new RootCommand();
            var testLoggerNew = new TestLogger();
            NuGet.CommandLine.XPlat.Commands.AddVerbParser.Register(newCli, () => testLoggerNew);

            certData.SetupCertificateInStorage();

            var cmd = new[] { "add", "client-cert", "--package-source", "Foo", "--store-name", "My", "--store-location", "CurrentUser", "--find-by", "Thumbprint", "--find-value", certData.Certificate.Thumbprint, "--configfile", certData.ConfigFile };

            // Act
            int statusCurrent = currentCli.Execute(cmd);
            certData.WriteConfigFile(); // reset config file
            int statusNew = newCli.Invoke(cmd);

            // Assert
            CommandTestUtils.AssertBothCommandSuccessfulExecution(statusCurrent, statusNew, testLoggerCurrent, testLoggerNew);
        }
    }
}
