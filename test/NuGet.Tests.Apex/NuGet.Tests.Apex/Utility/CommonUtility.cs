// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Test.Apex.VisualStudio;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.Packaging.Signing;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.VisualStudio;

namespace NuGet.Tests.Apex
{
    public class CommonUtility
    {
        public static async Task CreatePackageInSourceAsync(string packageSource, string packageName, string packageVersion)
        {
            var package = CreatePackage(packageName, packageVersion);
            await SimpleTestPackageUtility.CreatePackagesAsync(packageSource, package);
        }

        public static async Task CreateAuthorSignedPackageInSourceAsync(
            string packageSource,
            string packageName,
            string packageVersion,
            X509Certificate2 testCertificate,
            Uri timestampProviderUrl = null)
        {
            var package = CreateAuthorSignedPackage(packageName, packageVersion, testCertificate, timestampProviderUrl);
            await SimpleTestPackageUtility.CreatePackagesAsync(packageSource, package);
        }

        public static SimpleTestPackageContext CreateAuthorSignedPackage(
            string packageName,
            string packageVersion,
            X509Certificate2 testCertificate,
            Uri timestampProviderUrl = null)
        {
            var package = CreatePackage(packageName, packageVersion);
            return AuthorSignPackage(package, testCertificate, timestampProviderUrl);
        }

        public static SimpleTestPackageContext CreateRepositorySignedPackage(
            string packageName,
            string packageVersion,
            X509Certificate2 testCertificate,
            Uri v3ServiceIndexUrl,
            IReadOnlyList<string>packageOwners = null,
            Uri timestampProviderUrl = null)
        {
            var package = CreatePackage(packageName, packageVersion);
            return RepositorySignPackage(package, testCertificate, v3ServiceIndexUrl, packageOwners, timestampProviderUrl);
        }

        public static SimpleTestPackageContext CreateRepositoryCountersignedPackage(
            string packageName,
            string packageVersion,
            X509Certificate2 authorCertificate,
            X509Certificate2 repoCertificate,
            Uri v3ServiceIndexUrl,
            IReadOnlyList<string> packageOwners = null,
            Uri timestampProviderUrl = null)
        {
            var package = CreatePackage(packageName, packageVersion);
            var authorSignedPackage = AuthorSignPackage(package, authorCertificate, timestampProviderUrl);
            return RepositoryCountersignPackage(authorSignedPackage, repoCertificate, v3ServiceIndexUrl, packageOwners, timestampProviderUrl);
        }

        public static SimpleTestPackageContext AuthorSignPackage(
            SimpleTestPackageContext package,
            X509Certificate2 authorCertificate,
            Uri timestampProviderUrl = null)
        {
            package.IsPrimarySigned = true;
            package.PrimarySignatureCertificate = authorCertificate;

            if (package.PrimaryTimestampProvider == null && timestampProviderUrl != null) {
                package.PrimaryTimestampProvider = new Rfc3161TimestampProvider(timestampProviderUrl);
            }

            return package;
        }

        public static SimpleTestPackageContext RepositorySignPackage(
            SimpleTestPackageContext package,
            X509Certificate2 repoCertificate,
            Uri v3ServiceIndexUrl,
            IReadOnlyList<string> packageOwners = null,
            Uri timestampProviderUrl = null)
        {
            package.IsPrimarySigned = true;
            package.PrimarySignatureCertificate = repoCertificate;
            package.V3ServiceIndexUrl = v3ServiceIndexUrl;
            package.PackageOwners = packageOwners;

            if (package.PrimaryTimestampProvider == null && timestampProviderUrl != null)
            {
                package.PrimaryTimestampProvider = new Rfc3161TimestampProvider(timestampProviderUrl);
            }

            return package;
        }

        public static SimpleTestPackageContext RepositoryCountersignPackage(
            SimpleTestPackageContext package,
            X509Certificate2 repoCertificate,
            Uri v3ServiceIndexUrl,
            IReadOnlyList<string> packageOwners = null,
            Uri timestampProviderUrl = null)
        {
            if (!package.IsPrimarySigned)
            {
                throw new InvalidOperationException("Package has to be primary signed");
            }

            package.IsRepositoryCounterSigned = true;
            package.RepositoryCountersignatureCertificate = repoCertificate;
            package.V3ServiceIndexUrl = v3ServiceIndexUrl;
            package.PackageOwners = packageOwners;

            if (package.CounterTimestampProvider == null && timestampProviderUrl != null)
            {
                package.CounterTimestampProvider = new Rfc3161TimestampProvider(timestampProviderUrl);
            }

            return package;
        }

