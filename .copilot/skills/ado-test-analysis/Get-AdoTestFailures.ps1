<#
.SYNOPSIS
    Fetches test failures from an Azure DevOps build for a NuGet.Client PR.

.DESCRIPTION
    Uses the Azure DevOps REST API (authenticated via `az` CLI) to fetch test run
    results from a build, then filters for failures. Can also cross-reference with
    git diff to identify which failures are from NEW tests added in the PR.

.PARAMETER PrNumber
    The GitHub PR number. If omitted, auto-detects from current branch via `gh`.

.PARAMETER BuildId
    The ADO build ID. If omitted, auto-detects from PR checks (looks for NuGet.Client-VS).

.PARAMETER PipelineName
    The name of the CI check to look for. Defaults to "NuGet.Client-VS".

.PARAMETER ShowAllFailures
    If set, shows all failures, not just new ones from this PR.

.PARAMETER OutputJson
    If set, outputs structured JSON instead of formatted text.

.EXAMPLE
    .\Get-AdoTestFailures.ps1
    # Auto-detect PR from current branch, find NuGet.Client-VS build, show new failures

.EXAMPLE
    .\Get-AdoTestFailures.ps1 -PrNumber 7467 -ShowAllFailures
    # Show all failures for PR #7467
#>
[CmdletBinding()]
param(
    [int]$PrNumber,
    [int]$BuildId,
    [string]$PipelineName = "NuGet.Client-VS",
    [switch]$ShowAllFailures,
    [switch]$OutputJson
)

$ErrorActionPreference = "Stop"

# --- Helper: get ADO auth token ---
function Get-AdoToken {
    $token = az account get-access-token --resource "499b84ac-1321-427f-aa17-267ca6975798" --query accessToken -o tsv 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to get ADO token. Run 'az login' first. Error: $token"
    }
    return $token
}

# --- Helper: ADO REST call ---
function Invoke-AdoApi {
    param([string]$Url, [hashtable]$Headers)
    $response = Invoke-RestMethod -Uri $Url -Headers $Headers -Method Get -ErrorAction Stop
    return $response
}

# --- Step 1: Resolve PR number ---
if (-not $PrNumber) {
    $branch = git branch --show-current
    $prJson = gh pr list --head $branch --json number,title --limit 1 | ConvertFrom-Json
    if (-not $prJson -or $prJson.Count -eq 0) {
        throw "No PR found for branch '$branch'. Specify -PrNumber explicitly."
    }
    $PrNumber = $prJson[0].number
    Write-Host "Auto-detected PR #$PrNumber from branch '$branch'" -ForegroundColor Cyan
}

# --- Step 2: Resolve Build ID ---
if (-not $BuildId) {
    $checks = gh pr checks $PrNumber --json name,state,link 2>&1 | ConvertFrom-Json
    $vsCheck = $checks | Where-Object { $_.name -eq $PipelineName }
    if (-not $vsCheck) {
        throw "No '$PipelineName' check found for PR #$PrNumber. Available: $($checks.name -join ', ')"
    }

    # Extract build ID from link like: https://devdiv.visualstudio.com/.../_build/results?buildId=14404919
    if ($vsCheck.link -match 'buildId=(\d+)') {
        $BuildId = [int]$Matches[1]
    } else {
        throw "Could not extract buildId from check link: $($vsCheck.link)"
    }
    Write-Host "Build ID: $BuildId (state: $($vsCheck.state))" -ForegroundColor Cyan
}

# --- Step 3: Extract org/project from link or use defaults ---
$org = "devdiv"
$project = "0bdbc590-a062-4c3f-b0f6-9383f67865ee"

# --- Step 4: Get auth token ---
$token = Get-AdoToken
$headers = @{ Authorization = "Bearer $token" }

# --- Step 5: Fetch test runs for this build ---
$runsUrl = "https://dev.azure.com/$org/$project/_apis/test/runs?buildUri=vstfs:///Build/Build/$BuildId&api-version=7.1"
$runs = Invoke-AdoApi -Url $runsUrl -Headers $headers

Write-Host "`nTest Runs for Build $BuildId`:" -ForegroundColor Yellow
$runs.value | ForEach-Object {
    $failed = $_.totalTests - $_.passedTests - $_.unanalyzedTests
    $status = if ($failed -gt 0 -or $_.unanalyzedTests -gt 0) { "FAIL" } else { "PASS" }
    Write-Host ("  [{0}] {1}: {2} total, {3} passed, {4} unanalyzed" -f $status, $_.name, $_.totalTests, $_.passedTests, $_.unanalyzedTests) -ForegroundColor $(if ($status -eq "FAIL") { "Red" } else { "Green" })
}

