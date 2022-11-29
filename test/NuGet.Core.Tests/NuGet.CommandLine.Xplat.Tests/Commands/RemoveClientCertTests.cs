// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.CommandLine;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.CommandLine.XPlat;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class RemoveClientCertTests
    {
        [Fact]
        public void RemoveClientCert_RunSameCommandInBothCommandLineInterfaces_CommandOutputEqual()
        {
            using var certData = new ClientCertificateTestInfo();

            // Arrange
            certData.SetupCertificateInStorage();
            var addCertCMD = new[] { "add", "client-cert", "--package-source", "Foo", "--store-name", "My", "--store-location", "CurrentUser", "--find-by", "Thumbprint", "--find-value", certData.Certificate.Thumbprint, "--configfile", certData.ConfigFile };

            var addClientCertCli = new RootCommand();
            var addClientCertLogger = new TestLogger();
            NuGet.CommandLine.XPlat.Commands.AddVerbParser.Register(addClientCertCli, () => addClientCertLogger, e => NuGet.CommandLine.XPlat.Program.LogException(e, addClientCertLogger));
            int addClientCertStatus = addClientCertCli.Invoke(addCertCMD);
            Assert.Equal(0, addClientCertStatus);
            string nuGetConfigWithFile = File.ReadAllText(certData.ConfigFile);

            var currentCli = new CommandLineApplication();
            var testLoggerCurrent = new TestLogger();
            RemoveVerbParser.Register(currentCli, () => testLoggerCurrent);

            var newCli = new RootCommand();
            var testLoggerNew = new TestLogger();
            NuGet.CommandLine.XPlat.Commands.RemoveVerbParser.Register(newCli, () => testLoggerNew, e => NuGet.CommandLine.XPlat.Program.LogException(e, testLoggerNew));

            var cmd = new[] { "remove", "client-cert", "--package-source", certData.PackageSourceName, "--configfile", certData.ConfigFile };

            // Act
            int statusCurrent = currentCli.Execute(cmd);
            File.WriteAllText(certData.ConfigFile, nuGetConfigWithFile);
            int statusNew = newCli.Invoke(cmd);

            // Assert
            CommandTestUtils.AssertBothCommandSuccessfulExecution(statusCurrent, statusNew, testLoggerCurrent, testLoggerNew);
        }
    }
}
