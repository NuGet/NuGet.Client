// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Protocol;
using NuGet.Test.Utility;
using NuGet.Versioning;

namespace NuGet.Test.Utility
{
    public static class TestDotnetCLiUtility
    {
        internal static string SdkVersion { get; private set; }
        internal static NuGetFramework SdkTfm { get; private set; }
        internal static string CliDirSource { get; private set; }
        internal static string SdkDirSource { get; private set; }


        public static TestDirectory CopyAndPatchLatestDotnetCli(string sdkVersion = null, string sdkTfm = null)
        {

            CliDirSource = Path.GetDirectoryName(TestFileSystemUtility.GetDotnetCli());
            SdkDirSource = Path.Combine(CliDirSource, "sdk" + Path.DirectorySeparatorChar);

            if (sdkVersion == null)
            {
#if !IS_DESKTOP
                // Dynamically determine which SDK version to copy
                SdkVersion = GetSdkToTest();
#endif
            }
            else
            {
                // Use specified sdkVersion
                SdkVersion = GetSdkToTest(sdkVersion);
            }


            if (sdkTfm == null)
            {
#if !IS_DESKTOP
                // Dynamically determine the TFM of the dotnet.dll
                SdkTfm = AssemblyReader.GetTargetFramework(Path.Combine(SdkDirSource, SdkVersion, "dotnet.dll"));
#endif
            }
            else
            {
                // Use specified sdkVersion
                SdkTfm = NuGetFramework.Parse(sdkTfm);
            }

            var cliDirDestination = TestDirectory.Create();
            CopyLatestCliToTestDirectory(cliDirDestination);
            UpdateCliWithLatestNuGetAssemblies(cliDirDestination);

            // TODO - remove when SDK version for testing has Cryptography Dlls. See https://github.com/NuGet/Home/issues/8952
            var patchPath = Directory.EnumerateDirectories(Path.Combine(cliDirDestination, "sdk")).Single();
            PatchSDKWithCryptographyDlls(patchPath);

            return cliDirDestination;
        }

