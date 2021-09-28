// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;
using static NuGet.Frameworks.FrameworkConstants;

namespace Dotnet.Integration.Test
{
    [Collection("Dotnet Integration Tests")]
    public class DotnetRestoreTests
    {
        private const string OptInPackageVerification = "DOTNET_NUGET_SIGNATURE_VERIFICATION";
        private const string OptInPackageVerificationTypo = "DOTNET_NUGET_SIGNATURE_VERIFICATIOn";

        private MsbuildIntegrationTestFixture _msbuildFixture;

        public DotnetRestoreTests(MsbuildIntegrationTestFixture fixture)
        {
            _msbuildFixture = fixture;
        }

        [PlatformFact(Platform.Windows)]
        public void DotnetRestore_SolutionRestoreVerifySolutionDirPassedToProjects()
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                _msbuildFixture.CreateDotnetNewProject(pathContext.SolutionRoot, "proj");

                var slnContents = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.27330.1
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""proj"", ""proj\proj.csproj"", ""{216FF388-8C16-4AF4-87A8-9094030692FA}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{216FF388-8C16-4AF4-87A8-9094030692FA}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{216FF388-8C16-4AF4-87A8-9094030692FA}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{216FF388-8C16-4AF4-87A8-9094030692FA}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{216FF388-8C16-4AF4-87A8-9094030692FA}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ExtensibilityGlobals) = postSolution
		SolutionGuid = {9A6704E2-6E77-4FF4-9E54-B789D88829DD}
	EndGlobalSection
EndGlobal";

                var slnPath = Path.Combine(pathContext.SolutionRoot, "proj.sln");
                File.WriteAllText(slnPath, slnContents);

                var projPath = Path.Combine(pathContext.SolutionRoot, "proj", "proj.csproj");
                var doc = XDocument.Parse(File.ReadAllText(projPath));

                doc.Root.Add(new XElement(XName.Get("Target"),
                    new XAttribute(XName.Get("Name"), "ErrorOnSolutionDir"),
                    new XAttribute(XName.Get("BeforeTargets"), "CollectPackageReferences"),
                    new XElement(XName.Get("Error"),
                        new XAttribute(XName.Get("Text"), $"|SOLUTION $(SolutionDir) $(SolutionName) $(SolutionExt) $(SolutionFileName) $(SolutionPath)|"))));

                File.Delete(projPath);
                File.WriteAllText(projPath, doc.ToString());

                var result = _msbuildFixture.RunDotnet(pathContext.SolutionRoot, "msbuild proj.sln /t:restore /p:DisableImplicitFrameworkReferences=true", ignoreExitCode: true);

                result.ExitCode.Should().Be(1, "error text should be displayed");
                result.AllOutput.Should().Contain($"|SOLUTION {PathUtility.EnsureTrailingSlash(pathContext.SolutionRoot)} proj .sln proj.sln {slnPath}|");
            }
        }

        [Fact]
        public void DotnetRestore_WithAuthorSignedPackage_Succeeds()
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                var packageFile = new FileInfo(Path.Combine(pathContext.PackageSource, "TestPackage.AuthorSigned.1.0.0.nupkg"));
                var package = SigningTestUtility.GetResourceBytes(packageFile.Name);
                File.WriteAllBytes(packageFile.FullName, package);

                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(pathContext.SolutionRoot, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                _msbuildFixture.CreateDotnetNewProject(pathContext.SolutionRoot, projectName, "classlib -f netstandard2.0");

                using (var stream = File.Open(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);

                    var attributes = new Dictionary<string, string>() { { "Version", "1.0.0" } };

                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "TestPackage.AuthorSigned",
                        string.Empty,
                        new Dictionary<string, string>(),
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                _msbuildFixture.RestoreProject(workingDirectory, projectName, args: string.Empty);
            }
        }

