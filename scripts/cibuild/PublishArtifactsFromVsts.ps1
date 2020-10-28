<#
.SYNOPSIS
Publish generated packages to NuGet.Client's feed.

.DESCRIPTION
This script is used to publish all offical nupkgs (from dev and release-* branches) to "nightly" feeds, not nuget.org.

#>

param
(
    [Parameter(Mandatory=$True)]
    [string]$NuGetBuildFeedUrl,
    [Parameter(Mandatory=$True)]
    [string]$NuGetBuildFeedApiKey
)

function Push-ToFeed {
    [CmdletBinding(SupportsShouldProcess=$True)]
    param(
        [parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
        [string[]] $NupkgFiles,
        [string] $ApiKey,
        [string] $BuildFeed
    )
    Process {
        $NupkgFiles | %{
            $opts = 'nuget', 'push', $_, '-k', $ApiKey, '-s', $BuildFeed
            if ($VerbosePreference) {
                $opts += '-verbosity', 'detailed'
            }

            if ($pscmdlet.ShouldProcess($_, "push to '${Feed}'")) {
                Write-Output "dotnet $opts"
                & dotnet $opts
                if (-not $?) {
                    Write-Error "Failed to push a package '$_' to myget feed '${Feed}'. Exit code: ${LASTEXITCODE}"
                }
            }
        }
    }
}

$NupkgsDir = Join-Path $env:BUILD_REPOSITORY_LOCALPATH artifacts\nupkgs

if(Test-Path $NupkgsDir)
{
    # Push all nupkgs to the nuget-build feed on myget.
    Get-Item "$NupkgsDir\*.nupkg" -Exclude "Test.*.nupkg", "*.symbols.nupkg" | Push-ToFeed -ApiKey $NuGetBuildFeedApiKey -BuildFeed $NuGetBuildFeedUrl
}

