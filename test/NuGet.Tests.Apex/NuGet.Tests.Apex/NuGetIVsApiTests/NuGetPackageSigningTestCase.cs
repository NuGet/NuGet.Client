// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.StaFact;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Tests.Apex
{
    public class NuGetPackageSigningTestCase : SharedVisualStudioHostTestClass, IClassFixture<VisualStudioHostFixtureFactory>
    {
        private TrustedTestCert<TestCertificate> _trustedTestCert;

        public NuGetPackageSigningTestCase(VisualStudioHostFixtureFactory visualStudioHostFixtureFactory)
            : base(visualStudioHostFixtureFactory)
        {
            _trustedTestCert = SigningTestUtility.GenerateTrustedTestCertificate();
        }

        [CIOnlyNuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void InstallSignedPackageFromPMC(ProjectTemplate projectTemplate)
        {
            var packageName = "TestPackage";
            var packageVersion = "1.0.0";

            using (var pathContext = new SimpleTestPathContext())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                Task.Run(async () =>
                {
                    var packageToSign = Utils.CreatePackage(packageName, packageVersion);
                    await SignedArchiveTestUtility.CreateSignedPackageAsync(testCertificate, packageToSign, pathContext.PackageSource, packageToSign.PackageName);
                }).GetAwaiter().GetResult();

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.Name);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion));
                project.Build();
                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [CIOnlyNuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void UninstallSignedPackageFromPMC(ProjectTemplate projectTemplate)
        {
            var packageName = "TestPackage";
            var packageVersion = "1.0.0";

            using (var pathContext = new SimpleTestPathContext())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                Task.Run(async () =>
                {
                    var packageToSign = Utils.CreatePackage(packageName, packageVersion);
                    await SignedArchiveTestUtility.CreateSignedPackageAsync(testCertificate, packageToSign, pathContext.PackageSource, packageToSign.PackageName);
                }).GetAwaiter().GetResult();

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.Name);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion));
                project.Build();

                Assert.True(nugetConsole.UninstallPackageFromPMC(packageName));
                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion));

                solutionService.Save();
                project.Build();
                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());

                nugetConsole.Clear();
            }
        }

        [CIOnlyNuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void UpdateUnsignedPackageToSignedVersionFromPMC(ProjectTemplate projectTemplate)
        {
            var packageName = "TestPackage";
            var packageVersion1 = "1.0.0";
            var packageVersion2 = "2.0.0";

            using (var pathContext = new SimpleTestPathContext())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion1);

                Task.Run(async () =>
                {
                    var packageToSign = Utils.CreatePackage(packageName, packageVersion2);
                    await SignedArchiveTestUtility.CreateSignedPackageAsync(testCertificate, packageToSign, pathContext.PackageSource, packageToSign.PackageName);
                }).GetAwaiter().GetResult();

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.UniqueName);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion1));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion1));
                project.Build();

                Assert.True(nugetConsole.UpdatePackageFromPMC(packageName, packageVersion2));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion2));
                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion1));
                project.Build();

                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [CIOnlyNuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void InstallSignedAndUnsignedPackagesFromPMC(ProjectTemplate projectTemplate)
        {
            var packageName2 = "TestPackage2";
            var packageVersion2 = "1.2.3";

            var packageName1 = "TestPackage1";
            var packageVersion1 = "1.0.0";

            using (var pathContext = new SimpleTestPathContext())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                Utils.CreatePackageInSource(pathContext.PackageSource, packageName1, packageVersion1);

                Task.Run(async () =>
                {
                    var packageToSign = Utils.CreatePackage(packageName2, packageVersion2);
                    await SignedArchiveTestUtility.CreateSignedPackageAsync(testCertificate, packageToSign, pathContext.PackageSource, packageToSign.PackageName);
                }).GetAwaiter().GetResult();

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.Name);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName1, packageVersion1));
                Assert.True(nugetConsole.InstallPackageFromPMC(packageName2, packageVersion2));
                project.Build();
                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName1, packageVersion1));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName2, packageVersion2));

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [CIOnlyNuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void UninstallSignedAndUnsignedPackagesFromPMC(ProjectTemplate projectTemplate)
        {
            var packageName2 = "TestPackage2";
            var packageVersion2 = "1.2.3";

            var packageName1 = "TestPackage1";
            var packageVersion1 = "1.0.0";

            using (var pathContext = new SimpleTestPathContext())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                Utils.CreatePackageInSource(pathContext.PackageSource, packageName1, packageVersion1);

                Task.Run(async () =>
                {
                    var packageToSign = Utils.CreatePackage(packageName2, packageVersion2);
                    await SignedArchiveTestUtility.CreateSignedPackageAsync(testCertificate, packageToSign, pathContext.PackageSource, packageToSign.PackageName);
                }).GetAwaiter().GetResult();

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.Name);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName1, packageVersion1));
                Assert.True(nugetConsole.InstallPackageFromPMC(packageName2, packageVersion2));
                project.Build();
                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName1, packageVersion1));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName2, packageVersion2));

                Assert.True(nugetConsole.UninstallPackageFromPMC(packageName1));
                Assert.True(nugetConsole.UninstallPackageFromPMC(packageName2));
                project.Build();
                solutionService.SaveAll();

                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName1, packageVersion1));
                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName2, packageVersion2));

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [CIOnlyNuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void DowngradeSignedPackageToUnsignedVersionFromPMC(ProjectTemplate projectTemplate)
        {
            var packageName = "TestPackage";
            var packageVersion1 = "1.0.0";
            var packageVersion2 = "2.0.0";

            using (var pathContext = new SimpleTestPathContext())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                Task.Run(async () =>
                {
                    var packageToSign = Utils.CreatePackage(packageName, packageVersion1);
                    await SignedArchiveTestUtility.CreateSignedPackageAsync(testCertificate, packageToSign, pathContext.PackageSource, packageToSign.PackageName);
                }).GetAwaiter().GetResult();

                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion2);

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.UniqueName);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion2));
                project.Build();
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion2));

                Assert.True(nugetConsole.UpdatePackageFromPMC(packageName, packageVersion1));
                project.Build();

                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion2));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion1));

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [CIOnlyNuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void InstallSignedPackageWithExpiredCertificateFromPMC(ProjectTemplate projectTemplate)
        {
            var packageName = "TestPackage";
            var packageVersion = "1.0.0";

            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                Task.Run(async () =>
                {
                    var packageToSign = Utils.CreatePackage(packageName, packageVersion);
                    var certWillExpire = SigningTestUtility.GenerateTrustedTestCertificateThatExpiresIn15Seconds();
                    using (var testCertificate = new X509Certificate2(certWillExpire.Source.Cert))
                    {
                        await SignedArchiveTestUtility.CreateSignedPackageAsync(testCertificate, packageToSign, pathContext.PackageSource, packageToSign.PackageName);
                    }

                    // Wait for cert to expire
                    Thread.Sleep(15000);
                }).GetAwaiter().GetResult();

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.Name);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion));
                project.Build();
                Assert.True(VisualStudio.HasNoErrorsInErrorList());
                Assert.True(VisualStudio.HasNoErrorsInOutputWindows());

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [CIOnlyNuGetWpfTheory]
        [InlineData(ProjectTemplate.ClassLibrary)]
        [InlineData(ProjectTemplate.NetCoreConsoleApp)]
        [InlineData(ProjectTemplate.NetStandardClassLib)]
        public void InstallSignedTamperedPackageFromPMCAndFail(ProjectTemplate projectTemplate)
        {
            var packageName = "TestPackage";
            var packageVersion = "1.0.0";

            using (var pathContext = new SimpleTestPathContext())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
                project.Build();

                Task.Run(async () =>
                {
                    var packageToSign = Utils.CreatePackage(packageName, packageVersion);
                    var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(testCertificate, packageToSign, pathContext.PackageSource, packageToSign.PackageName);
                    SignedArchiveTestUtility.TamperWithPackage(signedPackagePath);
                }).GetAwaiter().GetResult();

                var nugetTestService = GetNuGetTestService();
                Assert.True(nugetTestService.EnsurePackageManagerConsoleIsOpen());

                var nugetConsole = nugetTestService.GetPackageManagerConsole(project.UniqueName);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion));
                project.Build();

                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion));

                nugetConsole.Clear();
                solutionService.Save();
            }
        }
    }
}