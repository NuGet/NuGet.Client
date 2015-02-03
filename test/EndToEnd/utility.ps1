function New-Guid {
    [System.Guid]::NewGuid().ToString("d").Substring(0, 4).Replace("-", "")
}

function Get-HostSemanticVersion {
    $currentHostSystemVersion = $host.Version
    $currentHostSemanticVersion = New-Object NuGet.Versioning.SemanticVersion($currentHostSystemVersion.Major, $currentHostSystemVersion.Minor, $currentHostSystemVersion.Build)

    return $currentHostSemanticVersion.ToNormalizedString()  
}