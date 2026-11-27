// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;

namespace NuGet.PackageManagement.Test.NuGetPackageManagerTests
{
    [Collection(nameof(NotThreadSafeResourceCollection))]
    public class PackageScriptTests
    {
        [Fact]
        public async Task Uninstall_FailPolicy_DoesNotRemoveEarlierDependencyOrContent()
        {
            using var test = new PackageScriptTestContext();
            var project = test.Solution.AddNewMSBuildProject();
            var dependency = CreatePackage("Dependency", "tools/uninstall.ps1");
            var root = CreatePackage("Root", "tools/init.ps1");
            root.Dependencies.Add(dependency);
            await SimpleTestPackageUtility.CreatePackagesAsync(test.Source, root);
            await test.InstallAsync(project, root);
            var configPath = Path.Combine(test.Solution.SolutionDirectory, project.ProjectSystem.ProjectName, "packages.config");
            var originalConfig = File.ReadAllText(configPath);
            test.Context.ExecutionContext.PackageScripts = PackageScriptBehavior.Fail;

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                test.Manager.UninstallPackageAsync(project, root.Id, new UninstallationContext(removeDependencies: true, forceRemove: false),
                    test.Context, CancellationToken.None));

            Assert.Contains("uninstall.ps1", exception.Message);
            Assert.Equal(originalConfig, File.ReadAllText(configPath));
            Assert.Equal(2, (await project.GetInstalledPackagesAsync(CancellationToken.None)).Count());
        }

        [Fact]
        public async Task Update_FailPolicy_PreservesInstalledVersion()
        {
            using var test = new PackageScriptTestContext();
            var project = test.Solution.AddNewMSBuildProject();
            var oldPackage = CreatePackage("Package", "tools/uninstall.ps1");
            var newPackage = CreatePackage("Package", "tools/uninstall.ps1");
            newPackage.Version = "2.0.0";
            await SimpleTestPackageUtility.CreatePackagesAsync(test.Source, oldPackage, newPackage);
            await test.InstallAsync(project, oldPackage);
            var installedPackage = (await project.GetInstalledPackagesAsync(CancellationToken.None)).Single().PackageIdentity;
            var configPath = Path.Combine(project.ProjectSystem.ProjectFullPath, "packages.config");
            var originalConfig = File.ReadAllText(configPath);
            var actions = new[]
            {
                NuGetProjectAction.CreateUninstallProjectAction(installedPackage, project),
                NuGetProjectAction.CreateInstallProjectAction(newPackage.Identity, test.Repository, project)
            };
            test.Context.ExecutionContext.PackageScripts = PackageScriptBehavior.Fail;
            using var cache = new SourceCacheContext();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                test.Manager.ExecuteNuGetProjectActionsAsync(project, actions, test.Context, cache, CancellationToken.None));

