# assembly containing the release file version to use for the package
$primaryDll = "src\Versioning\bin\Debug\NuGet.Versioning.dll"

$gitBranch = "ci"

git branch | foreach {
    if ($_ -match "^\*(.*)") {
        $gitBranch = $matches[1].Trim()
    }
}

if ($gitBranch.Length -gt 8) {
    $gitBranch = $gitBranch.SubString(0, 8)
}

Write-Host "Git branch: $gitBranch" 

$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($primaryDll).FileVersion

$now = [System.DateTime]::UtcNow

# (git branch)-(last digit of the year)(day of year)(hour)(minute)
$version = $version.TrimEnd('0').TrimEnd('.') + "-" + $gitBranch + "-" + $now.ToString("yyyy")[3] + $now.DayOfYear + $now.ToString("HHmm")

Write-Host "Package version: $version" 

if ((Test-Path nupkgs) -eq 0) {
    New-Item -ItemType directory -Path nupkgs | Out-Null
}

.\.nuget\nuget.exe pack .\src\Versioning\Versioning.csproj -Properties configuration=debug -symbols -build -OutputDirectory nupkgs -version $version

$nupkgPath = Get-ChildItem .\nupkgs -filter "*$version.nupkg" | % { $_.FullName }

Write-Host "Pushing: $nupkgPath"

# use nuget.exe setApiKey <key> before running this
.\.nuget\nuget.exe push $nupkgPath
