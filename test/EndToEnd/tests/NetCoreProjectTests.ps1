# basic create for .net core template
function Test-NetCoreConsoleAppCreate {

    # Arrange & Act
    $project = New-NetCoreConsoleApp ConsoleApp
    
    # Assert
    Assert-NetCoreProjectCreation $project
}

# install package test for .net core
function Test-NetCoreConsoleAppInstallPackage {
    
    # Arrange
    $project = New-NetCoreConsoleApp ConsoleApp
    $id = 'NuGet.Versioning'
    $version = '3.5.0'
    Assert-NetCoreProjectCreation $project

    # Act 
    Install-Package $id -ProjectName $project.Name -version $version
    $project.Save($project.FullName)
    Build-Solution

    # Assert
    Assert-NetCorePackageInstall $project $id $version
}

# install and uninstall package test for .net core
function Test-NetCoreConsoleAppUninstallPackage {
    
    # Arrange
    $project = New-NetCoreConsoleApp ConsoleApp
    $id = 'NuGet.Versioning'
    $version = '3.5.0'
    Assert-NetCoreProjectCreation $project

    # Act 
    Install-Package $id -ProjectName $project.Name -version $version
    $project.Save($project.FullName)
    Build-Solution
    Assert-NetCorePackageInstall $project $id $version

    Uninstall-Package $id -ProjectName $project.Name
    $project.Save($project.FullName)
    Build-Solution

    # Assert
    Assert-NetCorePackageUninstall $project $id
}

# install multiple packages test for .net core
function Test-NetCoreConsoleAppInstallMultiplePackages {

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
    Build-Solution

    # Assert
    Assert-NetCorePackageInstall $project $id1 $version1
    Assert-NetCorePackageInstall $project $id2 $version2
}

# install and uninstall multiple packages test for .net core
function Test-NetCoreConsoleAppUninstallMultiplePackage {
    
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
    Build-Solution
    Assert-NetCorePackageInstall $project $id1 $version1
    Assert-NetCorePackageInstall $project $id2 $version2
    Uninstall-Package $id1 -ProjectName $project.Name
    Uninstall-Package $id2 -ProjectName $project.Name
    $project.Save($project.FullName)
    Build-Solution

    # Assert
    Assert-NetCorePackageUninstall $project $id1
    Assert-NetCorePackageUninstall $project $id2
}

# install and upgrade package test for .net core
function Test-NetCoreConsoleAppUpgradePackage {
    
    # Arrange
    $project = New-NetCoreConsoleApp ConsoleApp
    $id = 'NuGet.Versioning'
    $oldVersion = '3.5.0'
    $newVersion = '4.0.0-rc2'
    Assert-NetCoreProjectCreation $project

    # Act 
    Install-Package $id -ProjectName $project.Name -version $oldVersion
    $project.Save($project.FullName)
    Build-Solution
    Assert-NetCorePackageInstall $project $id $oldVersion

    Update-Package $id -ProjectName $project.Name -version $newVersion
    $project.Save($project.FullName)
    Build-Solution

    # Assert
    Assert-NetCorePackageInstall $project $id $newVersion
}

# install and downgrade package test for .net core
function Test-NetCoreConsoleAppDowngradePackage {
    
    # Arrange
    $project = New-NetCoreConsoleApp ConsoleApp
    $id = 'NuGet.Versioning'
    $oldVersion = '4.0.0-rc2'
    $newVersion = '3.5.0'
    Assert-NetCoreProjectCreation $project

    # Act 
    Install-Package $id -ProjectName $project.Name -version $oldVersion
    $project.Save($project.FullName)
    Build-Solution
    Assert-NetCorePackageInstall $project $id $oldVersion

    Update-Package $id -ProjectName $project.Name -version $newVersion
    $project.Save($project.FullName)
    Build-Solution

    # Assert
    Assert-NetCorePackageInstall $project $id $newVersion
}

# project reference test for .net core
function Test-NetCoreConsoleAppProjectReference {
    
    # Arrange
    $projectA = New-NetCoreConsoleApp ConsoleAppA
    $projectB = New-NetCoreConsoleApp ConsoleAppB

    Assert-NetCoreProjectCreation $projectA
    Assert-NetCoreProjectCreation $projectB

    # Act 
    Add-ProjectReference $projectA $projectB

    $projectA.Save($projectA.FullName)
    $projectB.Save($projectB.FullName)
    Build-Solution

    # Assert
    Assert-NetCoreProjectReference $projectA $projectB
}

