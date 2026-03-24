# Instructions

## General Guidelines

- When creating pull requests, always follow the [PR template](PULL_REQUEST_TEMPLATE.md).
- Always format before submitting a pull request.

## Coding Standards

- Use the following coding guidelines: https://github.com/NuGet/NuGet.Client/blob/dev/docs/coding-guidelines.md
- Never use reflection.

## Project-Specific Rules

- All files in the repository are nullable by default (project-level nullable enable). No need to add `#nullable enable` directives to individual files.

## Migrating PowerShell E2E Tests to Apex Tests

### Overview

PowerShell E2E tests live in `test/EndToEnd/tests/`. Apex tests live in `test/NuGet.Tests.Apex/NuGet.Tests.Apex/NuGetEndToEndTests/`. The goal is to migrate PS tests to C# Apex tests that run the **exact same scenario**, then remove the PS test function.

### Template mapping

| PowerShell function | Apex `ProjectTemplate` | Package management |
|---|---|---|
| `New-ConsoleApplication` | `ProjectTemplate.ConsoleApplication` | packages.config |
| `New-ClassLibrary` | `ProjectTemplate.ClassLibrary` | packages.config |
| `New-WebSite` | `ProjectTemplate.WebSiteEmpty` | packages.config |
| `New-NetCoreConsoleApp` | `ProjectTemplate.NetCoreConsoleApp` | PackageReference |
| `New-NetStandardClassLib` | `ProjectTemplate.NetStandardClassLib` | PackageReference |

WebApplication, WPFApplication, MvcApplication, and FSharpLibrary have no direct Apex equivalents — skip those tests.

### Command execution

| Scenario | Apex API |
|---|---|
| Standard install with `-Version` | `nugetConsole.InstallPackageFromPMC(packageName, packageVersion)` |
| Install with extra flags (`-Source`, `-WhatIf`, `-IgnoreDependencies`) | `nugetConsole.Execute($"Install-Package {packageName} -ProjectName {project.Name} -Source {source}")` |
| Standard uninstall | `nugetConsole.UninstallPackageFromPMC(packageName)` |
| Standard update | `nugetConsole.UpdatePackageFromPMC(packageName, packageVersion)` |
| Any raw PMC command | `nugetConsole.Execute(command)` |

**Rule:** If the PS test does not use `-Version`, use `Execute()` with the raw command string. `InstallPackageFromPMC()` always adds `-Version`.

### Assertion mapping

| PowerShell assertion | Apex equivalent |
|---|---|
| `Assert-Package $p PackageName Version` (packages.config project) | `CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, packageName, version, Logger)` |
| `Assert-Package $p PackageName` (no version, packages.config) | `CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, packageName, Logger)` |
| `Assert-Package $p PackageName Version` (PackageReference project) | `CommonUtility.AssertPackageInAssetsFile(VisualStudio, testContext.Project, packageName, version, Logger)` |
| `Assert-Throws { ... } $expectedMessage` | `nugetConsole.IsMessageFoundInPMC(expectedMessage)` — PMC errors appear as text, not C# exceptions |
| `Assert-Null (Get-ProjectPackage ...)` / package not installed | `CommonUtility.AssertPackageNotInPackagesConfig(VisualStudio, testContext.Project, packageName, Logger)` |

### Package source mapping

| PowerShell source | Apex equivalent |
|---|---|
| `$context.RepositoryRoot` or `$context.RepositoryPath` | `testContext.PackageSource` — create packages with `CommonUtility.CreatePackageInSourceAsync()` |
| No `-Source` (uses nuget.org) | Create a local package with `CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, ...)` — never depend on nuget.org |
| Hardcoded invalid sources (`http://example.com`, `ftp://...`) | Use the same hardcoded strings directly |

### Test structure patterns

**Error-path tests** (no package creation needed) — synchronous:
```csharp
[TestMethod]
[Timeout(DefaultTimeout)]
public void DescriptiveTestName_Fails()
{
    using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger);

    var packageName = "Rules";
    var source = @"c:\temp\data";
    var expectedMessage = $"Unable to find package '{packageName}' at source '{source}'. Source not found.";

    var nugetConsole = GetConsole(testContext.Project);
    nugetConsole.Execute($"Install-Package {packageName} -ProjectName {testContext.Project.Name} -Source {source}");

    Assert.IsTrue(
        nugetConsole.IsMessageFoundInPMC(expectedMessage),
        $"Expected error message was not found in PMC output. Actual output: {nugetConsole.GetText()}");
}
```

**Success-path tests** (need package creation) — async:
```csharp
[TestMethod]
[Timeout(DefaultTimeout)]
public async Task DescriptiveTestNameAsync(/* or [DataTestMethod] with ProjectTemplate */)
{
    using var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, Logger);

    var packageName = "TestPackage";
    var packageVersion = "1.0.0";
    await CommonUtility.CreatePackageInSourceAsync(testContext.PackageSource, packageName, packageVersion);

    var nugetConsole = GetConsole(testContext.Project);
    nugetConsole.InstallPackageFromPMC(packageName, packageVersion);

    CommonUtility.AssertPackageInPackagesConfig(VisualStudio, testContext.Project, packageName, packageVersion, Logger);
}
```

### Style rules

- Use `using var` (inline declaration), not `using (var ...) { }`.
- Place migrated tests before the static helper methods (`GetNetCoreTemplates`, etc.) in the file.
- Method names: `{Action}FromPMC{Scenario}[_Fails|Async]`. Suffix with `_Fails` for error tests, `Async` for async tests.
- Always include `[Timeout(DefaultTimeout)]`.
- Include `nugetConsole.GetText()` in assertion failure messages for diagnostics.

### Tests that should NOT be migrated

Skip PS tests that:
- Use `WebApplication`/`WPFApplication`/`MvcApplication`/`FSharpLibrary` templates with no Apex equivalent (unless the scenario can be tested with `ConsoleApplication`, `ClassLibrary`, or `WebSiteEmpty`).
- Assert PS script execution (`Test-Path function:\Get-World`, `init.ps1`, `install.ps1`).
- Use `Assert-BindingRedirect` — binding redirect tests are already `[SkipTest]` in PS and not worth migrating.
- Depend on `Get-VSComponentModel` or `ISettings` manipulation (NuGet config changes at runtime).
- Use `Get-ProjectItem`, `Get-ProjectItemPath`, or other VS DTE project-item inspection not available in Apex.
- Create `New-SolutionFolder` or multi-project topologies not supported by `ApexTestContext`.
- Use `$context.TestRoot` for relative path manipulation that's specific to the E2E runner.

### After migration

1. Remove the migrated function from the PS test file.
2. If a PS test is already covered by an existing Apex test (duplicate), just delete the PS test — no new Apex test needed.
3. Verify with `get_errors` that the Apex file compiles cleanly.
