// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.VisualStudio;

namespace NuGet.Tests.Apex
{
    public class Utils
    {
        public static void CreatePackageInSource(string packageSource, string packageName, string packageVersion)
        {
            var package = CreatePackage(packageName, packageVersion);
            SimpleTestPackageUtility.CreatePackages(packageSource, package);
        }

        public static void CreateSignedPackageInSource(string packageSource, string packageName, string packageVersion, X509Certificate2 testCertificate)
        {
            var package = CreateSignedPackage(packageName, packageVersion, testCertificate);
            SimpleTestPackageUtility.CreatePackages(packageSource, package);
        }

        public static SimpleTestPackageContext CreateSignedPackage(string packageName, string packageVersion, X509Certificate2 testCertificate) {
            var package = CreatePackage(packageName, packageVersion);
            package.AuthorSignatureCertificate = testCertificate;

            return package;
        }

        public static SimpleTestPackageContext CreatePackage(string packageName, string packageVersion)
        {
            var package = new SimpleTestPackageContext(packageName, packageVersion);
            package.Files.Clear();
            package.AddFile("lib/net45/_._");

            return package;
        }

        public static bool IsPackageInstalledInAssetsFile(NuGetConsoleTestExtension nuGetConsole, string projectPath, string packageName, string packageVersion)
        {
            var assetsFile = GetAssetsFilePath(projectPath);
            var packagesConfig = GetPackagesConfigPath(projectPath);
            var packagesConfigExists = File.Exists(packagesConfig);

            return PackageExistsInLockFile(assetsFile, packageName, packageVersion);
        }

        /// <summary>
        /// Iterations to use for theory tests
        /// </summary>
        /// <remarks>This makes it easier when stressing bad tests</remarks>
        internal static int GetIterations()
        {
            var iterations = 1;

            if (int.TryParse(Environment.GetEnvironmentVariable("NUGET_APEX_TEST_ITERATIONS"), out var x) && x > 0)
            {
                iterations = x;
            }

            return iterations;
        }

        private static bool PackageExistsInLockFile(string pathToAssetsFile, string packageName, string packageVersion)
        {
            var version = NuGetVersion.Parse(packageVersion);
            var lockFile = GetAssetsFileWithRetry(pathToAssetsFile);
            var lockFileLibrary = lockFile.Libraries
                .SingleOrDefault(p => StringComparer.OrdinalIgnoreCase.Equals(p.Name, packageName)
                                    && p.Version.Equals(version));

            return lockFileLibrary != null;
        }

        private static LockFile GetAssetsFileWithRetry(string path)
        {
            var timeout = TimeSpan.FromSeconds(10);
            var timer = Stopwatch.StartNew();
            string content = null;

            do
            {
                if (File.Exists(path))
                {
                    try
                    {
                        content = File.ReadAllText(path);
                        var format = new LockFileFormat();
                        return format.Parse(content, path);
                    }
                    catch
                    {
                        // Ignore errors from conflicting writes.
                    }
                }

                Thread.Sleep(100);
            }
            while (timer.Elapsed < timeout);

            // File cannot be read
            if (File.Exists(path))
            {
                throw new InvalidOperationException("Unable to read: " + path);
            }
            else
            {
                throw new FileNotFoundException("Not found: " + path);
            }
        }

        private static string GetAssetsFilePath(string projectPath)
        {
            var projectDirectory = Path.GetDirectoryName(projectPath);
            return Path.Combine(projectDirectory, "obj", "project.assets.json");
        }

        private static string GetPackagesConfigPath(string projectPath)
        {
            var projectDirectory = Path.GetDirectoryName(projectPath);
            return Path.Combine(projectDirectory, "packages.config");
        }

        public static void UIInvoke(Action action)
        {
            var jtf = NuGetUIThreadHelper.JoinableTaskFactory;

            if (jtf != null)
            {
                jtf.Run(async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    action();
                });
            }
            else
            {
                // Run directly
                action();
            }
        }

        internal static ProjectTestExtension CreateAndInitProject(ProjectTemplate projectTemplate, SimpleTestPathContext pathContext, SolutionService solutionService)
        {
            solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
            var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
            solutionService.Save();
            project.Build();

            return project;
        }
    }
}
