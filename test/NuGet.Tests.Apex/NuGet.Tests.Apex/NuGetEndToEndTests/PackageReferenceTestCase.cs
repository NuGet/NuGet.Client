// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Test.Apex.VisualStudio.Solution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Test.Utility;

namespace NuGet.Tests.Apex
{
    [TestClass]
    public class PackageReferenceTestCase : SharedVisualStudioHostTestClass
    {
        // Legacy PackageReference: a classic (non-SDK) csproj that uses PackageReference format.
        // Requires calling simpleTestPathContext.Settings.SetPackageFormatToPackageReference() before creating the project.
        [DataTestMethod]
        [DynamicData(nameof(GetLegacyPackageReferenceTemplates), DynamicDataSourceType.Method)]
        [Timeout(DefaultTimeout)]
        public async Task InstallPackage_PMC(ProjectTemplate projectTemplate)
        {
            await InstallPackage_PMCAsync(projectTemplate);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetLegacyPackageReferenceTemplates), DynamicDataSourceType.Method)]
        [Timeout(DefaultTimeout)]
        public async Task UpdatePackage_PMC(ProjectTemplate projectTemplate)
        {
            await UpdatePackage_PMCAsync(projectTemplate);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetLegacyPackageReferenceTemplates), DynamicDataSourceType.Method)]
        [Timeout(DefaultTimeout)]
        public async Task UninstallPackage_PMC(ProjectTemplate projectTemplate)
        {
            await UninstallPackage_PMCAsync(projectTemplate);
        }

        public async Task InstallPackage_PMCAsync(ProjectTemplate projectTemplate)
        {
            using (var simpleTestPathContext = new SimpleTestPathContext())
            {
                // Arrange
                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                await CommonUtility.CreatePackageInSourceAsync(simpleTestPathContext.PackageSource, packageName, packageVersion);
                simpleTestPathContext.Settings.SetPackageFormatToPackageReference();

                using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, Logger, simpleTestPathContext: simpleTestPathContext))
                {
                    VisualStudio.AssertNoErrors();
                    testContext.SolutionService.Build();

                    // Act
                    var nugetConsole = GetConsole(testContext.Project);

                    nugetConsole.InstallPackageFromPMC(packageName, packageVersion);
                    testContext.SolutionService.Build();
                    testContext.NuGetApexTestService.WaitForAutoRestore();

                    // Assert
                    VisualStudio.AssertNuGetOutputDoesNotHaveErrors();
                    CommonUtility.AssertPackageInAssetsFile(VisualStudio, testContext.Project, packageName, packageVersion, Logger);
                    Assert.IsTrue(VisualStudio.HasNoErrorsInOutputWindows());
                }
            }
        }

        public async Task UpdatePackage_PMCAsync(ProjectTemplate projectTemplate)
        {
            using (var simpleTestPathContext = new SimpleTestPathContext())
            {
                // Arrange
                var packageName = "TestPackage";
                var packageVersion1 = "1.0.0";
                var packageVersion2 = "2.0.0";

                await CommonUtility.CreatePackageInSourceAsync(simpleTestPathContext.PackageSource, packageName, packageVersion1);
                await CommonUtility.CreatePackageInSourceAsync(simpleTestPathContext.PackageSource, packageName, packageVersion2);
                simpleTestPathContext.Settings.SetPackageFormatToPackageReference();

                using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, Logger, simpleTestPathContext: simpleTestPathContext))
                {
                    VisualStudio.AssertNoErrors();
                    testContext.SolutionService.Build();

                    // Act
                    var nugetConsole = GetConsole(testContext.Project);

                    nugetConsole.InstallPackageFromPMC(packageName, packageVersion1);
                    testContext.SolutionService.Build();
                    testContext.NuGetApexTestService.WaitForAutoRestore();

                    nugetConsole.UpdatePackageFromPMC(packageName, packageVersion2);
                    testContext.SolutionService.Build();
                    testContext.NuGetApexTestService.WaitForAutoRestore();

                    // Assert
                    VisualStudio.AssertNuGetOutputDoesNotHaveErrors();
                    CommonUtility.AssertPackageInAssetsFile(VisualStudio, testContext.Project, packageName, packageVersion2, Logger);
                    Assert.IsTrue(VisualStudio.HasNoErrorsInOutputWindows());
                }
            }
        }

        public async Task UninstallPackage_PMCAsync(ProjectTemplate projectTemplate)
        {
            using (var simpleTestPathContext = new SimpleTestPathContext())
            {
                // Arrange
                var packageName = "TestPackage";
                var packageVersion = "1.0.0";

                await CommonUtility.CreatePackageInSourceAsync(simpleTestPathContext.PackageSource, packageName, packageVersion);
                simpleTestPathContext.Settings.SetPackageFormatToPackageReference();

                using (var testContext = new ApexTestContext(VisualStudio, projectTemplate, Logger, simpleTestPathContext: simpleTestPathContext))
                {
                    VisualStudio.AssertNoErrors();
                    testContext.SolutionService.Build();
                    testContext.NuGetApexTestService.WaitForAutoRestore();

                    // Act
                    var nugetConsole = GetConsole(testContext.Project);

                    nugetConsole.InstallPackageFromPMC(packageName, packageVersion);
                    testContext.SolutionService.Build();
                    testContext.NuGetApexTestService.WaitForAutoRestore();

                    nugetConsole.UninstallPackageFromPMC(packageName);
                    testContext.SolutionService.Build();
                    testContext.NuGetApexTestService.WaitForAutoRestore();

                    // Assert
                    VisualStudio.AssertNuGetOutputDoesNotHaveErrors();
                    CommonUtility.AssertPackageNotInAssetsFile(VisualStudio, testContext.Project, packageName, packageVersion, Logger);
                    Assert.IsTrue(VisualStudio.HasNoErrorsInOutputWindows());
                }
            }
        }

        // Legacy PackageReference: a classic (non-SDK) csproj that uses PackageReference format.
        // Requires calling simpleTestPathContext.Settings.SetPackageFormatToPackageReference() before creating the project.
        public static IEnumerable<object[]> GetLegacyPackageReferenceTemplates()
        {
            yield return new object[] { ProjectTemplate.ConsoleApplication };
        }
    }
}
