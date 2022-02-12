# Verify Xunit 2.1.0 can be installed into a net45 project.
# https://github.com/NuGet/Home/issues/1711

function Test-BindingRedirectComplex {

    # Arrange
    $a = New-WebApplication
    $b = New-ConsoleApplication
    $c = New-ClassLibraryNET46
    $a | Install-Package PackageWithXmlTransformAndTokenReplacement -source $context.RepositoryRoot

    Add-ProjectReference $a $b
    Add-ProjectReference $b $c
    $projects = @($a, $b)
    Write-Error "xxxxxxxx:    " $context
    # Act
    $c | Install-Package E -source $context.RepositoryRoot
    $c | Update-Package F -Safe -source $context.RepositoryRoot

    Assert-Package $c E;

    # Assert
    Assert-BindingRedirect $a web.config F '0.0.0.0-1.0.5.0' '1.0.5.0'
    Assert-BindingRedirect $b app.config F '0.0.0.0-1.0.5.0' '1.0.5.0'
}

function Test-SimpleBindingRedirectsWebsite {

    # Arrange
    $a = New-WebSite
    Write-Error "zzzz:    " $context
    $a| Install-Package PackageWithFolder -source $context.RepositoryRoot
    # Act
    $a | Install-Package -source $context.RepositoryRoot -Id E
    $a | Update-Package -Safe -source $context.RepositoryRoot -Id F

    # Assert
    Assert-Package $a E;
    Assert-BindingRedirect $a web.config F '0.0.0.0-1.0.5.0' '1.0.5.0'
}

function Test-InstallPackagePreservesProjectConfigFile
{
    param($context)

    # Arrange
    $p = New-ClassLibrary "CoolProject"

    $projectPath = $p.Properties.Item("FullPath").Value
    $packagesConfigPath = Join-Path $projectPath 'packages.CoolProject.config'
    Write-Error "yyyyyyyy:    " $context
    # create file and add to project
    $newFile = New-Item $packagesConfigPath -ItemType File
    '<packages></packages>' > $newFile
    $p.ProjectItems.AddFromFile($packagesConfigPath)

    # Act
    $p | Install-Package PackageWithFolder -source $context.RepositoryRoot

    # Assert
    Assert-Package $p PackageWithFolder
    Assert-NotNull (Get-ProjectItem $p 'packages.CoolProject.config')
    Assert-Null (Get-ProjectItem $p 'packages.config')
}

function Test-InstallPackageWithDependencyVersionHighest
{
    param($context)

    # A depends on B >= 1.0.0
    # Available versions of B are: 1.0.0, 1.0.1, 1.2.0, 1.2.1, 2.0.0, 2.0.1
    Write-Error "yyyyyyyy:    " $context
    # Arrange
    $p = New-ClassLibrary

    # Act
    $p | Install-Package A -Source $context.RepositoryPath -DependencyVersion Highest

    # Assert
    Assert-Package $p A 1.0
    Assert-Package $p B 2.0.1
}