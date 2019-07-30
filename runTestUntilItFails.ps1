$msbuildFilePath= 'C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\MSBuild\Current\bin\msbuild.exe'
$xunitFilePath = 'packages\xunit.runner.console.2.3.1\tools\net452\xunit.console.exe' 

# & $msbuildFilePath -t:build /restore  'test\NuGet.Clients.Tests\NuGet.PackageManagement.UI.Test\NuGet.PackageManagement.UI.Test.csproj'

Clear-Host

$counter = 0
$stopwatch = [System.Diagnostics.Stopwatch]::new()

# Do
# {
    ++$counter
    $now = [System.DateTime]::Now.ToString("O")
    Clear-Host
    Write-Host "[$now]:  Last iteration elapsed:  $($stopwatch.Elapsed)"
    Write-Host "[$now]:  Starting iteration $counter..."
    $stopwatch.Restart()
    & $xunitFilePath 'test\NuGet.Clients.Tests\NuGet.PackageManagement.UI.Test\bin\16.0\Debug\NuGet.PackageManagement.UI.Test.dll' -method NuGet.PackageManagement.UI.Test.PackageItemLoaderTests.EmitsSearchTelemetryEvents
# } While ($LASTEXITCODE -eq 0)