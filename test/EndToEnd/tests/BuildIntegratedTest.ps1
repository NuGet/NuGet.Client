# basic install into a build integrated project
function Test-BuildIntegratedInstallPackage {
    # Arrange
    $project = New-BuildIntegratedProj UAPApp

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
    $project = New-BuildIntegratedProj UAPApp

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
    $project = New-BuildIntegratedProj UAPApp

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
    $project = New-BuildIntegratedProj UAPApp

    # Act
    Install-Package AppSupport.Win81 -ProjectName $project.Name -version 0.0.3-alpha

    # Assert
    Assert-ProjectJsonLockFilePackage $project WindowsAzure.MobileServices 1.0.2
    Assert-ProjectJsonDependencyNotFound $project WindowsAzure.MobileServices
} 

# basic uninstall
function Test-BuildIntegratedUninstallPackage {
    # Arrange
    $project = New-BuildIntegratedProj UAPApp
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
    $project = New-BuildIntegratedProj UAPApp
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
    $project = New-BuildIntegratedProj UAPApp

    # Act and Assert
    Assert-Throws { Update-Package NuGet.Versioning -ProjectName $project.Name -version 1.0.6 } "'NuGet.Versioning' was not installed in any project. Update failed."
}

function Test-BuildIntegratedUninstallNonExistantPackage {
    # Arrange
    $project = New-BuildIntegratedProj UAPApp

    # Act and Assert
    Assert-Throws { Uninstall-Package NuGet.Versioning -ProjectName $project.Name -version 1.0.6 } "Package 'NuGet.Versioning' to be uninstalled could not be found in project 'UAPApp'"
}

function Test-BuildIntegratedLockFileIsCreatedOnBuild {
    # Arrange
    $project = New-BuildIntegratedProj UAPApp
    Install-Package NuGet.Versioning -ProjectName $project.Name -version 1.0.7
    Remove-ProjectJsonLockFile $project

    # Act
    Build-Solution

    # Assert
    Assert-ProjectJsonLockFilePackage $project NuGet.Versioning 1.0.7
}

function Test-BuildIntegratedInstallPackagePrefersWindowsOverWindowsPhoneApp {
    # Arrange
    $project = New-BuildIntegratedProj UAPApp

    # Act
    Install-Package automapper -ProjectName $project.Name -version 3.3.1

    # Assert
    Assert-ProjectJsonLockFileRuntimeAssembly $project lib/windows8/AutoMapper.dll
}

function Test-BuildIntegratedInstallPackageWithWPA81 {
    # Arrange
    $project = New-BuildIntegratedProj UAPApp

    # Act
    Install-Package kinnara.toolkit -ProjectName $project.Name -version 0.3.0

    # Assert
    Assert-ProjectJsonLockFileRuntimeAssembly $project lib/wpa81/Kinnara.Toolkit.dll
}

function Test-BuildIntegratedPackageFailsIfDowngradeWasDetected {
    # Arrange
    $project = New-BuildIntegratedProj UAPApp

    # Act
    Install-Package Newtonsoft.Json -ProjectName $project.Name -version 6.0.4

	# Assert
    # DotNetRDF requires json.net >= 6.0.8, but the direct dependency attempts to downgrade it.
	Install-Package DotNetRDF  -ProjectName $project.Name -version 1.0.8.3533
    Assert-ProjectJsonLockFilePackage $project Newtonsoft.Json 6.0.4
}

function Test-BuildIntegratedDependencyUpdatedByInstall {
    # Arrange
    $project = New-BuildIntegratedProj UAPApp

    # Act
    Install-Package DotNetRDF  -ProjectName $project.Name -version 1.0.8.3533
    Install-Package Newtonsoft.Json -ProjectName $project.Name -version 7.0.1

    # Assert
    # DotNetRDF requires json.net 6.0.8
    Assert-ProjectJsonLockFilePackage $project Newtonsoft.Json 7.0.1
}

function Test-BuildIntegratedInstallPackageInvokeInitScript {
    param(
        $context
    )
    
    # Arrange
    $p = New-BuildIntegratedProj

    # Act
    Install-Package PackageWithScriptsB -Source $context.RepositoryRoot

    # Assert

    # This asserts init.ps1 gets called
    Assert-True (Test-Path function:\Get-WorldB)
}

