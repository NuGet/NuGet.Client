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
    Install-Package -Source $context.RepositoryPath -Project $p.Name MyAwesomeLibrary
    
    # Assert
    Assert-Package $p MyAwesomeLibrary
    Assert-SolutionPackage MyAwesomeLibrary
    
    $refreshFilePath = Join-Path (Get-ProjectDir $p) "bin\MyAwesomeLibrary.dll.refresh"
    $content = Get-Content $refreshFilePath
    
    Assert-AreEqual "..\packages\MyAwesomeLibrary.1.0\lib\net40\MyAwesomeLibrary.dll" $content
}

function Test-DiamondDependencies {
    param(
        $context
    )
    
    # Scenario:
    # D 1.0 -> B 1.0, C 1.0
    # B 1.0 -> A 1.0 
    # C 1.0 -> A 2.0
    #     D 1.0
    #      /  \
    #  B 1.0   C 1.0
    #     |    |
    #  A 1.0   A 2.0
    
    # Arrange 
    $packages = @("A", "B", "C", "D")
    $project = New-ClassLibrary
    
    # Act
    Install-Package D -Project $project.Name -Source $context.RepositoryPath
    
    # Assert
    $packages | %{ Assert-SolutionPackage $_ }
    $packages | %{ Assert-Package $project $_ }
    $packages | %{ Assert-Reference $project $_ }
    Assert-Package $project A 2.0
    Assert-Reference $project A 2.0.0.0
    Assert-Null (Get-ProjectPackage $project A 1.0.0.0) 
    Assert-Null (Get-SolutionPackage A 1.0.0.0)
}

function Test-WebsiteWillNotDuplicateConfigOnReInstall {
    # Arrange
    $p = New-WebSite
    
    # Act
    Install-Package elmah -Project $p.Name -Version 1.1
    $item = Get-ProjectItem $p packages.config
    $item.Delete()
    Install-Package elmah -Project $p.Name -Version 1.1
    
    # Assert
    $config = [xml](Get-Content (Get-ProjectItemPath $p web.config))
    Assert-AreEqual 4 $config.configuration.configSections.sectionGroup.section.count
}

function Test-WebsiteConfigElementsAreRemovedEvenIfReordered {
    # Arrange
    $p = New-WebSite
    
    # Act
    Install-Package elmah -Project $p.Name -Version 1.1
    $configPath = Get-ProjectItemPath $p web.config
    $config = [xml](Get-Content $configPath)
    $sectionGroup = $config.configuration.configSections.sectionGroup
    $security = $sectionGroup.section[0]
    $sectionGroup.RemoveChild($security) | Out-Null
    $sectionGroup.AppendChild($security) | Out-Null
    $config.Save($configPath)
    Uninstall-Package elmah
    $config = [xml](Get-Content $configPath)
    
    # Assert
    Assert-Null $config.configuration.configSections
}

function Test-FailedInstallRollsBackInstall {
    param(
        $context
    )
    # Arrange
    $p = New-ClassLibrary

    # Act
    Install-Package haack.metaweblog -Project $p.Name -Source $context.RepositoryPath

    # Assert
    Assert-NotNull (Get-ProjectPackage $p haack.metaweblog 0.1.0)
    Assert-NotNull (Get-SolutionPackage haack.metaweblog 0.1.0)
}

function Test-PackageWithIncompatibleAssembliesRollsInstallBack {
    param(
        $context
    )
    # Arrange
    $p = New-WebApplication

    # Act & Assert
    Assert-Throws { Install-Package BingMapAppSDK -Project $p.Name -Source $context.RepositoryPath } "Could not install package 'BingMapAppSDK 1.0.1011.1716'. You are trying to install this package into a project that targets '.NETFramework,Version=v4.0', but the package does not contain any assembly references or content files that are compatible with that framework. For more information, contact the package author.."
    Assert-Null (Get-ProjectPackage $p BingMapAppSDK 1.0.1011.1716)
    Assert-Null (Get-SolutionPackage BingMapAppSDK 1.0.1011.1716)
}

function Test-InstallPackageInvokeInstallScriptAndInitScript {
    param(
        $context
    )
    
    # Arrange
    $p = New-ConsoleApplication

    # Act
    Install-Package PackageWithScripts -Source $context.RepositoryRoot

    # Assert

    # This asserts init.ps1 gets called
    Assert-True (Test-Path function:\Get-World)
}

