<#
.SYNOPSIS
Script to post status of tests for the commit to GitHub
https://developer.github.com/v3/repos/statuses/ 

.DESCRIPTION
Uses the Personal Access Token of NuGetLurker to automate the tagging process.
#>

# set security protocol for Invoke-RestMethod
. "$PSScriptRoot\SetSecurityProtocol.ps1"

Function Update-GitCommitStatus {
    param(
        [Parameter(Mandatory = $True)]
        [string]$PersonalAccessToken,
        [Parameter(Mandatory = $True)]
        [ValidateSet( "Unit Tests On Windows", "Tests On Mac", "Tests On Linux", "Functional Tests On Windows", "EndToEnd Tests On Windows", "Apex Tests On Windows")]
        [string]$TestName,
        [Parameter(Mandatory = $True)]
        [ValidateSet( "pending", "success", "error", "failure")]
        [string]$Status,
        [Parameter(Mandatory = $True)]
        [string]$CommitSha,
        [Parameter(Mandatory = $True)]
        [string]$TargetUrl,
        [Parameter(Mandatory = $True)]
        [string]$Description
    )

    $Token = $PersonalAccessToken
    $Base64Token = [System.Convert]::ToBase64String([char[]]$Token)

    $Headers = @{
        Authorization = 'Basic {0}' -f $Base64Token;
    }

    $Body = @{
        state      = $Status;
        context    = $TestName;
        target_url = $TargetUrl;
        description = $Description
    } | ConvertTo-Json;

    Write-Host $Body
    $r1 = Invoke-RestMethod -Headers $Headers -Method Post -Uri "https://api.github.com/repos/nuget/nuget.client/statuses/$CommitSha" -Body $Body

    Write-Host $r1
}

Function InitializeAllTestsToPending {
    param(
        [Parameter(Mandatory = $True)]
        [string]$PersonalAccessToken,
        [Parameter(Mandatory = $True)]
        [string]$CommitSha
    )

    Update-GitCommitStatus -PersonalAccessToken $PersonalAccessToken -TestName "Unit Tests On Windows" -Status "pending" -CommitSha $CommitSha -TargetUrl $env:BUILDURL -Description "in progress"
    Update-GitCommitStatus -PersonalAccessToken $PersonalAccessToken -TestName "Functional Tests On Windows" -Status "pending" -CommitSha $CommitSha -TargetUrl $env:BUILDURL -Description "in progress"
    Update-GitCommitStatus -PersonalAccessToken $PersonalAccessToken -TestName "Tests On Mac" -Status "pending" -CommitSha $CommitSha -TargetUrl $env:BUILDURL -Description "in progress"
    Update-GitCommitStatus -PersonalAccessToken $PersonalAccessToken -TestName "Tests On Linux" -Status "pending" -CommitSha $CommitSha -TargetUrl $env:BUILDURL -Description "in progress"
    Update-GitCommitStatus -PersonalAccessToken $PersonalAccessToken -TestName "EndToEnd Tests On Windows" -Status "pending" -CommitSha $CommitSha -TargetUrl $env:BUILDURL -Description "in progress"
}

function SetCommitStatusForTestResult {
    param(
        [Parameter(Mandatory = $True)]
        [string]$PersonalAccessToken,
        [Parameter(Mandatory = $True)]
        [string]$TestName,
        [Parameter(Mandatory = $True)]
        [string]$CommitSha
    )

    if ($env:AGENT_JOBSTATUS -eq "Succeeded") {
        Update-GitCommitStatus -PersonalAccessToken $PersonalAccessToken -TestName $TestName -Status "success" -CommitSha $CommitSha -TargetUrl $env:BUILDURL -Description "succeeded"
    }
    else {
        Update-GitCommitStatus -PersonalAccessToken $PersonalAccessToken -TestName $TestName -Status "failure" -CommitSha $CommitSha -TargetUrl $env:BUILDURL -Description "failed"
    }

    # If the build gets cancelled or fails when the unit tests are running , we also need to call the github api to update status
    # for mac, apex and e2e tests as they only run when the unit tests phase succeeds (or partially succeeds). If we don't do this,
    # the status for those tests will forever be in pending state.
    if(($env:AGENT_JOBSTATUS -eq "Failed" -or $env:AGENT_JOBSTATUS -eq "Canceled") -and $TestName -eq "Unit Tests On Windows")
    {
        Update-GitCommitStatus -PersonalAccessToken $PersonalAccessToken -TestName "Tests On Mac" -Status "failure" -CommitSha $CommitSha -TargetUrl $env:BUILDURL -Description "failed"
        Update-GitCommitStatus -PersonalAccessToken $PersonalAccessToken -TestName "EndToEnd Tests On Windows" -Status "failure" -CommitSha $CommitSha -TargetUrl $env:BUILDURL -Description "failed"
    }
}