        private static void CopyLatestCliToTestDirectory(string destinationDir)
        {
            WriteGlobalJson(destinationDir);

            var sdkPath = Path.Combine(SdkDirSource, SdkVersion + Path.DirectorySeparatorChar);
            var fallbackFolderPath = Path.Combine(SdkDirSource, "NuGetFallbackFolder");

            Func<string, bool> predicate = path =>
            {
                if (!path.StartsWith(SdkDirSource))
                {
                    return true;
                }

                return path.StartsWith(sdkPath) || path.StartsWith(fallbackFolderPath);
            };

            //Create sub-directory structure in destination, ignoring any SDK version not selected.
            foreach (var directory in Directory.EnumerateDirectories(CliDirSource, "*", SearchOption.AllDirectories).Where(predicate))
            {
                var destDir = destinationDir + directory.Substring(CliDirSource.Length);
                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
            }

            var lastWriteTime = DateTime.Now.AddDays(-2);

            //Copy files recursively to destination directories, ignoring any SDK version not selected.
            foreach (var fileName in Directory.EnumerateFiles(CliDirSource, "*", SearchOption.AllDirectories).Where(predicate))
            {
                var destFileName = destinationDir + fileName.Substring(CliDirSource.Length);
                File.Copy(fileName, destFileName);
                File.SetLastWriteTime(destFileName, lastWriteTime);
            }
        }

#if !IS_DESKTOP
        private static string GetSdkToTest()
        {
            // The TFM we're testing
            var testTfm = AssemblyReader.GetTargetFramework(typeof(TestDotnetCLiUtility).Assembly.Location);

            var selectedVersion =
                Directory.EnumerateDirectories(SdkDirSource) // get all directories in sdk folder
                .Where(path =>
                { // SDK is for TFM to test
                    if (string.Equals(Path.GetFileName(path), "NuGetFallbackFolder", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    var dotnetPath = Path.Combine(path, "dotnet.dll");
                    var sdkTfm = AssemblyReader.GetTargetFramework(dotnetPath);

                    return testTfm == sdkTfm;
                })
                .Select(Path.GetFileName) // just the folder name (version string)
                .OrderByDescending(path => NuGetVersion.Parse(Path.GetFileName(path))) // in case there are multiple matching SDKs, selected the highest version
                .FirstOrDefault();

            if (selectedVersion == null)
            {
                var message = $@"Could not find suitable SDK to test in {SdkDirSource}
TFM being tested: {testTfm.DotNetFrameworkName}
SDKs found: {string.Join(", ", Directory.EnumerateDirectories(SdkDirSource).Select(Path.GetFileName).Where(d => !string.Equals(d, "NuGetFallbackFolder", StringComparison.OrdinalIgnoreCase)))}";

                throw new Exception(message);
            }

            return selectedVersion;
        }
#endif
        private static string GetSdkToTest(string sdkVersion)
        {
            var selectedVersion =
                Directory.EnumerateDirectories(SdkDirSource) // get all directories in sdk folder
                .Where(path =>
                { // SDK is for TFM to test
                    if (string.Equals(Path.GetFileName(path), "NuGetFallbackFolder", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    var version = path.Substring(Path.GetDirectoryName(path).Length + 1);

                    return version.StartsWith(sdkVersion);
                })
                .Select(Path.GetFileName) // just the folder name (version string)
                .OrderByDescending(path => NuGetVersion.Parse(Path.GetFileName(path))) // in case there are multiple matching SDKs, selected the highest version
                .FirstOrDefault();

            if (selectedVersion == null)
            {
                var message = $@"Could not find suitable SDK to test in {SdkDirSource}
sdkVersion specified: {sdkVersion}
SDKs found: {string.Join(", ", Directory.EnumerateDirectories(SdkDirSource).Select(Path.GetFileName).Where(d => !string.Equals(d, "NuGetFallbackFolder", StringComparison.OrdinalIgnoreCase)))}";

                throw new Exception(message);
            }

            return selectedVersion;
        }

        private static void UpdateCliWithLatestNuGetAssemblies(string cliDirectory)
        {
            var artifactsDirectory = TestFileSystemUtility.GetArtifactsDirectoryInRepo();
            var pathToSdkInCli = Path.Combine(
                    Directory.EnumerateDirectories(Path.Combine(cliDirectory, "sdk"))
                        .First());
            const string configuration =
#if DEBUG
                "Debug";
#else
                "Release";
#endif
            const string toolsetVersion = "16.0";
            CopyPackSdkArtifacts(artifactsDirectory, pathToSdkInCli, configuration, toolsetVersion);
            CopyRestoreArtifacts(artifactsDirectory, pathToSdkInCli, configuration, toolsetVersion);
        }

        private static void CopyRestoreArtifacts(string artifactsDirectory, string pathToSdkInCli, string configuration, string toolsetVersion)
        {
            const string restoreProjectName = "NuGet.Build.Tasks";
            const string restoreTargetsName = "NuGet.targets";
            const string restoreTargetsExtName = "NuGet.RestoreEx.targets";

            var sdkDependencies = new List<string> { restoreProjectName, "NuGet.Versioning", "NuGet.Protocol", "NuGet.ProjectModel", "NuGet.Packaging", "NuGet.LibraryModel", "NuGet.Frameworks", "NuGet.DependencyResolver.Core", "NuGet.Configuration", "NuGet.Common", "NuGet.Commands", "NuGet.CommandLine.XPlat", "NuGet.Credentials", "NuGet.Build.Tasks.Console" };

            // Copy rest of the NuGet assemblies.
            foreach (var projectName in sdkDependencies)
            {
                var projectArtifactsBinFolder = Path.Combine(artifactsDirectory, projectName, toolsetVersion, "bin", configuration);

                var tfmToCopy = GetTfmToCopy(projectArtifactsBinFolder);
                var frameworkArtifactsFolder = new DirectoryInfo(Path.Combine(projectArtifactsBinFolder, tfmToCopy));

                var fileName = projectName + ".dll";
                File.Copy(
                        sourceFileName: Path.Combine(frameworkArtifactsFolder.FullName, fileName),
                        destFileName: Path.Combine(pathToSdkInCli, fileName),
                        overwrite: true);
                // Copy the restore targets.
                if (projectName.Equals(restoreProjectName))
                {
                    File.Copy(
                        sourceFileName: Path.Combine(frameworkArtifactsFolder.FullName, restoreTargetsName),
                        destFileName: Path.Combine(pathToSdkInCli, restoreTargetsName),
                        overwrite: true);
                    File.Copy(
                        sourceFileName: Path.Combine(frameworkArtifactsFolder.FullName, restoreTargetsExtName),
                        destFileName: Path.Combine(pathToSdkInCli, restoreTargetsExtName),
                        overwrite: true);
                }
            }
        }

        private static string GetTfmToCopy(string projectArtifactsBinFolder)
        {
            var compiledTfms =
                Directory.EnumerateDirectories(projectArtifactsBinFolder) // get all directories in bin folder
                .Select(Path.GetFileName) // just the folder name (tfm)
                .ToDictionary(folder => NuGetFramework.Parse(folder));

            var reducer = new FrameworkReducer();
            var selectedTfm = reducer.GetNearest(SdkTfm, compiledTfms.Keys);

            if (selectedTfm == null)
            {
                var message = $@"Could not find suitable assets to copy in {projectArtifactsBinFolder}
TFM being tested: {SdkTfm}
project TFMs found: {string.Join(", ", compiledTfms.Keys.Select(k => k.ToString()))}";

                throw new Exception(message);
            }

            var selectedVersion = compiledTfms[selectedTfm];

            return selectedVersion;
        }

        private static void CopyPackSdkArtifacts(string artifactsDirectory, string pathToSdkInCli, string configuration, string toolsetVersion)
        {
            var pathToPackSdk = Path.Combine(pathToSdkInCli, "Sdks", "NuGet.Build.Tasks.Pack");

            const string packProjectName = "NuGet.Build.Tasks.Pack";
            const string packTargetsName = "NuGet.Build.Tasks.Pack.targets";

            // Copy the pack SDK.
            var packProjectBinDirectory = Path.Combine(artifactsDirectory, packProjectName, toolsetVersion, "bin", configuration);
            var tfmToCopy = GetTfmToCopy(packProjectBinDirectory);

            var packProjectCoreArtifactsDirectory = new DirectoryInfo(Path.Combine(packProjectBinDirectory, tfmToCopy));

            // We are only copying the CoreCLR assets, since, we're testing only them under Core MSBuild.
            var targetRuntimeType = "CoreCLR";

            var packAssemblyDestinationDirectory = Path.Combine(pathToPackSdk, targetRuntimeType);
            // Be smart here so we don't have to call ILMerge in the VS build. It takes ~15s total.
            // In VisualStudio, simply use the non il merged version.
            var ilMergedPackDirectoryPath = Path.Combine(packProjectCoreArtifactsDirectory.FullName, "ilmerge");
            if (Directory.Exists(ilMergedPackDirectoryPath))
            {
                var packFileName = packProjectName + ".dll";
                // Only use the il merged assembly if it's newer than the build.
                DateTime packAssemblyCreationDate = File.GetCreationTimeUtc(Path.Combine(packProjectCoreArtifactsDirectory.FullName, packFileName));
                DateTime ilMergedPackAssemblyCreationDate = File.GetCreationTimeUtc(Path.Combine(ilMergedPackDirectoryPath, packFileName));
                if (ilMergedPackAssemblyCreationDate > packAssemblyCreationDate)
                {
                    FileUtility.Replace(
                        sourceFileName: Path.Combine(packProjectCoreArtifactsDirectory.FullName, "ilmerge", packFileName),
                        destFileName: Path.Combine(packAssemblyDestinationDirectory, packFileName));
                }
            }
            else
            {
                foreach (var assembly in packProjectCoreArtifactsDirectory.EnumerateFiles("*.dll"))
                {
                    File.Copy(
                        sourceFileName: assembly.FullName,
                        destFileName: Path.Combine(packAssemblyDestinationDirectory, assembly.Name),
                        overwrite: true);
                }
            }
            // Copy the pack targets
            var packTargetsSource = Path.Combine(packProjectCoreArtifactsDirectory.FullName, packTargetsName);
            var targetsDestination = Path.Combine(pathToPackSdk, "build", packTargetsName);
            var targetsDestinationCrossTargeting = Path.Combine(pathToPackSdk, "buildCrossTargeting", packTargetsName);
            File.Copy(packTargetsSource, targetsDestination, overwrite: true);
            File.Copy(packTargetsSource, targetsDestinationCrossTargeting, overwrite: true);
        }

        public static void WriteGlobalJson(string path)
        {
            string globalJsonText = $"{{\"sdk\": {{\"version\": \"{SdkVersion}\"}}}}";
            var globalJsonPath = Path.Combine(path, "global.json");
            File.WriteAllText(globalJsonPath, globalJsonText);
        }

        /// <summary>
        /// Temporary patching process to bring in Cryptography DLLs for testing while SDK gets around to including them in 5.0.
        /// See also: https://github.com/NuGet/Home/issues/8508
        /// </summary>
        private static void PatchSDKWithCryptographyDlls(string sdkPath)
        {
            var assemblyNames = new string[1] { "System.Security.Cryptography.Pkcs.dll" };
            PatchDepsJsonFiles(assemblyNames, sdkPath);

            string userProfilePath = Environment.GetEnvironmentVariable(RuntimeEnvironmentHelper.IsWindows ? "USERPROFILE" : "HOME");
            string globalPackagesPath = Path.Combine(userProfilePath, ".nuget", "packages");

            CopyNewlyAddedDlls(assemblyNames, GetPkcsDllPath("System.Security.Cryptography.Pkcs.dll"), sdkPath);
        }

        private static void PatchDepsJsonFiles(string[] assemblyNames, string patchDir)
        {
            string[] fileNames = new string[3] { "dotnet.deps.json", "MSBuild.deps.json", "NuGet.CommandLine.XPlat.deps.json" };
            string[] fullNames = fileNames.Select(filename => Path.Combine(patchDir, filename)).ToArray();
            PatchDepsJsonWithNewlyAddedDlls(assemblyNames, fullNames);
        }

        private static void CopyNewlyAddedDlls(string[] assemblyNames, string copyFromPath, string copyToPath)
        {
            foreach (var assemblyName in assemblyNames)
            {
                File.Copy(
                    Path.Combine(copyFromPath, assemblyName),
                    Path.Combine(copyToPath, assemblyName)
                );
            }
        }

        private static void PatchDepsJsonWithNewlyAddedDlls(string[] assemblyNames, string[] filePaths)
        {
            foreach (string assemblyName in assemblyNames)
            {
                foreach (string filePath in filePaths)
                {
                    JObject jsonFile = GetJson(filePath);

                    JObject targets = jsonFile.GetJObjectProperty<JObject>("targets");

                    JObject netcoreapp50 = targets.GetJObjectProperty<JObject>(".NETCoreApp,Version=v5.0");

                    JProperty nugetBuildTasksProperty = netcoreapp50.Properties().
                        FirstOrDefault(prop => prop.Name.StartsWith("NuGet.Build.Tasks/", StringComparison.OrdinalIgnoreCase));

                    JObject nugetBuildTasks = nugetBuildTasksProperty.Value.FromJToken<JObject>();

                    JObject runtime = nugetBuildTasks.GetJObjectProperty<JObject>("runtime");

                    var assemblyPath = Path.Combine(GetPkcsDllPath(assemblyName), assemblyName);
                    var assemblyVersion = Assembly.LoadFile(assemblyPath).GetName().Version.ToString();
                    var assemblyFileVersion = FileVersionInfo.GetVersionInfo(assemblyPath).FileVersion;
                    var jproperty = new JProperty("lib/netcoreapp5.0/" + assemblyName,
                        new JObject
                        {
                            new JProperty("assemblyVersion", assemblyVersion),
                            new JProperty("fileVersion", assemblyFileVersion),
                        }
                    );
                    runtime.Add(jproperty);
                    nugetBuildTasks["runtime"] = runtime;
                    netcoreapp50[nugetBuildTasksProperty.Name] = nugetBuildTasks;
                    targets[".NETCoreApp,Version=v5.0"] = netcoreapp50;
                    jsonFile["targets"] = targets;
                    SaveJson(jsonFile, filePath);
                }
            }
        }

        private static JObject GetJson(string jsonFilePath)
        {
            try
            {
                return FileUtility.SafeRead(jsonFilePath, (stream, filePath) =>
                {
                    using (var reader = new StreamReader(stream))
                    {
                        return JObject.Parse(reader.ReadToEnd());
                    }
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    string.Format("Failed to read json file at {0}: {1}", jsonFilePath, ex.Message),
                    ex
                );
            }
        }

        private static void SaveJson(JObject json, string jsonFilePath)
        {
            FileUtility.Replace((outputPath) =>
            {
                using (var writer = new StreamWriter(outputPath, append: false, encoding: Encoding.UTF8))
                {
                    writer.Write(json.ToString());
                }
            },
            jsonFilePath);
        }

        private static string GetPkcsDllPath(string assemblyName)
        {
            var currentDir = Directory.CreateDirectory(Directory.GetCurrentDirectory());
            while (currentDir != null)
            {
                if (currentDir.GetFiles().Any(e => e.Name.Equals("NuGet.sln", StringComparison.OrdinalIgnoreCase)))
                {
                    // We have found the repo root.
                    break;
                }

                currentDir = currentDir.Parent;
            }
            const string configuration =
#if DEBUG
                "Debug";
#else
                "Release";
#endif
            var assemblyDir = Path.Combine(currentDir.FullName, "test", "NuGet.Core.FuncTests", "NuGet.Packaging.FuncTest", "bin", configuration, "netcoreapp5.0");

            if (!File.Exists(Path.Combine(assemblyDir, assemblyName)))
            {
                var message = $@"Could not find {assemblyName} in {assemblyDir}";

                throw new Exception(message);
            }
            return assemblyDir;
        }
    }
}
