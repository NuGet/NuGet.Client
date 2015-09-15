function New-Guid {
    [System.Guid]::NewGuid().ToString("d").Substring(0, 4).Replace("-", "")
}

function Get-HostSemanticVersion {
    $currentHostSystemVersion = $host.Version
    $currentHostSemanticVersion = New-Object NuGet.Versioning.SemanticVersion($currentHostSystemVersion.Major, $currentHostSystemVersion.Minor, $currentHostSystemVersion.Build)

    return $currentHostSemanticVersion.ToNormalizedString()  
}

function Verify-BuildIntegratedMsBuildTask {
    $msBuildTaskPath = [IO.Path]::Combine(${env:ProgramFiles(x86)}, "MsBuild", "Microsoft", "NuGet", "Microsoft.NuGet.targets")

    if (!(Test-Path $msBuildTaskPath)) {
        Write-Warning "Build integrated NuGet target not found at $msBuildTaskPath"
        return $false
    }

    return $true
}