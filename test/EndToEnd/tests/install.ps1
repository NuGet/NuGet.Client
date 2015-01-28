function Test-SinglePackageInstallIntoSingleProject {
    # Arrange
    $project = New-ConsoleApplication
    
    # Act
    Install-Package FakeItEasy -ProjectName $project.Name -version 1.8.0
    
    # Assert
    Assert-Reference $project Castle.Core
    Assert-Reference $project FakeItEasy   
    Assert-Package $project FakeItEasy
    Assert-Package $project Castle.Core
    Assert-SolutionPackage FakeItEasy
    Assert-SolutionPackage Castle.Core
}

function Test-PackageInstallWhatIf {
    # Arrange
    $project = New-ConsoleApplication
    
    # Act
    Install-Package FakeItEasy -Project $project.Name -version 1.8.0 -WhatIf
    
    # Assert: no packages are installed
	Assert-Null (Get-ProjectPackage $project FakeItEasy)
}

# Test install-package -WhatIf to downgrade an installed package.
function Test-PackageInstallDowngradeWhatIf {
    # Arrange
    $project = New-ConsoleApplication    
    
    Install-Package TestUpdatePackage -Version 2.0.0.0 -Source $context.RepositoryRoot    
	Assert-Package $project TestUpdatePackage '2.0.0.0'

	# Act
	Install-Package TestUpdatePackage -Version 1.0.0.0 -Source $context.RepositoryRoot -WhatIf

	# Assert
	# that the installed package is not touched.
	Assert-Package $project TestUpdatePackage '2.0.0.0'
}

function Test-WebsiteSimpleInstall {
    param(
        $context
    )
    # Arrange
    $p = New-WebSite
    
    # Act
    Write-Host $context.RepositoryPath
    Install-Package -Source $context.RepositoryPath -Project $p.Name MyAwesomeLibrary
    
    # Assert
    Assert-Package $p MyAwesomeLibrary
    Assert-SolutionPackage MyAwesomeLibrary
    
    $refreshFilePath = Join-Path (Get-ProjectDir $p) "bin\MyAwesomeLibrary.dll.refresh"
    $content = Get-Content $refreshFilePath
    
    Assert-AreEqual "..\packages\MyAwesomeLibrary.1.0\lib\net40\MyAwesomeLibrary.dll" $content
}