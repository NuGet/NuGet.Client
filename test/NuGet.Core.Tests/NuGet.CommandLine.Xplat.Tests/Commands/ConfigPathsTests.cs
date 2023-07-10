// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.CommandLine;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class ConfigPathsTests
    {
        [Fact]
        public void ConfigPaths_RunSameCommandInBothCommandLineInterfaces_CommandOutputEqual()
        {
            // Arrange
            string file1 = Path.GetTempFileName();
            string file2 = Path.GetTempFileName();
            string initialConfig = $@"
<configuration>
    <packageSources>
        <add key=""Foo"" value=""https://contoso.test/v3/index.json"" />
    </packageSources>
    <config>
        <add key=""http_proxy"" value=""http://company-squid:3128@contoso.test"" />
    </config>
</configuration>
";
            File.WriteAllText(file1, initialConfig);
            File.WriteAllText(file2, initialConfig);

            var enableSourceCmd1 = new[] { "config", "paths", "C:" };
            var enableSourceCmd2 = new[] { "config", "paths", "C:" };

            var currentCli = new CommandLineApplication();
            var testLoggerCurrent = new TestLogger();
            XPlat.ConfigCommand.Register(currentCli, () => testLoggerCurrent);

            var newCli = new RootCommand();
            var testLoggerNew = new TestLogger();
            XPlat.Commands.ConfigVerbParser.Register(newCli, getLogger: () => testLoggerNew, commandExceptionHandler: e =>
            {
                XPlat.Program.LogException(e, testLoggerNew);
                return 1;
            });

            // Act
            int statusCurrent = currentCli.Execute(enableSourceCmd1);
            int statusNew = newCli.Invoke(enableSourceCmd2);

            // Assert
            CommandTestUtils.AssertEqualCommandOutput(statusCurrent, statusNew, testLoggerCurrent, testLoggerNew);
            Assert.Equal(initialConfig, File.ReadAllText(file1));
            Assert.Equal(initialConfig, File.ReadAllText(file2));
        }
    }
}
