param(
    [Parameter()]
    [switch]$SixtyFourBit
)

$vspath = & "$PSScriptRoot\Get-VSPath.ps1"
if ($vspath) {
    $MSBuildPath = join-path $vspath 'msbuild\current\bin'

    if ($SixtyFourBit) {
        $MSBuildPath = join-path $MSBuildPath 'amd64'
    }
    
    Write-Verbose "Using MSBuild from $MSBuildPath"

    Write-Output $MSBuildPath
}