#if IS_SIGNING_SUPPORTED
        [PlatformFact(Platform.Windows)]
        public async Task DotnetRestore_WithUnSignedPackageAndSignatureValidationModeAsRequired_FailsAsync()
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                //Setup packages and feed
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/netcoreapp2.0/x.dll");
                packageX.AddFile("ref/netcoreapp2.0/x.dll");
                packageX.AddFile("lib/net472/x.dll");
                packageX.AddFile("ref/net472/x.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Set up solution, and project
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(pathContext.SolutionRoot, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                _msbuildFixture.CreateDotnetNewProject(pathContext.SolutionRoot, projectName, "classlib");

                using (var stream = File.Open(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);

                    var attributes = new Dictionary<string, string>() { { "Version", "1.0.0" } };

                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        packageX.Id,
                        string.Empty,
                        new Dictionary<string, string>(),
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                //set nuget.config properties
                var doc = new XDocument();
                var configuration = new XElement(XName.Get("configuration"));
                doc.Add(configuration);

                var config = new XElement(XName.Get("config"));
                configuration.Add(config);

                var signatureValidationMode = new XElement(XName.Get("add"));
                signatureValidationMode.Add(new XAttribute(XName.Get("key"), "signatureValidationMode"));
                signatureValidationMode.Add(new XAttribute(XName.Get("value"), "require"));
                config.Add(signatureValidationMode);

                File.WriteAllText(Path.Combine(workingDirectory, "NuGet.Config"), doc.ToString());

                // Act
                var result = _msbuildFixture.RunDotnet(workingDirectory, "restore", ignoreExitCode: true);

                result.AllOutput.Should().Contain($"error NU3004: Package '{packageX.Id} {packageX.Version}' from source '{pathContext.PackageSource}': signatureValidationMode is set to require, so packages are allowed only if signed by trusted signers; however, this package is unsigned.");
                result.Success.Should().BeFalse();
                result.ExitCode.Should().Be(1, because: "error text should be displayed as restore failed");
            }
        }

        [PlatformFact(Platform.Linux, Platform.Darwin)]
        public async Task DotnetRestore_WithUnSignedPackageAndSignatureValidationModeAsRequired_SucceedAsync()
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                //Setup packages and feed
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/netcoreapp2.0/x.dll");
                packageX.AddFile("ref/netcoreapp2.0/x.dll");
                packageX.AddFile("lib/net472/x.dll");
                packageX.AddFile("ref/net472/x.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Set up solution, and project
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(pathContext.SolutionRoot, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                _msbuildFixture.CreateDotnetNewProject(pathContext.SolutionRoot, projectName, "classlib");

                using (FileStream stream = File.Open(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    XDocument xml = XDocument.Load(stream);

                    var attributes = new Dictionary<string, string>() { { "Version", "1.0.0" } };

                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        packageX.Id,
                        string.Empty,
                        new Dictionary<string, string>(),
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                //set nuget.config properties
                var doc = new XDocument();
                var configuration = new XElement(XName.Get("configuration"));
                doc.Add(configuration);

                var config = new XElement(XName.Get("config"));
                configuration.Add(config);

                var signatureValidationMode = new XElement(XName.Get("add"));
                signatureValidationMode.Add(new XAttribute(XName.Get("key"), "signatureValidationMode"));
                signatureValidationMode.Add(new XAttribute(XName.Get("value"), "require"));
                config.Add(signatureValidationMode);

                File.WriteAllText(Path.Combine(workingDirectory, "NuGet.Config"), doc.ToString());

                // Act
                CommandRunnerResult result = _msbuildFixture.RunDotnet(workingDirectory, "restore", ignoreExitCode: true);

                result.AllOutput.Should().NotContain($"error NU3004");
                result.Success.Should().BeTrue();
                result.ExitCode.Should().Be(0);
            }
        }

        [Fact]
        public async Task DotnetRestore_WithUnSignedPackageAndSignatureValidationModeAsRequired_OptInEnvVar_True_Fails()
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                //Arrange
                var envVarName = OptInPackageVerification;
                var envVarValue = "TRUE";
                //Setup packages and feed
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/netcoreapp2.0/x.dll");
                packageX.AddFile("ref/netcoreapp2.0/x.dll");
                packageX.AddFile("lib/net472/x.dll");
                packageX.AddFile("ref/net472/x.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Set up solution, and project
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(pathContext.SolutionRoot, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                _msbuildFixture.CreateDotnetNewProject(pathContext.SolutionRoot, projectName, "classlib");

                using (FileStream stream = File.Open(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    XDocument xml = XDocument.Load(stream);

                    var attributes = new Dictionary<string, string>() { { "Version", "1.0.0" } };

                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        packageX.Id,
                        string.Empty,
                        new Dictionary<string, string>(),
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                //set nuget.config properties
                var doc = new XDocument();
                var configuration = new XElement(XName.Get("configuration"));
                doc.Add(configuration);

                var config = new XElement(XName.Get("config"));
                configuration.Add(config);

                var signatureValidationMode = new XElement(XName.Get("add"));
                signatureValidationMode.Add(new XAttribute(XName.Get("key"), "signatureValidationMode"));
                signatureValidationMode.Add(new XAttribute(XName.Get("value"), "require"));
                config.Add(signatureValidationMode);

                File.WriteAllText(Path.Combine(workingDirectory, "NuGet.Config"), doc.ToString());

                // Act
                CommandRunnerResult result = _msbuildFixture.RunDotnet(
                    workingDirectory, "restore",
                    ignoreExitCode: true,
                    additionalEnvVars: new Dictionary<string, string>()
                        {
                            { envVarName, envVarValue }
                        }
                    );

                result.AllOutput.Should().Contain($"error NU3004: Package '{packageX.Id} {packageX.Version}' from source '{pathContext.PackageSource}': signatureValidationMode is set to require, so packages are allowed only if signed by trusted signers; however, this package is unsigned.");
                result.Success.Should().BeFalse(because: "error text should be displayed as restore failed");
            }
        }

        [PlatformFact(Platform.Linux, Platform.Darwin)]
        public async Task DotnetRestore_WithUnSignedPackageAndSignatureValidationModeAsRequired_OptInEnvVar_NameCaseSensitive_Succeed()
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                //Arrange
                var envVarName = OptInPackageVerificationTypo;
                var envVarValue = "xyz";
                //Setup packages and feed
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/netcoreapp2.0/x.dll");
                packageX.AddFile("ref/netcoreapp2.0/x.dll");
                packageX.AddFile("lib/net472/x.dll");
                packageX.AddFile("ref/net472/x.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Set up solution, and project
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(pathContext.SolutionRoot, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                _msbuildFixture.CreateDotnetNewProject(pathContext.SolutionRoot, projectName, "classlib");

                using (FileStream stream = File.Open(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    XDocument xml = XDocument.Load(stream);

                    var attributes = new Dictionary<string, string>() { { "Version", "1.0.0" } };

                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        packageX.Id,
                        string.Empty,
                        new Dictionary<string, string>(),
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                //set nuget.config properties
                var doc = new XDocument();
                var configuration = new XElement(XName.Get("configuration"));
                doc.Add(configuration);

                var config = new XElement(XName.Get("config"));
                configuration.Add(config);

                var signatureValidationMode = new XElement(XName.Get("add"));
                signatureValidationMode.Add(new XAttribute(XName.Get("key"), "signatureValidationMode"));
                signatureValidationMode.Add(new XAttribute(XName.Get("value"), "require"));
                config.Add(signatureValidationMode);

                File.WriteAllText(Path.Combine(workingDirectory, "NuGet.Config"), doc.ToString());

                // Act
                CommandRunnerResult result = _msbuildFixture.RunDotnet(
                    workingDirectory, "restore",
                    ignoreExitCode: true,
                    additionalEnvVars: new Dictionary<string, string>()
                        {
                            { envVarName, envVarValue }
                        }
                    );

                result.AllOutput.Should().NotContain($"error NU3004");
                result.Success.Should().BeTrue();
                result.ExitCode.Should().Be(0);
            }
        }

        [PlatformFact(Platform.Linux, Platform.Darwin)]
        public async Task DotnetRestore_WithUnSignedPackageAndSignatureValidationModeAsRequired_ValueCaseSensitive_OptInEnvVar_Succeed()
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                //Arrange
                var envVarName = OptInPackageVerification;
                var envVarValue = "true";
                //Setup packages and feed
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/netcoreapp2.0/x.dll");
                packageX.AddFile("ref/netcoreapp2.0/x.dll");
                packageX.AddFile("lib/net472/x.dll");
                packageX.AddFile("ref/net472/x.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Set up solution, and project
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(pathContext.SolutionRoot, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                _msbuildFixture.CreateDotnetNewProject(pathContext.SolutionRoot, projectName, "classlib");

                using (FileStream stream = File.Open(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    XDocument xml = XDocument.Load(stream);

                    var attributes = new Dictionary<string, string>() { { "Version", "1.0.0" } };

                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        packageX.Id,
                        string.Empty,
                        new Dictionary<string, string>(),
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                //set nuget.config properties
                var doc = new XDocument();
                var configuration = new XElement(XName.Get("configuration"));
                doc.Add(configuration);

                var config = new XElement(XName.Get("config"));
                configuration.Add(config);

                var signatureValidationMode = new XElement(XName.Get("add"));
                signatureValidationMode.Add(new XAttribute(XName.Get("key"), "signatureValidationMode"));
                signatureValidationMode.Add(new XAttribute(XName.Get("value"), "require"));
                config.Add(signatureValidationMode);

                File.WriteAllText(Path.Combine(workingDirectory, "NuGet.Config"), doc.ToString());

                // Act
                CommandRunnerResult result = _msbuildFixture.RunDotnet(
                    workingDirectory, "restore",
                    ignoreExitCode: true,
                    additionalEnvVars: new Dictionary<string, string>()
                        {
                            { envVarName, envVarValue }
                        }
                    );

                result.AllOutput.Should().NotContain($"error NU3004");
                result.Success.Should().BeTrue();
                result.ExitCode.Should().Be(0);
            }
        }

        [PlatformFact(Platform.Windows)]
        public void DotnetRestore_WithAuthorSignedPackageAndSignatureValidationModeAsRequired_Succeeds()
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                var packageFile = new FileInfo(Path.Combine(pathContext.PackageSource, "TestPackage.AuthorSigned.1.0.0.nupkg"));
                var package = SigningTestUtility.GetResourceBytes(packageFile.Name);
                File.WriteAllBytes(packageFile.FullName, package);

                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(pathContext.SolutionRoot, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                _msbuildFixture.CreateDotnetNewProject(pathContext.SolutionRoot, projectName, "classlib -f netstandard2.0");

                using (var stream = File.Open(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);

                    var attributes = new Dictionary<string, string>() { { "Version", "1.0.0" } };

                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "TestPackage.AuthorSigned",
                        string.Empty,
                        new Dictionary<string, string>(),
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                var projectDir = Path.GetDirectoryName(workingDirectory);
                //Directory.CreateDirectory(projectDir);
                var configPath = Path.Combine(projectDir, "NuGet.Config");

                //set nuget.config properties
                var doc = new XDocument();
                var configuration = new XElement(XName.Get("configuration"));
                doc.Add(configuration);

                var config = new XElement(XName.Get("config"));
                configuration.Add(config);

                var trustedSigners = new XElement(XName.Get("trustedSigners"));
                configuration.Add(trustedSigners);

                var signatureValidationMode = new XElement(XName.Get("add"));
                signatureValidationMode.Add(new XAttribute(XName.Get("key"), "signatureValidationMode"));
                signatureValidationMode.Add(new XAttribute(XName.Get("value"), "require"));
                config.Add(signatureValidationMode);

                //add trusted signers
                var author = new XElement(XName.Get("author"));
                author.Add(new XAttribute(XName.Get("name"), "microsoft"));
                trustedSigners.Add(author);

                var certificate = new XElement(XName.Get("certificate"));
                certificate.Add(new XAttribute(XName.Get("fingerprint"), "3F9001EA83C560D712C24CF213C3D312CB3BFF51EE89435D3430BD06B5D0EECE"));
                certificate.Add(new XAttribute(XName.Get("hashAlgorithm"), "SHA256"));
                certificate.Add(new XAttribute(XName.Get("allowUntrustedRoot"), "false"));
                author.Add(certificate);

                var repository = new XElement(XName.Get("repository"));
                repository.Add(new XAttribute(XName.Get("name"), "nuget.org"));
                repository.Add(new XAttribute(XName.Get("serviceIndex"), "https://api.nuget.org/v3/index.json"));
                trustedSigners.Add(repository);

                var rcertificate = new XElement(XName.Get("certificate"));
                rcertificate.Add(new XAttribute(XName.Get("fingerprint"), "0E5F38F57DC1BCC806D8494F4F90FBCEDD988B46760709CBEEC6F4219AA6157D"));
                rcertificate.Add(new XAttribute(XName.Get("hashAlgorithm"), "SHA256"));
                rcertificate.Add(new XAttribute(XName.Get("allowUntrustedRoot"), "false"));
                repository.Add(rcertificate);

                var owners = new XElement(XName.Get("owners"));
                owners.Add("dotnetframework;microsoft");
                repository.Add(owners);

                File.WriteAllText(configPath, doc.ToString());

                _msbuildFixture.RestoreProject(workingDirectory, projectName, args: string.Empty);
            }
        }
#endif //IS_SIGNING_SUPPORTED

        [PlatformTheory(Platform.Windows)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DotnetRestore_OneLinePerRestore(bool useStaticGraphRestore)
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                var testDirectory = pathContext.SolutionRoot;
                var pkgX = new SimpleTestPackageContext("x", "1.0.0");
                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, pkgX);

                var projectName1 = "ClassLibrary1";
                var workingDirectory1 = Path.Combine(testDirectory, projectName1);
                var projectFile1 = Path.Combine(workingDirectory1, $"{projectName1}.csproj");
                _msbuildFixture.CreateDotnetNewProject(testDirectory, projectName1, " classlib");

                using (var stream = File.Open(projectFile1, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFrameworks", "net45");

                    var attributes = new Dictionary<string, string>() { { "Version", "1.0.0" } };

                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "x",
                        "netstandard1.3",
                        new Dictionary<string, string>(),
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                var slnContents = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.27330.1
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""ClassLibrary1"", ""ClassLibrary1\ClassLibrary1.csproj"", ""{216FF388-8C16-4AF4-87A8-9094030692FA}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{216FF388-8C16-4AF4-87A8-9094030692FA}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{216FF388-8C16-4AF4-87A8-9094030692FA}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{216FF388-8C16-4AF4-87A8-9094030692FA}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{216FF388-8C16-4AF4-87A8-9094030692FA}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ExtensibilityGlobals) = postSolution
		SolutionGuid = {9A6704E2-6E77-4FF4-9E54-B789D88829DD}
	EndGlobalSection
EndGlobal";

                var slnPath = Path.Combine(pathContext.SolutionRoot, "proj.sln");
                File.WriteAllText(slnPath, slnContents);

                // Act
                var arguments = $"restore proj.sln {$"--source \"{pathContext.PackageSource}\""}" + (useStaticGraphRestore ? " /p:RestoreUseStaticGraphEvaluation=true" : string.Empty);
                var result = _msbuildFixture.RunDotnet(pathContext.SolutionRoot, arguments, ignoreExitCode: true);

                // Assert
                Assert.True(result.ExitCode == 0);
                Assert.True(2 == result.AllOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length, result.AllOutput);

                // Act - make sure no-op does the same thing.
                result = _msbuildFixture.RunDotnet(pathContext.SolutionRoot, arguments, ignoreExitCode: true);

                // Assert
                Assert.True(result.ExitCode == 0);
                Assert.True(2 == result.AllOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length, result.AllOutput);

            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task DotnetRestore_ProjectMovedDoesNotRunRestore()
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                var tfm = "net472";
                var testDirectory = pathContext.SolutionRoot;
                var pkgX = new SimpleTestPackageContext("x", "1.0.0");
                pkgX.Files.Clear();
                pkgX.AddFile($"lib/{tfm}/x.dll", tfm);

                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, pkgX);

                var projectName = "ClassLibrary1";
                var projectDirectory = Path.Combine(testDirectory, projectName);
                var movedDirectory = Path.Combine(testDirectory, projectName + "-new");

                var projectFile1 = Path.Combine(projectDirectory, $"{projectName}.csproj");
                var movedProjectFile = Path.Combine(movedDirectory, $"{projectName}.csproj");

                _msbuildFixture.CreateDotnetNewProject(testDirectory, projectName, "classlib");

                using (var stream = File.Open(projectFile1, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFrameworks", tfm);

                    var attributes = new Dictionary<string, string>() { { "Version", "1.0.0" } };
                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "x",
                        tfm,
                        new Dictionary<string, string>(),
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }


                // Act
                var result = _msbuildFixture.RunDotnet(projectDirectory, $"build {projectFile1} {$"--source \"{pathContext.PackageSource}\""}", ignoreExitCode: true);


                // Assert
                Assert.True(result.ExitCode == 0, result.AllOutput);
                Assert.Contains("Restored ", result.AllOutput);

                Directory.Move(projectDirectory, movedDirectory);

                result = _msbuildFixture.RunDotnet(movedDirectory, $"build {movedProjectFile} --no-restore", ignoreExitCode: true);

                // Assert
                Assert.True(result.ExitCode == 0, result.AllOutput);
                Assert.DoesNotContain("Restored ", result.AllOutput);

            }
        }

        [PlatformFact(Platform.Windows)]
        public void DotnetRestore_PackageDownloadSupported_IsSet()
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                _msbuildFixture.CreateDotnetNewProject(pathContext.SolutionRoot, "proj");

                var projPath = Path.Combine(pathContext.SolutionRoot, "proj", "proj.csproj");
                var doc = XDocument.Parse(File.ReadAllText(projPath));
                var errorText = "PackageDownload is available!";

                doc.Root.Add(new XElement(XName.Get("Target"),
                    new XAttribute(XName.Get("Name"), "ErrorIfPackageDownloadIsSupported"),
                    new XAttribute(XName.Get("BeforeTargets"), "Restore"),
                    new XAttribute(XName.Get("Condition"), "'$(PackageDownloadSupported)' == 'true' "),
                    new XElement(XName.Get("Error"),
                        new XAttribute(XName.Get("Text"), errorText))));
                File.Delete(projPath);
                File.WriteAllText(projPath, doc.ToString());

                var result = _msbuildFixture.RunDotnet(pathContext.SolutionRoot, $"msbuild /t:restore {projPath}", ignoreExitCode: true);

                result.ExitCode.Should().Be(1, because: "error text should be displayed");
                result.AllOutput.Should().Contain(errorText);
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task DotnetRestore_LockedMode_NewProjectOutOfBox()
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {

                // Set up solution, and project
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);


                var projFramework = FrameworkConstants.CommonFrameworks.Net462.GetShortFolderName();

                var projectA = SimpleTestProjectContext.CreateNETCore(
                   "a",
                   pathContext.SolutionRoot,
                   NuGetFramework.Parse(projFramework));

                var runtimeidentifiers = new List<string>() { "win7-x64", "win-x86", "win" };
                projectA.Properties.Add("RuntimeIdentifiers", string.Join(";", runtimeidentifiers));
                projectA.Properties.Add("RestorePackagesWithLockFile", "true");

                //Setup packages and feed
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/netcoreapp2.0/x.dll");
                packageX.AddFile("ref/netcoreapp2.0/x.dll");
                packageX.AddFile("lib/net461/x.dll");
                packageX.AddFile("ref/net461/x.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);


                //add the packe to the project
                projectA.AddPackageToAllFrameworks(packageX);
                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);
                solution.Save();
                projectA.Save();


                // Act
                var args = $" --source \"{pathContext.PackageSource}\" ";
                var projdir = Path.GetDirectoryName(projectA.ProjectPath);
                var projfilename = Path.GetFileNameWithoutExtension(projectA.ProjectName);

                _msbuildFixture.RestoreProject(projdir, projfilename, args);
                Assert.True(File.Exists(projectA.NuGetLockFileOutputPath));

                //Now set it to locked mode
                projectA.Properties.Add("RestoreLockedMode", "true");
                projectA.Save();

                //Act
                //Run the restore and it should still properly restore.
                //Assert within RestoreProject piece
                _msbuildFixture.RestoreProject(projdir, projfilename, args);
                Assert.True(File.Exists(projectA.NuGetLockFileOutputPath));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void DotnetRestore_LockedMode_Net5WithAndWithoutPlatform()
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                // Arrange
                string projectFileContents =
@"<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <TargetFrameworks>net5.0;net5.0-windows</TargetFrameworks>
    </PropertyGroup>
</Project>";
                File.WriteAllText(Path.Combine(pathContext.SolutionRoot, "a.csproj"), projectFileContents);

                _msbuildFixture.RestoreProject(pathContext.SolutionRoot, "a", args: "--use-lock-file");
                string lockFilePath = Path.Combine(pathContext.SolutionRoot, PackagesLockFileFormat.LockFileName);
                Assert.True(File.Exists(lockFilePath));
                Directory.Delete(Path.Combine(pathContext.SolutionRoot, "obj"), recursive: true);

                // Act
                _msbuildFixture.RestoreProject(pathContext.SolutionRoot, "a", args: "--locked-mode");

                // Assert
                PackagesLockFile lockFile = PackagesLockFileFormat.Read(lockFilePath);
                Assert.Equal(2, lockFile.Targets.Count);
                Assert.Contains(lockFile.Targets, target => target.TargetFramework == FrameworkConstants.CommonFrameworks.Net50);
                NuGetFramework net5win7 = NuGetFramework.Parse("net5.0-windows7.0");
                Assert.Contains(lockFile.Targets, target => target.TargetFramework == net5win7);
            }
        }

        /// <summary>
        /// Create 3 projects, each with their own nuget.config file and source.
        /// When restoring in PackageReference the settings should be found from the project folder.
        /// </summary>
        [PlatformFact(Platform.Windows)]
        public async Task DotnetRestore_VerifyPerProjectConfigSourcesAreUsedForChildProjectsWithoutSolutionAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projects = new Dictionary<string, SimpleTestProjectContext>();
                var sources = new List<string>();
                var projFramework = FrameworkConstants.CommonFrameworks.Net462;

                foreach (var letter in new[] { "A", "B", "C" })
                {
                    // Project
                    var project = SimpleTestProjectContext.CreateNETCore(
                        $"project{letter}",
                        pathContext.SolutionRoot,
                        projFramework);

                    projects.Add(letter, project);
                    solution.Projects.Add(project);

                    // Package
                    var package = new SimpleTestPackageContext()
                    {
                        Id = $"package{letter}",
                        Version = "1.0.0"
                    };

                    // Do not flow the reference up
                    package.PrivateAssets = "all";

                    project.AddPackageToAllFrameworks(package);
                    project.Properties.Clear();

                    // Source
                    var source = Path.Combine(pathContext.WorkingDirectory, $"source{letter}");
                    await SimpleTestPackageUtility.CreatePackagesAsync(source, package);
                    sources.Add(source);

                    // Create a nuget.config for the project specific source.
                    var projectDir = Path.GetDirectoryName(project.ProjectPath);
                    Directory.CreateDirectory(projectDir);
                    var configPath = Path.Combine(projectDir, "NuGet.Config");

                    var doc = new XDocument();
                    var configuration = new XElement(XName.Get("configuration"));
                    doc.Add(configuration);

                    var config = new XElement(XName.Get("config"));
                    configuration.Add(config);

                    var packageSources = new XElement(XName.Get("packageSources"));
                    configuration.Add(packageSources);

                    var sourceEntry = new XElement(XName.Get("add"));
                    sourceEntry.Add(new XAttribute(XName.Get("key"), "projectSource"));
                    sourceEntry.Add(new XAttribute(XName.Get("value"), source));
                    packageSources.Add(sourceEntry);

                    File.WriteAllText(configPath, doc.ToString());
                }

                // Create root project
                var projectRoot = SimpleTestProjectContext.CreateNETCore(
                    "projectRoot",
                    pathContext.SolutionRoot,
                    projFramework);

                // Link the root project to all other projects
                foreach (var child in projects.Values)
                {
                    projectRoot.AddProjectToAllFrameworks(child);
                }

                projectRoot.Save();
                solution.Projects.Add(projectRoot);

                solution.Create(pathContext.SolutionRoot);

                // Act
                var result = _msbuildFixture.RunDotnet(pathContext.SolutionRoot, $"restore {projectRoot.ProjectPath}");

                result.Success.Should().BeTrue(because: result.AllOutput);

                // Assert
                projects.Should().NotBeEmpty();

                foreach (var letter in projects.Keys)
                {
                    projects[letter].AssetsFile.Should().NotBeNull(because: result.AllOutput);
                    projects[letter].AssetsFile.Libraries.Select(e => e.Name).Should().Contain($"package{letter}", because: result.AllOutput);
                }
            }
        }

        /// <summary>
        /// Create 3 projects, each with their own nuget.config file and source.
        /// When restoring in PackageReference the settings should be found from the project folder.
        /// </summary>
        [PlatformFact(Platform.Windows)]
        public async Task DotnetRestore_VerifyPerProjectConfigSourcesAreUsedForChildProjectsWithSolutionAsync()
        {
            // Arrange
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                var projects = new Dictionary<string, SimpleTestProjectContext>();
                var sources = new List<string>();
                var projFramework = FrameworkConstants.CommonFrameworks.Net462;

                foreach (var letter in new[] { "A", "B", "C" })
                {
                    // Project
                    var project = SimpleTestProjectContext.CreateNETCore(
                        $"project{letter}",
                        pathContext.SolutionRoot,
                        projFramework);

                    projects.Add(letter, project);

                    // Package
                    var package = new SimpleTestPackageContext()
                    {
                        Id = $"package{letter}",
                        Version = "1.0.0"
                    };

                    // Do not flow the reference up
                    package.PrivateAssets = "all";

                    project.AddPackageToAllFrameworks(package);
                    project.Properties.Clear();

                    // Source
                    var source = Path.Combine(pathContext.WorkingDirectory, $"source{letter}");
                    await SimpleTestPackageUtility.CreatePackagesAsync(source, package);
                    sources.Add(source);

                    // Create a nuget.config for the project specific source.
                    var projectDir = Path.GetDirectoryName(project.ProjectPath);
                    Directory.CreateDirectory(projectDir);
                    var configPath = Path.Combine(projectDir, "NuGet.Config");

                    var doc = new XDocument();
                    var configuration = new XElement(XName.Get("configuration"));
                    doc.Add(configuration);

                    var config = new XElement(XName.Get("config"));
                    configuration.Add(config);

                    var packageSources = new XElement(XName.Get("packageSources"));
                    configuration.Add(packageSources);

                    var sourceEntry = new XElement(XName.Get("add"));
                    sourceEntry.Add(new XAttribute(XName.Get("key"), "projectSource"));
                    sourceEntry.Add(new XAttribute(XName.Get("value"), source));
                    packageSources.Add(sourceEntry);

                    File.WriteAllText(configPath, doc.ToString());
                }

                // Create root project
                var projectRoot = SimpleTestProjectContext.CreateNETCore(
                    "projectRoot",
                    pathContext.SolutionRoot,
                    projFramework);

                // Link the root project to all other projects
                // Save them.
                foreach (var child in projects.Values)
                {
                    projectRoot.AddProjectToAllFrameworks(child);
                    child.Save();
                }
                projectRoot.Save();
                var solutionPath = Path.Combine(pathContext.SolutionRoot, "solution.sln");
                _msbuildFixture.RunDotnet(pathContext.SolutionRoot, $"new sln {solutionPath}");

                foreach (var child in projects.Values)
                {
                    _msbuildFixture.RunDotnet(pathContext.SolutionRoot, $"sln {solutionPath} add {child.ProjectPath}");
                }
                _msbuildFixture.RunDotnet(pathContext.SolutionRoot, $"sln {solutionPath} add {projectRoot.ProjectPath}");

                // Act
                var result = _msbuildFixture.RunDotnet(pathContext.SolutionRoot, $"restore {solutionPath}");

                result.Success.Should().BeTrue(because: result.AllOutput);

                // Assert
                projects.Count.Should().BeGreaterThan(0);

                foreach (var letter in projects.Keys)
                {
                    projects[letter].AssetsFile.Should().NotBeNull(because: result.AllOutput);
                    projects[letter].AssetsFile.Libraries.Select(e => e.Name).Should().Contain($"package{letter}", because: result.AllOutput);
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task DotnetRestore_PackageReferenceWithAliases_ReflectedInTheAssetsFile()
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                // Set up solution, and project
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projFramework = FrameworkConstants.CommonFrameworks.Net462;
                var projectA = SimpleTestProjectContext.CreateNETCore(
                   "a",
                   pathContext.SolutionRoot,
                   projFramework);

                //Setup packages and feed
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile($"lib/{projFramework.GetShortFolderName()}/x.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);
                packageX.Aliases = "Core";

                //add the packe to the project
                projectA.AddPackageToAllFrameworks(packageX);
                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var args = $" --source \"{pathContext.PackageSource}\" ";
                var projdir = Path.GetDirectoryName(projectA.ProjectPath);
                var projfilename = Path.GetFileNameWithoutExtension(projectA.ProjectName);

                _msbuildFixture.RestoreProject(projdir, projfilename, args);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath));

                var library = projectA.AssetsFile.Targets.First(e => e.RuntimeIdentifier == null).Libraries.First();
                library.Should().NotBeNull("The assets file is expect to have a single library");
                library.CompileTimeAssemblies.Count.Should().Be(1, because: "The package has 1 compatible file");
                library.CompileTimeAssemblies.Single().Properties.Should().Contain(new KeyValuePair<string, string>(LockFileItem.AliasesProperty, "Core"));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void RestoreCommand_ProjectUsingCPVM_DisplaysCPVMInPreviewMessage()
        {
            using (var testDirectory = _msbuildFixture.CreateTestDirectory())
            {
                // Arrange
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                _msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib", 60000);

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(
                        xml,
                        "ManagePackageVersionsCentrally",
                        "true");

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                // The test depends on the presence of these packages and their versions.
                // Change to Directory.Packages.props when new cli that supports NuGet.props will be downloaded
                var directoryPackagesPropsName = Path.Combine(workingDirectory, $"Directory.Build.props");
                var directoryPackagesPropsContent = @"<Project>
                        <PropertyGroup>
                            <CentralPackageVersionsFileImported>true</CentralPackageVersionsFileImported>
                        </PropertyGroup>
                    </Project>";
                File.WriteAllText(directoryPackagesPropsName, directoryPackagesPropsContent);

                // Act
                var result = _msbuildFixture.RunDotnet(workingDirectory, "restore");

                // Assert
                Assert.True(result.Output.Contains($"The project {projectFile} is using CentralPackageVersionManagement, a NuGet preview feature."));
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task DotnetRestore_MultiTargettingWithAliases_Succeeds()
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                var testDirectory = pathContext.SolutionRoot;
                var pkgX = new SimpleTestPackageContext("x", "1.0.0");
                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, pkgX);

                var latestNetFrameworkAlias = "latestnetframework";
                var notLatestNetFrameworkAlias = "notlatestnetframework";
                var projectName1 = "ClassLibrary1";
                var workingDirectory1 = Path.Combine(testDirectory, projectName1);
                var projectFile1 = Path.Combine(workingDirectory1, $"{projectName1}.csproj");
                _msbuildFixture.CreateDotnetNewProject(testDirectory, projectName1, " classlib");

                using (var stream = File.Open(projectFile1, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFrameworks", $"{latestNetFrameworkAlias};{notLatestNetFrameworkAlias}");

                    var attributes = new Dictionary<string, string>() { { "Version", "1.0.0" } };

                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "x",
                        notLatestNetFrameworkAlias,
                        new Dictionary<string, string>(),
                        attributes);

                    var latestNetFrameworkProps = new Dictionary<string, string>();
                    latestNetFrameworkProps.Add("TargetFrameworkIdentifier", ".NETFramework");
                    latestNetFrameworkProps.Add("TargetFrameworkVersion", "v4.7.2");

                    ProjectFileUtils.AddProperties(xml, latestNetFrameworkProps, $" '$(TargetFramework)' == '{latestNetFrameworkAlias}' ");

                    var notLatestNetFrameworkProps = new Dictionary<string, string>();
                    notLatestNetFrameworkProps.Add("TargetFrameworkIdentifier", ".NETFramework");
                    notLatestNetFrameworkProps.Add("TargetFrameworkVersion", "v4.6.3");
                    ProjectFileUtils.AddProperties(xml, notLatestNetFrameworkProps, $" '$(TargetFramework)' == '{notLatestNetFrameworkAlias}' ");

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                // Act
                var result = _msbuildFixture.RunDotnet(pathContext.SolutionRoot, $"restore {projectFile1} {$"--source \"{pathContext.PackageSource}\" /p:AutomaticallyUseReferenceAssemblyPackages=false"}", ignoreExitCode: true);

                // Assert
                result.ExitCode.Should().Be(0, because: result.AllOutput);
                var assetsFilePath = Path.Combine(workingDirectory1, "obj", "project.assets.json");
                File.Exists(assetsFilePath).Should().BeTrue(because: "The assets file needs to exist");
                var assetsFile = new LockFileFormat().Read(assetsFilePath);
                LockFileTarget nonLatestTarget = assetsFile.Targets.Single(e => e.TargetFramework.Equals(CommonFrameworks.Net463) && string.IsNullOrEmpty(e.RuntimeIdentifier));
                nonLatestTarget.Libraries.Should().ContainSingle(e => e.Name.Equals("x"));
                LockFileTarget latestTarget = assetsFile.Targets.Single(e => e.TargetFramework.Equals(NuGetFramework.Parse("net472")) && string.IsNullOrEmpty(e.RuntimeIdentifier));
                latestTarget.Libraries.Should().NotContain(e => e.Name.Equals("x"));
            }
        }

#if NET5_0
        [Fact]
        public async Task DotnetRestore_WithTargetFrameworksProperty_StaticGraphAndRegularRestore_AreEquivalent()
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                var testDirectory = pathContext.SolutionRoot;
                var pkgX = new SimpleTestPackageContext("x", "1.0.0");
                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, pkgX);

                var projectName1 = "ClassLibrary1";
                var workingDirectory1 = Path.Combine(testDirectory, projectName1);
                var projectFile1 = Path.Combine(workingDirectory1, $"{projectName1}.csproj");
                _msbuildFixture.CreateDotnetNewProject(testDirectory, projectName1, " classlib");

                using (var stream = File.Open(projectFile1, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFrameworks", "net472");

                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "x",
                        framework: "net472",
                        new Dictionary<string, string>(),
                        new Dictionary<string, string>() { { "Version", "1.0.0" } });

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                // Preconditions
                var command = $"restore {projectFile1} {$"--source \"{pathContext.PackageSource}\" /p:AutomaticallyUseReferenceAssemblyPackages=false"}";
                var result = _msbuildFixture.RunDotnet(pathContext.SolutionRoot, command, ignoreExitCode: true);

                result.ExitCode.Should().Be(0, because: result.AllOutput);
                var assetsFilePath = Path.Combine(workingDirectory1, "obj", "project.assets.json");
                File.Exists(assetsFilePath).Should().BeTrue(because: "The assets file needs to exist");
                var assetsFile = new LockFileFormat().Read(assetsFilePath);
                LockFileTarget target = assetsFile.Targets.Single(e => e.TargetFramework.Equals(NuGetFramework.Parse("net472")) && string.IsNullOrEmpty(e.RuntimeIdentifier));
                target.Libraries.Should().ContainSingle(e => e.Name.Equals("x"));

                // Act static graph restore
                result = _msbuildFixture.RunDotnet(pathContext.SolutionRoot, command + " /p:RestoreUseStaticGraphEvaluation=true", ignoreExitCode: true);

                // Ensure static graph restore no-ops
                result.ExitCode.Should().Be(0, because: result.AllOutput);
                result.AllOutput.Should().Contain("All projects are up-to-date for restore.");
            }
        }

        [Fact]
        public void GenerateRestoreGraphFile_StandardAndStaticGraphRestore_AreEquivalent()
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                var testDirectory = pathContext.SolutionRoot;
                var projectName1 = "ClassLibrary";
                var projectName2 = "ConsoleApp";
                var projectName3 = "WebApplication";

                _msbuildFixture.CreateDotnetNewProject(testDirectory, projectName1, " classlib");
                _msbuildFixture.CreateDotnetNewProject(testDirectory, projectName2, " console");
                _msbuildFixture.CreateDotnetNewProject(testDirectory, projectName3, " webapp");
                _msbuildFixture.RunDotnet(testDirectory, "new sln --name test");
                _msbuildFixture.RunDotnet(testDirectory, $"sln add {projectName1}");
                _msbuildFixture.RunDotnet(testDirectory, $"sln add {projectName2}");
                _msbuildFixture.RunDotnet(testDirectory, $"sln add {projectName3}");
                var targetPath = Path.Combine(testDirectory, "test.sln");
                var standardDgSpecFile = Path.Combine(pathContext.WorkingDirectory, "standard.dgspec.json");
                var staticGraphDgSpecFile = Path.Combine(pathContext.WorkingDirectory, "staticGraph.dgspec.json");
                _msbuildFixture.RunDotnet(testDirectory, $"msbuild /t:GenerateRestoreGraphFile /p:RestoreGraphOutputPath=\"{standardDgSpecFile}\" {targetPath}");
                _msbuildFixture.RunDotnet(testDirectory, $"msbuild /t:GenerateRestoreGraphFile /p:RestoreGraphOutputPath=\"{staticGraphDgSpecFile}\" /p:RestoreUseStaticGraphEvaluation=true {targetPath}");

                var regularDgSpec = File.ReadAllText(standardDgSpecFile);
                var staticGraphDgSpec = File.ReadAllText(staticGraphDgSpecFile);

                regularDgSpec.Should().BeEquivalentTo(staticGraphDgSpec);
            }
        }
