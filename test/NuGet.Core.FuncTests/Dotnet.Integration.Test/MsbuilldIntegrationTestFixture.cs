﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NuGet.XPlat.FuncTest;
using NuGet.Test.Utility;
using NuGet.Packaging;
using NuGet.Packaging.PackageExtraction;
using Xunit;

namespace Dotnet.Integration.Test
{
    public class MsbuilldIntegrationTestFixture : IDisposable
    {
        private readonly string _dotnetCli = DotnetCliUtil.GetDotnetCli(true);
        internal readonly string TestDotnetCli;

        public MsbuilldIntegrationTestFixture()
        {
            var cliDirectory = CopyLatestCliForPack();
            TestDotnetCli = Path.Combine(cliDirectory, "dotnet.exe");
        }

        internal void CreateDotnetNewProject(string solutionRoot, string projectName, string args = "console")
        {
            var workingDirectory = Path.Combine(solutionRoot, projectName);
            if (!Directory.Exists(workingDirectory))
            {
                Directory.CreateDirectory(workingDirectory);
            }
            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"new {args}",
                waitForExit: true,
                timeOutInMilliseconds: 300000);

            // TODO : remove this workaround when https://github.com/dotnet/templating/issues/294 is fixed
            if (result.Item1 != 0)
            {
                result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"new {args} --debug:reinit",
                waitForExit: true,
                timeOutInMilliseconds: 300000);

                result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"new {args} ",
                waitForExit: true,
                timeOutInMilliseconds: 300000);
            }
            Assert.True(result.Item1 == 0, $"Creating project failed with following log information :\n {result.Item3}");
            Assert.True(result.Item3 == "", $"Creating project failed with following message in error stream :\n {result.Item3}");
        }

        internal void RestoreProject(string workingDirectory, string projectName, string args)
        {
            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"restore {projectName}.csproj {args}",
                waitForExit: true);
            Assert.True(result.Item1 == 0, $"Restore failed with following log information :\n {result.Item3}");
            Assert.True(result.Item3 == "", $"Restore failed with following message in error stream :\n {result.Item3}");
        }

        internal void PackProject(string workingDirectory, string projectName, string args)
        {
            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"pack {projectName}.csproj {args}",
                waitForExit: true);
            Assert.True(result.Item1 == 0, $"Pack failed with following log information :\n {result.Item3}");
            Assert.True(result.Item3 == "", $"Pack failed with following message in error stream :\n {result.Item3}");
        }

        internal void BuildProject(string workingDirectory, string projectName, string args)
        {
            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"msbuild {projectName}.csproj {args}",
                waitForExit: true);
            Assert.True(result.Item1 == 0, $"Build failed with following log information :\n {result.Item3}");
            Assert.True(result.Item3 == "", $"Build failed with following message in error stream :\n {result.Item3}");
        }

        private string CopyLatestCliForPack()
        {
            var cliDirectory = TestDirectory.Create();
            CopyLatestCliToTestDirectory(cliDirectory);
            UpdateCliWithLatestNuGetAssemblies(cliDirectory);
            return cliDirectory.Path;
        }

        private void CopyLatestCliToTestDirectory(string destinationDir)
        {
            var cliDir = Path.GetDirectoryName(_dotnetCli);
            
            //Create sub-directory structure in destination
            foreach (var directory in Directory.GetDirectories(cliDir, "*", SearchOption.AllDirectories))
            {
                var destDir = destinationDir + directory.Substring(cliDir.Length);
                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
            }

            //Copy files recursively to destination directories
            foreach (var fileName in Directory.GetFiles(cliDir, "*", SearchOption.AllDirectories))
            {
                File.Copy(fileName, destinationDir + fileName.Substring(cliDir.Length));
            }
        }

        private void UpdateCliWithLatestNuGetAssemblies(string cliDirectory)
        {
            var nupkgsDirectory = DotnetCliUtil.GetNupkgDirectoryInRepo();
            var productVersionInfo = FileVersionInfo.GetVersionInfo(DotnetCliUtil.GetXplatDll());
            var pathToPackNupkg = nupkgsDirectory + Path.DirectorySeparatorChar + "NuGet.Build.Tasks.Pack." +
                                  productVersionInfo.ProductVersion + ".nupkg";
            var pathToSdkInCli = Path.Combine(
                    Directory.GetDirectories(Path.Combine(cliDirectory, "sdk"))
                        .First());
            using (var nupkg = new PackageArchiveReader(pathToPackNupkg))
            {
                var pathToPackSdk = Path.Combine(pathToSdkInCli, "Sdks", "NuGet.Build.Tasks.Pack");
                var files = nupkg.GetFiles()
                .Where(fileName => fileName.StartsWith("Desktop")
                                || fileName.StartsWith("CoreCLR")
                                || fileName.StartsWith("build")
                                || fileName.StartsWith("buildCrossTargeting"));

                DeleteFiles(pathToPackSdk);
                CopyNupkgFilesToTarget(nupkg, pathToPackSdk, files);

                foreach (var coreClrDll in Directory.GetFiles(Path.Combine(pathToPackSdk, "CoreCLR")))
                {
                    var fileName = Path.GetFileName(coreClrDll);
                    if (fileName != "NuGet.Build.Tasks.Pack.dll")
                    {
                        File.Copy(coreClrDll, Path.Combine(pathToSdkInCli, fileName), true);
                    }
                }
            }

            var pathToRestoreNupkg = nupkgsDirectory + Path.DirectorySeparatorChar + "NuGet.Build.Tasks." +
                                  productVersionInfo.ProductVersion + ".nupkg";
            using (var nupkg = new PackageArchiveReader(pathToRestoreNupkg))
            {
                var files = nupkg.GetFiles()
                    .Where(fileName => fileName.StartsWith("lib/netstandard1.3")
                                       || fileName.StartsWith("runtimes"));
                File.Delete(Path.Combine(pathToSdkInCli, "NuGet.Build.Tasks.dll"));
                File.Delete(Path.Combine(pathToSdkInCli, "NuGet.Build.Tasks.xml"));
                File.Delete(Path.Combine(pathToSdkInCli, "NuGet.targets"));
                foreach (var file in files)
                {
                    var stream = nupkg.GetStream(file);
                    stream.CopyToFile(Path.Combine(pathToSdkInCli, Path.GetFileName(file)));
                }
            }
        }

        private void CopyNupkgFilesToTarget(PackageArchiveReader nupkg, string destPath, IEnumerable<string> files )
        {
            var packageFileExtractor = new PackageFileExtractor(files,
                                         PackageExtractionBehavior.XmlDocFileSaveMode);

            nupkg.CopyFiles(destPath, files, packageFileExtractor.ExtractPackageFile, new TestCommandOutputLogger(),
                CancellationToken.None);

        }

        private void DeleteFiles(string destinationDir)
        {
            Directory.Delete(destinationDir, true);
        }

        public void Dispose()
        {
            Directory.Delete(Path.GetDirectoryName(TestDotnetCli), true);
        }
    }
}
