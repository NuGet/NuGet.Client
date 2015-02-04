
function Test-GetPackageRetunsMoreThanServerPagingLimit {
    # Act
    $packages = Get-Package -ListAvailable
    
    # Assert
    Assert-True $packages.Count -gt 100 "Get-Package cmdlet returns less than (or equal to) than server side paging limit"
}

function Test-GetPackageListsInstalledPackages {
    # Arrange
    $p = New-WebApplication
    
    # Act
    Install-Package elmah -Project $p.Name -Version 1.1
    Install-Package jQuery -Project $p.Name    
    $packages = Get-Package
    
    # Assert
    Assert-AreEqual 2 $packages.Count
}

function Test-GetPackageWithoutOpenSolutionThrows {
    Assert-Throws { Get-Package } "The current environment doesn't have a solution open."
}

function Test-GetPackageWithUpdatesListsUpdates {
    # Arrange
    $p = New-WebApplication
    
    # Act
    Install-Package NuGet.Core -Version 1.6.0 -Project $p.Name
    Install-Package NuGet.CommandLine -Version 1.6.0 -Project $p.Name    
    $packages = Get-Package -Updates
    
    # Assert
    Assert-AreEqual 2 $packages.Count
}

function Test-GetPackageCollapsesPackageVersionsForListAvailable 
{
    # Act
    $packages = Get-Package -ListAvailable jQuery 
    $packagesWithMoreThanOne = $packages | group "Id" | Where { $_.count -gt 1 } 
    
    # Assert
    # Ensure we have at least some packages
    Assert-True (1 -le $packages.Count)
    Assert-Null $packagesWithMoreThanOne
}

function Test-GetPackageAcceptsSourceName {
    # Act
    $p = @(Get-Package -Filter elmah -ListAvailable -Source $SourceNuGet )

    # Assert
    Assert-True (1 -le $p.Count)
}

function Test-GetPackageWithUpdatesAcceptsSourceName {
    # Arrange
    $p = New-WebApplication
    
    # Act
    Install-Package Antlr -Version 3.1.1 -Project $p.Name -Source $SourceNuGet
    Install-Package jQuery -Version 1.4.1 -Project $p.Name -Source $SourceNuGet
    $packages = Get-Package -Updates -Source $SourceNuGet
    
    # Assert
    Assert-AreEqual 2 $packages.Count
}

function Test-GetPackageAcceptsAllAsSourceName {
     # Act
    $p = @(Get-Package -Filter elmah -ListAvailable -Source 'All')

    # Assert
    Assert-True (1 -le $p.Count)
}

