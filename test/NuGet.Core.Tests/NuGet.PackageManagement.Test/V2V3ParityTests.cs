﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using Test.Utility;
using Xunit;
using NuGet.Protocol.Core.Types;
using Xunit.Abstractions;
using NuGet.Test.Utility;

namespace NuGet.Test
{
    public class V2V3ParityTests
    {
        ITestOutputHelper _output;

        public V2V3ParityTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private async Task<IEnumerable<NuGetProjectAction>> PacManCleanInstall(SourceRepositoryProvider sourceRepositoryProvider, PackageIdentity target)
        {
            // Arrange
            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomPackagesConfigFolderPath = TestDirectory.Create())
            {
                var testSettings = NullSettings.Instance;
                var token = CancellationToken.None;
                var deleteOnRestartManger = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManger);
                var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

                var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);

                // Act
                var nugetProjectActions = await nuGetPackageManager.PreviewInstallPackageAsync(msBuildNuGetProject, target,
                    new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

                return nugetProjectActions;
            }
        }

        private bool Compare(IEnumerable<NuGetProjectAction> x, IEnumerable<NuGetProjectAction> y)
        {
            var xyExcept = x.Except(y, new NuGetProjectActionComparer()).ToList();

            _output.WriteLine("xyExcept:");
            foreach (var entry in xyExcept)
            {
                _output.WriteLine("{0} {1}", entry.NuGetProjectActionType, entry.PackageIdentity.ToString());
            }

            var yxExcept = y.Except(x, new NuGetProjectActionComparer()).ToList();

            _output.WriteLine("yxExcept:");
            foreach (var entry in yxExcept)
            {
                _output.WriteLine("{0} {1}", entry.NuGetProjectActionType, entry.PackageIdentity.ToString());
            }

            return (xyExcept.Count() == 0 && yxExcept.Count() == 0);
        }

        [Fact]
        public async Task TestPacManCleanInstall()
        {
            var target = new PackageIdentity("Umbraco", NuGetVersion.Parse("5.1.0.175"));

            _output.WriteLine("target: {0}", target);

            var actionsV2 = await PacManCleanInstall(TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider(), target);
            var actionsV3 = await PacManCleanInstall(TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider(), target);

            Assert.True(Compare(actionsV2, actionsV3));
        }

        class NuGetProjectActionComparer : IEqualityComparer<NuGetProjectAction>
        {
            public bool Equals(NuGetProjectAction x, NuGetProjectAction y)
            {
                var packageIdentityEquals = x.PackageIdentity.Equals(y.PackageIdentity);
                var NuGetProjectActionTypeEquals = x.NuGetProjectActionType == y.NuGetProjectActionType;

                return packageIdentityEquals & NuGetProjectActionTypeEquals;
            }

            public int GetHashCode(NuGetProjectAction obj)
            {
                var combiner = new NuGet.Shared.HashCodeCombiner();
                combiner.AddObject(obj.PackageIdentity.GetHashCode());
                combiner.AddObject(obj.NuGetProjectActionType);
                return combiner.CombinedHash;
            }
        }
    }
}
