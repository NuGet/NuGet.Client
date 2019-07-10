// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.Protocol;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    [Collection("NuGet XPlat Test Collection")]
    public class ListPackageTests
    {
        [Fact]
        public void BasicListPackageParsing_Interactive()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var projectPath = Path.Combine(testDirectory, "project.csproj");
                File.WriteAllText(projectPath, string.Empty);

                var argList = new List<string>() {
                    "list",
                    "--interactive",
                    projectPath};

                var logger = new TestCommandOutputLogger();
                var testApp = new CommandLineApplication();
                var mockCommandRunner = new Mock<IListPackageCommandRunner>();
                mockCommandRunner
                    .Setup(m => m.ExecuteCommandAsync(It.IsAny<ListPackageArgs>()))
                    .Returns(Task.CompletedTask);

                testApp.Name = "dotnet nuget_test";
                ListPackageCommand.Register(testApp,
                    () => logger,
                    () => mockCommandRunner.Object);

                // Act
                var result = testApp.Execute(argList.ToArray());

                XPlatTestUtils.DisposeTemporaryFile(projectPath);

                // Assert
                mockCommandRunner.Verify();
                Assert.NotNull(HttpHandlerResourceV3.CredentialService);
                Assert.Equal(0, result);
            }
        }

        [Fact]
        public void BasicListPackageParsing_InteractiveTakesNoArguments()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var projectPath = Path.Combine(testDirectory, "project.csproj");
                File.WriteAllText(projectPath, string.Empty);

                var argList = new List<string>() {
                    "list",
                    "--interactive",
                    "no",
                    projectPath};

                var logger = new TestCommandOutputLogger();
                var testApp = new CommandLineApplication();
                var mockCommandRunner = new Mock<IListPackageCommandRunner>();
                mockCommandRunner
                    .Setup(m => m.ExecuteCommandAsync(It.IsAny<ListPackageArgs>()))
                    .Returns(Task.CompletedTask);

                testApp.Name = "dotnet nuget_test";
                ListPackageCommand.Register(testApp,
                    () => logger,
                    () => mockCommandRunner.Object);

                // Act
                Assert.Throws<CommandParsingException>(() => testApp.Execute(argList.ToArray()));
            }
        }
    }
}