function Test-GetPackageAcceptsRelativePathSource {
    param(
        $context
    )

    pushd

    # Act
    cd $context.TestRoot
    $p = @(Get-Package -ListAvailable -Source '..\')

    # Assert
    Assert-True (1 -le $p.Count)

    popd
}

function Test-GetPackageAcceptsRelativePathSource2 {
    param(
        $context
    )

    pushd

    # Arrange
    $repositoryRoot = $context.RepositoryRoot
    $parentOfRoot = Split-Path $repositoryRoot
    $relativePath = Split-Path $repositoryRoot -Leaf

    # Act
    cd $parentOfRoot
    $p = @(Get-Package -ListAvailable -Source $relativePath)

    # Assert
    Assert-True (1 -le $p.Count)

    popd
}

function Test-GetPackageThrowsWhenSourceIsInvalid {
    # Act & Assert
    Assert-Throws { Get-Package -ListAvailable -source "d:package" } "Invalid URI: A Dos path must be rooted, for example, 'c:\'."
}

function Test-GetPackageForProjectReturnsEmptyProjectIfItHasNoInstalledPackage {
    # Arrange
    $p = New-ConsoleApplication

    # Act
    $result = @(Get-Package -ProjectName $p.Name)

    # Assert
    Assert-AreEqual 0 $result.Count
}

function Test-GetPackageForProjectReturnsCorrectPackages {
    # Arrange
    $p = New-ConsoleApplication
    Install-Package jQuery -Version 1.5 -Source $context.RepositoryPath

    # Act
    $result = @(Get-Package -ProjectName $p.Name)

    # Assert
    Assert-AreEqual 1 $result.Count
    Assert-AreEqual "jQuery" $result[0].Id
    Assert-AreEqual "1.5" $result[0].Version
}

function Test-GetPackageForProjectReturnsCorrectPackages2 {
    # Arrange
    $p1 = New-ConsoleApplication
    $p2 = New-ClassLibrary

    Install-Package jQuery -Version 1.5 -Source $context.RepositoryPath -ProjectName $p1.Name
    Install-Package MyAwesomeLibrary -Version 1.0 -Source $context.RepositoryPath -ProjectName $p2.Name

    # Act
    $result = @(Get-Package -ProjectName $p1.Name)

    # Assert
    Assert-AreEqual 1 $result.Count
    Assert-AreEqual "jQuery" $result[0].Id
    Assert-AreEqual "1.5" $result[0].Version
}

function Test-GetPackageForFSharpProjectReturnsCorrectPackages {
    # Arrange
    $p = New-FSharpConsoleApplication

    Install-Package jQuery -Version 1.5 -Source $context.RepositoryPath

    # Act
    $result = @(Get-Package -ProjectName $p.Name)

    # Assert
    Assert-AreEqual 1 $result.Count
    Assert-AreEqual "jQuery" $result[0].Id
    Assert-AreEqual "1.5" $result[0].Version
}

function Test-GetPackageForFSharpProjectReturnsCorrectPackages2 {
    # Arrange
    $p = New-FSharpConsoleApplication

    # Note that installation of the second package on a project involves update of an existing packages.config
    # which is different from the installation of the first package
    Install-Package jQuery -Version 1.5 -Source $context.RepositoryPath
    Install-Package MyAwesomeLibrary -Version 1.0 -Source $context.RepositoryPath

    # Act
    $result = @(Get-Package -ProjectName $p.Name)

    # Assert
    Assert-AreEqual 2 $result.Count
    Assert-AreEqual "jQuery" $result[0].Id
    Assert-AreEqual "1.5" $result[0].Version
    Assert-AreEqual "MyAwesomeLibrary" $result[1].Id
    Assert-AreEqual "1.0" $result[1].Version
}

function Test-GetPackageForProjectReturnsEmptyIfItHasNoInstalledPackage {
    # Arrange
    $p = New-ConsoleApplication

    # Act
    $result = @(Get-Package -ProjectName $p.Name)

    # Assert
    Assert-AreEqual 0 $result.Count
}

function Test-GetPackageForProjectReturnsEmptyIfItHasNoInstalledPackage2 {
    param(
        $context
    )

    # Arrange
    $p1 = New-ConsoleApplication
    $p2 = New-ClassLibrary

    Install-Package jQuery -Source $context.RepositoryPath -Project $p1.Name
 
    # Act
    $result = @(Get-Package -ProjectName $p2.Name)

    # Assert
    Assert-AreEqual 0 $result.Count
}

function Test-GetPackageForProjectThrowIfProjectNameIsInvalid {
    param(
        $context
    )

    # Arrange
    $p1 = New-ConsoleApplication
 
    # Act & Assert
    Assert-Throws { Get-Package -ProjectName "invalidname" } "No compatible project(s) found in the active solution."
}

function Test-GetPackageWithoutProjectNameReturnsInstalledPackagesInTheSolution {
    param(
        $context
    )

    # Arrange
    $p1 = New-ConsoleApplication
    $p2 = New-ClassLibrary

    Install-Package jQuery -Source $context.RepositoryPath -Project $p1.Name
    Install-Package netfx-Guard -Source $context.RepositoryPath -Project $p2.Name
 
    # Act
    $result = @(Get-Package)

    # Assert
    Assert-AreEqual 2 $result.Count
    Assert-AreEqual "jQuery" $result[0].Id
    Assert-AreEqual "netfx-Guard" $result[1].Id
}

function Test-ZipPackageLoadsReleaseNotesAttribute {
    param(
        $context
    )

    # Act
    $p = Get-Package -ListAvailable -Source $context.RepositoryRoot -Filter ReleaseNotesPackage

    # Assert
    Assert-AreEqual "This is a release note." $p.ReleaseNotes
}

function Test-GetPackagesWithNoUpdatesReturnPackagesWithIsUpdateNotSet {    
    # Arrange & Act
    $package = Get-Package -ListAvailable -First 1
    
    # Assert
    Assert-NotNull $package
    Assert-False $package.IsUpdate
}

function Test-GetPackagesDoesNotShowPrereleasePackagesWhenSwitchIsNotSpecified {
    param(
        $context
    )

    # Act
    $packages = @(Get-Package -Source $context.RepositoryRoot -ListAvailable -Filter PreReleaseTestPackage)

    # Assert
    Assert-AreEqual 3 $packages.Count
    Assert-AreEqual "PackageWithDependencyOnPrereleaseTestPackage" $packages[0].Id
    Assert-AreEqual "1.0" $packages[0].Version
    Assert-AreEqual "PreReleaseTestPackage" $packages[1].Id
    Assert-AreEqual "1.0.0" $packages[1].Version
    Assert-AreEqual "PreReleaseTestPackage.A" $packages[2].Id
    Assert-AreEqual "1.0.0" $packages[2].Version
    
}

function Test-GetPackagesAllVersionsDoesNotShowPrereleasePackagesWhenSwitchIsNotSpecified {
    param(
        $context
    )

    # Act
    $packages = @(Get-Package -ListAvailable -Source $context.RepositoryRoot -AllVersions -Filter PreReleaseTestPackage)

    # Assert
    Assert-AreEqual 3 $packages.Count
    Assert-AreEqual "PackageWithDependencyOnPrereleaseTestPackage" $packages[0].Id
    Assert-AreEqual "1.0" $packages[0].Version
    Assert-AreEqual "PreReleaseTestPackage" $packages[1].Id
    Assert-AreEqual "1.0.0" $packages[1].Version
    Assert-AreEqual "PreReleaseTestPackage.A" $packages[2].Id
    Assert-AreEqual "1.0.0" $packages[2].Version
}

function Test-GetPackagesWithPrereleaseSwitchShowsPrereleasePackages {
    param(
        $context
    )

    # Act
    $packages = @(Get-Package -ListAvailable -Source $context.RepositoryRoot -Prerelease -Filter PreReleaseTestPackage)

    # Assert
    Assert-AreEqual 3 $packages.Count
    Assert-AreEqual "PackageWithDependencyOnPrereleaseTestPackage" $packages[0].Id
    Assert-AreEqual "1.0" $packages[0].Version
    Assert-AreEqual "PreReleaseTestPackage" $packages[1].Id
    Assert-AreEqual "1.0.1-a" $packages[1].Version
    Assert-AreEqual "PreReleaseTestPackage.A" $packages[2].Id
    Assert-AreEqual "1.0.0" $packages[2].Version
}

function Test-GetPackagesWithAllAndPrereleaseSwitchShowsAllPackages {
    param(
        $context
    )

    # Act
    $packages = @(Get-Package -ListAvailable -Source $context.RepositoryRoot -Prerelease -AllVersions -Filter PreReleaseTestPackage)

    # Assert
    Assert-AreEqual 3 $packages.Count
    Assert-AreEqual "PackageWithDependencyOnPrereleaseTestPackage" $packages[0].Id
    Assert-AreEqual "1.0" $packages[0].Version[0]
    
    Assert-AreEqual "PreReleaseTestPackage" $packages[1].Id
    Assert-AreEqual "1.0.0-a" $packages[1].Version[3]

    Assert-AreEqual "PreReleaseTestPackage" $packages[1].Id
    Assert-AreEqual "1.0.0-b" $packages[1].Version[2]

    Assert-AreEqual "PreReleaseTestPackage" $packages[1].Id
    Assert-AreEqual "1.0.0" $packages[1].Version[1]

    Assert-AreEqual "PreReleaseTestPackage" $packages[1].Id
    Assert-AreEqual "1.0.1-a" $packages[1].Version[0]

    Assert-AreEqual "PreReleaseTestPackage.A" $packages[2].Id
    Assert-AreEqual "1.0.0-a" $packages[2].Version[1]

    Assert-AreEqual "PreReleaseTestPackage.A" $packages[2].Id
    Assert-AreEqual "1.0.0" $packages[2].Version[0]
}

function Test-GetPackageUpdatesDoNotReturnPrereleasePackagesIfFlagIsNotSpecified {
    param(
        $context
    )

    # Arrange
    $p = New-ClassLibrary

    $p | Install-Package PrereleaseTestPackage -Version 1.0.0.0-b -Source $context.RepositoryRoot -Prerelease
    Assert-Package $p 'PrereleaseTestPackage' '1.0.0.0-b'
    
    # Act
    $updates = @(Get-Package -Updates -Source $context.RepositoryRoot)

    # Assert
    Assert-AreEqual 1 $updates.Count
    Assert-AreEqual PrereleaseTestPackage $updates[0].Id
    #Assert-AreEqual '1.0.0.0' $updates[0].Version
}

function Test-GetPackageUpdatesReturnPrereleasePackagesIfFlagIsSpecified {
    param(
        $context
    )

    # Arrange
    $p = New-ClassLibrary

    $p | Install-Package PrereleaseTestPackage -Version 1.0.0.0-a -Source $context.RepositoryRoot -Prerelease
    Assert-Package $p 'PrereleaseTestPackage' '1.0.0.0-a'
    
    # Act
    $updates = @(Get-Package -Updates -Prerelease -Source $context.RepositoryRoot)

    # Assert
    Assert-AreEqual 1 $updates.Count
    Assert-AreEqual 'PrereleaseTestPackage' $updates[0].Id
    Assert-AreEqual '1.0.1-a' $updates[0].Version
}

function Test-GetPackageDoesNotThrowIfSolutionIsTemporary {
    param($context)

    # Arrange
    New-TextFile
    
    # Act and Assert
    Assert-Throws { Get-Package } "The current environment doesn't have a solution open."
}

function Test-GetPackageUpdatesReturnAllVersionsIfFlagIsSpecified 
{
    param
    (
        $context
    )

    # Arrange
    $p = New-ClassLibrary

    $p | Install-Package PrereleaseTestPackage -Version '1.0.0-a' -Source $context.RepositoryRoot -Prerelease
    Assert-Package $p 'PrereleaseTestPackage' '1.0.0-a'
    
    # Act
    $updates = @(Get-Package -Updates -AllVersions -Source $context.RepositoryRoot)

    # Assert
    Assert-AreEqual 1 $updates.Count
    Assert-AreEqual 'PrereleaseTestPackage' $updates[0].Id
    Assert-AreEqual '1.0.0' $updates[0].Version
}

function Test-GetPackageUpdatesReturnAllVersionsAndPrereleaseVersionsIfTwoFlagsAreSpecified 
{
    param
    (
        $context
    )

    # Arrange
    $p = New-ClassLibrary

    $p | Install-Package PrereleaseTestPackage -Version '1.0.0-b' -Source $context.RepositoryRoot -Prerelease
    Assert-Package $p 'PrereleaseTestPackage' '1.0.0-b'
    
    # Act
    $updates = @(Get-Package -Updates -AllVersions -Prerelease -Source $context.RepositoryRoot)

    # Assert
    Assert-AreEqual 2 $updates.Count

    Assert-AreEqual 'PrereleaseTestPackage' $updates[0].Id
    Assert-AreEqual '1.0.0' $updates[0].Version

    Assert-AreEqual 'PrereleaseTestPackage' $updates[1].Id
    Assert-AreEqual '1.0.1-a' $updates[1].Version
}

function Test-GetInstalledPackageWithFilterReturnsCorrectPackage
{
    param
    (
        $context
    )

    # Arrange
    $p = New-ClassLibrary
    
    $p | Install-Package PrereleaseTestPackage -Version '1.0.0-b' -Source $context.RepositoryRoot
    Assert-Package $p 'PrereleaseTestPackage' '1.0.0-b'
    
    # Act
    $packages = @(Get-Package 'Prerelease')

    # Assert
    Assert-AreEqual 1 $packages.Count
    Assert-AreEqual 'PrereleaseTestPackage' $packages[0].Id
    Assert-AreEqual '1.0.0-b' $packages[0].Version
}