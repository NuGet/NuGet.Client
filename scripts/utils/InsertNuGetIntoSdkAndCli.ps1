<#
.SYNOPSIS
Script to insert NuGet into CLI and SDK

.DESCRIPTION
Uses the Personal Access Token of NuGetLurker to automate the insertion process into CLI and SDK

.PARAMETER PersonalAccessToken
PersonalAccessToken of the NuGetLurker account

.PARAMETER RepositoryName
The Repository to insert into (SDK or CLI)

.PARAMETER BranchName
The repository's branch to insert into

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
    [string]$NuGetTag,
    [Parameter(Mandatory=$True)]
    [string]$BuildOutputPath
)

$repoOwner = "dotnet"
$Base64Token = [System.Convert]::ToBase64String([char[]]$PersonalAccessToken)

$Headers= @{
    Authorization='Basic {0}' -f $Base64Token;
}

$Build = ${env:BUILD_BUILDNUMBER}
$Branch = ${env:BUILD_SOURCEBRANCHNAME}
$NuGetExePath = [System.IO.Path]::Combine($BuildOutputPath, $Branch, $Build, 'artifacts', 'VS15', "NuGet.exe")

$ProductVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($NuGetExePath).ProductVersion
$CreatedBranchName = "nuget-insertbuild$Build"

Function UpdateNuGetVersionInXmlFile {
    param(
        [string]$XmlContents,
        [string]$NuGetVersion,
        [string]$NuGetTag
    )

$xmlString = $XmlContents.Split([environment]::NewLine) | where { $_ -cmatch $NuGetTag }
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
    [string]$BranchName

)

$commits = Invoke-RestMethod -Method Get -Uri "https://api.github.com/repos/$repoOwner/$RepositoryName/commits?sha=$BranchName"
$headSha = $commits[0].sha
$refName = "refs/heads/$CreatedBranchName"

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
    [string]$FileContent

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

Function PrintPullRequestUrlToVsts {
param
(
    [Parameter(Mandatory=$True)]
    [string]$RepositoryName,
    [Parameter(Mandatory=$True)]
    [string]$PullRequestUrl

)

$mdFolder = Join-Path $env:SYSTEM_DEFAULTWORKINGDIRECTORY (Join-Path 'MicroBuild' 'Output')
New-Item -ItemType Directory -Force -Path $mdFolder | Out-Null
$fileExtension = "_Url.md"
$mdFile = Join-Path $mdFolder "$RepositoryName$fileExtension"
$PullRequestUrl | Set-Content $mdFile
Write-Host "##vso[task.addattachment type=Distributedtask.Core.Summary;name=$RepositoryName Pull Request Url;]$mdFile"  
}

$xml = GetDependencyVersionPropsFile -RepositoryName $RepositoryName -BranchName $BranchName -FilePath build/DependencyVersions.props
Write-Host $xml

$updatedXml = UpdateNuGetVersionInXmlFile -XmlContents $xml -NuGetVersion $ProductVersion -NuGetTag $NuGetTag

CreateBranchForPullRequest -RepositoryName $RepositoryName -Headers $Headers -BranchName $BranchName
UpdateFileContent -RepositoryName $RepositoryName -Headers $Headers -FilePath build/DependencyVersions.props -FileContent $updatedXml
$PullRequestUrl = CreatePullRequest -RepositoryName $RepositoryName -Headers $Headers -CreatedBranchName $CreatedBranchName -BaseBranch $BranchName
PrintPullRequestUrlToVsts -RepositoryName $RepositoryName -PullRequestUrl $PullRequestUrl