# TODO: We need to modify our console host to allow creating nested pipeline
#       in order for this test to run successfully.
#
#function Test-OpeningExistingSolutionInvokeInitScriptIfAny {
#    param(
#        $context
#    )
#    
#    # Arrange
#    $p = New-ConsoleApplication
#
#    # Act
#    Install-Package PackageWithScripts -Source $context.RepositoryRoot
#
#    # Now close the solution and reopen it
#    $solutionDir = $dte.Solution.FullName
#    Close-Solution
#    Remove-Item function:\Get-World
#    Assert-False (Test-Path function:\Get-World)
#    
#    Open-Solution $solutionDir
#
#    # This asserts init.ps1 gets called
#    Assert-True (Test-Path function:\Get-World)
#}

function Test-InstallPackageResolvesDependenciesAcrossSources {
    param(
        $context
    )
    
    # Arrange
    $p = New-ConsoleApplication

    # Act
    # Ensure Antlr is not avilable in local repo.
    Assert-Null (Get-Package -ListAvailable -Source $context.RepositoryRoot Antlr)
    Install-Package PackageWithExternalDependency -Source $context.RepositoryRoot

    # Assert
    Assert-Package $p PackageWithExternalDependency
    Assert-Package $p Antlr
}

function Test-VariablesPassedToInstallScriptsAreValidWithWebSite {
    param(
        $context
    )
    
    # Arrange
    $p = New-WebSite

    # Act
    Install-Package PackageWithScripts -Project $p.Name -Source $context.RepositoryRoot

    # Assert

    # This asserts install.ps1 gets called with the correct project reference and package
    Assert-Reference $p System.Windows.Forms
}

function Test-InstallComplexPackageStructure {
    param(
        $context
    )

    # Arrange
    $p = New-WebApplication

    # Act
    Install-Package MyFirstPackage -Project $p.Name -Source $context.RepositoryPath

    # Assert
    Assert-NotNull (Get-ProjectItem $p Pages\Blocks\Help\Security)
    Assert-NotNull (Get-ProjectItem $p Pages\Blocks\Security\App_LocalResources)
}

function Test-InstallPackageWithWebConfigDebugChanges {
    param(
        $context
    )

    # Arrange
    $p = New-WebApplication

    # Act
    Install-Package PackageWithWebDebugConfig -Project $p.Name -Source $context.RepositoryRoot

    # Assert
    $configItem = Get-ProjectItem $p web.config
    $configDebugItem = $configItem.ProjectItems.Item("web.debug.config")
    $configDebugPath = $configDebugItem.Properties.Item("FullPath").Value
    $configDebug = [xml](Get-Content $configDebugPath)
    Assert-NotNull $configDebug
    Assert-NotNull ($configDebug.configuration.connectionStrings.add)
    $addNode = $configDebug.configuration.connectionStrings.add
    Assert-AreEqual MyDB $addNode.name
    Assert-AreEqual "Data Source=ReleaseSQLServer;Initial Catalog=MyReleaseDB;Integrated Security=True" $addNode.connectionString
}

function Test-FSharpSimpleInstallWithContentFiles {
    param(
        $context
    )

    # Arrange
    $p = New-FSharpLibrary
    
    # Act
    Install-Package jquery -Version 1.5 -Project $p.Name -Source $context.RepositoryPath
    
    # Assert
    Assert-Package $p jquery
    Assert-SolutionPackage jquery
    Assert-NotNull (Get-ProjectItem $p Scripts\jquery-1.5.js)
    Assert-NotNull (Get-ProjectItem $p Scripts\jquery-1.5.min.js)
}

function Test-FSharpSimpleWithAssemblyReference {
    # Arrange
    $p = New-FSharpLibrary
    
    # Act
    Install-Package Antlr -Project $p.Name -Source $context.RepositoryPath
    
    # Assert
    Assert-Package $p Antlr
    Assert-SolutionPackage Antlr
    Assert-Reference $p Runtime
}

function Test-WebsiteInstallPackageWithRootNamespace {
    param(
        $context
    )

    # Arrange
    $p = New-WebSite
    
    # Act
    Install-Package PackageWithRootNamespaceFileTransform -Source $context.RepositoryRoot

    # Assert
    Assert-NotNull (Get-ProjectItem $p App_Code\foo.cs)
    $path = (Get-ProjectItemPath $p App_Code\foo.cs)
    $content = [System.IO.File]::ReadAllText($path)
    Assert-True ($content.Contains("namespace ASP"))
}

function Test-AddBindingRedirectToWebsiteWithNonExistingOutputPath {
    # Arrange
    $p = New-WebSite
    
    # Act
    Add-BindingRedirect -ProjectName $p.Name

    # Assert
    Assert-Null $redirects
}

function Test-InstallCanPipeToFSharpProjects {
    # Arrange
    $p = New-FSharpLibrary

    # Act
    $p | Install-Package elmah -Version 1.1 -source $context.RepositoryPath

    # Assert
    Assert-Package $p elmah
    Assert-SolutionPackage elmah
}