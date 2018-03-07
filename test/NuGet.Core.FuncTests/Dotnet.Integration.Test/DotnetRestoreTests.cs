// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace Dotnet.Integration.Test
{
    [Collection("Dotnet Integration Tests")]
    public class DotnetRestoreTests
    {
        private MsbuildIntegrationTestFixture _msbuildFixture;

        public DotnetRestoreTests(MsbuildIntegrationTestFixture fixture)
        {
            _msbuildFixture = fixture;
        }

        [PlatformFact(Platform.Windows)]
        public void DotnetRestore_WithAuthorSignedPackage_Succeeds()
        {
            using (var packageSourceDirectory = TestDirectory.Create())
            using (var testDirectory = TestDirectory.Create())
            {
                var packageFile = new FileInfo(Path.Combine(packageSourceDirectory.Path, "TestPackage.AuthorSigned.1.0.0.nupkg"));
                var package = GetResource(packageFile.Name);

                File.WriteAllBytes(packageFile.FullName, package);

                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                _msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = File.Open(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);

                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFrameworks", "netstandard1.3");

                    var attributes = new Dictionary<string, string>() { { "Version", "1.0.0" } };

                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "TestPackage.AuthorSigned",
                        "netstandard1.3",
                        new Dictionary<string, string>(),
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                var args = $"--source \"{packageSourceDirectory.Path}\" ";

                _msbuildFixture.RestoreProject(workingDirectory, projectName, args);
            }
        }

        private static byte[] GetResource(string name)
        {
            return ResourceTestUtility.GetResourceBytes(
                $"Dotnet.Integration.Test.compiler.resources.{name}",
                typeof(DotnetRestoreTests));
        }
    }
}