# transitive package dependency test for .net core
# A -> B
# B -> C
# C -> Nuget.Versioning 3.5.0
# Assert A has reference to NuGet.Versioning
function Test-NetCoreConsoleAppTransitivePackage {
    
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
    Build-Solution

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
function Test-NetCoreConsoleAppTransitivePackageLimit {
    
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
    Build-Solution

    # Assert
    Assert-NetCorePackageInstall $projectC $id $version
    Assert-NetCorePackageInLockFile $projectB $id $version
    Assert-NetCorePackageInLockFile $projectA $id $version
    Assert-NetCoreNoPackageReference $projectX $id
    Assert-NetCorePackageNotInLockFile $projectX $id 
}

# basic create for .net core template
function Test-NetCoreWebApp10Create {

    # Arrange & Act
    $project = New-NetCoreWebApp10 ConsoleApp
    
    # Assert
    Assert-NetCoreProjectCreation $project
}


# install package test for .net core
function Test-NetCoreWebApp10AppInstallPackage {
    
    # Arrange
    $project = New-NetCoreWebApp10 ConsoleApp
    $id = 'NuGet.Versioning'
    $version = '3.5.0'
    Assert-NetCoreProjectCreation $project

    # Act 
    Install-Package $id -ProjectName $project.Name -version $version
    $project.Save($project.FullName)
    Build-Solution

    # Assert
    Assert-NetCorePackageInstall $project $id $version
}

# install and uninstall package test for .net core
function Test-NetCoreWebApp10UninstallPackage {
    
    # Arrange
    $project = New-NetCoreWebApp10 ConsoleApp
    $id = 'NuGet.Versioning'
    $version = '3.5.0'
    Assert-NetCoreProjectCreation $project

    # Act 
    Install-Package $id -ProjectName $project.Name -version $version
    $project.Save($project.FullName)
    Build-Solution
    Assert-NetCorePackageInstall $project $id $version

    Uninstall-Package $id -ProjectName $project.Name
    $project.Save($project.FullName)
    Build-Solution

    # Assert
    Assert-NetCorePackageUninstall $project $id
}

# install multiple packages test for .net core
function Test-NetCoreWebApp10InstallMultiplePackages {

    # Arrange
    $project = New-NetCoreWebApp10 ConsoleApp
    $id1 = 'NuGet.Versioning'
    $version1 = '3.5.0'
    $id2 = 'NUnit'
    $version2 = '3.6.0'

    # Act
    Install-Package $id1 -ProjectName $project.Name -version $version1
    Install-Package $id2 -ProjectName $project.Name -version $version2
    $project.Save($project.FullName)
    Build-Solution

    # Assert
    Assert-NetCorePackageInstall $project $id1 $version1
    Assert-NetCorePackageInstall $project $id2 $version2
}

# install and uninstall multiple packages test for .net core
function Test-NetCoreWebApp10UninstallMultiplePackage {
    
    # Arrange
    $project = New-NetCoreWebApp10 ConsoleApp
    $id1 = 'NuGet.Versioning'
    $version1 = '3.5.0'
    $id2 = 'NUnit'
    $version2 = '3.6.0'

    # Act
    Install-Package $id1 -ProjectName $project.Name -version $version1
    Install-Package $id2 -ProjectName $project.Name -version $version2
    $project.Save($project.FullName)
    Build-Solution

    Assert-NetCorePackageInstall $project $id1 $version1
    Assert-NetCorePackageInstall $project $id2 $version2
    Uninstall-Package $id1 -ProjectName $project.Name
    Uninstall-Package $id2 -ProjectName $project.Name
    $project.Save($project.FullName)
    Build-Solution

    # Assert
    Assert-NetCorePackageUninstall $project $id1
    Assert-NetCorePackageUninstall $project $id2
}

# install and upgrade package test for .net core
function Test-NetCoreWebApp10UpgradePackage {
    
    # Arrange
    $project = New-NetCoreWebApp10 ConsoleApp
    $id = 'NuGet.Versioning'
    $oldVersion = '3.5.0'
    $newVersion = '4.0.0-rc2'
    Assert-NetCoreProjectCreation $project

    # Act 
    Install-Package $id -ProjectName $project.Name -version $oldVersion
    $project.Save($project.FullName)
    Build-Solution
    Assert-NetCorePackageInstall $project $id $oldVersion

    Update-Package $id -ProjectName $project.Name -version $newVersion
    $project.Save($project.FullName)
    Build-Solution

    # Assert
    Assert-NetCorePackageInstall $project $id $newVersion
}

# install and downgrade package test for .net core
function Test-NetCoreWebApp10DowngradePackage {
    
    # Arrange
    $project = New-NetCoreWebApp10 ConsoleApp
    $id = 'NuGet.Versioning'
    $oldVersion = '4.0.0-rc2'
    $newVersion = '3.5.0'
    Assert-NetCoreProjectCreation $project

    # Act 
    Install-Package $id -ProjectName $project.Name -version $oldVersion
    $project.Save($project.FullName)
    Build-Solution
    Assert-NetCorePackageInstall $project $id $oldVersion

    Update-Package $id -ProjectName $project.Name -version $newVersion
    $project.Save($project.FullName)
    Build-Solution

    # Assert
    Assert-NetCorePackageInstall $project $id $newVersion
}

# project reference test for .net core
function Test-NetCoreWebApp10ProjectReference {
    
    # Arrange
    $projectA = New-NetCoreWebApp10 ConsoleAppA
    $projectB = New-NetCoreWebApp10 ConsoleAppB

    Assert-NetCoreProjectCreation $projectA
    Assert-NetCoreProjectCreation $projectB

    # Act 
    Add-ProjectReference $projectA $projectB

    $projectA.Save($projectA.FullName)
    $projectB.Save($projectB.FullName)
    Build-Solution

    # Assert
    Assert-NetCoreProjectReference $projectA $projectB
}

function Test-NetCoreWebAppExecuteInitScriptsOnlyOnce
{
    param($context)

    # Arrange
    $global:PackageInitPS1Var = 0
    $p = New-NetCoreWebApp10 WebApp
    
    # Act & Assert
    Install-Package PackageInitPS1 -Project $p.Name -Source $context.RepositoryPath
    Build-Solution    
    Assert-True ($global:PackageInitPS1Var -eq 1)

    $p | Install-Package jquery -Version 1.9
    Build-Solution
    Assert-True ($global:PackageInitPS1Var -eq 1)
}

# VSSolutionManager and ProjectSystemCache event test for .net core
function Test-NetCoreProjectSystemCacheUpdateEvent {
    
    # Arrange
    $projectA = New-NetCoreConsoleApp ConsoleAppA
    Assert-NetCoreProjectCreation $projectA

    # Act
    Try
    {
        [API.Test.InternalAPITestHook]::ProjectCacheEventApi_AttachHandler()
        [API.Test.InternalAPITestHook]::CacheUpdateEventCount = 0
        [API.Test.InternalAPITestHook]::ProjectCacheEventApi_InstallPackage("Newtonsoft.Json", "9.0.1")
        $projectA.Save($projectA.FullName)
        Build-Solution
    }
    Finally 
    {
        [API.Test.InternalAPITestHook]::ProjectCacheEventApi_DetachHandler()
        $result = [API.Test.InternalAPITestHook]::CacheUpdateEventCount 
    }

    # Assert
    Assert-True $result -eq 1
}


# # transitive package dependency test for .net core
# # A -> B
# # B -> C
# # C -> Nuget.Versioning 3.5.0
# # Assert A has reference to NuGet.Versioning
# function Test-NetCoreWebApp10TransitivePackage {
    
#     # Arrange
#     $projectA = New-NetCoreWebApp10 ConsoleAppA
#     $projectB = New-NetCoreWebApp10 ConsoleAppB
#     $projectC = New-NetCoreWebApp10 ConsoleAppC
#     $id = 'NuGet.Versioning'
#     $version = '3.5.0'
#     Assert-NetCoreProjectCreation $projectA
#     Assert-NetCoreProjectCreation $projectB
#     Assert-NetCoreProjectCreation $projectC

#     # Act 
#     Add-ProjectReference $projectB $projectC
#     Add-ProjectReference $projectA $projectB
#     Install-Package $id -ProjectName $projectC.Name -version $version

#     $projectA.Save($projectA.FullName)
#     $projectB.Save($projectB.FullName)
#     $projectC.Save($projectC.FullName)
#     Build-Solution

#     # Assert
#     Assert-NetCorePackageInstall $projectC $id $version
#     Assert-NetCorePackageInLockFile $projectB $id $version
#     Assert-NetCorePackageInLockFile $projectA $id $version
# }

# # transitive package dependency limit test for .net core
# # A -> X, B
# # B -> C
# # C -> Nuget.Versioning 3.5.0
# # Assert X does not have reference to NuGet.Versioning
# function Test-NetCoreWebApp10TransitivePackageLimit {
    
#     # Arrange
#     $projectA = New-NetCoreWebApp10 ConsoleAppA
#     $projectB = New-NetCoreWebApp10 ConsoleAppB
#     $projectC = New-NetCoreWebApp10 ConsoleAppC
#     $projectX = New-NetCoreWebApp10 ConsoleAppX
#     $id = 'NuGet.Versioning'
#     $version = '3.5.0'
#     Assert-NetCoreProjectCreation $projectA
#     Assert-NetCoreProjectCreation $projectB
#     Assert-NetCoreProjectCreation $projectC
#     Assert-NetCoreProjectCreation $projectX

#     # Act
#     Add-ProjectReference $projectA $projectX
#     Add-ProjectReference $projectA $projectB
#     Add-ProjectReference $projectB $projectC
#     Install-Package $id -ProjectName $projectC.Name -version $version

#     $projectA.Save($projectA.FullName)
#     $projectB.Save($projectB.FullName)
#     $projectC.Save($projectC.FullName)
#     $projectX.Save($projectX.FullName)
#     Build-Solution

#     # Assert
#     Assert-NetCorePackageInstall $projectC $id $version
#     Assert-NetCorePackageInLockFile $projectB $id $version
#     Assert-NetCorePackageInLockFile $projectA $id $version
#     Assert-NetCoreNoPackageReference $projectX $id
#     Assert-NetCorePackageNotInLockFile $projectX $id 
# }


