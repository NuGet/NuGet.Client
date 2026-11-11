// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Test.Apex.VisualStudio.Solution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Test.Utility;

namespace NuGet.Tests.Apex
{
    [TestClass]
    public class NuGetConsoleScriptExecutionTestCase : SharedVisualStudioHostTestClass
    {
        [DataTestMethod]
        [DataRow("Fail")]
        [DataRow("Run")]
        [DataRow("RunWithInit")]
        [DataRow("Skip")]
        [Timeout(DefaultTimeout)]
        public async Task UninstallPackageScriptsFromPMCRespectsOperationPolicyAsync(string behavior)
        {
            using var pathContext = new SimpleTestPathContext();
            pathContext.Settings.SetPackageFormatToPackagesConfig();
            using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger, simpleTestPathContext: pathContext);
            var package = new SimpleTestPackageContext("ScriptPolicy", "1.0.0");
            package.AddFile("tools/init.ps1", "$global:PackageScriptLog += @('init')");
            package.AddFile("tools/uninstall.ps1", "$global:PackageScriptLog += @('uninstall')");
            await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, package);
            var console = GetConsole(testContext.Project);
            console.Execute("$global:PackageScriptLog = @()");
            var source = pathContext.PackageSource.Replace("'", "''");
            console.Execute($"Install-Package ScriptPolicy -Version 1.0.0 -Source '{source}'");
            CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, package.Id, package.Version, Logger);
            console.Execute("$global:PackageScriptLog = @()");
            // Omit the parameter for Fail to exercise the default policy.
            var argument = behavior == "Fail" ? "" : $" -PackageScripts {behavior}";
            console.Execute($"try {{ Uninstall-Package ScriptPolicy{argument} -ErrorAction Stop }} " +
                "catch { Write-Host ('uninstall-blocked:' + $_.Exception.Message) }");
            var isBlocked = ControlPowerShellScriptExecutionEnabled && behavior == "Fail";
            var expectedLog = !ControlPowerShellScriptExecutionEnabled || behavior == "RunWithInit" || behavior == "Run"
                ? "uninstall" : "";
            AssertState("after-uninstall", isBlocked ? 1 : 0, expectedLog);
            if (isBlocked)
            {
                Assert.IsTrue(console.IsMessageFoundInPMC("-PackageScripts Run"), console.GetText());
                Assert.IsTrue(console.IsMessageFoundInPMC("uninstall.ps1"), console.GetText());
                console.Execute("Uninstall-Package ScriptPolicy -PackageScripts Skip");
                AssertState("after-skipped-uninstall", 0, "");
            }

            void AssertState(string stage, int packageCount, string scriptLog)
            {
                console.Execute($"Write-Host ('{stage}-packages:' + @(Get-Package | Where-Object Id -eq ScriptPolicy).Count); " +
                    $"Write-Host ('{stage}-scripts:[' + ($global:PackageScriptLog -join ',') + ']')");
                Assert.IsTrue(console.IsMessageFoundInPMC($"{stage}-packages:{packageCount}"), console.GetText());
                Assert.IsTrue(console.IsMessageFoundInPMC($"{stage}-scripts:[{scriptLog}]"), console.GetText());
            }
        }

        [DataTestMethod]
        [DataRow(ProjectTemplate.ConsoleApplication)]
        [DataRow(ProjectTemplate.NetCoreConsoleApp)]
        [Timeout(DefaultTimeout)]
        public async Task ImportPackageInitScriptFromPMCRespectsFeatureAsync(ProjectTemplate projectTemplate)
        {
            using var pathContext = new SimpleTestPathContext();
            if (projectTemplate == ProjectTemplate.ConsoleApplication)
            {
                pathContext.Settings.SetPackageFormatToPackagesConfig();
            }

            using var testContext = new ApexTestContext(VisualStudio, projectTemplate, Logger, simpleTestPathContext: pathContext);
            var first = new SimpleTestPackageContext("ExplicitInitScript", "1.0.0");
            first.AddFile("tools/init.ps1", "$global:ExplicitInitCount++; function global:Get-ExplicitInitVersion { 'version-one' }");
            var second = new SimpleTestPackageContext("ExplicitInitScript", "2.0.0");
            second.AddFile("tools/init.ps1", "$global:ExplicitInitCount++; function global:Get-ExplicitInitVersion { 'version-two' }");
            await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, first, second);

            var console = GetConsole(testContext.Project);
            console.Execute("$global:ExplicitInitCount = 0; Remove-Item function:\\Get-ExplicitInitVersion -ErrorAction Ignore");
            console.InstallPackageFromPMC(first.Id, first.Version);
            AssertInstalled(first);
            AssertInitState("after-install", ControlPowerShellScriptExecutionEnabled ? 0 : 1,
                ControlPowerShellScriptExecutionEnabled ? "not-initialized" : "version-one");

            if (ControlPowerShellScriptExecutionEnabled)
            {
                console.Execute("Get-PackageInitScript | Out-Null");
                AssertInitState("after-discovery", 0, "not-initialized");
                console.Execute("Get-PackageInitScript | Import-PackageInitScript");
            }
            else
            {
                console.Execute("try { Get-PackageInitScript -ErrorAction Stop } catch { Write-Host ('get-disabled:' + $_.FullyQualifiedErrorId) }");
                console.Execute("try { Import-PackageInitScript -ErrorAction Stop } catch { Write-Host ('import-disabled:' + $_.FullyQualifiedErrorId) }");
                Assert.IsTrue(console.IsMessageFoundInPMC("get-disabled:NuGetPackageInitScriptFeatureDisabled"), console.GetText());
                Assert.IsTrue(console.IsMessageFoundInPMC("import-disabled:NuGetPackageInitScriptFeatureDisabled"), console.GetText());
            }
            AssertInitState("after-commands", 1, "version-one");

            console.UpdatePackageFromPMC(second.Id, second.Version);
            AssertInstalled(second);
            AssertInitState("after-update", ControlPowerShellScriptExecutionEnabled ? 1 : 2,
                ControlPowerShellScriptExecutionEnabled ? "version-one" : "version-two");

            if (ControlPowerShellScriptExecutionEnabled)
            {
                console.Execute("Import-PackageInitScript -Packages ExplicitInitScript@2.0.0");
                AssertInitState("after-updated-import", 2, "version-two");
            }

            var solutionPath = testContext.SolutionService.FilePath;
            Assert.IsNotNull(solutionPath, console.GetText());
            testContext.SolutionService.SaveAndClose();
            console.Execute("$global:ExplicitInitCount = 0; Remove-Item function:\\Get-ExplicitInitVersion -ErrorAction Ignore");
            testContext.SolutionService.Open(solutionPath);
            GetNuGetTestService().WaitForAutoRestore();
            AssertInitState("after-reopen", ControlPowerShellScriptExecutionEnabled ? 0 : 1,
                ControlPowerShellScriptExecutionEnabled ? "not-initialized" : "version-two");

            if (ControlPowerShellScriptExecutionEnabled)
            {
                console.Execute("Import-PackageInitScript -Packages ExplicitInitScript@2.0.0");
                AssertInitState("after-reopened-import", 1, "version-two");
            }

            void AssertInstalled(SimpleTestPackageContext package)
            {
                if (projectTemplate == ProjectTemplate.NetCoreConsoleApp)
                {
                    CommonUtility.AssertPackageInAssetsFile(VisualStudio, testContext.Project, package.Id, package.Version, Logger);
                }
                else
                {
                    CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, package.Id, package.Version, Logger);
                }
            }

            void AssertInitState(string stage, int expectedCount, string expectedVersion)
            {
                console.Execute($"Write-Host ('{stage}-count:' + $global:ExplicitInitCount); " +
                    $"Write-Host ('{stage}-version:' + $(if (Test-Path function:\\Get-ExplicitInitVersion) {{ Get-ExplicitInitVersion }} else {{ 'not-initialized' }}))");
                Assert.IsTrue(console.IsMessageFoundInPMC($"{stage}-count:{expectedCount}"), console.GetText());
                Assert.IsTrue(console.IsMessageFoundInPMC($"{stage}-version:{expectedVersion}"), console.GetText());
            }
        }
    }

    [TestClass]
    public class NuGetConsoleLegacyScriptExecutionTestCase : NuGetConsoleScriptExecutionTestCase
    {
        protected override bool ControlPowerShellScriptExecutionEnabled => false;
    }
}
