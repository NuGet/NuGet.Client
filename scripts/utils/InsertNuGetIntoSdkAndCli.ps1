<#
.SYNOPSIS
Script to insert NuGet into dotnet; CLI, SDK, and TOOLSET repos

.DESCRIPTION
Uses the Personal Access Token of NuGetLurker to automate the insertion process into CLI, SDK, and TOOLSET
Note - This script can only be used from a VSTS Release Definition because of the env variables it
depends on.

.PARAMETER PersonalAccessToken
PersonalAccessToken of the NuGetLurker account

.PARAMETER RepositoryName
The Repository to insert into (SDK, CLI, or TOOLSET)

.PARAMETER BranchName
Semicolon separated list of the repository's branches to insert into

.PARAMETER NuGetTag
The xml tag in the DependencyVersions.props file that defines the NuGet version

.PARAMETER BuildOutputPath
The output path for NuGet Build artifacts.

#>

[CmdletBinding()]
param
(
    [Parameter(Mandatory=$True)]
    [string]$PersonalAccessToken,
    [Parameter(Mandatory=$True)]
    [string]$RepositoryName,
    [Parameter(Mandatory=$True)]
    [string]$BranchName,
    [Parameter(Mandatory=$True)]
    [string]$FilePath,
    [Parameter(Mandatory=$True)]
    [string]$NuGetTag,
    [Parameter(Mandatory=$True)]
    [string]$BuildOutputPath
)

Function UpdateNuGetVersionInXmlFile {
    param(
        [string]$XmlContents,
        [string]$NuGetVersion,
        [string]$NuGetTag
    )

$xmlString = $XmlContents.Split([environment]::NewLine) | Where-Object { $_ -cmatch "<$NuGetTag>" }
Write-Host $xmlString
$newXmlString = "<$NuGetTag>$NuGetVersion</$NuGetTag>"
Write-Host $newXmlString
$updatedXml = $XmlContents.Replace($xmlString.Trim(), $newXmlString) 
Write-Host $updatedXml
return $updatedXml

}

Function GetDependencyVersionPropsFile {
param
(
    [Parameter(Mandatory=$True)]
    [string]$RepositoryName,
    [Parameter(Mandatory=$True)]
    [string]$BranchName,
    [Parameter(Mandatory=$True)]
    [string]$FilePath
)

$url = "https://raw.githubusercontent.com/$repoOwner/$RepositoryName/$BranchName/$FilePath"
Write-Host $url
$xmlContent = Invoke-WebRequest -Uri $url -UseBasicParsing

return $xmlContent

}

Function CreateBranchForPullRequest {
param
(
    [Parameter(Mandatory=$True)]
    [string]$RepositoryName,
    [Parameter(Mandatory=$True)]
    [System.Collections.IDictionary]$Headers,
    [Parameter(Mandatory=$True)]
    [string]$BranchName,
    [Parameter(Mandatory=$True)]
    [string]$BranchNameToCreate
)

$commits = Invoke-RestMethod -Method Get -Uri "https://api.github.com/repos/$repoOwner/$RepositoryName/commits?sha=$BranchName"
$headSha = $commits[0].sha
$refName = "refs/heads/$BranchNameToCreate"

$Body = @{
sha = $headSha;
ref = $refName;
} | ConvertTo-Json;

$r1 = Invoke-RestMethod -Headers $Headers -Method Post -Uri "https://api.github.com/repos/$repoOwner/$RepositoryName/git/refs" -Body $Body
Write-Host $r1
}

Function UpdateFileContent {
param
(
    [Parameter(Mandatory=$True)]
    [string]$RepositoryName,
    [Parameter(Mandatory=$True)]
    [System.Collections.IDictionary]$Headers,
    [Parameter(Mandatory=$True)]
    [string]$FilePath,
    [Parameter(Mandatory=$True)]
    [string]$FileContent,
    [Parameter(Mandatory=$True)]
    [string]$CreatedBranchName
)
$params = "?ref=$CreatedBranchName"
$httpGetUrl = "https://api.github.com/repos/$repoOwner/$RepositoryName/contents/$FilePath$Params"
$content = Invoke-RestMethod -Method Get -Uri $httpGetUrl
$shaBlob = $content.sha
$commitMessage = "Insert NuGet Build $ProductVersion into $RepositoryName"

$base64Content = [System.Convert]::ToBase64String([char[]]$FileContent)
$Uri = "https://api.github.com/repos/$repoOwner/$RepositoryName/contents/$FilePath"

$Body = @{
path = $FilePath;
sha = $shaBlob;
branch = $CreatedBranchName;
message = $commitMessage;
content =  $base64Content;
} | ConvertTo-Json;

$r1 = Invoke-RestMethod -Headers $Headers -Method Put -Uri $Uri -Body $Body
Write-Host $r1
}

