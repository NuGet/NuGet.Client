# basic install into a build integrated project
function Test-BuildIntegratedInstallPackage {
    # Arrange
    $project = New-UAPApplication UAPApp

    # Act
    Install-Package NuGet.Versioning -ProjectName $project.Name -version 1.0.7
    
    # Assert
    Assert-ProjectJsonDependency $project NuGet.Versioning 1.0.7
    Assert-ProjectJsonLockFilePackage $project NuGet.Versioning 1.0.7
    Assert-ProjectJsonLockFileRuntimeAssembly $project lib/portable-net40+win/NuGet.Versioning.dll
}

# install multiple packages into a project
function Test-BuildIntegratedInstallMultiplePackages {
    # Arrange
    $project = New-UAPApplication UAPApp

    # Act
    Install-Package NuGet.Versioning -ProjectName $project.Name -version 1.0.7
    Install-Package DotNetRDF -version 1.0.8.3533
    
    # Assert
    Assert-ProjectJsonDependency $project NuGet.Versioning 1.0.7
    Assert-ProjectJsonDependency $project DotNetRDF 1.0.8.3533
    Assert-ProjectJsonLockFilePackage $project NuGet.Versioning 1.0.7
    Assert-ProjectJsonLockFilePackage $project DotNetRDF 1.0.8.3533
    Assert-ProjectJsonLockFilePackage $project Newtonsoft.Json 6.0.8
    Assert-ProjectJsonLockFileRuntimeAssembly $project lib/portable-net40+win/NuGet.Versioning.dll
    Assert-ProjectJsonLockFileRuntimeAssembly $project lib/netcore45/Newtonsoft.Json.dll
    Assert-ProjectJsonLockFileRuntimeAssembly $project lib/portable-net4+sl5+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1/dotNetRDF.dll
    Assert-ProjectJsonLockFileRuntimeAssembly $project lib/portable-net4+sl5+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1/Portable.Runtime.dll
}

# install and then uninstall multiple packages
function Test-BuildIntegratedInstallAndUninstallAll {
    # Arrange
    $project = New-UAPApplication UAPApp

    # Act
    Install-Package NuGet.Versioning -ProjectName $project.Name -version 1.0.7
    Install-Package DotNetRDF  -ProjectName $project.Name -version 1.0.8.3533
    Uninstall-Package NuGet.Versioning -ProjectName $project.Name
    Uninstall-Package DotNetRDF -ProjectName $project.Name
    
    # Assert
    Assert-ProjectJsonDependencyNotFound $project NuGet.Versioning
    Assert-ProjectJsonDependencyNotFound $project DotNetRDF
    Assert-ProjectJsonLockFilePackageNotFound $project NuGet.Versioning
    Assert-ProjectJsonLockFilePackageNotFound $project DotNetRDF
    Assert-ProjectJsonLockFilePackageNotFound $project Newtonsoft.Json
}

# install a package with dependencies
function Test-BuildIntegratedInstallAndVerifyLockFileContainsChildDependency {
    # Arrange
    $project = New-UAPApplication UAPApp

    # Act
    Install-Package json-ld.net -ProjectName $project.Name -version 1.0.4
    
    # Assert
    Assert-ProjectJsonLockFilePackage $project Newtonsoft.Json 4.0.1
    Assert-ProjectJsonDependencyNotFound $project Newtonsoft.Json
} 

# basic uninstall
function Test-BuildIntegratedUninstallPackage {    
    # Arrange
    $project = New-UAPApplication UAPApp
    Install-Package NuGet.Versioning -ProjectName $project.Name -version 1.0.7

    # Act
    Uninstall-Package NuGet.Versioning -ProjectName $project.Name
    
    # Assert
    Assert-ProjectJsonDependencyNotFound $project NuGet.Versioning
    Assert-ProjectJsonLockFilePackageNotFound $project NuGet.Versioning
}

# basic update package
function Test-BuildIntegratedUpdatePackage {    
    # Arrange
    $project = New-UAPApplication UAPApp
    Install-Package NuGet.Versioning -ProjectName $project.Name -version 1.0.5

    # Act
    Update-Package NuGet.Versioning -ProjectName $project.Name -version 1.0.6
    
    # Assert
    Assert-ProjectJsonDependency $project NuGet.Versioning 1.0.6
    Assert-ProjectJsonLockFilePackage $project NuGet.Versioning 1.0.6
    Assert-ProjectJsonLockFileRuntimeAssembly $project lib/portable-net40+win/NuGet.Versioning.dll
}

function Test-BuildIntegratedUpdateNonExistantPackage {    
    # Arrange
    $project = New-UAPApplication UAPApp

    # Act and Assert
    Assert-Throws { Update-Package NuGet.Versioning -ProjectName $project.Name -version 1.0.6 } "'NuGet.Versioning' was not installed in any project. Update failed."
}

function Test-BuildIntegratedUninstallNonExistantPackage {    
    # Arrange
    $project = New-UAPApplication UAPApp

    # Act and Assert
    Assert-Throws { Uninstall-Package NuGet.Versioning -ProjectName $project.Name -version 1.0.6 } "Package 'NuGet.Versioning' to be uninstalled could not be found in project 'UAPApp'"
}

function Test-BuildIntegratedLockFileIsCreatedOnBuild {
    # Arrange
    $project = New-UAPApplication UAPApp
    Install-Package NuGet.Versioning -ProjectName $project.Name -version 1.0.7
    Remove-ProjectJsonLockFile $project

    # Act
    Build-Solution
    
    # Assert
    Assert-ProjectJsonLockFilePackage $project NuGet.Versioning 1.0.7
}

function Test-BuildIntegratedInstallPackagePrefersWindowsOverWindowsPhoneApp {
    # Arrange
    $project = New-UAPApplication UAPApp

    # Act
    Install-Package automapper -ProjectName $project.Name -version 3.3.1
    
    # Assert
    Assert-ProjectJsonLockFileRuntimeAssembly $project lib/windows8/AutoMapper.dll
}

function Test-BuildIntegratedInstallPackageWithWPA81 {
    # Arrange
    $project = New-UAPApplication UAPApp

    # Act
    Install-Package kinnara.toolkit -ProjectName $project.Name -version 0.3.0
    
    # Assert
    Assert-ProjectJsonLockFileRuntimeAssembly $project lib/wpa81/Kinnara.Toolkit.dll
}

function Test-BuildIntegratedPackageOverrideDependencyRequirement {
    # Arrange
    $project = New-UAPApplication UAPApp

    # Act
    Install-Package Newtonsoft.Json -ProjectName $project.Name -version 6.0.4
    Install-Package DotNetRDF  -ProjectName $project.Name -version 1.0.8.3533
    
    # Assert
    # DotNetRDF requires json.net >= 6.0.8, but the direct dependency overrides it
    Assert-ProjectJsonLockFilePackage $project Newtonsoft.Json 6.0.4
}

function Test-BuildIntegratedDependencyUpdatedByInstall {
    # Arrange
    $project = New-UAPApplication UAPApp

    # Act
    Install-Package DotNetRDF  -ProjectName $project.Name -version 1.0.8.3533
    Install-Package Newtonsoft.Json -ProjectName $project.Name -version 7.0.1-beta3
    
    # Assert
    # DotNetRDF requires json.net 6.0.8
    Assert-ProjectJsonLockFilePackage $project Newtonsoft.Json 7.0.1-beta3
}