#endif

        [Theory]
        [InlineData("netcoreapp3.0;net5.0;net472", true)]
        [InlineData("netcoreapp2.1;netcoreapp3.0;netcoreapp3.1", true)]
        [InlineData("netcoreapp3.0;net5.0;net472", false)]
        [InlineData("netcoreapp2.1;netcoreapp3.0;netcoreapp3.1", false)]
        public async Task DotnetRestore_MultiTargettingProject_WithDifferentPackageReferences_ForceDoesNotRewriteAssetsFile(string targetFrameworks, bool useStaticGraphRestore)
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                var projectName = "ClassLibrary1";
                var testDirectory = pathContext.SolutionRoot;
                var originalFrameworks = MSBuildStringUtility.Split(targetFrameworks);

                var packages = new SimpleTestPackageContext[originalFrameworks.Length];
                for (int i = 0; i < originalFrameworks.Length; i++)
                {
                    packages[i] = CreateNetstandardCompatiblePackage("x", $"{i + 1}.0.0");
                }

                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, packages);

                var projectDirectory = Path.Combine(testDirectory, projectName);
                var projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");

                _msbuildFixture.CreateDotnetNewProject(testDirectory, projectName, "classlib");

                using (var stream = File.Open(projectFilePath, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFrameworks", targetFrameworks);
                    ProjectFileUtils.AddProperty(xml, "AutomaticallyUseReferenceAssemblyPackages", "false");
                    ProjectFileUtils.AddProperty(xml, "DisableImplicitFrameworkReferences", "true");
                    for (int i = 0; i < originalFrameworks.Length; i++)
                    {
                        var attributes = new Dictionary<string, string>() { { "Version", packages[i].Version } };
                        ProjectFileUtils.AddItem(
                            xml,
                            "PackageReference",
                            packages[i].Id,
                            originalFrameworks[i],
                            new Dictionary<string, string>(),
                            attributes);
                    }

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                // Preconditions
                var additionalArgs = useStaticGraphRestore ? "/p:RestoreUseStaticGraphEvaluation=true" : string.Empty;
                var result = _msbuildFixture.RunDotnet(projectDirectory, $"restore {projectFilePath} {additionalArgs}", ignoreExitCode: true);
                result.Success.Should().BeTrue(because: result.AllOutput);
                var assetsFilePath = Path.Combine(projectDirectory, "obj", "project.assets.json");
                DateTime originalAssetsFileWriteTime = new FileInfo(assetsFilePath).LastWriteTimeUtc;

                //Act
                result = _msbuildFixture.RunDotnet(projectDirectory, $"restore {projectFilePath} --force {additionalArgs}", ignoreExitCode: true);
                result.Success.Should().BeTrue(because: result.AllOutput);
                DateTime forceAssetsFileWriteTime = new FileInfo(assetsFilePath).LastWriteTimeUtc;

                forceAssetsFileWriteTime.Should().Be(originalAssetsFileWriteTime);
            }
        }

        [Theory]
        [InlineData("net5.0;netcoreapp3.0;net472", true)]
        [InlineData("netcoreapp3.0;netcoreapp2.1;netcoreapp3.1", true)]
        [InlineData("net5.0;netcoreapp3.0;net472", false)]
        [InlineData("netcoreapp3.0;netcoreapp2.1;netcoreapp3.1", false)]
        public async Task DotnetRestore_MultiTargettingProject_WithDifferentProjectReferences_ForceDoesNotRewriteAssetsFile(string targetFrameworks, bool useStaticGraphRestore)
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                var projectName = "ClassLibrary1";
                var testDirectory = pathContext.SolutionRoot;
                var originalFrameworks = MSBuildStringUtility.Split(targetFrameworks);

                var projectDirectory = Path.Combine(testDirectory, projectName);
                var projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");

                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, CreateNetstandardCompatiblePackage("x", "1.0.0"));

                _msbuildFixture.CreateDotnetNewProject(testDirectory, projectName, "classlib");

                var projectPaths = new List<string>(originalFrameworks.Length);
                foreach (var originalFramework in originalFrameworks)
                {
                    var p2pProjectName = $"project-{originalFramework}";
                    _msbuildFixture.CreateDotnetNewProject(testDirectory, p2pProjectName, "classlib");
                    var p2pProjectFilePath = Path.Combine(testDirectory, p2pProjectName, $"{p2pProjectName}.csproj");
                    projectPaths.Add(p2pProjectFilePath);

                    using (var stream = File.Open(p2pProjectFilePath, FileMode.Open, FileAccess.ReadWrite))
                    {
                        var xml = XDocument.Load(stream);
                        ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", originalFramework);
                        ProjectFileUtils.AddProperty(xml, "AutomaticallyUseReferenceAssemblyPackages", "false");
                        ProjectFileUtils.AddProperty(xml, "DisableImplicitFrameworkReferences", "true");
                        ProjectFileUtils.WriteXmlToFile(xml, stream);
                    }
                }

                using (var stream = File.Open(projectFilePath, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFrameworks", targetFrameworks);
                    ProjectFileUtils.AddProperty(xml, "AutomaticallyUseReferenceAssemblyPackages", "false");
                    ProjectFileUtils.AddProperty(xml, "DisableImplicitFrameworkReferences", "true");
                    for (int i = 0; i < originalFrameworks.Length; i++)
                    {
                        var attributes = new Dictionary<string, string>() { { "Version", "1.0.0" } };
                        ProjectFileUtils.AddItem(
                            xml,
                            "PackageReference",
                            "x",
                            originalFrameworks[i],
                            new Dictionary<string, string>(),
                            attributes);

                        ProjectFileUtils.AddItem(
                            xml,
                            "ProjectReference",
                            projectPaths[i],
                            originalFrameworks[i],
                            new Dictionary<string, string>(),
                            new Dictionary<string, string>());
                    }

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                // Preconditions
                var additionalArgs = useStaticGraphRestore ? "/p:RestoreUseStaticGraphEvaluation=true" : string.Empty;
                var result = _msbuildFixture.RunDotnet(projectDirectory, $"restore {projectFilePath} {additionalArgs}", ignoreExitCode: true);
                result.Success.Should().BeTrue(because: result.AllOutput);
                var assetsFilePath = Path.Combine(projectDirectory, "obj", "project.assets.json");
                DateTime originalAssetsFileWriteTime = new FileInfo(assetsFilePath).LastWriteTimeUtc;

                //Act
                result = _msbuildFixture.RunDotnet(projectDirectory, $"restore {projectFilePath} --force {additionalArgs}", ignoreExitCode: true);
                result.Success.Should().BeTrue(because: result.AllOutput);
                DateTime forceAssetsFileWriteTime = new FileInfo(assetsFilePath).LastWriteTimeUtc;

                forceAssetsFileWriteTime.Should().Be(originalAssetsFileWriteTime);
            }
        }

        [Fact]
        public void CollectFrameworkReferences_WithTransitiveFrameworkReferences_ExcludesTransitiveFrameworkReferences()
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                // Arrange
                // Library project with Framework Reference
                var libraryName = "Library";
                var libraryProjectFilePath = Path.Combine(Path.Combine(pathContext.SolutionRoot, libraryName), $"{libraryName}.csproj");
                _msbuildFixture.CreateDotnetNewProject(pathContext.SolutionRoot, libraryName, "classlib");

                using (var stream = File.Open(libraryProjectFilePath, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddItem(
                        xml,
                        name: "FrameworkReference",
                        identity: "Microsoft.AspNetCore.App",
                        framework: (string)null,
                        attributes: new Dictionary<string, string>(),
                        properties: new Dictionary<string, string>()
                        );
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                var packResult = _msbuildFixture.RunDotnet(pathContext.SolutionRoot, $"pack {libraryProjectFilePath} /p:PackageOutputPath=\"{pathContext.PackageSource}\"", ignoreExitCode: true);
                packResult.Success.Should().BeTrue(because: packResult.AllOutput);

                // Consumer project.
                var consumerName = "Consumer";
                _msbuildFixture.CreateDotnetNewProject(pathContext.SolutionRoot, consumerName, "console");

                var consumerProjectFilePath = Path.Combine(Path.Combine(pathContext.SolutionRoot, consumerName), $"{consumerName}.csproj");

                using (var stream = File.Open(consumerProjectFilePath, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddItem(
                        xml,
                        name: "PackageReference",
                        identity: "Library",
                        framework: (string)null,
                        attributes: new Dictionary<string, string>() { { "Version", "1.0.0" } },
                        properties: new Dictionary<string, string>()
                        );
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                File.WriteAllText(Path.Combine(pathContext.SolutionRoot, "Directory.Build.targets"),
@"<Project>
    <Target Name=""PrintFrameworkReferences"" AfterTargets=""Build"" DependsOnTargets=""CollectFrameworkReferences"">
        <Message Text=""Framework References: '@(_FrameworkReferenceForRestore)'"" Importance=""High"" />
    </Target>
</Project>");

                // Act
                var buildResult = _msbuildFixture.RunDotnet(pathContext.SolutionRoot, $"build {consumerProjectFilePath}", ignoreExitCode: true);

                // Assert
                buildResult.Success.Should().BeTrue(because: buildResult.AllOutput);
                buildResult.AllOutput.Should().Contain("Microsoft.NETCore.App");
                buildResult.AllOutput.Should().NotContain("Microsoft.AspNetCore.App");
            }
        }

        [PlatformTheory(Platform.Linux)]
        [InlineData("worker")]
        [InlineData("mstest")]
        [InlineData("nunit")]
        [InlineData("xunit")]
        [InlineData("blazorserver")]
        [InlineData("blazorwasm")]
        [InlineData("web")]
        [InlineData("mvc")]
        [InlineData("webapp")]
        [InlineData("angular")]
        [InlineData("react")]
        [InlineData("reactredux")]
        [InlineData("webapi")]
        [InlineData("grpc")]
        public void Dotnet_New_Template_Restore_Success(string template)
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var solutionDirectory = pathContext.SolutionRoot;

                // Act
                CommandRunnerResult newResult = _msbuildFixture.RunDotnet(solutionDirectory, "new " + template);

                // Assert
                // Make sure restore action was success.
                newResult.Success.Should().BeTrue(because: newResult.AllOutput);
                Assert.True(File.Exists(Path.Combine(solutionDirectory, "obj", "project.assets.json")));
                // Pack doesn't work because `IsPackable` is set to false.
            }
        }


        [PlatformFact(Platform.Windows)]
        public async Task DotnetRestore_SameNameSameKeyProjectPackageReferencing_Succeeds()
        {
            using (var pathContext = _msbuildFixture.CreateSimpleTestPathContext())
            {
                // Set up solution, and project
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projFramework = FrameworkConstants.CommonFrameworks.Net462;
                var projectPackageName = "projectA";
                var projectA = SimpleTestProjectContext.CreateNETCore(
                   projectPackageName,
                   pathContext.SolutionRoot,
                   projFramework);
                var projectIntermed = SimpleTestProjectContext.CreateNETCore(
                   "projectIntermed",
                   pathContext.SolutionRoot,
                   projFramework);
                var projectMain = SimpleTestProjectContext.CreateNETCore(
                   "projectMain",
                   pathContext.SolutionRoot,
                   projFramework);

                //Setup packages and feed
                var packageA = new SimpleTestPackageContext()
                {
                    Id = projectPackageName,
                    Version = "1.0.0"
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageA);

                //add the packe to the project
                projectIntermed.AddPackageToAllFrameworks(packageA);
                projectMain.AddProjectToAllFrameworks(projectA);
                projectMain.AddProjectToAllFrameworks(projectIntermed);
                solution.Projects.Add(projectA);
                solution.Projects.Add(projectIntermed);
                solution.Projects.Add(projectMain);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var args = $" --source \"{pathContext.PackageSource}\" ";
                var reader = new LockFileFormat();

                var projdir = Path.GetDirectoryName(projectA.ProjectPath);
                var projfilename = Path.GetFileNameWithoutExtension(projectA.ProjectName);
                _msbuildFixture.RestoreProject(projdir, projfilename, args);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath));

                projdir = Path.GetDirectoryName(projectIntermed.ProjectPath);
                projfilename = Path.GetFileNameWithoutExtension(projectIntermed.ProjectName);
                _msbuildFixture.RestoreProject(projdir, projfilename, args);
                Assert.True(File.Exists(projectIntermed.AssetsFileOutputPath));
                var lockFile = reader.Read(projectIntermed.AssetsFileOutputPath);
                IList<LockFileTargetLibrary> libraries = lockFile.Targets[0].Libraries;
                Assert.True(libraries.Any(l => l.Type == "package" && l.Name == projectA.ProjectName));

                projdir = Path.GetDirectoryName(projectMain.ProjectPath);
                projfilename = Path.GetFileNameWithoutExtension(projectMain.ProjectName);
                _msbuildFixture.RestoreProject(projdir, projfilename, args);
                Assert.True(File.Exists(projectMain.AssetsFileOutputPath));
                lockFile = reader.Read(projectMain.AssetsFileOutputPath);
                var errors = lockFile.LogMessages.Where(m => m.Level == LogLevel.Error);
                var warnings = lockFile.LogMessages.Where(m => m.Level == LogLevel.Warning);
                Assert.Equal(0, errors.Count());
                Assert.Equal(0, warnings.Count());
                libraries = lockFile.Targets[0].Libraries;
                Assert.Equal(2, libraries.Count);
                Assert.True(libraries.Any(l => l.Type == "project" && l.Name == projectA.ProjectName));
                Assert.True(libraries.Any(l => l.Type == "project" && l.Name == projectIntermed.ProjectName));
            }
        }

        [Fact]
        public async Task WhenPackageSourceMappingConfiguredInstallsPackageReferencesAndDownloadsFromExpectedSources_Success()
        {
            using var pathContext = _msbuildFixture.CreateSimpleTestPathContext();

            // Set up solution, and project
            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
            var projFramework = FrameworkConstants.CommonFrameworks.Net50;
            var projectPackageName = "projectA";
            var projectA = SimpleTestProjectContext.CreateNETCore(
               projectPackageName,
               pathContext.SolutionRoot,
               projFramework);

            const string version = "1.0.0";
            const string packageX = "X", packageY = "Y", packageZ = "Z", packageK = "K";

            var packageX100 = new SimpleTestPackageContext(packageX, version);
            var packageY100 = new SimpleTestPackageContext(packageY, version);
            var packageZ100 = new SimpleTestPackageContext(packageZ, version);
            var packageK100 = new SimpleTestPackageContext(packageK, version);

            packageX100.Dependencies.Add(packageZ100);

            projectA.AddPackageToAllFrameworks(packageX100);
            projectA.AddPackageToAllFrameworks(packageY100);
            projectA.AddPackageDownloadToAllFrameworks(packageK100);

            solution.Projects.Add(projectA);
            solution.Create(pathContext.SolutionRoot);

            var packageSource2 = new DirectoryInfo(Path.Combine(pathContext.WorkingDirectory, "source2"));
            packageSource2.Create();

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageY100,
                    packageZ100,
                    packageK100);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource2.FullName,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageY100,
                    packageZ100,
                    packageK100);

            var configFile = @$"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""source2"" value=""{packageSource2.FullName}"" />
    </packageSources>
        <packageSourceMapping>
            <packageSource key=""source"">
                <package pattern=""{packageY}*"" />
                <package pattern=""{packageZ}*"" />
            </packageSource>
            <packageSource key=""source2"">
                <package pattern=""{packageX}*"" />
                <package pattern=""{packageK}*"" />
            </packageSource>
    </packageSourceMapping>
