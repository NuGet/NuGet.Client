# Ensure all packages are created before publishing
[CmdletBinding(SupportsShouldProcess=$True)]
Param (
    [string]$NupkgOutputPath,
    [string]$AdditionalExcludeRules
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

[System.Collections.ArrayList]$ExclusionRules = @(
".*.symbols.nupkg",
"NuGet.CommandLine.*.nupkg",
".*Test.*.nupkg",
"Microsoft.Build.NuGetSdkResolver.*.nupkg",
"NuGet.Packaging.Extraction.*.nupkg",
"NuGet.Build.Tasks.*.nupkg",
"NuGet.Packaging.Core.*.nupkg",
"NuGet.VisualStudio.*.nupkg"
)

if($AdditionalExcludeRules)
{
    Write-Host "Add more rules"
    $ExclusionRules.Add($AdditionalExcludeRules)
} else {
    Write-Host "no more rules $AdditionalExcludeRules"
}


$filtered = New-Object System.Collections.ArrayList

ForEach ($package in $allPkgs)
{
    $packageFullName = $package.FullName

    $ViolatesRule = $false
    Foreach ($Rule in $ExclusionRules)
    {
        if ($packageFullName -match $Rule)
        {
            $ViolatesRule = $True
        }
    }
    if (-not $ViolatesRule)
    {
        $filtered.Add($package)
    }
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
