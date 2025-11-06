# Ensure all packages are created before publishing

[CmdletBinding(SupportsShouldProcess=$True)]
Param (
    [string]$NupkgOutputPath,
    [switch]$BuildRTM
)
# Determine destination (sibling of the source folder)
$ShippingFolder = Join-Path -Path $(Split-Path -Path $NupkgOutputPath -Parent) -ChildPath "shipping"
New-Item -ItemType Directory -Force -Path $ShippingFolder | Out-Null

# Gather .nupkg files (top-level by default; recurse if requested)
$searchParams = @{
    LiteralPath = $NupkgOutputPath
    Filter      = '*.nupkg'
    File        = $true
}

$allPkgs = Get-ChildItem @searchParams

# Exclusion rules (case-insensitive)
$excludeContainsTest     = { $_.Name -match '(?i)Test' }
$excludePrefixExtraction = { $_.Name -match '^(?i)NuGet\.Packaging\.Extraction\.' }
$excludePrefixCore       = { $_.Name -match '^(?i)NuGet\.Packaging\.Core\.' }

$filtered = $allPkgs | Where-Object {
    -not (& $excludeContainsTest) -and
    -not (& $excludePrefixExtraction) -and
    -not (& $excludePrefixCore)
}

# Report and copy
Write-Host "Source:      $NupkgOutputPath"
Write-Host "Destination: $ShippingFolder"
Write-Host "Found       : $($allPkgs.Count) .nupkg file(s)"
Write-Host "Will copy   : $($filtered.Count) file(s) after exclusions"
Write-Host ""

if ($filtered.Count -eq 0) {
    Write-Host "No packages to copy after applying filters."
    return
}

foreach ($pkg in $filtered) {
    $targetPath = Join-Path -Path $ShippingFolder -ChildPath $pkg.Name
    Copy-Item -LiteralPath $pkg.FullName -Destination $targetPath -Force
}

Write-Host ""
Write-Host "Done. Copied $($filtered.Count) package(s) to '$ShippingFolder'."