        public static SimpleTestPackageContext CreatePackage(string packageName, string packageVersion)
        {
            var package = new SimpleTestPackageContext(packageName, packageVersion);
            package.Files.Clear();
            package.AddFile("lib/net45/_._");
            package.AddFile("lib/netstandard1.0/_._");

            return package;
        }

        public static void AssertPackageReferenceExists(VisualStudioHost visualStudio, ProjectTestExtension project, string packageName, string packageVersion, ILogger logger)
        {
            logger.LogInformation($"Checking for PackageReference {packageName} {packageVersion}");

            var matches = GetPackageReferences(project)
                .Where(e => e.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase)
                        && e.LibraryRange.VersionRange.MinVersion.Equals(NuGetVersion.Parse(packageVersion)))
                .ToList();

            logger.LogInformation($"Matches: {matches.Count}");

            matches.Any().Should().BeTrue($"A PackageReference with {packageName}/{packageVersion} was not found in {project.FullPath}");
        }

        public static void AssertPackageReferenceDoesNotExist(VisualStudioHost visualStudio, ProjectTestExtension project, string packageName, string packageVersion, ILogger logger)
        {
            logger.LogInformation($"Checking for PackageReference {packageName} {packageVersion}");

            var matches = GetPackageReferences(project)
                .Where(e => e.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase)
                        && e.LibraryRange.VersionRange.MinVersion.Equals(NuGetVersion.Parse(packageVersion)))
                .ToList();

            logger.LogInformation($"Matches: {matches.Count}");

