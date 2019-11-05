<#
.SYNOPSIS
Sets build variables during a VSTS build dynamically.

.DESCRIPTION
This script is used to publish all offical nupkgs (from dev and release-* branches) to 
myget feeds.

#>

param
(
    [Parameter(Mandatory=$True)]
    [string]$NuGetBuildFeedUrl,

    [Parameter(Mandatory=$True)]
    [string]$NuGetBuildSymbolsFeedUrl,

    [Parameter(Mandatory=$True)]
    [string]$DotnetCoreFeedUrl,

    [Parameter(Mandatory=$True)]
    [string]$DotnetCoreSymbolsFeedUrl,

    [Parameter(Mandatory=$True)]
    [string]$NuGetBuildFeedApiKey,

    [Parameter(Mandatory=$True)]
    [string]$DotnetCoreFeedApiKey
)

function Push-ToMyGet {
    [CmdletBinding(SupportsShouldProcess=$True)]
    param(
        [parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
        [string[]] $NupkgFiles,
        [string] $ApiKey,
        [string] $BuildFeed,
        [string] $SymbolSource
    )
    Process {
        $NupkgFiles | %{
            $Feed = Switch -Wildcard ($_) {
                "*.symbols.nupkg" { $SymbolSource }
                Default { $BuildFeed }
            }
            $opts = 'nuget', 'push', $_, '-k', $ApiKey, '-s', $Feed
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
    Get-Item "$NupkgsDir\*.nupkg" -Exclude "Test.*.nupkg", "*.symbols.nupkg" | Push-ToMyGet -ApiKey $NuGetBuildFeedApiKey -BuildFeed $NuGetBuildFeedUrl -SymbolSource $NuGetBuildSymbolsFeedUrl
    # We push NuGet.Build.Tasks.Pack nupkg to dotnet-core feed as per the request of the CLI team.
    Get-Item "$NupkgsDir\NuGet.Build.Tasks.Pack*.nupkg" -Exclude "*.symbols.nupkg" | Push-ToMyGet -ApiKey $DotnetCoreFeedApiKey -BuildFeed $DotnetCoreFeedUrl -SymbolSource $DotnetCoreSymbolsFeedUrl
}

