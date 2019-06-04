// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NuGet.XPlat.FuncTest;
using NuGet.Test.Utility;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.PackageExtraction;
using NuGet.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet.Integration.Test
{
    public class MsbuildIntegrationTestFixture : IDisposable
    {
        private readonly TestDirectory _cliDirectory;
        private readonly string _dotnetCli = DotnetCliUtil.GetDotnetCli();
        internal readonly string TestDotnetCli;
        internal readonly string MsBuildSdksPath;
        private readonly Dictionary<string, string> _processEnvVars = new Dictionary<string, string>();

        private string NuGetClient;
        private string HandleExe;
        private string Handle64Exe;

    public MsbuildIntegrationTestFixture()
        {
            var paths = _dotnetCli.Split('\\');
            var len = paths.Length;
            var len1 = paths.ElementAt(len - 1).Length;
            var len2 = paths.ElementAt(len - 2).Length;
            NuGetClient = _dotnetCli.Substring(0, _dotnetCli.Length - (len1 + len2 + 1));
            HandleExe = Path.Combine(NuGetClient, "Handle", "handle.exe");
            Handle64Exe = Path.Combine(NuGetClient, "Handle", "handle64.exe");

            var cliDirectory = CopyLatestCliForPack();
            TestDotnetCli = Path.Combine(cliDirectory, "dotnet.exe");

            var sdkPaths = Directory.GetDirectories(Path.Combine(cliDirectory, "sdk"));
#if IS_NETCORE30
            MsBuildSdksPath = Path.Combine(
             sdkPaths.Where(path => path.Split('\\').Last().StartsWith("3")).First()
             , "Sdks");
#else
            MsBuildSdksPath = Path.Combine(
             sdkPaths.Where(path => path.Split('\\').Last().StartsWith("2")).First()
             , "Sdks");
#endif        
            _processEnvVars.Add("MSBuildSDKsPath", MsBuildSdksPath);
            _processEnvVars.Add("UseSharedCompilation", "false");
            _processEnvVars.Add("DOTNET_MULTILEVEL_LOOKUP", "0");
            _processEnvVars.Add("MSBUILDDISABLENODEREUSE ", "true");
            // We do this here so that dotnet new will extract all the packages on the first run on the machine.
            InitDotnetNewToExtractPackages();
        }

        private void InitDotnetNewToExtractPackages()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                CreateDotnetNewProject(testDirectory.Path, projectName, " classlib", timeOut: 300000);
            }

            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ConsoleApp1";
                CreateDotnetNewProject(testDirectory.Path, projectName, " console", timeOut: 300000);
            }
        }

        internal void CreateDotnetNewProject(string solutionRoot, string projectName, string args = "console", int timeOut = 60000)
        {
            var workingDirectory = Path.Combine(solutionRoot, projectName);
            if (!Directory.Exists(workingDirectory))
            {
                Directory.CreateDirectory(workingDirectory);
            }
 
            CreateTempGlobalJson(solutionRoot);

            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"new {args}",
                waitForExit: true,
                timeOutInMilliseconds: timeOut,
                environmentVariables: _processEnvVars);

            Assert.True(result.Item1 == 0, $"Creating project failed with following log information :\n {result.AllOutput}");
            Assert.True(string.IsNullOrWhiteSpace(result.Item3), $"Creating project failed with following message in error stream :\n {result.AllOutput}");
        }

        //create a global.json file in temperary testing folder, to make sure testing with the correct sdk when there're multiple of them in CLI folder.
        internal void CreateTempGlobalJson(string solutionRoot)
        {
            //put the global.json at one level up to solutionRoot path
            var pathToPlaceGlobalJsonFile = solutionRoot.Substring(0, solutionRoot.Length - 1 - solutionRoot.Split("\\").Last().Length);
            if (File.Exists(pathToPlaceGlobalJsonFile + "\\global.json"))
            {
                return;
            }

            var sdkVersion = MsBuildSdksPath.Split('\\').ElementAt(MsBuildSdksPath.Split('\\').Count() - 2);

            var globalJsonFile =
@"{
    ""sdk"": {
              ""version"": """  + sdkVersion + @"""
             }
}";

            using (var outputFile = new StreamWriter(Path.Combine(pathToPlaceGlobalJsonFile, "global.json")))
            {
                outputFile.WriteLine(globalJsonFile);
                outputFile.Close();
            }
        }
        internal void CreateDotnetToolProject(string solutionRoot, string projectName, string targetFramework, string rid, string source, IList<PackageIdentity> packages, int timeOut = 60000)
        {
            var workingDirectory = Path.Combine(solutionRoot, projectName);
            if (!Directory.Exists(workingDirectory))
            {
                Directory.CreateDirectory(workingDirectory);
            }
            var projectFileName = Path.Combine(workingDirectory, projectName + ".csproj");

            var restorePackagesPath = Path.Combine(workingDirectory, "tools", "packages");
            var restoreSolutionDirectory = workingDirectory;
            var msbuildProjectExtensionsPath = Path.Combine(workingDirectory);
            var packageReference = string.Empty;
            foreach (var package in packages) {
                packageReference = string.Concat(packageReference, Environment.NewLine, $@"<PackageReference Include=""{ package.Id }"" Version=""{ package.Version.ToString()}""/>");
            }

            var projectFile = $@"<Project Sdk=""Microsoft.NET.Sdk"">
                <PropertyGroup><RestoreProjectStyle>DotnetToolReference</RestoreProjectStyle>
                <OutputType>Exe</OutputType>
                <TargetFramework> {targetFramework} </TargetFramework>
                <RuntimeIdentifier>{rid} </RuntimeIdentifier> 
                <!-- Things that do change-->
                <RestorePackagesPath>{restorePackagesPath}</RestorePackagesPath>
                <RestoreSolutionDirectory>{restoreSolutionDirectory}</RestoreSolutionDirectory>
                <MSBuildProjectExtensionsPath>{msbuildProjectExtensionsPath}</MSBuildProjectExtensionsPath>
                <RestoreSources>{source}</RestoreSources>
                <!--Things that don't change -->
                <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                <RestoreFallbackFolders>clear</RestoreFallbackFolders>
                <RestoreAdditionalProjectSources></RestoreAdditionalProjectSources>
                <RestoreAdditionalProjectFallbackFolders></RestoreAdditionalProjectFallbackFolders>
                <RestoreAdditionalProjectFallbackFoldersExcludes></RestoreAdditionalProjectFallbackFoldersExcludes>
              </PropertyGroup>
                <ItemGroup>
                    {packageReference}
                </ItemGroup>
            </Project>";

            try
            {
                File.WriteAllText(projectFileName, projectFile);
            }
            catch
            {
                // ignore
            }
            Assert.True(File.Exists(projectFileName));
        }

        internal CommandRunnerResult RestoreToolProject(string workingDirectory, string projectName, string args = "")
        {
            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"restore {projectName}.csproj {args}",
                waitForExit: true,
                environmentVariables: _processEnvVars);
            return result;
        }

        internal void RestoreProject(string workingDirectory, string projectName, string args)
            => RestoreProjectOrSolution(workingDirectory, $"{projectName}.csproj", args);

        internal void RestoreSolution(string workingDirectory, string solutionName, string args)
            => RestoreProjectOrSolution(workingDirectory, $"{solutionName}.sln", args);

        private void RestoreProjectOrSolution(string workingDirectory, string fileName, string args)
        {
            var envVar = new Dictionary<string, string>();
            envVar.Add("MSBuildSDKsPath", MsBuildSdksPath);

            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"restore {fileName} {args}",
                waitForExit: true,
                environmentVariables: _processEnvVars);
            Assert.True(result.Item1 == 0, $"Restore failed with following log information :\n {result.AllOutput}");
            Assert.True(result.Item3 == "", $"Restore failed with following message in error stream :\n {result.AllOutput}");
        }

        /// <summary>
        /// dotnet.exe args
        /// </summary>
        internal CommandRunnerResult RunDotnet(string workingDirectory, string args, bool ignoreExitCode=false)
        {

            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                args,
                waitForExit: true,
                environmentVariables: _processEnvVars);

            if (!ignoreExitCode)
            {
                Assert.True(result.ExitCode == 0, $"dotnet.exe {args} command failed with following log information :\n {result.AllOutput}");
            }

            return result;
        }
        internal CommandRunnerResult RunHandle(string args)
        {
            var result = CommandRunner.Run(HandleExe,
                                            NuGetClient,
                                            args,
                                            waitForExit: true);
            return result;
        }

        internal CommandRunnerResult RunHandle64(string args)
        {
            var result = CommandRunner.Run(Handle64Exe,
                                            NuGetClient,
                                            args,
                                            waitForExit: true);
            return result;
        }


        internal CommandRunnerResult PackProject(string workingDirectory, string projectName, string args, string nuspecOutputPath = "obj", bool validateSuccess = true)
            => PackProjectOrSolution(workingDirectory, $"{projectName}.csproj", args, nuspecOutputPath, validateSuccess);

        internal CommandRunnerResult PackSolution(string workingDirectory, string solutionName, string args, string nuspecOutputPath = "obj", bool validateSuccess = true)
            => PackProjectOrSolution(workingDirectory, $"{solutionName}.sln", args, nuspecOutputPath, validateSuccess);

        private CommandRunnerResult PackProjectOrSolution(string workingDirectory, string file, string args, string nuspecOutputPath, bool validateSuccess)
        {
            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"pack {file} {args} /p:NuspecOutputPath={nuspecOutputPath}",
                waitForExit: true,
                environmentVariables: _processEnvVars);
            if (validateSuccess)
            {
                Assert.True(result.Item1 == 0, $"Pack failed with following log information :\n {result.AllOutput}");
                Assert.True(result.Item3 == "", $"Pack failed with following message in error stream :\n {result.AllOutput}");
            }
            return result;
        }

        internal void BuildProject(string workingDirectory, string projectName, string args)
        {
            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"msbuild {projectName}.csproj {args} /p:AppendRuntimeIdentifierToOutputPath=false",
                waitForExit: true,
                environmentVariables: _processEnvVars);
            Assert.True(result.Item1 == 0, $"Build failed with following log information :\n {result.AllOutput}");
            Assert.True(result.Item3 == "", $"Build failed with following message in error stream :\n {result.AllOutput}");
        }

        private TestDirectory CopyLatestCliForPack()
        {
            var cliDirectory = TestDirectory.Create();
            CopyLatestCliToTestDirectory(cliDirectory);
            UpdateCliWithLatestNuGetAssemblies(cliDirectory);
            return cliDirectory;
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
#if IS_NETCORE30
            var pathToPackNupkg = FindMostRecentNupkg(nupkgsDirectory, "NuGet.Build.Tasks.Pack");
#else
            var pathToPackNupkg = FindMostRecentNupkg(nupkgsDirectory, "NuGet.Build.Tasks.Pack.Sdk2x");
#endif

            var nupkgsToCopy = new List<string> { "NuGet.Build.Tasks", "NuGet.Versioning", "NuGet.Protocol", "NuGet.ProjectModel", "NuGet.Packaging", "NuGet.LibraryModel", "NuGet.Frameworks", "NuGet.DependencyResolver.Core", "NuGet.Configuration", "NuGet.Common", "NuGet.Commands", "NuGet.CommandLine.XPlat", "NuGet.Credentials" };

            var sdkPaths = Directory.GetDirectories(Path.Combine(cliDirectory, "sdk"));

#if IS_NETCORE30
            var pathToSdkInCli = sdkPaths.Where(path => path.Split('\\').Last().StartsWith("3")).First();
#else
            var pathToSdkInCli = sdkPaths.Where(path => path.Split('\\').Last().StartsWith("2")).First();
#endif
          //  var pathToSdkInCli = Path.Combine(
          //          Directory.GetDirectories(Path.Combine(cliDirectory, "sdk"))
          //              .First());
            using (var nupkg = new PackageArchiveReader(pathToPackNupkg))
            {
                var pathToPackSdk = Path.Combine(pathToSdkInCli, "Sdks", "NuGet.Build.Tasks.Pack");
                var files = nupkg.GetFiles()
                .Where(fileName => fileName.StartsWith("Desktop")
                                || fileName.StartsWith("CoreCLR")
                                || fileName.StartsWith("build")
                                || fileName.StartsWith("buildCrossTargeting"));

                DeleteDirectory(pathToPackSdk);
                CopyNupkgFilesToTarget(nupkg, pathToPackSdk, files);
            }


            foreach (var nupkgName in nupkgsToCopy) {
                using (var nupkg = new PackageArchiveReader(FindMostRecentNupkg(nupkgsDirectory, nupkgName)))
                {
#if IS_NETCORE30
                     var files = nupkg.GetFiles()
                    .Where(fileName => fileName.StartsWith("lib/netstandard2.1")
                                    || fileName.StartsWith("lib/netcoreapp3.0")
                                    || fileName.Contains("NuGet.targets"));
                    if (!files.Any()) {
                        files = nupkg.GetFiles()
                        .Where(fileName => fileName.StartsWith("lib/netstandard2.0")
                                    || fileName.Contains("NuGet.targets"));

                    }
#else
                    var files = nupkg.GetFiles()
                    .Where(fileName => fileName.StartsWith("lib/netstandard2.0")
                                    || fileName.StartsWith("lib/netcoreapp2.1")
                                    || fileName.Contains("NuGet.targets"));
#endif

                    CopyFlatlistOfFilesToTarget(nupkg, pathToSdkInCli, files);

                }
            }
        }

        private void CopyFlatlistOfFilesToTarget(PackageArchiveReader nupkg, string destination, IEnumerable<string> packageFiles)
        {
            var packageFileExtractor = new PackageFileExtractor(packageFiles,
                             PackageExtractionBehavior.XmlDocFileSaveMode);
            var logger = new TestCommandOutputLogger();
            var token = CancellationToken.None;
            var filesCopied = new List<string>();

            foreach (var packageFile in packageFiles)
            {
                token.ThrowIfCancellationRequested();

                var entry = nupkg.GetEntry(packageFile);

                var packageFileName = entry.FullName;
                // An entry in a ZipArchive could start with a '/' based on how it is zipped
                // Remove it if present
                if (packageFileName.StartsWith("/", StringComparison.Ordinal))
                {
                    packageFileName = packageFileName.Substring(1);
                }
                // Get only the name, without the path, since we are extracting to flat list
                packageFileName = Path.GetFileName(packageFileName);

                // ZipArchive always has forward slashes in them. By replacing them with DirectorySeparatorChar;
                // in windows, we get the windows-style path
                var normalizedPath = Uri.UnescapeDataString(packageFileName.Replace('/', Path.DirectorySeparatorChar));

                var targetFilePath = Path.Combine(destination, normalizedPath);
                if (!targetFilePath.StartsWith(destination, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                try
                {
                    File.Delete(targetFilePath);
                }
                catch
                {
                    // Do nothing
                }
                using (var stream = entry.Open())
                {
                    var copiedFile = packageFileExtractor.ExtractPackageFile(packageFileName, targetFilePath, stream);
                    if (copiedFile != null)
                    {
                        entry.UpdateFileTimeFromEntry(copiedFile, logger);

                        filesCopied.Add(copiedFile);
                    }
                }
            }

        }

        private void CopyNupkgFilesToTarget(PackageArchiveReader nupkg, string destPath, IEnumerable<string> files)
        {
            var packageFileExtractor = new PackageFileExtractor(files,
                                         PackageExtractionBehavior.XmlDocFileSaveMode);

            nupkg.CopyFiles(destPath, files, packageFileExtractor.ExtractPackageFile, new TestCommandOutputLogger(),
                CancellationToken.None);

        }

        private static string FindMostRecentNupkg(string nupkgDirectory, string id)
        {
            var info = LocalFolderUtility.GetPackagesV2(nupkgDirectory, new TestLogger());

            return info.Where(t => t.Identity.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .Where(t => !Path.GetExtension(Path.GetFileNameWithoutExtension(t.Path)).Equals(".symbols"))
                .OrderByDescending(p => p.LastWriteTimeUtc)
                .First().Path;
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
        private void DeleteDirectory(string path)
        {
            var handleArgs = " /accepteula " + path;
            foreach (string directory in Directory.GetDirectories(path))
            {
                DeleteDirectory(directory);
            }
            var resultbefore = "";
            try
            {
                var result = RunHandle(handleArgs);
                var result64 = RunHandle64(handleArgs);
                resultbefore = "@@@@before delete : \n" + "The path is : " + path + "\n" +
                                 "  @@@@The reuslts of running handle.exe is : \n" +
                                                 result.AllOutput + "\n" +
                                 "  @@@@The reuslts of running handle64.exe is : \n" +
                                                 result64.AllOutput + "\n\n";

                Directory.Delete(path, true);
            }
            catch (IOException)
            {
                Directory.Delete(path, true);
            }
            catch (UnauthorizedAccessException)
            {
                var result = RunHandle(handleArgs);
                var result64 = RunHandle64(handleArgs);
                throw new UnauthorizedAccessException(resultbefore + "throw unauthorizedAccessException customized by Heng : \n" +
                                                 "The path is : " + path + "\n" +
                                                 "  %%%%The reuslts of running handle.exe is : \n" +
                                                 result.AllOutput + "\n" +
                                                 "  %%%%The reuslts of running handle64.exe is : \n" +
                                                 result64.AllOutput + "\n");
            }
            catch
            {
                
            }
        }
    }
}