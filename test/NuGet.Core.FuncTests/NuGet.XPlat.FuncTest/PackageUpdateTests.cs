// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using FluentAssertions;
using Microsoft.Internal.NuGet.Testing.SignedPackages.ChildProcess;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.XPlat.FuncTest
{
    public class PackageUpdateTests
    {
        private static readonly string XplatDll = DotnetCliUtil.GetXplatDll();
        private static readonly string DotnetCli = TestFileSystemUtility.GetDotnetCli();
        private readonly ITestOutputHelper _testOutputHelper;

        // The .NET SDK downloads reference assembly packages for target frameworks it can't find ref assemblies for
        // locally. Ideally this should be solved, so it's not necessary to download the packages every time the test
        // runs. At a minimum, this should be changed to use package source mapping, once update package supports PSM.
        private const string NugetConfigFormat = """
            <configuration>
              <packageSources>
                <clear />
                <add key="test" value="{0}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
              <packageSourceMapping>
                <clear />
              </packageSourceMapping>
            </configuration>
            """;

        public PackageUpdateTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task SingleTfmProject_PackageVersionUpdated()
        {
            // Arrange & Act
            using var testContext = new SimpleTestPathContext();
            File.WriteAllText(testContext.NuGetConfig, string.Format(NugetConfigFormat, testContext.PackageSource));

            var a1 = new SimpleTestPackageContext("NuGet.Internal.Test.a", "1.0.0");
            var a2 = new SimpleTestPackageContext("NuGet.Internal.Test.a", "2.0.0");

            SimpleTestPackageContext[] packages = new SimpleTestPackageContext[] { a1, a2 };
            await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, packages);

            var csprojContents = """
                <Project Sdk="Microsoft.NET.Sdk">
                    <PropertyGroup>
                        <TargetFramework>net48</TargetFramework>
                    </PropertyGroup>
                    <ItemGroup>
                        <PackageReference Include="NuGet.Internal.Test.a" Version="1.0.0" />
                    </ItemGroup>
                </Project>
                """;
            var csprojPath = Path.Combine(testContext.SolutionRoot, "my.csproj");
            File.WriteAllText(csprojPath, csprojContents);

            var result = CommandRunner.Run(
                DotnetCli,
                testContext.SolutionRoot,
                $"{XplatDll} package update NuGet.Internal.Test.a",
                testOutputHelper: _testOutputHelper);

            // Assert
            result.ExitCode.Should().Be(0);

            XDocument csproj = XDocument.Load(csprojPath);
            var packageReferenceA = csproj.XPathSelectElements("//PackageReference[@Include='NuGet.Internal.Test.a']").ToList();
            packageReferenceA.Count.Should().Be(1);
            packageReferenceA[0].Attribute("Version").Value.Should().Be("2.0.0");
        }

        [Fact]
        public async Task MultiTfmProject_PackageVersionUpdated()
        {
            // Arrange & Act
            using var testContext = new SimpleTestPathContext();
            File.WriteAllText(testContext.NuGetConfig, string.Format(NugetConfigFormat, testContext.PackageSource));

            var a1 = new SimpleTestPackageContext("NuGet.Internal.Test.a", "1.0.0");
            var a2 = new SimpleTestPackageContext("NuGet.Internal.Test.a", "2.0.0");

            SimpleTestPackageContext[] packages = new SimpleTestPackageContext[] { a1, a2 };
            await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, packages);

            var csprojContents = """
                <Project Sdk="Microsoft.NET.Sdk">
                    <PropertyGroup>
                        <TargetFrameworks>net48;net481</TargetFrameworks>
                    </PropertyGroup>
                    <ItemGroup>
                        <PackageReference Include="NuGet.Internal.Test.a" Version="1.0.0" />
                    </ItemGroup>
                </Project>
                """;
            var csprojPath = Path.Combine(testContext.SolutionRoot, "my.csproj");
            File.WriteAllText(csprojPath, csprojContents);

            var result = CommandRunner.Run(
                DotnetCli,
                testContext.SolutionRoot,
                $"{XplatDll} package update NuGet.Internal.Test.a",
                testOutputHelper: _testOutputHelper);

            // Assert
            result.ExitCode.Should().Be(0);

            XDocument csproj = XDocument.Load(csprojPath);
            var packageReferenceA = csproj.XPathSelectElements("//PackageReference[@Include='NuGet.Internal.Test.a']").ToList();
            packageReferenceA.Count.Should().Be(1);
            packageReferenceA[0].Attribute("Version").Value.Should().Be("2.0.0");
        }

        [Fact]
        public async Task MultiTfmProjectWithConditionalPackageRef_PackageVersionUpdated()
        {
            // Arrange & Act
            using var testContext = new SimpleTestPathContext();
            File.WriteAllText(testContext.NuGetConfig, string.Format(NugetConfigFormat, testContext.PackageSource));

            var a1 = new SimpleTestPackageContext("NuGet.Internal.Test.a", "1.0.0");
            var a2 = new SimpleTestPackageContext("NuGet.Internal.Test.a", "2.0.0");

            SimpleTestPackageContext[] packages = new SimpleTestPackageContext[] { a1, a2 };
            await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, packages);

            var csprojContents = """
                <Project Sdk="Microsoft.NET.Sdk">
                    <PropertyGroup>
                        <TargetFrameworks>net48;net481</TargetFrameworks>
                    </PropertyGroup>
                    <ItemGroup>
                        <PackageReference Include="NuGet.Internal.Test.a" Version="1.0.0" Condition=" '$(TargetFramework)' == 'net48' " />
                    </ItemGroup>
                </Project>
                """;
            var csprojPath = Path.Combine(testContext.SolutionRoot, "my.csproj");
            File.WriteAllText(csprojPath, csprojContents);

            var result = CommandRunner.Run(
                DotnetCli,
                testContext.SolutionRoot,
                $"{XplatDll} package update NuGet.Internal.Test.a",
                testOutputHelper: _testOutputHelper);

            // Assert
            result.ExitCode.Should().Be(0);

            XDocument csproj = XDocument.Load(csprojPath);
            var packageReferenceA = csproj.XPathSelectElements("//PackageReference[@Include='NuGet.Internal.Test.a']").ToList();
            packageReferenceA.Count.Should().Be(1);
            packageReferenceA[0].Attribute("Version").Value.Should().Be("2.0.0");
            packageReferenceA[0].Attribute("Condition").Value.Should().Be(" '$(TargetFramework)' == 'net48' ");
        }

        [Fact]
        public async Task SingleTfmProject_CommitsRestore()
        {
            // Arrange & Act
            using var testContext = new SimpleTestPathContext();
            File.WriteAllText(testContext.NuGetConfig, string.Format(NugetConfigFormat, testContext.PackageSource));

            var a1 = new SimpleTestPackageContext("NuGet.Internal.Test.a", "1.0.0");
            var a2 = new SimpleTestPackageContext("NuGet.Internal.Test.a", "2.0.0");

            SimpleTestPackageContext[] packages = new SimpleTestPackageContext[] { a1, a2 };
            await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, packages);

            var csprojContents = """
                <Project Sdk="Microsoft.NET.Sdk">
                    <PropertyGroup>
                        <TargetFramework>net48</TargetFramework>
                    </PropertyGroup>
                    <ItemGroup>
                        <PackageReference Include="NuGet.Internal.Test.a" Version="1.0.0" />
                    </ItemGroup>
                </Project>
                """;
            var csprojPath = Path.Combine(testContext.SolutionRoot, "my.csproj");
            File.WriteAllText(csprojPath, csprojContents);

            var result = CommandRunner.Run(
                DotnetCli,
                testContext.SolutionRoot,
                $"{XplatDll} package update NuGet.Internal.Test.a",
                testOutputHelper: _testOutputHelper);

            // Assert
            result.ExitCode.Should().Be(0);

            string assetsFilePath = Path.Combine(testContext.SolutionRoot, "obj", "project.assets.json");
            LockFile assetsFile = new LockFileFormat().Read(assetsFilePath);
            assetsFile.Libraries[0].Name.Should().Be("NuGet.Internal.Test.a");
            assetsFile.Libraries[0].Version.Should().Be(new NuGetVersion("2.0.0"));
        }
    }
}
