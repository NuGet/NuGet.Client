Verify Xunit 2.1.0 can be installed into a net45 project.
https://github.com/NuGet/Home/issues/1711

function Test-BindingRedirectComplex {
    param($context)

    # Arrange
    $a = New-WebApplication
    $b = New-ConsoleApplication
    $c = New-ClassLibraryNET46

    Add-ProjectReference $a $b
    Add-ProjectReference $b $c

    $projects = @($a, $b)

    # Act
    $c | Install-Package E -Source $testRepositoryPath
    $c | Update-Package F -Safe -Source $testRepositoryPath

    Assert-Package $c E;

    # Assert
    Assert-BindingRedirect $a web.config F '0.0.0.0-1.0.5.0' '1.0.5.0'
    Assert-BindingRedirect $b app.config F '0.0.0.0-1.0.5.0' '1.0.5.0'
}

function Test-SimpleBindingRedirectsWebsite {
    param(
        $context
    )
    # Arrange
    $a = New-WebSite

    # Act
    $a | Install-Package E -Source $testRepositoryPath
    $a | Update-Package F -Safe -Source $testRepositoryPath

    # Assert
    Assert-Package $a E;
    Assert-BindingRedirect $a web.config F '0.0.0.0-1.0.5.0' '1.0.5.0'
}


function Test-BindingRedirectInstallLargeProject {
    param($context)

    $numProjects = 25
    $projects = 0..$numProjects | %{ New-ClassLibraryNET46 $_ }
    $p = New-WebApplication

    for($i = 0; $i -lt $numProjects; $i++) {
        Add-ProjectReference $projects[$i] $projects[$i+1]
    }

    Add-ProjectReference $p $projects[0]

    $projects[$projects.Length - 1] | Install-Package E -Source $testRepositoryPath
    $projects[$projects.Length - 1] | Update-Package F -Safe -Source $testRepositoryPath
    Assert-BindingRedirect $p web.config F '0.0.0.0-1.0.5.0' '1.0.5.0'
}

function Test-BindingRedirectDuplicateReferences {
    param($context)

    # Arrange
    $a = New-WebApplication
    $b = New-ConsoleApplication
    $c = New-ClassLibraryNET46

    ($a, $b) | Install-Package A -Source $testRepositoryPath -IgnoreDependencies

    Add-ProjectReference $a $b
    Add-ProjectReference $b $c

    # Act
    $c | Install-Package E -Source $testRepositoryPath
    $c | Update-Package F -Safe -Source $testRepositoryPath

    Assert-Package $c E

    # Assert
    Assert-BindingRedirect $a web.config F '0.0.0.0-1.0.5.0' '1.0.5.0'
    Assert-BindingRedirect $b app.config F '0.0.0.0-1.0.5.0' '1.0.5.0'
}

function Test-BindingRedirectClassLibraryWithDifferentDependents {
    param($context)

    # Arrange
    $a = New-WebApplication
    $b = New-ConsoleApplication
    $c = New-ClassLibraryNET46

    ($a, $b) | Install-Package A -Source $testRepositoryPath -IgnoreDependencies

    Add-ProjectReference $a $c
    Add-ProjectReference $b $c

    # Act
    $c | Install-Package E -Source $testRepositoryPath
    $c | Update-Package F -Safe -Source $testRepositoryPath

    Assert-Package $c E

    # Assert
    Assert-BindingRedirect $a web.config F '0.0.0.0-1.0.5.0' '1.0.5.0'
    Assert-BindingRedirect $b app.config F '0.0.0.0-1.0.5.0' '1.0.5.0'
}

function Test-BindingRedirectProjectsThatReferenceSameAssemblyFromDifferentLocations {
    param($context)

    # Arrange
    $a = New-WebApplication
    $b = New-ConsoleApplication
    $c = New-ClassLibraryNET46
####### Change to A
    $a | Install-Package G -Source $testRepositoryPath -IgnoreDependencies
    $aPath = ls (Get-SolutionDir) -Recurse -Filter G.dll
    cp $aPath.FullName (Get-SolutionDir)
    $aNewLocation = Join-Path (Get-SolutionDir) G.dll

    $b.Object.References.Add($aNewLocation)

    Add-ProjectReference $a $b
    Add-ProjectReference $b $c

    # Act
    $c | Install-Package E -Source $testRepositoryPath
    $c | Update-Package F -Safe -Source $testRepositoryPath

    Assert-Package $c E

    # Assert
    Assert-BindingRedirect $a web.config F '0.0.0.0-1.0.5.0' '1.0.5.0'
    Assert-BindingRedirect $b app.config F '0.0.0.0-1.0.5.0' '1.0.5.0'
}

function Test-BindingRedirectsMixNonStrongNameAndStrongNameAssemblies {
    param(
        $context
    )
    # Arrange
    $a = New-ConsoleApplication

    # Act
    Write-Host "-------------------"    $context.RepositoryRoot
    $a | Install-Package PackageWithNonStrongNamedLibA -Source $context.RepositoryRoot
    $a | Install-Package PackageWithNonStrongNamedLibB -Source $context.RepositoryRoot

    # Assert
    Assert-Package $a PackageWithNonStrongNamedLibA
    Assert-Package $a PackageWithNonStrongNamedLibA
    Assert-Package $a PackageWithStrongNamedLib 1.1
    Assert-Reference $a A 1.0.0.0
    Assert-Reference $a B 1.0.0.0
    Assert-Reference $a Core 1.1.0.0

    Assert-BindingRedirect $a app.config Core '0.0.0.0-1.1.0.0' '1.1.0.0'
}

function Test-BindingRedirectProjectsThatReferenceDifferentVersionsOfSameAssembly {
    param($context)

    # Arrange
    $a = New-WebApplication
    $b = New-ConsoleApplication
    $c = New-ClassLibraryNET46

    $a | Install-Package A -Source $testRepositoryPath -IgnoreDependencies
    $b | Install-Package A -Version 1.0 -Source $testRepositoryPath -IgnoreDependencies

    Add-ProjectReference $a $b
    Add-ProjectReference $b $c

    # Act
    $c | Install-Package E -Source $testRepositoryPath
    $c | Update-Package F -Safe -Source $testRepositoryPath

    Assert-Package $c E

    # Assert
    Assert-BindingRedirect $a web.config F '0.0.0.0-1.0.5.0' '1.0.5.0'
    Assert-BindingRedirect $b app.config F '0.0.0.0-1.0.5.0' '1.0.5.0'
}
