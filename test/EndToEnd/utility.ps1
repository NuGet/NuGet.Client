function New-Guid {
    [System.Guid]::NewGuid().ToString("d").Substring(0, 4).Replace("-", "")
}

function Get-HostSemanticVersion {
    return [NuGet.Common.ClientVersionUtility]::GetNuGetAssemblyVersion()
}

function Verify-BuildIntegratedMsBuildTask {
    $msBuildTaskPath = [IO.Path]::Combine(${env:ProgramFiles(x86)}, "MsBuild", "Microsoft", "NuGet", "Microsoft.NuGet.targets")

    if (!(Test-Path $msBuildTaskPath)) {
        Write-Warning "Build integrated NuGet target not found at $msBuildTaskPath"
        return $false
    }

    return $true
}