</configuration>
";
            File.WriteAllText(Path.Combine(pathContext.SolutionRoot, projectA.ProjectName, "NuGet.Config"), configFile);

            //Act
            var result = _msbuildFixture.RunDotnet(pathContext.WorkingDirectory, $"restore {projectA.ProjectPath} -v n", ignoreExitCode: true);

            result.Success.Should().BeTrue(because: result.AllOutput);
            Assert.Contains($"Installed {packageX} {version} from {packageSource2.FullName}", result.AllOutput);
            Assert.Contains($"Installed {packageZ} {version} from {pathContext.PackageSource}", result.AllOutput);
            Assert.Contains($"Installed {packageY} {version} from {pathContext.PackageSource}", result.AllOutput);
            Assert.Contains($"Installed {packageK} {version} from {packageSource2.FullName}", result.AllOutput);
        }

        [Fact]
        public async Task WhenPackageSourceMappingConfiguredAndNoMatchingSourceFound_Fails()
        {
            using var pathContext = _msbuildFixture.CreateSimpleTestPathContext();

            // Set up solution, and project
            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
            var projFramework = FrameworkConstants.CommonFrameworks.Net50;
            var projectPackageName = "projectA";
            var projectA = SimpleTestProjectContext.CreateNETCore(
               projectPackageName,
               pathContext.SolutionRoot,
               projFramework);

            const string version = "1.0.0";
            const string packageX = "X", packageY = "Y", packageZ = "Z", packageK = "K";

            var packageX100 = new SimpleTestPackageContext(packageX, version);
            var packageY100 = new SimpleTestPackageContext(packageY, version);
            var packageZ100 = new SimpleTestPackageContext(packageZ, version);
            var packageK100 = new SimpleTestPackageContext(packageK, version);

            packageX100.Dependencies.Add(packageZ100);

            projectA.AddPackageToAllFrameworks(packageX100);
            projectA.AddPackageToAllFrameworks(packageY100);
            projectA.AddPackageDownloadToAllFrameworks(packageK100);

            solution.Projects.Add(projectA);
            solution.Create(pathContext.SolutionRoot);

            var packageSource2 = new DirectoryInfo(Path.Combine(pathContext.WorkingDirectory, "source2"));
            packageSource2.Create();

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageY100,
                    packageZ100,
                    packageK100);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource2.FullName,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageY100,
                    packageZ100,
                    packageK100);

            var configFile = @$"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""source2"" value=""{packageSource2.FullName}"" />
    </packageSources>
        <packageSourceMapping>
            <packageSource key=""source"">
                <package pattern=""{packageY}*"" />
            </packageSource>
            <packageSource key=""source2"">
                <package pattern=""{packageX}*"" />
            </packageSource>
    </packageSourceMapping>