            matches.Any().Should().BeFalse($"A PackageReference with {packageName}/{packageVersion} was not found in {project.FullPath}");
        }

        public static void AssertPackageReferenceDoesNotExist(VisualStudioHost visualStudio, ProjectTestExtension project, string packageName, ILogger logger)
        {
            logger.LogInformation($"Checking for PackageReference {packageName}");

            var matches = GetPackageReferences(project)
                .Where(e => e.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            logger.LogInformation($"Matches: {matches.Count}");

            matches.Any().Should().BeFalse($"A PackageReference for {packageName} was not found in {project.FullPath}");
        }

        public static List<LibraryDependency> GetPackageReferences(ProjectTestExtension project)
        {
            project.Save();
            var doc = XDocument.Load(project.FullPath);

            return doc.Root.Descendants()
                .Where(e => e.Name.LocalName.Equals("PackageReference", StringComparison.OrdinalIgnoreCase))
                .Select(e => new LibraryDependency(
                    new LibraryRange(e.Attribute(XName.Get("Include")).Value,
                        VersionRange.Parse(e.Attribute(XName.Get("Version")).Value),
                        LibraryDependencyTarget.Package),
                        LibraryDependencyType.Default,
                        LibraryIncludeFlags.All,
                        LibraryIncludeFlags.None,
                        new List<NuGetLogCode>(),
                        autoReferenced: false))
                .ToList();
        }

        public static void AssertPackageInAssetsFile(VisualStudioHost visualStudio, ProjectTestExtension project, string packageName, string packageVersion, ILogger logger)
        {
            logger.LogInformation($"Checking assets file for {packageName}");

            var testService = visualStudio.Get<NuGetApexTestService>();
            testService.WaitForAutoRestore();

            var assetsFilePath = GetAssetsFilePath(project.FullPath);
            File.Exists(assetsFilePath).Should().BeTrue(AppendErrors($"File does not exist: {assetsFilePath}", visualStudio));

            // Project has an assets file, let's look there to assert
            var inAssetsFile = IsPackageInstalledInAssetsFile(assetsFilePath, packageName, packageVersion);

            logger.LogInformation($"Exists: {inAssetsFile}");

            inAssetsFile.Should().BeTrue(AppendErrors($"{packageName}/{packageVersion} should be installed in {project.Name}", visualStudio));
        }

        public static void AssetPackageInPackagesConfig(VisualStudioHost visualStudio, ProjectTestExtension project, string packageName, string packageVersion, ILogger logger)
        {
            logger.LogInformation($"Checking project {packageName}");
            var testService = visualStudio.Get<NuGetApexTestService>();

            // Check using the IVs APIs
            var exists = testService.IsPackageInstalled(project.UniqueName, packageName, packageVersion);

            logger.LogInformation($"Exists: {exists}");

            exists.Should().BeTrue(AppendErrors($"{packageName}/{packageVersion} should be installed in {project.Name}", visualStudio));
        }

        public static void AssetPackageInPackagesConfig(VisualStudioHost visualStudio, ProjectTestExtension project, string packageName, ILogger logger)
        {
            logger.LogInformation($"Checking project {packageName}");
            var testService = visualStudio.Get<NuGetApexTestService>();

            // Check using the IVs APIs
            var exists = testService.IsPackageInstalled(project.UniqueName, packageName);
            logger.LogInformation($"Exists: {exists}");

            exists.Should().BeTrue(AppendErrors($"{packageName} should be installed in {project.Name}", visualStudio));
        }

        public static void AssertPackageNotInAssetsFile(VisualStudioHost visualStudio, ProjectTestExtension project, string packageName, string packageVersion, ILogger logger)
        {
            logger.LogInformation($"Checking assets file for {packageName}");
            var testService = visualStudio.Get<NuGetApexTestService>();
            testService.WaitForAutoRestore();

            var assetsFilePath = GetAssetsFilePath(project.FullPath);
            File.Exists(assetsFilePath).Should().BeTrue(AppendErrors($"File does not exist: {assetsFilePath}", visualStudio));

            // Project has an assets file, let's look there to assert
            var inAssetsFile = IsPackageInstalledInAssetsFile(assetsFilePath, packageName, packageVersion);
            logger.LogInformation($"Exists: {inAssetsFile}");

            inAssetsFile.Should().BeFalse(AppendErrors($"{packageName}/{packageVersion} should be installed in {project.Name}", visualStudio));
        }

        public static void AssertPackageNotInPackagesConfig(VisualStudioHost visualStudio, ProjectTestExtension project, string packageName, string packageVersion, ILogger logger)
        {
            logger.LogInformation($"Checking project for {packageName}");
            var testService = visualStudio.Get<NuGetApexTestService>();

            // Check using the IVs APIs
            var exists = testService.IsPackageInstalled(project.UniqueName, packageName, packageVersion);
            logger.LogInformation($"Exists: {exists}");

            exists.Should().BeFalse(AppendErrors($"{packageName}/{packageVersion} should NOT be in {project.Name}", visualStudio));
        }

        public static void AssertPackageNotInPackagesConfig(VisualStudioHost visualStudio, ProjectTestExtension project, string packageName, ILogger logger)
        {
            logger.LogInformation($"Checking project for {packageName}");
            var testService = visualStudio.Get<NuGetApexTestService>();

            // Check using the IVs APIs
            var exists = testService.IsPackageInstalled(project.UniqueName, packageName);
            logger.LogInformation($"Exists: {exists}");

            exists.Should().BeFalse(AppendErrors($"{packageName} should NOT be in {project.Name}", visualStudio));
        }


        public static bool IsPackageInstalledInAssetsFile(string assetsFilePath, string packageName, string packageVersion)
        {
            return PackageExistsInLockFile(assetsFilePath, packageName, packageVersion);
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

        internal static ProjectTestExtension CreateAndInitProject(ProjectTemplate projectTemplate, SimpleTestPathContext pathContext, SolutionService solutionService, ILogger logger)
        {
            logger.LogInformation("Creating solution");
            solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);

            logger.LogInformation("Adding project");
            var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");

            logger.LogInformation("Saving solution");
            solutionService.Save();

            logger.LogInformation("Building solution");
            project.Build();

            return project;
        }

        public static string AppendErrors(string s, VisualStudioHost visualStudio)
        {
            var errors = visualStudio.GetErrorsInOutputWindows();

            if (errors.Any())
            {
                s += Environment.NewLine + string.Join("\n\t error: ", errors);
            }

            return s;
        }
    }
}
