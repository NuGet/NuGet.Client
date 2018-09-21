using System;
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
using NuGet.Protocol;

namespace Dotnet.Integration.Test
{
    public class MsbuilldIntegrationTestFixture : IDisposable
    {
        private readonly string _dotnetCli = DotnetCliUtil.GetDotnetCli(true);
        internal readonly string TestDotnetCli;
        internal readonly string MsBuildSdksPath;
        private readonly Dictionary<string, string> _processEnvVars = new Dictionary<string, string>();

        public MsbuilldIntegrationTestFixture()
        {
            var cliDirectory = CopyLatestCliForPack();
            TestDotnetCli = Path.Combine(cliDirectory, "dotnet.exe");
            MsBuildSdksPath = Path.Combine(Directory.GetDirectories
                (Path.Combine(cliDirectory, "sdk"))
                .First(), "Sdks");
            _processEnvVars.Add("MSBuildSDKsPath", MsBuildSdksPath);
            _processEnvVars.Add("UseSharedCompilation", "false");
            _processEnvVars.Add("DOTNET_MULTILEVEL_LOOKUP", "0");
            _processEnvVars.Add("MSBUILDDISABLENODEREUSE ", "true");
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
                timeOutInMilliseconds: 300000,
                environmentVariables: _processEnvVars);

            // TODO : remove this workaround when https://github.com/dotnet/templating/issues/294 is fixed
            if (result.Item1 != 0)
            {
                result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"new {args} --debug:reinit",
                waitForExit: true,
                timeOutInMilliseconds: 300000,
                environmentVariables: _processEnvVars);

                result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"new {args} ",
                waitForExit: true,
                timeOutInMilliseconds: 300000,
                environmentVariables: _processEnvVars);
            }
            Assert.True(result.Item1 == 0, $"Creating project failed with following log information :\n {result.Item3}");
            Assert.True(result.Item3 == "", $"Creating project failed with following message in error stream :\n {result.Item3}");
        }

        internal void RestoreProject(string workingDirectory, string projectName, string args)
        {
            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"restore {projectName}.csproj {args}",
                waitForExit: true,
                environmentVariables: _processEnvVars);
            Assert.True(result.Item1 == 0, $"Restore failed with following log information :\n {result.Item3}");
            Assert.True(result.Item3 == "", $"Restore failed with following message in error stream :\n {result.Item3}");
        }

        internal void PackProject(string workingDirectory, string projectName, string args)
        {
            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"pack {projectName}.csproj {args}",
                waitForExit: true,
                environmentVariables: _processEnvVars);
            Assert.True(result.Item1 == 0, $"Pack failed with following log information :\n {result.Item3}");
            Assert.True(result.Item3 == "", $"Pack failed with following message in error stream :\n {result.Item3}");
        }

        internal void BuildProject(string workingDirectory, string projectName, string args)
        {
            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"msbuild {projectName}.csproj {args}",
                waitForExit: true,
                environmentVariables: _processEnvVars);
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
            var pathToPackNupkg = FindMostRecentNupkg(nupkgsDirectory, "NuGet.Build.Tasks.Pack");
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

            //var pathToRestoreNupkg = FindMostRecentNupkg(nupkgsDirectory, "NuGet.Build.Tasks");
            //using (var nupkg = new PackageArchiveReader(pathToRestoreNupkg))
            //{
            //    var files = nupkg.GetFiles()
            //        .Where(fileName => fileName.StartsWith("lib/netstandard1.3")
            //                           || fileName.StartsWith("runtimes"));
            //    File.Delete(Path.Combine(pathToSdkInCli, "NuGet.Build.Tasks.dll"));
            //    File.Delete(Path.Combine(pathToSdkInCli, "NuGet.Build.Tasks.xml"));
            //    File.Delete(Path.Combine(pathToSdkInCli, "NuGet.targets"));
            //    foreach (var file in files)
            //    {
            //        var stream = nupkg.GetStream(file);
            //        stream.CopyToFile(Path.Combine(pathToSdkInCli, Path.GetFileName(file)));
            //    }
            //}
        }

        private static string FindMostRecentNupkg(string nupkgDirectory, string id)
        {
            var info = LocalFolderUtility.GetPackagesV2(nupkgDirectory, new TestLogger());

            return info.Where(t => t.Identity.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .Where(t => !Path.GetExtension(Path.GetFileNameWithoutExtension(t.Path)).Equals(".symbols"))
                .OrderByDescending(p => p.LastWriteTimeUtc)
                .First().Path;
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

        /// <summary>
        /// dotnet.exe args
        /// </summary>
        internal CommandRunnerResult RunDotnet(string workingDirectory, string args, bool ignoreExitCode = false)
        {

            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                args,
                waitForExit: true,
                environmentVariables: _processEnvVars);
            

            return result;
        }

        public void Dispose()
        {
            RunDotnet(Path.GetDirectoryName(TestDotnetCli), "build-server shutdown");
            KillDotnetExe(TestDotnetCli);
            DeleteDirectory(Path.GetDirectoryName(TestDotnetCli));
        }

        private static void KillDotnetExe(string pathToDotnetExe)
        {

            var processes = Process.GetProcessesByName("dotnet")
                .Where(t => string.Compare(t.MainModule.FileName, Path.GetFullPath(pathToDotnetExe), ignoreCase: true) == 0);
            var testDirProcesses = Process.GetProcesses()
                .Where(t => t.MainModule.FileName.StartsWith(TestFileSystemUtility.NuGetTestFolder, StringComparison.OrdinalIgnoreCase));
            try
            {
                if (processes != null)
                {
                    foreach (var process in processes)
                    {
                        if (string.Compare(process.MainModule.FileName, Path.GetFullPath(pathToDotnetExe), true) == 0)
                        {
                            process.Kill();
                        }
                    }
                }

                if (testDirProcesses != null)
                {
                    foreach (var process in testDirProcesses)
                    {
                        process.Kill();
                    }
                }

            }
            catch { }
        }

        /// <summary>
        /// Depth-first recursive delete, with handling for descendant 
        /// directories open in Windows Explorer or used by another process
        /// </summary>
        private static void DeleteDirectory(string path)
        {
            foreach (string directory in Directory.GetDirectories(path))
            {
                DeleteDirectory(directory);
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
                Directory.Delete(path, true);
            }
            catch (UnauthorizedAccessException)
            {
                Directory.Delete(path, true);
            }
            catch
            {

            }
        }
    }
}
