# Display the build env vars for debugging

Write-Host "NUGET_BUILD_KEY_PATH: $env:NUGET_BUILD_KEY_PATH" -ForegroundColor Yellow
Write-Host "NUGET_BUILD_DELAY_SIGN: $env:NUGET_BUILD_DELAY_SIGN" -ForegroundColor Yellow
Write-Host "NUGET_BUILD_REQUIRE_SIGNING: $env:NUGET_BUILD_REQUIRE_SIGNING" -ForegroundColor Yellow
Write-Host ""

# Find sn.exe
$snPath = Join-Path ${env:ProgramFiles(x86)} "Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools\x64\sn.exe"

Function GetSigning($path)
{
    Write-Host $path -ForegroundColor Cyan

    Invoke-Expression '& "$snPath" -Tp "$path"' 
}

# List every dll in the build output
$buildDir = [System.IO.Path]::GetDirectoryName($myInvocation.MyCommand.Definition)
Get-ChildItem -path "$buildDir\..\artifacts\build" -Recurse -Include *.dll | foreach { GetSigning($_)  }


