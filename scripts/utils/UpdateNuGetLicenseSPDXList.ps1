<#
.SYNOPSIS
Updates the NuGet License file with the latest SPDX License list.

.DESCRIPTION
Downloads the SPDX License list, builds and runs the GenerateLicenseList tool and later cleans up the check out SPDX license list data.

#>

$licenseListDirectory = $([System.IO.Path]::Combine($env:TEMP, "NuGet", "licenseList"))

try {
    $gitRepo = "https://github.com/spdx/license-list-data.git"
    Write-Host "Downloading the license list from $gitRepo"
    git clone -b master --single-branch https://github.com/spdx/license-list-data.git $licenseListDirectory
    $generateLicenseList = $([System.IO.Path]::GetFullPath($([System.IO.Path]::Combine($PSScriptRoot, "..", "..", "test\TestExtensions\GenerateLicenseList\GenerateLicenseList.csproj"))))

    $licenses = $([System.IO.Path]::Combine($licenseListDirectory, "json", "licenses.json"))
    $exceptions = $([System.IO.Path]::Combine($licenseListDirectory, "json", "exceptions.json"))
    $targetFile = $([System.IO.Path]::GetFullPath($([System.IO.Path]::Combine($PSScriptRoot, "..", "..", "src\NuGet.Core\NuGet.Packaging\Licenses\NuGetLicenseData.cs"))))
    
    Write-Host "Generating the license list."

    dotnet run --project $generateLicenseList $licenses $exceptions $targetFile
}
finally {
    Write-Host "Cleaning up."
    Write-Host "Removing $licenseListDirectory."
    Remove-Item -Force -Recurse $licenseListDirectory
}