</configuration>
";
            File.WriteAllText(Path.Combine(pathContext.SolutionRoot, projectA.ProjectName, "NuGet.Config"), configFile);

            //Act
            var result = _msbuildFixture.RunDotnet(pathContext.WorkingDirectory, $"restore {projectA.ProjectPath} -v n", ignoreExitCode: true);

            result.Success.Should().BeFalse(because: result.AllOutput);
            Assert.Contains($"NU1100: Unable to resolve '{packageZ} (>= {version})'", result.AllOutput);
            Assert.Contains($"NU1100: Unable to resolve '{packageK} (= {version})'", result.AllOutput);
            Assert.Contains($"Installed {packageX} {version} from {packageSource2.FullName}", result.AllOutput);
            Assert.Contains($"Installed {packageY} {version} from {pathContext.PackageSource}", result.AllOutput);
        }

        [Fact]
        public async Task DotnetRestore_NameSpaceFilter_WithAllSourceOptions_Succeed()
        {
            using var pathContext = _msbuildFixture.CreateSimpleTestPathContext();

            // Set up solution, and project
            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
            var projFramework = FrameworkConstants.CommonFrameworks.Net50;
            var projectPackageName = "projectA";
            var projectA = SimpleTestProjectContext.CreateNETCore(
               projectPackageName,
               pathContext.SolutionRoot,
               projFramework);

            const string version = "1.0.0";
            const string packageX = "X", packageY = "Y", packageZ = "Z", packageK = "K";

            var packageX100 = new SimpleTestPackageContext(packageX, version);
            var packageY100 = new SimpleTestPackageContext(packageY, version);
            var packageZ100 = new SimpleTestPackageContext(packageZ, version);
            var packageK100 = new SimpleTestPackageContext(packageK, version);

            packageX100.Dependencies.Add(packageZ100);

            projectA.AddPackageToAllFrameworks(packageX100);
            projectA.AddPackageToAllFrameworks(packageY100);
            projectA.AddPackageDownloadToAllFrameworks(packageK100);

            solution.Projects.Add(projectA);
            solution.Create(pathContext.SolutionRoot);

            var packageSource2 = new DirectoryInfo(Path.Combine(pathContext.WorkingDirectory, "source2"));
            packageSource2.Create();

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageY100,
                    packageZ100,
                    packageK100);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource2.FullName,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageY100,
                    packageZ100,
                    packageK100);

            var configFile = @$"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""source1"" value=""{pathContext.PackageSource}"" />
        <add key=""source2"" value=""{packageSource2.FullName}"" />
    </packageSources>
        <packageSourceMapping>
            <packageSource key=""source1"">
                <package pattern=""{packageY}*"" />
                <package pattern=""{packageZ}*"" />
            </packageSource>
            <packageSource key=""source2"">
                <package pattern=""{packageX}*"" />
                <package pattern=""{packageK}*"" />
            </packageSource>
    </packageSourceMapping>
