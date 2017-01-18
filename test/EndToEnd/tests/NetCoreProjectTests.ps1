# basic create for .net core template
function Test-NetCoreCreateConsoleApp {

    # Arrange & Act
    $project = New-NetCoreConsoleApp ConsoleApp
    
    # Assert
    Assert-NetCoreProjectCreation $project
}

# install package test for .net core
function Test-NetCoreInstallPackage {
    
    # Arrange
    $project = New-NetCoreConsoleApp ConsoleApp
    $id = 'NuGet.Versioning'
    $version = '3.5.0'
    Assert-NetCoreProjectCreation $project

    # Act 
    Install-Package $id -ProjectName $project.Name -version $version
    $project.Save($project.FullName)

    # Assert
    Assert-NetCorePackageInstall $project $id $version
}

# install and uninstall package test for .net core
function Test-NetCoreUninstallPackage {
    
    # Arrange
    $project = New-NetCoreConsoleApp ConsoleApp
    $id = 'NuGet.Versioning'
    $version = '3.5.0'
    Assert-NetCoreProjectCreation $project

    # Act 
    Install-Package $id -ProjectName $project.Name -version $version
    $project.Save($project.FullName)
    Assert-NetCorePackageInstall $project $id $version

    Uninstall-Package $id -ProjectName $project.Name
    $project.Save($project.FullName)

    # Assert
    Assert-NetCorePackageUninstall $project $id
}

# install multiple packages test for .net core
function Test-NetCoreInstallMultiplePackages {

    # Arrange
    $project = New-NetCoreConsoleApp ConsoleApp
    $id1 = 'NuGet.Versioning'
    $version1 = '3.5.0'
    $id2 = 'NUnit'
    $version2 = '3.6.0'

    # Act
    Install-Package $id1 -ProjectName $project.Name -version $version1
    Install-Package $id2 -ProjectName $project.Name -version $version2
    $project.Save($project.FullName)

    # Assert
    Assert-NetCorePackageInstall $project $id1 $version1
    Assert-NetCorePackageInstall $project $id2 $version2
}

# install and uninstall multiple packages test for .net core
function Test-NetCoreUninstallMultiplePackage {
    
    # Arrange
    $project = New-NetCoreConsoleApp ConsoleApp
    $id1 = 'NuGet.Versioning'
    $version1 = '3.5.0'
    $id2 = 'NUnit'
    $version2 = '3.6.0'

    # Act
    Install-Package $id1 -ProjectName $project.Name -version $version1
    Install-Package $id2 -ProjectName $project.Name -version $version2
    $project.Save($project.FullName)
    Assert-NetCorePackageInstall $project $id1 $version1
    Assert-NetCorePackageInstall $project $id2 $version2
    Uninstall-Package $id1 -ProjectName $project.Name
    Uninstall-Package $id2 -ProjectName $project.Name
    $project.Save($project.FullName)

    # Assert
    Assert-NetCorePackageUninstall $project $id1
    Assert-NetCorePackageUninstall $project $id2
}

# install and upgrade package test for .net core
function Test-NetCoreUpgradePackage {
    
    # Arrange
    $project = New-NetCoreConsoleApp ConsoleApp
    $id = 'NuGet.Versioning'
    $oldVersion = '3.5.0'
    $newVersion = '4.0.0-rc2'
    Assert-NetCoreProjectCreation $project

    # Act 
    Install-Package $id -ProjectName $project.Name -version $oldVersion
    $project.Save($project.FullName)
    Assert-NetCorePackageInstall $project $id $oldVersion

    Update-Package $id -ProjectName $project.Name -version $newVersion
    $project.Save($project.FullName)

    # Assert
    Assert-NetCorePackageInstall $project $id $newVersion
}

# install and downgrade package test for .net core
function Test-NetCoreDowngradePackage {
    
    # Arrange
    $project = New-NetCoreConsoleApp ConsoleApp
    $id = 'NuGet.Versioning'
    $oldVersion = '4.0.0-rc2'
    $newVersion = '3.5.0'
    Assert-NetCoreProjectCreation $project

    # Act 
    Install-Package $id -ProjectName $project.Name -version $oldVersion
    $project.Save($project.FullName)
    Assert-NetCorePackageInstall $project $id $oldVersion

    Update-Package $id -ProjectName $project.Name -version $newVersion
    $project.Save($project.FullName)

    # Assert
    Assert-NetCorePackageInstall $project $id $newVersion
}