function Test-BuildIntegratedInstallPackageJsonNet701Beta3 {
    # Arrange
    $project = New-BuildIntegratedProj UAPApp

    # Act
    Install-Package newtonsoft.json -ProjectName $project.Name -version 7.0.1-beta3

    # Assert
    Assert-ProjectJsonLockFileRuntimeAssembly $project "lib/portable-net45+wp80+win8+wpa81+dnxcore50/Newtonsoft.Json.dll"
}

function Test-BuildIntegratedProjectClosure {
    if (!(Verify-BuildIntegratedMsBuildTask)) {
        Write-Host "Skipping BuildIntegratedProjectClosure"
    }

    # Arrange
    $project1 = New-Project BuildIntegratedClassLibrary Project1
    $project2 = New-Project BuildIntegratedClassLibrary Project2
    Add-ProjectReference $project1 $project2

    Install-Package NuGet.Versioning -ProjectName $project2.Name -version 1.0.7
    Remove-ProjectJsonLockFile $project2

    # Act
    Build-Solution

    # Assert
    Assert-ProjectJsonLockFilePackage $project1 NuGet.Versioning 1.0.7
    Assert-ProjectJsonLockFilePackage $project2 NuGet.Versioning 1.0.7
}

function Test-BuildIntegratedProjectClosureWithLegacyProjects {
    if (!(Verify-BuildIntegratedMsBuildTask)) {
        Write-Host "Skipping BuildIntegratedProjectClosureWithLegacyProjects"
    }

    # Arrange
    $project1 = New-Project BuildIntegratedClassLibrary Project1
    $project2 = New-ClassLibrary Project2
    $project3 = New-ClassLibrary Project3

    Add-ProjectReference $project1 $project2
    Add-ProjectReference $project2 $project3

    Install-Package Comparers -ProjectName $project2.Name -version 4.0.0

    # Act
    Build-Solution

    # Assert
    Assert-NotNull Get-ProjectJsonLockFile $project1
}

# Tests that packages are restored on build
function Test-BuildIntegratedMixedLegacyProjects {
    if (!(Verify-BuildIntegratedMsBuildTask)) {
        Write-Host "Skipping BuildIntegratedMixedLegacyProjects"
    }

    # Arrange
    $project1 = New-ClassLibrary
    $project1 | Install-Package Newtonsoft.Json -Version 5.0.6

    $project2 = New-Project BuildIntegratedClassLibrary
    $project2 | Install-Package NuGet.Versioning -Version 1.0.7

    # delete the packages folder
    $packagesDir = Get-PackagesDir
    Remove-Item -Recurse -Force $packagesDir
    Assert-False (Test-Path $packagesDir)

    # delete the lock file
    $lockFile = Get-ProjectJsonLockFilePath $project2
    Remove-Item $lockFile
    Assert-False (Test-Path $lockFile)

    # Act
    Build-Solution

    # Assert
    Assert-True (Test-Path $packagesDir)
    Assert-Package $project1 Newtonsoft.Json
    Assert-True (Test-Path $lockFile)
    Assert-ProjectJsonLockFilePackage $project2 NuGet.Versioning 1.0.7
}

function Test-BuildIntegratedMixedLegacyProjectsProjectJsonOnly {
    if (!(Verify-BuildIntegratedMsBuildTask)) {
        Write-Host "Skipping BuildIntegratedMixedLegacyProjects"
    }

    # Arrange
    $project1 = New-ClassLibrary
    $project1 | Install-Package Newtonsoft.Json -Version 5.0.6

    $project2 = New-Project BuildIntegratedClassLibrary
    $project2 | Install-Package NuGet.Versioning -Version 1.0.7

    # delete the lock file
    $lockFile = Get-ProjectJsonLockFilePath $project2
    Remove-Item $lockFile
    Assert-False (Test-Path $lockFile)

    # Act
    Build-Solution

    # Assert
    Assert-True (Test-Path $lockFile)
    Assert-ProjectJsonLockFilePackage $project2 NuGet.Versioning 1.0.7
}

