param (
    [Parameter(Mandatory=$true)]
    [string]$nuGetRoot,
    [Parameter(Mandatory=$true)]
    [string]$oldVersion,
    [Parameter(Mandatory=$true)]
    [string]$newVersion)

function ReplaceNuGetVersion
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$file
    )

    if ((gc $file | Out-String) -match $oldVersion)
    {
        Write-Host "On $file, replacing version from $oldVersion to $newVersion"
        $c = Get-Content $file
        $d = $c | %{
            $_.Replace($oldVersion, $newVersion)
        }

        Set-Content $file $d | Out-Null
    }
}

ls -r project.json | ?{ ReplaceNuGetVersion($_.FullName) }

$vsixManifestFile = Join-Path $nuGetRoot "src\NuGet.Clients\VsExtension\source.extension.vsixmanifest"
ReplaceNuGetVersion($vsixManifestFile)

$nugetPackageCSFile = Join-Path $nuGetRoot "src\NuGet.Clients\VsExtension\NuGetPackage.cs"
ReplaceNuGetVersion($nugetPackageCSFile)

$commonProps = Join-Path $nuGetRoot "build\common.props"
ReplaceNuGetVersion($commonProps)