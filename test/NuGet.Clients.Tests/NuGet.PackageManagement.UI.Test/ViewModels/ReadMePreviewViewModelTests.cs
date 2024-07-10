// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Common;
using NuGet.PackageManagement.UI.ViewModels;
using NuGet.Packaging;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.ViewModels
{
    public class ReadMePreviewViewModelTests
    {
        [Fact]
        public async void ReadMePreviewViewModelTests_NoPackagePathOrPackageId_NoErrorNoReadmeMarkdown()
        {
            var readMePreviewViewModel = new ReadMePreviewViewModel();

            await readMePreviewViewModel.LoadReadme("", "");

            Assert.False(readMePreviewViewModel.IsErrorWithReadMe);
            Assert.True(string.IsNullOrWhiteSpace(readMePreviewViewModel.ReadMeMarkdown));
        }

        [LocalOnlyFact]
        public async void ReadMePreviewViewModelTests_PackageWithoutReadme_NoErrorNoReadmeMarkdown()
        {
            var readMePreviewViewModel = new ReadMePreviewViewModel();
            var packageId = "testpackage";
            var packageVersion = "1.0.0";
            var packageIdVer = $"{packageId}.{packageVersion}";
            var packageNupkg = $"{packageIdVer}.nupkg";
            using var testDir = TestDirectory.Create();
            var zipPath = Path.Combine(testDir, packageNupkg);
            var extractPath = Path.Combine(testDir, "extract");

            CreateDummyPackage("testpackage", "1.0.0", zipPath, extractPath);

            var packageLocation = Path.Combine(extractPath, packageIdVer, packageNupkg);
            await readMePreviewViewModel.LoadReadme(packageLocation, packageId);

            Assert.False(readMePreviewViewModel.IsErrorWithReadMe);
            Assert.True(string.IsNullOrWhiteSpace(readMePreviewViewModel.ReadMeMarkdown));
        }

        [LocalOnlyFact]
        public async void ReadMePreviewViewModelTests_PackageWithReadme_NoErrorReadmeMarkdown()
        {
            var readMePreviewViewModel = new ReadMePreviewViewModel();
            var packageId = "testpackage";
            var packageVersion = "1.0.0";
            var packageIdVer = $"{packageId}.{packageVersion}";
            var packageNupkg = $"{packageIdVer}.nupkg";
            using var testDir = TestDirectory.Create();
            var zipPath = Path.Combine(testDir, packageNupkg);
            var extractPath = Path.Combine(testDir, "extract");

            CreateDummyPackage(packageId, packageVersion, zipPath, extractPath, "readme.md", "some readme content");

            var packageLocation = Path.Combine(extractPath, packageIdVer, packageNupkg);
            await readMePreviewViewModel.LoadReadme(packageLocation, packageId);

            Assert.False(readMePreviewViewModel.IsErrorWithReadMe);
            Assert.False(string.IsNullOrWhiteSpace(readMePreviewViewModel.ReadMeMarkdown));
            Assert.Equal("some readme content", readMePreviewViewModel.ReadMeMarkdown);
        }

        [LocalOnlyFact]
        public async void ReadMePreviewViewModelTests_InvalidPackagePath_NoErrorNoReadmeMarkdown()
        {
            var readMePreviewViewModel = new ReadMePreviewViewModel();
            await readMePreviewViewModel.LoadReadme("path/to/nowhere", "packageId");

            Assert.False(readMePreviewViewModel.IsErrorWithReadMe);
            Assert.True(string.IsNullOrWhiteSpace(readMePreviewViewModel.ReadMeMarkdown));
        }

        private static async void CreateDummyPackage(
            string packageId,
            string packageVersion,
            string nupkgPath,
            string extractedPath,
            string readmePath = "",
            string readmeContent = "")
        {
            var iconFile = "icon.png";
            var dir = Path.GetDirectoryName(nupkgPath);
            var holdDir = "pkg";
            var folderPath = Path.Combine(dir, holdDir);

            // base dir
            Directory.CreateDirectory(folderPath);

            // create nuspec
            var nuspec = NuspecBuilder.Create()
                .WithPackageId(packageId)
                .WithPackageVersion(packageVersion)
                .WithIcon(iconFile)
                .WithFile("icon.png", "");

            var iconPath = Path.Combine(folderPath, iconFile);
            var iconDir = Path.GetDirectoryName(iconPath);
            Directory.CreateDirectory(iconDir);
            File.WriteAllText(iconPath, "I am an image");

            if (!string.IsNullOrWhiteSpace(readmePath))
            {
                nuspec.WithReadme(readmePath)
                    .WithFile(readmePath);

                // create readme
                var readmePathFullPath = Path.Combine(folderPath, readmePath);
                var readmeDirectory = Path.GetDirectoryName(readmePathFullPath);
                Directory.CreateDirectory(readmeDirectory);

                if (!string.IsNullOrEmpty(readmeContent))
                {
                    File.WriteAllText(readmePathFullPath, readmeContent);
                }
            }

            // Create nuget package
            using (var nuspecStream = new MemoryStream())
            using (FileStream nupkgStream = File.Create(nupkgPath))
            using (var writer = new StreamWriter(nuspecStream))
            {
                nuspec.Write(writer);
                writer.Flush();
                nuspecStream.Position = 0;
                var pkgBuilder = new PackageBuilder(stream: nuspecStream, basePath: folderPath);
                pkgBuilder.Save(nupkgStream);
            }

            using (FileStream nupkgstream = File.OpenRead(nupkgPath))
            {
                await PackageExtractor.ExtractPackageAsync(
                    extractedPath,
                    nupkgstream,
                    new PackagePathResolver(extractedPath),
                    new PackageExtractionContext(
                        PackageSaveMode.Defaultv3,
                        XmlDocFileSaveMode.Skip,
                        null,
                        NullLogger.Instance),
                    System.Threading.CancellationToken.None);

            }
        }
    }
}