function Test-BuildIntegratedMixedLegacyProjectsPackagesFolderOnly {
    if (!(Verify-BuildIntegratedMsBuildTask)) {
        Write-Host "Skipping BuildIntegratedMixedLegacyProjects"
    }

    # Arrange
    $project1 = New-ClassLibrary
    $project1 | Install-Package Newtonsoft.Json -Version 5.0.6

    $project2 = New-Project BuildIntegratedClassLibrary
    $project2 | Install-Package NuGet.Versioning -Version 1.0.7

    # delete the packages folder
    $packagesDir = Get-PackagesDir
    Remove-Item -Recurse -Force $packagesDir
    Assert-False (Test-Path $packagesDir)

    # Act
    Build-Solution

    # Assert
    Assert-True (Test-Path $packagesDir)
    Assert-Package $project1 Newtonsoft.Json
}

# Verifies that project.json that specified in project.json referenced transitively through a non-project.json project
# are correctly pulled in.
function Test-BuildIntegratedTransitiveProjectJsonRestores {
    if (!(Verify-BuildIntegratedMsBuildTask)) {
        Write-Host "Skipping BuildIntegratedMixedLegacyProjects"
    }

    # Arrange
    $project1 = New-Project BuildIntegratedClassLibrary
    $project2 = New-ClassLibrary
    $project3 = New-Project BuildIntegratedClassLibrary

    Add-ProjectReference $project2 $project1
    Add-ProjectReference $project3 $project2

    # Act
    $project1 | Install-Package NuGet.Versioning -Version 1.0.7
    Build-Solution

    # Assert
    Assert-NoPackage $project2 NuGet.Versioning 1.0.7
    Assert-ProjectJsonLockFilePackage $project1 NuGet.Versioning 1.0.7
    Assert-ProjectJsonLockFilePackage $project3 NuGet.Versioning 1.0.7
}

# Verifies that parent projects are restored after an install
function Test-BuildIntegratedParentProjectIsRestoredAfterInstall {
    if (!(Verify-BuildIntegratedMsBuildTask)) {
        Write-Host "Skipping BuildIntegratedMixedLegacyProjects"
    }

    # Arrange
    $project1 = New-BuildIntegratedProj UAPApp1
    $project2 = New-BuildIntegratedProj UAPApp2
    $project3 = New-BuildIntegratedProj UAPApp3
    $project4 = New-BuildIntegratedProj UAPApp4

    Add-ProjectReference $project1 $project2
    Add-ProjectReference $project2 $project3
    Add-ProjectReference $project3 $project4

    # Act
    $project3 | Install-Package NuGet.Versioning -Version 1.0.7

    # Assert
    Assert-ProjectJsonLockFilePackage $project1 NuGet.Versioning 1.0.7
    Assert-ProjectJsonLockFilePackage $project2 NuGet.Versioning 1.0.7
    Assert-ProjectJsonLockFilePackage $project3 NuGet.Versioning 1.0.7

    # the child project should not be restored
    Assert-ProjectJsonLockFileDoesNotExist $project4
}

# Verifies that parent projects are restored after an uninstall
function Test-BuildIntegratedParentProjectIsRestoredAfterUnInstall {
    if (!(Verify-BuildIntegratedMsBuildTask)) {
        Write-Host "Skipping BuildIntegratedMixedLegacyProjects"
    }

    # Arrange
    $project1 = New-BuildIntegratedProj UAPApp1
    $project2 = New-BuildIntegratedProj UAPApp2
    $project3 = New-BuildIntegratedProj UAPApp3
    $project4 = New-BuildIntegratedProj UAPApp4

    Add-ProjectReference $project1 $project2
    Add-ProjectReference $project2 $project3
    Add-ProjectReference $project3 $project4

    $project3 | Install-Package NuGet.Versioning -Version 1.0.7
    Remove-ProjectJsonLockFile $project1
    Remove-ProjectJsonLockFile $project2
    Remove-ProjectJsonLockFile $project3

    # Act
    $project3 | Uninstall-Package NuGet.Versioning -Version 1.0.7

    # Assert
    Assert-ProjectJsonLockFilePackageNotFound $project1 NuGet.Versioning
    Assert-ProjectJsonLockFilePackageNotFound $project2 NuGet.Versioning
    Assert-ProjectJsonLockFilePackageNotFound $project3 NuGet.Versioning

    # the child project should not be restored
    Assert-ProjectJsonLockFileDoesNotExist $project4
}

