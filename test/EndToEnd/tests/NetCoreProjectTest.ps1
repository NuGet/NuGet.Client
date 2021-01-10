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
    
    & "$MSBuildExe" /t:restore  $project.FullName
    Assert-True ($LASTEXITCODE -eq 0)

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

    & "$MSBuildExe" /t:restore  $project.FullName
    Assert-True ($LASTEXITCODE -eq 0)

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
    
    & "$MSBuildExe" /t:restore  $project.FullName
    Assert-True ($LASTEXITCODE -eq 0)

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
    
    & "$MSBuildExe" /t:restore  $project.FullName
    Assert-True ($LASTEXITCODE -eq 0)

    $MsBuildRestoreTimestamp =( [datetime](Get-ItemProperty -Path $ToolsCacheFile -Name LastWriteTime).lastwritetime).Ticks

    #Assert
    Assert-True ($MsBuildRestoreTimestamp -eq $VSRestoreTimestamp)
}

function Test-NetCorePackageVersionNoInclusiveLowerBoundNU1604 {
    # Arrange
    $project = New-NetCoreConsoleApp ConsoleApp
    $projectDirectory = [System.IO.Path]::GetDirectoryName($project.FullName)

    # Act
    $projectXML = [xml](Get-Content $project.FullName)
    $itemGroup = $projectXML.CreateElement("ItemGroup")
    $packageReference = $projectXML.CreateElement("PackageReference")
    $packageReference.SetAttribute("Include", "TestUpdatePackage")
    $projectXML.DocumentElement.AppendChild($itemGroup)
    $itemGroup.AppendChild($packageReference)
    $projectXML.Save($project.FullName)       

    Build-Solution

    # Give time for restore to finish, so we can capture VS UI warnings.
    Start-Sleep -s 5

    # Assert   
    $warnings = Get-Warnings   
    
    # Make sure VS UI warning has NU1604 warning
    Write-Host $warnings
    Write-Host $warnings.Count
    Assert-True $warnings.Contains("Project dependency TestUpdatePackage does not contain an inclusive lower bound. Include a lower bound in the dependency version to ensure consistent restore results.")

    # Make sure asset file has NU1604 warning

    $projectAssetFile = Join-Path $projectDirectory -ChildPath 'obj\project.assets.json'
    $assetJson = Get-Content -Raw -Path $projectAssetFile | ConvertFrom-Json
    $warningCodes = $assetJson.logs | Select-Object Code
    Assert-True $warningCodes.Code.Contains("NU1604")    
}