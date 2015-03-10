if (![string]::IsNullOrEmpty($env:BuildCounter))
{
    $num = 0 + $env:BuildCounter;
    $env:DNX_BUILD_VERSION="ci-" + $num.ToString("0000")
    Write-Host "DNX_BUILD_VERSION $env:DNX_BUILD_VERSION"
}

.\build.cmd

exit 0