# Verifies that parent projects are restored after an update
function Test-BuildIntegratedParentProjectIsRestoredAfterUpdate {
    if (!(Verify-BuildIntegratedMsBuildTask)) {
        Write-Host "Skipping BuildIntegratedMixedLegacyProjects"
    }

    # Arrange
    $project1 = New-BuildIntegratedProj UAPApp1
    $project2 = New-BuildIntegratedProj UAPApp2
    $project3 = New-BuildIntegratedProj UAPApp3
    $project4 = New-BuildIntegratedProj UAPApp4

    Add-ProjectReference $project1 $project2
    Add-ProjectReference $project2 $project3
    Add-ProjectReference $project3 $project4

    $project3 | Install-Package NuGet.Versioning -Version 1.0.5
    Remove-ProjectJsonLockFile $project1
    Remove-ProjectJsonLockFile $project2
    Remove-ProjectJsonLockFile $project3

    # Act    
    $project3 | Update-Package NuGet.Versioning -Version 1.0.7

    # Assert
    Assert-ProjectJsonLockFilePackage $project1 NuGet.Versioning 1.0.7    
    Assert-ProjectJsonLockFilePackage $project2 NuGet.Versioning 1.0.7
    Assert-ProjectJsonLockFilePackage $project3 NuGet.Versioning 1.0.7

    # the child project should not be restored
    Assert-ProjectJsonLockFileDoesNotExist $project4
}

# Verify that all build integrated projects are included in the closure, even when a 
# non-build integrated project exists in between them
function Test-BuildIntegratedParentProjectIsRestoredAfterInstallWithClassLibInTree {
    if (!(Verify-BuildIntegratedMsBuildTask)) {
        Write-Host "Skipping BuildIntegratedMixedLegacyProjects"
    }

    # Arrange
    $project1 = New-Project BuildIntegratedClassLibrary
    $project2 = New-ClassLibrary ClassLib2
    $project3 = New-Project BuildIntegratedClassLibrary

    Add-ProjectReference $project1 $project2
    Add-ProjectReference $project2 $project3

    # Act
    $project3 | Install-Package NuGet.Versioning -Version 1.0.7

    # Assert
    Assert-ProjectJsonLockFilePackage $project1 NuGet.Versioning 1.0.7
}

function Test-BuildIntegratedParentProjectsStayLockedOnUninstall {
    if (!(Verify-BuildIntegratedMsBuildTask)) {
        Write-Host "Skipping BuildIntegratedMixedLegacyProjects"
    }

    # Arrange
    $project1 = New-Project BuildIntegratedClassLibrary
    $project2 = New-Project BuildIntegratedClassLibrary
    $project3 = New-ClassLibrary ClassLib2

    Add-ProjectReference $project1 $project2
    Add-ProjectReference $project2 $project3

    $project2 | Install-Package NuGet.Versioning -Version 1.0.7

    Set-LockFileLocked $project1 $True
    Set-LockFileLocked $project2 $True

    # Act
    Uninstall-Package NuGet.Versioning -ProjectName $project2.Name

    $project1Locked = Get-LockFileLocked $project1
    $project2Locked = Get-LockFileLocked $project2

    # Assert
    Assert-True $project1 $project1Locked
    Assert-True $project2 $project2Locked
}

function Test-BuildIntegratedParentProjectsStayLockedOnBuild {
    if (!(Verify-BuildIntegratedMsBuildTask)) {
        Write-Host "Skipping BuildIntegratedMixedLegacyProjects"
    }

    # Arrange
    $project1 = New-Project BuildIntegratedClassLibrary
    $project2 = New-Project BuildIntegratedClassLibrary
    $project3 = New-ClassLibrary ClassLib2

    Add-ProjectReference $project1 $project2
    Add-ProjectReference $project2 $project3

    $project2 | Install-Package NuGet.Versioning -Version 1.0.7

    Set-LockFileLocked $project1 $True
    Set-LockFileLocked $project2 $True

    # Act
    Build-Solution

    $project1Locked = Get-LockFileLocked $project1
    $project2Locked = Get-LockFileLocked $project2

    # Assert
    Assert-True $project1 $project1Locked
    Assert-True $project2 $project2Locked
}