</configuration>
";
            File.WriteAllText(Path.Combine(pathContext.SolutionRoot, projectA.ProjectName, "NuGet.Config"), configFile);

            //Act
            var result = _msbuildFixture.RunDotnet(pathContext.WorkingDirectory, $"restore {projectA.ProjectPath} --source {packageSource2.FullName};{pathContext.PackageSource} -v d", ignoreExitCode: true);

            result.Success.Should().BeTrue(because: result.AllOutput);
            Assert.Contains($"Installed {packageX} {version} from {packageSource2.FullName}", result.AllOutput);
            Assert.Contains($"Installed {packageZ} {version} from {pathContext.PackageSource}", result.AllOutput);
            Assert.Contains($"Installed {packageY} {version} from {pathContext.PackageSource}", result.AllOutput);
            Assert.Contains($"Installed {packageK} {version} from {packageSource2.FullName}", result.AllOutput);
            Assert.Contains($"Package source mapping matches found for package ID 'Y' are: 'source1'.", result.AllOutput);
            Assert.Contains($"Package source mapping matches found for package ID 'Z' are: 'source1'.", result.AllOutput);
            Assert.Contains($"Package source mapping matches found for package ID 'X' are: 'source2'.", result.AllOutput);
            Assert.Contains($"Package source mapping matches found for package ID 'K' are: 'source2'.", result.AllOutput);
        }

        [Fact]
        public async Task DotnetRestore_PackageSourceMappingFilter_WithNotEnoughSourceOptions_Fails()
        {
            using var pathContext = _msbuildFixture.CreateSimpleTestPathContext();

            // Set up solution, and project
            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
            var projFramework = FrameworkConstants.CommonFrameworks.Net50;
            var projectPackageName = "projectA";
            var projectA = SimpleTestProjectContext.CreateNETCore(
               projectPackageName,
               pathContext.SolutionRoot,
               projFramework);

            const string version = "1.0.0";
            const string packageX = "X", packageY = "Y";

            var packageX100 = new SimpleTestPackageContext(packageX, version);
            var packageY100 = new SimpleTestPackageContext(packageY, version);


            projectA.AddPackageToAllFrameworks(packageX100);
            projectA.AddPackageToAllFrameworks(packageY100);

            solution.Projects.Add(projectA);
            solution.Create(pathContext.SolutionRoot);

            var packageSource2 = new DirectoryInfo(Path.Combine(pathContext.WorkingDirectory, "source2"));
            packageSource2.Create();

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageY100);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource2.FullName,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageY100);

            var configFile = @$"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""source1"" value=""{pathContext.PackageSource}"" />
        <add key=""source2"" value=""{packageSource2.FullName}"" />
    </packageSources>
        <packageSourceMapping>
            <packageSource key=""source1"">
                <package pattern=""{packageY}*"" />
            </packageSource>
            <packageSource key=""source2"">
                <package pattern=""{packageX}*"" />
            </packageSource>
    </packageSourceMapping>
</configuration>
";
            File.WriteAllText(Path.Combine(pathContext.SolutionRoot, projectA.ProjectName, "NuGet.Config"), configFile);

            //Act
            var result = _msbuildFixture.RunDotnet(pathContext.WorkingDirectory, $"restore {projectA.ProjectPath} --source {packageSource2.FullName} -v d", ignoreExitCode: true);

            result.Success.Should().BeFalse(because: result.AllOutput);
            Assert.Contains("Package source mapping match not found for package ID 'Y'", result.AllOutput);
            Assert.Contains($"NU1100: Unable to resolve '{packageY} (>= {version})'", result.AllOutput);
            Assert.Contains($"Package source mapping matches found for package ID 'X' are: 'source2'.", result.AllOutput);
            Assert.Contains($"Installed {packageX} {version} from {packageSource2.FullName}", result.AllOutput);
        }

        private static SimpleTestPackageContext CreateNetstandardCompatiblePackage(string id, string version)
        {
            var pkgX = new SimpleTestPackageContext(id, version);
            pkgX.Files.Clear();
            pkgX.AddFile($"lib/netstandard2.0/x.dll");
            return pkgX;
        }
    }
}
