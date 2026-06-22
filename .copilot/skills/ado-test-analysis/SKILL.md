---
name: ado-test-analysis
description: >-
  Fetch and analyze Azure DevOps CI test failures for NuGet.Client PRs, focusing on the NuGet.Client-VS pipeline.
  Use this skill when the user asks about CI test failures, ADO build results, "what's failing in CI",
  "analyze test failures", "check NuGet.Client-VS results", "fetch test results from ADO",
  "why is my build failing", or wants to understand and fix test failures from Azure DevOps pipelines.
  Also trigger when they mention "Apex test failures", "VS test failures", or "E2E test failures" in CI.
---

# Analyzing Azure DevOps Test Failures for NuGet.Client PRs

## Overview

NuGet.Client PRs run through several CI pipelines. The **NuGet.Client-VS** pipeline runs
Apex integration tests and E2E PowerShell tests inside Visual Studio. This skill helps
fetch, filter, and analyze those test results.

## Quick Start — Running the Script

The script at `.copilot/skills/ado-test-analysis/Get-AdoTestFailures.ps1` automates the
entire flow. It requires:

- **`az` CLI** authenticated (`az login`) with access to the `devdiv` Azure DevOps org
- **`gh` CLI** authenticated for GitHub PR lookups

### Basic usage (auto-detect PR from current branch):
```powershell
.\.copilot\skills\ado-test-analysis\Get-AdoTestFailures.ps1
```

### Specify a PR number:
```powershell
.\.copilot\skills\ado-test-analysis\Get-AdoTestFailures.ps1 -PrNumber 7467
```

### Show ALL failures (not just new ones from this PR):
```powershell
.\.copilot\skills\ado-test-analysis\Get-AdoTestFailures.ps1 -ShowAllFailures
```

### Get JSON output (for programmatic use):
```powershell
.\.copilot\skills\ado-test-analysis\Get-AdoTestFailures.ps1 -OutputJson
```

## Manual API Flow (for when the script can't be used)

### Prerequisites
- Get a token: `az account get-access-token --resource "499b84ac-1321-427f-aa17-267ca6975798" --query accessToken -o tsv`
- The ADO org is `devdiv`, project GUID is `0bdbc590-a062-4c3f-b0f6-9383f67865ee`

### Step 1: Find the Build ID
From a PR, use `gh pr checks <PR_NUMBER> --json name,state,link` and look for the
`NuGet.Client-VS` check. Extract `buildId` from the link URL parameter.

### Step 2: List Test Runs
```
GET https://dev.azure.com/devdiv/{project}/_apis/test/runs?buildUri=vstfs:///Build/Build/{buildId}&api-version=7.1
```
This returns all test runs for the build with counts of passed/failed/unanalyzed tests.

### Step 3: Get Failed Test Results
For each run with failures:
```
GET https://dev.azure.com/devdiv/{project}/_apis/test/runs/{runId}/results?outcomes=Failed&api-version=7.1
```
Returns detailed results including `testCaseTitle`, `automatedTestName`, `errorMessage`, `stackTrace`.

### Step 4: Filter to New Failures
Compare failed test names against `git diff dev...HEAD --name-only` to identify which
test files were changed in the PR. Only failures from those files are "new".

## Common NuGet.Client-VS Test Run Names

| Run Name | What It Tests |
|----------|--------------|
| `Apex Tests On Windows.NuGet.Tests.Apex` | C# Apex integration tests running in VS |
| `NuGet.Client EndToEnd Tests On Windows` | PowerShell E2E tests (GetPackageTest.ps1, etc.) |
| `Visual Studio Deployment (dtl-*)` | VS deployment validation (usually passes) |
| `CloudTest Internal Test Run for Group Logs` | Infrastructure logging (0 tests, ignore) |

## Common Failure Patterns

### Timeout Failures
- **Symptom**: "Test exceeded execution timeout period" or "Test timed out after 600 seconds"
- **Cause**: Apex tests have `[Timeout(DefaultTimeout)]` — the test took too long in VS
- **Fix**: Check if the test does too many sequential PMC operations. Consider splitting.
  Also check if VS is slow to restore packages from local source.

### MEF Composition Failures
- **Symptom**: `CompositionException` about cycles in `HostProcessLauncherService`
- **Cause**: This is a known Apex infrastructure flaky failure, NOT a test code issue.
  Previous test in the same VS session may have left state that corrupts MEF composition.
- **Fix**: Usually resolves on re-run. If consistent, ensure `TestCleanup` properly
  closes solutions and clears state.

### PMC Output Assertion Failures
- **Symptom**: `Expected string "..." to contain "..."` where PMC output doesn't have expected text
- **Cause**: The PMC command executed but output wasn't captured correctly, or the command
  produced unexpected output (e.g., `$pkg.IsUpdate` returned nothing instead of `False`).
- **Fix**: 
  1. Check if the cmdlet property actually exists — it may be a custom property that
     needs the NuGet PowerShell module loaded
  2. Check timing — `nugetConsole.Execute()` may return before output is ready
  3. Add `WaitForOutputContaining()` or similar synchronization

### Pre-existing / Flaky Failures
These tests fail regularly and are NOT caused by PR changes:
- `InstallPackagesConfigLocal` — known flaky
- `AmbiguousStartupProject` — environment-dependent
- `UwpPackageRefClassLibraryCreate` — UWP template availability
- `CreateVsPathContextUsesAssetsFileIfAvailable` — timeout flaky
- `BuildIntegratedProjectGetPackageTransitive` — TFM compatibility issues
- `WPFPackageVersionNoInclusiveLowerBoundNU1604` — environment-dependent

## Analyzing New Test Failures

When analyzing failures for new tests (added in the PR):

1. **Run the script** to get the filtered failure list
2. **Categorize each failure**:
   - **Infrastructure** (MEF, timeout, VS deployment) → likely flaky, re-run
   - **Assertion** (expected vs actual mismatch) → real bug in test or code
   - **Setup** (TestInitialize failure) → check test fixtures and project setup
3. **For assertion failures**, read the test source and the error message carefully:
   - Does the PMC command actually produce the expected output?
   - Is there a timing issue (output not ready when assertion runs)?
   - Does the assertion check the right thing?
4. **Cross-reference with unit tests** — if cmdlet unit tests pass but Apex tests fail,
   the issue is likely in VS integration (project system, source provider setup) not
   in the cmdlet logic itself.
