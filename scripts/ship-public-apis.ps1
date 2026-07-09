<#
.SYNOPSIS
    Runs the ship-public-apis tool and opens a pull request that references
    https://github.com/NuGet/Client.Engineering/issues/3874 as progress.

.DESCRIPTION
    This script moves all APIs from every PublicAPI.Unshipped.txt into the
    corresponding PublicAPI.Shipped.txt by invoking the repo's ship-public-apis
    tool (tools-local\ship-public-apis\ship-public-apis.csproj). When the tool
    succeeds and produces changes, the script creates a topic branch, commits the
    changes, pushes it, and opens a pull request via the GitHub CLI (gh).

    Guardrails:
      * Must be run from a release/* branch (per docs\nuget-sdk.md, shipping is
        done on the release branch).
      * Requires the GitHub CLI (gh) to be installed and authenticated.

.PARAMETER Issue
    The tracking issue the PR should reference as progress
    (e.g. https://github.com/NuGet/Client.Engineering/issues/3874).

.PARAMETER Draft
    Creates the pull request as a draft.

.EXAMPLE
    .\scripts\Ship-PublicApis.ps1 -Issue https://github.com/NuGet/Client.Engineering/issues/3874

.EXAMPLE
    .\scripts\Ship-PublicApis.ps1 -Issue https://github.com/NuGet/Client.Engineering/issues/3874 -Draft
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Issue,
    [switch] $Draft
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Native {
    param(
        [Parameter(Mandatory)][string] $Exe,
        [Parameter(ValueFromRemainingArguments)][string[]] $Arguments
    )
    & $Exe @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed (exit $LASTEXITCODE): $Exe $($Arguments -join ' ')"
    }
}

# --- Resolve repo root (this script lives in <root>\scripts) ---------------
$repoRoot = Split-Path -Parent $PSScriptRoot
$toolProject = Join-Path $repoRoot 'tools-local\ship-public-apis\ship-public-apis.csproj'
if (-not (Test-Path $toolProject)) {
    throw "Could not find ship-public-apis tool at '$toolProject'."
}

# --- Validate required tooling: git, dotnet, gh ----------------------------
foreach ($tool in @('git', 'dotnet', 'gh')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "Required command '$tool' was not found on PATH. Please install it and try again."
    }
}

# gh must be authenticated.
& gh auth status *> $null
if ($LASTEXITCODE -ne 0) {
    throw "GitHub CLI is not authenticated. Run 'gh auth login' and try again."
}

Push-Location $repoRoot
try {
    # --- Validate we are on a release/* branch -----------------------------
    $currentBranch = (& git rev-parse --abbrev-ref HEAD).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to determine the current git branch. Are you inside a git repository?"
    }
    if ($currentBranch -notlike 'release/*') {
        throw "This script must be run from a 'release/*' branch. Current branch is '$currentBranch'."
    }
    Write-Host "On release branch '$currentBranch'." -ForegroundColor Green

    # --- Require a clean PublicAPI surface so the PR only contains -----------
    # --- freshly-shipped APIs (unrelated changes elsewhere are fine). -------
    $dirtyApis = & git status --porcelain -- '*PublicAPI.Shipped.txt' '*PublicAPI.Unshipped.txt'
    if (-not [string]::IsNullOrWhiteSpace(($dirtyApis -join ''))) {
        throw "There are uncommitted PublicAPI.*.txt changes. Commit or stash them before running this script."
    }

    # --- Run the ship-public-apis tool -------------------------------------
    Write-Host 'Running ship-public-apis tool...' -ForegroundColor Cyan
    Invoke-Native dotnet run --project $toolProject
    Write-Host 'ship-public-apis tool completed successfully.' -ForegroundColor Green

    # --- Bail out if nothing changed ---------------------------------------
    $changed = & git status --porcelain
    if ([string]::IsNullOrWhiteSpace(($changed -join ''))) {
        Write-Host 'No PublicAPI changes were produced. Nothing to ship; no PR created.' -ForegroundColor Yellow
        return
    }

    # --- Create topic branch, commit, push ---------------------------------
    $user = if ($env:USERNAME) { $env:USERNAME } else { (& git config user.name).Trim() }
    $user = ($user -replace '[^A-Za-z0-9]', '')
    if ([string]::IsNullOrWhiteSpace($user)) { $user = 'ship' }

    $timestamp = Get-Date -Format 'yyyyMMddHHmmss'
    $topicBranch = "dev-$user-shipPublicApis-$timestamp"

    Write-Host "Creating branch '$topicBranch'..." -ForegroundColor Cyan
    Invoke-Native git checkout -b $topicBranch
    Invoke-Native git add ':(glob)**/PublicAPI.Shipped.txt' ':(glob)**/PublicAPI.Unshipped.txt'

    $commitMessage = "Ship public APIs on $currentBranch"
    Invoke-Native git commit -m $commitMessage
    Invoke-Native git push -u origin $topicBranch

    # --- Open the pull request via gh --------------------------------------
    $prTitle = "Ship public APIs for $currentBranch"
    $prBody = @"
# Engineering

<!-- Engineering/test-only change; no NuGet/Home issue required. -->
Progresses $Issue

## Description

Moves all APIs from ``PublicAPI.Unshipped.txt`` into ``PublicAPI.Shipped.txt`` by running the ``ship-public-apis`` tool on ``$currentBranch``. Generated by ``scripts\Ship-PublicApis.ps1``.

## PR Checklist

- [x] Meaningful title, helpful description and a linked issue
- [ ] Added tests
- [ ] Link to an issue or pull request to update docs if this PR changes settings, environment variables, new feature, etc.
"@

    Write-Host 'Creating pull request...' -ForegroundColor Cyan
    $prArgs = @(
        'pr', 'create',
        '--base', $currentBranch,
        '--head', $topicBranch,
        '--title', $prTitle,
        '--body', $prBody
    )
    if ($Draft) { $prArgs += '--draft' }

    Invoke-Native gh @prArgs
    Write-Host 'Pull request created successfully.' -ForegroundColor Green
}
finally {
    Pop-Location
}
