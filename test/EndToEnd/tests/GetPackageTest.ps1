# TOOD NK - Is this a legit thing?
function Test-GetPackageRetunsMoreThanServerPagingLimit {
    # Act
    $packages = Get-Package -ListAvailable

    # Assert
    Assert-True $packages.Count -gt 100 "Get-Package cmdlet returns less than (or equal to) than server side paging limit"
}

#TODO NK - This should be probably be done too. We should probably ensure the exact output since we now control the exact test.
function Test-GetPackageCollapsesPackageVersionsForListAvailable {
    param()

    # Act
    $packages = Get-Package -ListAvailable jQuery
    $packagesWithMoreThanOne = $packages | group "Id" | Where { $_.count -gt 1 }

    # Assert
    # Ensure we have at least some packages
    Assert-True (1 -le $packages.Count)
    Assert-Null $packagesWithMoreThanOne
}

# TODO - When migrating this, have 2 sources and then run a command with `--source` to test.
function Test-GetPackageAcceptsSourceName {
    # Act
    $p = @(Get-Package -Filter elmah -ListAvailable -Source $SourceNuGet )

    # Assert
    Assert-True (1 -le $p.Count)
}

# TODO - Same thing here.
function Test-GetPackageWithUpdatesAcceptsSourceName {
    # Arrange
    $p = New-ConsoleApplication

    # Act
    Install-Package Antlr -Version 3.1.1 -Project $p.Name -Source $SourceNuGet
    Install-Package jQuery -Version 1.4.1 -Project $p.Name -Source $SourceNuGet
    $packages = Get-Package -Updates -Source $SourceNuGet

    # Assert
    Assert-AreEqual 2 $packages.Count
}

# Should be simpe to migrate too.
function GetPackageAcceptsAllAsSourceName {
     # Act
    $p = @(Get-Package -Filter elmah -ListAvailable -Source 'All')

    # Assert
    Assert-True (1 -le $p.Count)
}

# This can be done with local sources.
function Test-GetPackageUpdatesAfterSwitchToSourceThatDoesNotContainInstalledPackageId
{
    param
    (
        $context
    )

    # Arrange
    $p = New-ClassLibrary

    $p | Install-Package antlr -Version '3.1.1' -Source $SourceNuGet

    # Act
    $packages = @(Get-Package -updates -Source 'https://pkgs.dev.azure.com/dnceng/public/_packaging/nuget-build/nuget/v3/index.json')

    # Assert
    Assert-AreEqual 0 $packages.Count
}