# project reference test for .net core
function Test-NetCoreProjectReference {
    
    # Arrange
    $projectA = New-NetCoreConsoleApp ConsoleAppA
    $projectB = New-NetCoreConsoleApp ConsoleAppB

    Assert-NetCoreProjectCreation $projectA
    Assert-NetCoreProjectCreation $projectB

    # Act 
    Add-ProjectReference $projectA $projectB

    $projectA.Save($projectA.FullName)
    $projectB.Save($projectB.FullName)

    # Assert
    Assert-NetCoreProjectReference $projectA $projectB
}

# # P.Json project reference test for .net core
# function Test-NetCoreProjectJsonProjectReference {
    
#     # Arrange
#     $projectA = New-NetCoreConsoleApp ConsoleAppA
#     $projectB = New-BuildIntegratedProj UAPAppB

#     Assert-NetCoreProjectCreation $projectA

#     # Act 
#     Add-ProjectReference $projectA $projectB

#     $projectA.Save($projectA.FullName)

#     # Assert
#     Assert-NetCoreProjectReference $projectA $projectB
# }

# transitive package dependency test for .net core
# A -> B
# B -> C
# C -> Nuget.Versioning 3.5.0
# Assert A has reference to NuGet.Versioning
function Test-NetCoreTransitivePackage {
    
    # Arrange
    $projectA = New-NetCoreConsoleApp ConsoleAppA
    $projectB = New-NetCoreConsoleApp ConsoleAppB
    $projectC = New-NetCoreConsoleApp ConsoleAppC
    $id = 'NuGet.Versioning'
    $version = '3.5.0'
    Assert-NetCoreProjectCreation $projectA
    Assert-NetCoreProjectCreation $projectB
    Assert-NetCoreProjectCreation $projectC

    # Act 
    Add-ProjectReference $projectB $projectC
    Add-ProjectReference $projectA $projectB
    Install-Package $id -ProjectName $projectC.Name -version $version

    $projectA.Save($projectA.FullName)
    $projectB.Save($projectB.FullName)
    $projectC.Save($projectC.FullName)

    # Assert
    Assert-NetCorePackageInstall $projectC $id $version
    Assert-NetCorePackageInLockFile $projectB $id $version
    Assert-NetCorePackageInLockFile $projectA $id $version
}

# transitive package dependency limit test for .net core
# A -> X, B
# B -> C
# C -> Nuget.Versioning 3.5.0
# Assert X does not have reference to NuGet.Versioning
function Test-NetCoreTransitivePackageLimit {
    
    # Arrange
    $projectA = New-NetCoreConsoleApp ConsoleAppA
    $projectB = New-NetCoreConsoleApp ConsoleAppB
    $projectC = New-NetCoreConsoleApp ConsoleAppC
    $projectX = New-NetCoreConsoleApp ConsoleAppX
    $id = 'NuGet.Versioning'
    $version = '3.5.0'
    Assert-NetCoreProjectCreation $projectA
    Assert-NetCoreProjectCreation $projectB
    Assert-NetCoreProjectCreation $projectC
    Assert-NetCoreProjectCreation $projectX

    # Act
    Add-ProjectReference $projectA $projectX
    Add-ProjectReference $projectA $projectB
    Add-ProjectReference $projectB $projectC
    Install-Package $id -ProjectName $projectC.Name -version $version

    $projectA.Save($projectA.FullName)
    $projectB.Save($projectB.FullName)
    $projectC.Save($projectC.FullName)
    $projectX.Save($projectX.FullName)

    # Assert
    Assert-NetCorePackageInstall $projectC $id $version
    Assert-NetCorePackageInLockFile $projectB $id $version
    Assert-NetCorePackageInLockFile $projectA $id $version
    Assert-NetCoreNoPackageReference $projectX $id
    Assert-NetCorePackageNotInLockFile $projectX $id 
}