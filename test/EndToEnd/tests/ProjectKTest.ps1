# Basic install into project K project
function Test-ProjectKInstallPackage {
    # Arrange
    $project = New-DNXClassLibrary

    # Act
    Install-Package jQuery -ProjectName $project.Name -version 2.1.4

    # Assert
    Assert-ProjectJsonDependency $project jQuery 2.1.4
}

# Basic uninstall
function Test-ProjectKUninstallPackage {
    # Arrange
    $project = New-DNXConsoleApp
    Install-Package EntityFramework -ProjectName $project.Name -version 6.1.3

    # Act
    Uninstall-Package EntityFramework 

    # Assert
    Assert-ProjectJsonDependencyNotFound $project EntityFramework
}

# Basic update
function ProjectKUpdatePackage {
    # Arrange
    $project = New-DNXConsoleApp
    Install-Package log4net -ProjectName $project.Name -version 2.0.1

    # Act
    Update-Package log4net 

    # Assert
    Assert-ProjectJsonDependencyWithinTargetFramework $project log4net 2.0.3
}

# Install multiple packages
function Test-ProjectKInstallMultiplePackages {
    # Arrange
    $project = New-DNXClassLibrary

    # Act
    Install-Package newtonsoft.json -ProjectName $project.Name -version 7.0.1
	Install-Package jQuery.Validation -version 1.14.0 -DependencyVersion HighestMinor

    # Assert
	Assert-ProjectJsonDependency $project newtonsoft.json 7.0.1
	Assert-ProjectJsonDependency $project jQuery.Validation 1.14.0
	Assert-ProjectJsonDependencyNotFound $project jQuery
}

# Uninstall multiple packages
function ProjectKUninstallMultiplePackages {
    # Arrange
    $project = New-DNXClassLibrary

    # Act
    Install-Package Microsoft.Dnx.TestHost -ProjectName $project.Name -pre
	Install-Package Knockoutjs -ProjectName $project.Name 
	Install-Package TestPackage.OnlyDNXCore -ProjectName $project.Name 
	Uninstall-Package Microsoft.Dnx.TestHost -RemoveDependencies
	UnInstall-Package TestPackage.OnlyDNXCore 

    # Assert
	Assert-ProjectJsonDependencyNotFound $project Microsoft.Dnx.TestHost
	Assert-ProjectJsonDependencyNotFound $project TestPackage.OnlyDNXCore
	Assert-ProjectJsonDependency $project Knockoutjs
}

# Update to prerelease MVC packages that contains DNXCore50 dependencies
function Test-ProjectKUpdateMVCPackagePrerelease {
    # Arrange
    $project = New-DNXConsoleApp
    Install-Package Microsoft.AspNet.Mvc -ProjectName $project.Name -version 5.2.2

    # Act
    Update-Package Microsoft.AspNet.Mvc -version 6.0.0-beta7

    # Assert
    Assert-ProjectJsonDependency $project Microsoft.AspNet.Mvc 6.0.0-beta7
}

# Update all packages
function Test-ProjectKUpdateAllPackages {
    # Arrange
    $project = New-DNXConsoleApp
    Install-Package TestPackage.danliutestpackage -ProjectName $project.Name -version 6.0.0

    # Act
    Update-Package

    # Assert
    Assert-ProjectJsonDependency $project TestPackage.danliutestpackage 7.0.0
}

# No package update available. Latest version already installed.
function ProjectKUpdatePackageNoUpdateAvailable {
    # Arrange
    $project = New-DNXConsoleApp 

    # Act
	Install-Package TestPackage.JustContent -version 1.0.0
	Update-Package TestPackage.JustContent

	#Assert
    Assert-ProjectJsonDependencyWithinTargetFramework $project TestPackage.JustContent 1.0.0
}

# Update-Package -reinstall
function Test-ProjectKUpdatePackageReinstall {
    # Arrange
    $project = New-DNXClassLibrary
    Install-Package Microsoft.AspNet.Mvc -ProjectName $project.Name -version 5.2.3

    # Act
    Update-Package Microsoft.AspNet.Mvc -reinstall 

    # Assert
    Assert-ProjectJsonDependency $project Microsoft.AspNet.Mvc 5.2.3
}

# Test whatIf for package actions
function Test-ProjectKPackageActionsWhatIf {
    # Arrange
    $project = New-DNXConsoleApp

    # Act
    Install-Package jQuery -version 2.0.1 -WhatIf
	Install-Package jQuery -version 2.0.3 
	Uninstall-Package jQuery -WhatIf
	Update-Package jQuery -WhatIf

    # Assert
    Assert-ProjectJsonDependency $project jQuery 2.0.3
}

function Test-ProjectKInstallNonExistentPackage {
    # Arrange
    $project = New-DNXClassLibrary

    # Act and Assert
    Assert-Throws { Install-Package NonExisting } "Unable to find package 'NonExisting'"
}

function Test-ProjectKUpdateNonExistentPackage {
    # Arrange
    $project = New-DNXConsoleApp

    # Act and Assert
    Assert-Throws { Update-Package WebGrease -ProjectName $project.Name -version 1.0.5 } "'WebGrease' was not installed in any project. Update failed."
}

function Test-ProjectKUninstallNonExistentPackage {
    # Arrange
    $project = New-DNXClassLibrary

    # Act and Assert
	$expectedMessage = "Package 'Antlr' to be uninstalled could not be found in project '" + $project.Name + "'"
    Assert-Throws { Uninstall-Package Antlr -ProjectName $project.Name } $expectedMessage
}

# This test covers the scenario of installing a package that supports only non-dnxcore frameworks
# It should be installed the correct dependency group, not global dependencies.
function ProjectKInstallNonDNXCorePackage {
    # Arrange
    $project = New-DNXClassLibrary

    # Act
	Install-Package Moq -version 4.2.1510.2205
	Build-Solution

	# Assert
	# Verify Moq is not added to global dependencies group, i.e. outer "dependencies": { ... }
    Assert-ProjectJsonDependencyNotFound $project Moq

	# Verify Moq is only added under frameworks/dnx451
	Assert-ProjectJsonDependencyWithinTargetFramework $project Moq 4.2.1510.2205

	# "frameworks": {
    # "dnx451": {
    #  "dependencies": {
    #    "Moq": "4.2.1510.2205"
    #  }
    # },
}