# --- Step 6: Fetch all failed test results ---
$allFailures = @()
foreach ($run in $runs.value) {
    if ($run.unanalyzedTests -gt 0 -or ($run.totalTests - $run.passedTests) -gt 0) {
        $resultsUrl = "https://dev.azure.com/$org/$project/_apis/test/runs/$($run.id)/results?outcomes=Failed&api-version=7.1"
        $results = Invoke-AdoApi -Url $resultsUrl -Headers $headers
        foreach ($r in $results.value) {
            $allFailures += [PSCustomObject]@{
                RunId             = $run.id
                RunName           = $run.name
                TestName          = $r.testCaseTitle
                AutomatedTestName = $r.automatedTestName
                Outcome           = $r.outcome
                ErrorMessage      = $r.errorMessage
                StackTrace        = $r.stackTrace
                DurationMs        = $r.durationInMs
            }
        }
    }
}

Write-Host "`nTotal failed tests: $($allFailures.Count)" -ForegroundColor $(if ($allFailures.Count -gt 0) { "Red" } else { "Green" })

# --- Step 7: Identify new tests from this PR ---
if (-not $ShowAllFailures) {
    # Get the list of files changed in this PR
    $changedFiles = git --no-pager diff dev...HEAD --name-only

    # Get new test names from changed files (look for test methods)
    $newTestNames = @()
    foreach ($file in $changedFiles) {
        if ($file -match '\.(cs|ps1)$' -and (Test-Path $file)) {
            $content = Get-Content $file -Raw
            # Match C# test methods
            $csMatches = [regex]::Matches($content, '\[TestMethod\]\s*(?:\[.*?\]\s*)*public\s+(?:async\s+)?(?:Task|void)\s+(\w+)')
            foreach ($m in $csMatches) {
                $newTestNames += $m.Groups[1].Value
            }
            # Match PS test functions
            $psMatches = [regex]::Matches($content, 'function\s+([\w-]+)')
            foreach ($m in $psMatches) {
                $newTestNames += $m.Groups[1].Value
            }
        }
    }

    $newTestNames = $newTestNames | Sort-Object -Unique

    # Filter failures to only new tests
    $newFailures = $allFailures | Where-Object {
        $testShort = $_.TestName -replace '.*\.', ''
        $newTestNames -contains $testShort -or $newTestNames -contains $_.TestName
    }

    $existingFailures = $allFailures | Where-Object {
        $testShort = $_.TestName -replace '.*\.', ''
        -not ($newTestNames -contains $testShort -or $newTestNames -contains $_.TestName)
    }

    Write-Host "`nNew test failures (from this PR): $($newFailures.Count)" -ForegroundColor $(if ($newFailures.Count -gt 0) { "Red" } else { "Green" })
    Write-Host "Existing/pre-existing failures:   $($existingFailures.Count)" -ForegroundColor DarkGray
} else {
    $newFailures = $allFailures
}

# --- Step 8: Output results ---
if ($OutputJson) {
    $output = @{
        PrNumber         = $PrNumber
        BuildId          = $BuildId
        PipelineName     = $PipelineName
        TotalFailures    = $allFailures.Count
        NewFailures      = $newFailures
        ExistingFailures = if ($existingFailures) { $existingFailures } else { @() }
    }
    $output | ConvertTo-Json -Depth 5
} else {
    if ($newFailures.Count -gt 0) {
        Write-Host "`n=== NEW TEST FAILURES ===" -ForegroundColor Red
        foreach ($f in $newFailures) {
            Write-Host "`n--- $($f.TestName) ---" -ForegroundColor Yellow
            Write-Host "  Run:   $($f.RunName)"
            Write-Host "  Error: " -NoNewline
            if ($f.ErrorMessage) {
                # Truncate long errors for readability
                $errLines = $f.ErrorMessage -split "`n" | Select-Object -First 5
                Write-Host ($errLines -join "`n         ")
            } else {
                Write-Host "(no error message)"
            }
            if ($f.StackTrace) {
                Write-Host "  Stack (first 3 lines):"
                $stackLines = $f.StackTrace -split "`n" | Select-Object -First 3
                foreach ($sl in $stackLines) {
                    Write-Host "    $($sl.Trim())"
                }
            }
        }
    } else {
        Write-Host "`nNo new test failures from this PR!" -ForegroundColor Green
    }

    if ($existingFailures -and $existingFailures.Count -gt 0) {
        Write-Host "`n=== PRE-EXISTING FAILURES (not from this PR) ===" -ForegroundColor DarkGray
        foreach ($f in $existingFailures) {
            Write-Host "  - $($f.TestName)" -ForegroundColor DarkGray
        }
    }
}
