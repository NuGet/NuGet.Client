# basic create for .net core template
function Test-NetCoreWebApp10Create {

    # Arrange & Act
    $project = New-NetCoreWebApp10 ConsoleApp
    Build-Solution

    # Assert
    Assert-NetCoreProjectCreation $project
}


# install package test for .net core
function Test-NetCoreWebApp10AppInstallPackage {

    # Arrange
    $project = New-NetCoreWebApp10 ConsoleApp
    Build-Solution
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
    Build-Solution
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
    Build-Solution
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
    Build-Solution
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
    Build-Solution
    $id = 'NuGet.Versioning'
    $oldVersion = '3.5.0'
    $newVersion = '4.5.0'
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
    Build-Solution
    $id = 'NuGet.Versioning'
    $oldVersion = '4.5.0'
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
    Build-Solution

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

# VSSolutionManager and ProjectSystemCache event test for .net core
function Test-NetCoreProjectSystemCacheUpdateEvent {

    # Arrange
    $projectA = New-NetCoreConsoleApp
    Build-Solution
    Assert-NetCoreProjectCreation $projectA

    $componentModel = Get-VSComponentModel
    $solutionManager = $componentModel.GetService([NuGet.PackageManagement.ISolutionManager])

    $cacheEvent = $null

    Get-Event | Remove-Event
    Register-ObjectEvent -InputObject $solutionManager -EventName AfterNuGetCacheUpdated -SourceIdentifier SolutionManagerCacheUpdated

    Try
    {
        # Act
        $projectA | Install-Package Newtonsoft.Json -Version '9.0.1'

        $cacheEvent = Wait-Event -SourceIdentifier SolutionManagerCacheUpdated -TimeoutSec 10
    }
    Finally
    {
        Unregister-Event -SourceIdentifier SolutionManagerCacheUpdated
    }

    # Assert
    Assert-NotNull $cacheEvent -Message "Cache update event should've been raised"
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

function Test-NetCoreConsoleAppClean {

    # Arrange & Act
    $project = New-NetCoreConsoleApp ConsoleApp

    Build-Solution

    Assert-ProjectCacheFileExists $project

    #Act
    Clean-Solution

    #Assert
    Assert-ProjectCacheFileNotExists $project
}

function Test-NetCoreConsoleAppRebuildDoesNotDeleteCacheFile {
    # Arrange & Act
    $project = New-NetCoreConsoleApp ConsoleApp
    Build-Solution

    Assert-ProjectCacheFileExists $project

    AdviseSolutionEvents

    #Act
    Rebuild-Solution

    WaitUntilRebuildCompleted
    UnadviseSolutionEvents

    #Assert
    Assert-ProjectCacheFileExists $project
}

function Test-NetCoreVSandMSBuildNoOp {
    
    # Arrange
    $project = New-NetCoreConsoleApp ConsoleApp
    Build-Solution

    Assert-ProjectCacheFileExists $project
    $cacheFile = Get-ProjectCacheFilePath $project

    #Act
    
    $VSRestoreTimestamp =( [datetime](Get-ItemProperty -Path $cacheFile -Name LastWriteTime).lastwritetime).Ticks
    
    $MSBuildExe = Get-MSBuildExe
    
    & "$MSBuildExe" /t:restore

    $MsBuildRestoreTimestamp =( [datetime](Get-ItemProperty -Path $cacheFile -Name LastWriteTime).lastwritetime).Ticks

    #Assert
    Assert-True ($MsBuildRestoreTimestamp -eq $VSRestoreTimestamp)
}

function Test-NetCoreTargetFrameworksVSandMSBuildNoOp {
    
    # Arrange
    $project = New-NetCoreConsoleTargetFrameworksApp ConsoleApp
    Build-Solution

    Assert-ProjectCacheFileExists $project
    $cacheFile = Get-ProjectCacheFilePath $project

    #Act
    
    $VSRestoreTimestamp =( [datetime](Get-ItemProperty -Path $cacheFile -Name LastWriteTime).lastwritetime).Ticks
    
    $MSBuildExe = Get-MSBuildExe
    
    & "$MSBuildExe" /t:restore

    $MsBuildRestoreTimestamp =( [datetime](Get-ItemProperty -Path $cacheFile -Name LastWriteTime).lastwritetime).Ticks

    #Assert
    Assert-True ($MsBuildRestoreTimestamp -eq $VSRestoreTimestamp)
}

function Test-NetCoreMultipleTargetFrameworksVSandMSBuildNoOp {
    
    # Arrange
    $project = New-NetCoreConsoleMultipleTargetFrameworksApp ConsoleApp
    Build-Solution

    Assert-ProjectCacheFileExists $project
    $cacheFile = Get-ProjectCacheFilePath $project

    #Act
    
    $VSRestoreTimestamp =( [datetime](Get-ItemProperty -Path $cacheFile -Name LastWriteTime).lastwritetime).Ticks
    
    $MSBuildExe = Get-MSBuildExe
    
    & "$MSBuildExe" /t:restore

    $MsBuildRestoreTimestamp =( [datetime](Get-ItemProperty -Path $cacheFile -Name LastWriteTime).lastwritetime).Ticks

    #Assert
    Assert-True ($MsBuildRestoreTimestamp -eq $VSRestoreTimestamp)
}

function Test-NetCoreToolsVSandMSBuildNoOp {
    
    # Arrange
    $project = New-NetCoreWebApp10 ConsoleApp
    Assert-NetCoreProjectCreation $project

    $ToolsCacheFile = Get-ProjectToolsCacheFilePath $project
    
    #Act
    $VSRestoreTimestamp =( [datetime](Get-ItemProperty -Path $ToolsCacheFile -Name LastWriteTime).lastwritetime).Ticks
    
    $MSBuildExe = Get-MSBuildExe
    
    & "$MSBuildExe" /t:restore

    $MsBuildRestoreTimestamp =( [datetime](Get-ItemProperty -Path $ToolsCacheFile -Name LastWriteTime).lastwritetime).Ticks

    #Assert
    Assert-True ($MsBuildRestoreTimestamp -eq $VSRestoreTimestamp)
}