Function CreatePullRequest {
param
(
    [Parameter(Mandatory=$True)]
    [string]$RepositoryName,
    [Parameter(Mandatory=$True)]
    [System.Collections.IDictionary]$Headers,
    [Parameter(Mandatory=$True)]
    [string]$CreatedBranchName,
    [Parameter(Mandatory=$True)]
    [string]$BaseBranch

)

$Uri = "https://api.github.com/repos/$repoOwner/$RepositoryName/pulls"
$Title = "Insert NuGet Build $ProductVersion into $RepositoryName"
$PrMessage = "$Title $BaseBranch branch"

$Body = @{
title = $Title;
head = $CreatedBranchName;
base = $BaseBranch;
body = $PrMessage;
} | ConvertTo-Json;

Write-Host $Body

$r1 = Invoke-RestMethod -Headers $Headers -Method Post -Uri $Uri -Body $Body
Write-Host $r1.html_url
return $r1.html_url
}

Function PrintPullRequestsUrlToVsts {
param
(
    [Parameter(Mandatory=$True)]
    [string]$RepositoryName,
    [Parameter(Mandatory=$True)]
    [string]$PullRequestsUrl

)

$mdFolder = Join-Path $env:SYSTEM_DEFAULTWORKINGDIRECTORY (Join-Path 'MicroBuild' 'Output')
New-Item -ItemType Directory -Force -Path $mdFolder | Out-Null
$fileExtension = "_Url.md"
$mdFile = Join-Path $mdFolder "$RepositoryName$fileExtension"
$PullRequestsUrl | Set-Content $mdFile
Write-Host "##vso[task.addattachment type=Distributedtask.Core.Summary;name=$RepositoryName Pull Requests Url;]$mdFile"  
}

## This is where the script logic beings.

# Set security protocol to tls1.2 for Invoke-RestMethod powershell cmdlet
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$repoOwner = "dotnet"
$Base64Token = [System.Convert]::ToBase64String([char[]]$PersonalAccessToken)

$Headers= @{
    Authorization='Basic {0}' -f $Base64Token;
}

$Build = ${env:BUILD_BUILDNUMBER}
$Branch = ${env:BUILD_SOURCEBRANCHNAME}
$AttemptNum = ${env:RELEASE_ATTEMPTNUMBER}
$Release = ${env:RELEASE_RELEASENAME}
$NuGetExePath = [System.IO.Path]::Combine($BuildOutputPath, $Branch, $Build, 'artifacts', 'VS15', "NuGet.exe")

$ProductVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($NuGetExePath).ProductVersion
$index = $ProductVersion.LastIndexOf('+')
if($index -ne '-1')
{
    $ProductVersion = $ProductVersion.Substring(0,$index).Trim()
}

$BranchesToInsert = $BranchName.Split(';')
$AllPullRequestsUrls = ""
ForEach ($Branch in $BranchesToInsert) {

    $VersionsFilePath = $FilePath
    # Hack to allow us to specify the arcade version props location if the branch is 3.0 or later. 
    # We can remove this when we stop inserting into 2.x
    if($Branch.StartsWith("master") -or $Branch.StartsWith("release/3")){
        $VersionsFilePath = "eng/Versions.props"
    } 

    $xml = GetDependencyVersionPropsFile -RepositoryName $RepositoryName -BranchName $Branch -FilePath $VersionsFilePath
    Write-Host $xml
    
    $updatedXml = UpdateNuGetVersionInXmlFile -XmlContents $xml -NuGetVersion $ProductVersion -NuGetTag $NuGetTag
    
    $CreatedBranchName = "$Release-$Branch-$AttemptNum"
    
    CreateBranchForPullRequest -RepositoryName $RepositoryName -Headers $Headers -BranchName $Branch -BranchNameToCreate $CreatedBranchName
    UpdateFileContent -RepositoryName $RepositoryName -Headers $Headers -FilePath $VersionsFilePath -FileContent $updatedXml -CreatedBranchName $CreatedBranchName
    $PullRequestUrl = CreatePullRequest -RepositoryName $RepositoryName -Headers $Headers -CreatedBranchName $CreatedBranchName -BaseBranch $Branch
    $AllPullRequestsUrls = $AllPullRequestsUrls + "$PullRequestUrl`n"
}

PrintPullRequestsUrlToVsts -RepositoryName $RepositoryName -PullRequestsUrl $AllPullRequestsUrls