            Assert.Contains(Path.Combine("tools", "uninstall.ps1"), exception.Message);
            Assert.Equal(originalConfig, File.ReadAllText(configPath));
            Assert.Equal(installedPackage, (await project.GetInstalledPackagesAsync(CancellationToken.None)).Single().PackageIdentity);
            Assert.False(Directory.Exists(Path.Combine(test.Solution.PackagesFolder, "Package.2.0.0")));
        }

        [Fact]
        public async Task Uninstall_FailPolicy_UsesInstalledFrameworkAfterRetargeting()
        {
            using var test = new PackageScriptTestContext();
            var project = test.Solution.AddNewMSBuildProject();
            var package = CreatePackage("Package", "tools/net45/uninstall.ps1");
            await SimpleTestPackageUtility.CreatePackagesAsync(test.Source, package);
            await test.InstallAsync(project, package);
            var projectDirectory = project.ProjectSystem.ProjectFullPath;
            var originalConfig = File.ReadAllText(Path.Combine(projectDirectory, "packages.config"));
            var retargetedSystem = new TestMSBuildNuGetProjectSystem(NuGetFramework.Parse("net40"), test.Context, projectFullPath: projectDirectory);
            var retargeted = new TestMSBuildNuGetProject(retargetedSystem, test.Solution.PackagesFolder, projectDirectory);
            test.Context.ExecutionContext.PackageScripts = PackageScriptBehavior.Fail;

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                test.Manager.UninstallPackageAsync(retargeted, package.Id, new UninstallationContext(),
                    test.Context, CancellationToken.None));

            Assert.Contains(Path.Combine("tools", "net45", "uninstall.ps1"), exception.Message);
            Assert.Equal(originalConfig, File.ReadAllText(Path.Combine(projectDirectory, "packages.config")));
            Assert.Single(await retargeted.GetInstalledPackagesAsync(CancellationToken.None));
        }

        [Fact]
        public async Task ExecuteActions_UninstallRunWithMissingScript_PreservesAllProjects()
        {
            using var test = new PackageScriptTestContext();
            var projects = new[] { test.Solution.AddNewMSBuildProject("First"), test.Solution.AddNewMSBuildProject("Second") };
            var packages = new[] { CreatePackage("Plain", "tools/init.ps1"), CreatePackage("Scripted", "tools/uninstall.ps1") };
            await SimpleTestPackageUtility.CreatePackagesAsync(test.Source, packages);
            for (var i = 0; i < projects.Length; i++)
            {
                await test.InstallAsync(projects[i], packages[i]);
            }
            var originalConfigs = projects.Select(project => File.ReadAllText(Path.Combine(project.ProjectSystem.ProjectFullPath, "packages.config"))).ToArray();
            var installedPath = projects[1].FolderNuGetProject.GetInstalledPath(packages[1].Identity);
            File.Delete(Path.Combine(installedPath, "tools", "uninstall.ps1"));
            test.Context.ExecutionContext.PackageScripts = PackageScriptBehavior.Run;
            var actions = projects.Select((project, i) => NuGetProjectAction.CreateUninstallProjectAction(packages[i].Identity, project));
            using var cache = new SourceCacheContext();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                test.Manager.ExecuteNuGetProjectActionsAsync(projects, actions, test.Context, cache, CancellationToken.None));

            Assert.Contains(packages[1].Identity.ToString(), exception.Message);
            Assert.Contains("Restore packages before continuing", exception.Message);
            Assert.Contains("-PackageScripts Skip", exception.Message);
            for (var i = 0; i < projects.Length; i++)
            {
                Assert.Equal(originalConfigs[i], File.ReadAllText(Path.Combine(projects[i].ProjectSystem.ProjectFullPath, "packages.config")));
                Assert.True(File.Exists(Path.Combine(projects[i].ProjectSystem.ProjectFullPath, "marker.txt")));
                Assert.Equal(packages[i].Identity, (await projects[i].GetInstalledPackagesAsync(CancellationToken.None)).Single().PackageIdentity);
            }
        }

        private sealed class PackageScriptTestContext : IDisposable
        {
            private readonly PowerShellScriptExecutionFeatureScope _feature = new PowerShellScriptExecutionFeatureScope("1");

            public TestDirectory Source { get; } = TestDirectory.Create();
            public TestSolutionManager Solution { get; } = new TestSolutionManager();
            public TestNuGetProjectContext Context { get; } = new TestNuGetProjectContext
            {
                TestExecutionContext = new TestExecutionContext(null) { PackageScripts = PackageScriptBehavior.Skip }
            };
            public SourceRepository Repository { get; }
            public NuGetPackageManager Manager { get; }

            public PackageScriptTestContext()
            {
                var provider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(new[] { new PackageSource(Source.Path) });
                var settings = new Settings(Source.Path);
                settings.AddOrUpdate(ConfigurationConstants.Config,
                    new AddItem("globalPackagesFolder", Path.Combine(Source.Path, "global-packages")));
                Repository = provider.GetRepositories().Single();
                Manager = new NuGetPackageManager(provider, settings, Solution, new TestDeleteOnRestartManager());
            }

            public Task InstallAsync(NuGetProject project, SimpleTestPackageContext package) =>
                Manager.InstallPackageAsync(project, package.Identity, new ResolutionContext(), Context,
                    new[] { Repository }, null, CancellationToken.None);

            public void Dispose()
            {
                Solution.Dispose();
                Source.Dispose();
                _feature.Dispose();
            }
        }

        private static SimpleTestPackageContext CreatePackage(string id, string script)
        {
            var package = new SimpleTestPackageContext(id);
            package.AddFile("lib/net45/_._");
            package.AddFile("content/marker.txt", id);
            package.AddFile(script);
            return package;
        }
    }
}
