[CmdletBinding()]
Param (
    [Parameter(Mandatory=$true, Position=0)]
    [string]$OldVersion,
    [Parameter(Mandatory=$true, Position=1)]
    [string]$NewVersion,
    [Parameter(Mandatory=$false, Position=2)]
    [string]$NuGetRoot = (Join-Path $PSScriptRoot '..\..\'))

function ReplaceNuGetVersion
{
    Param(
        [Parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
        [string[]]$Files
    )
    Process {
        $Files`
            | ?{ (Get-Content $_ | Out-String) -match $OldVersion }`
            | %{
                Write-Output "Processing $_"

                $updated = (Get-Content $_) | %{
                    $_.Replace($OldVersion, $NewVersion)
                }

                Set-Content $_ $updated | Out-Null
            }
    }
}

Write-Output "Updating NuGet version [$OldVersion => $NewVersion]"

ls -r project.json | %{ $_.FullName } | ReplaceNuGetVersion

$miscFiles = @(
    "src\NuGet.Clients\VsExtension\source.extension.dev14.vsixmanifest",
    "src\NuGet.Clients\VsExtension\source.extension.dev15.vsixmanifest",
    "src\NuGet.Clients\VsExtension\NuGetPackage.cs",
    "build\common.props",
    ".teamcity.properties",
    "appveyor.yml"
)

$miscFiles | %{ Join-Path $NuGetRoot $_ } | ReplaceNuGetVersion