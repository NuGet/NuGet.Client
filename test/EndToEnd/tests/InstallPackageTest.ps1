
function GeneratePackage
{
    param(
		[Parameter(Mandatory)]
		[string]$Name,
		[string]$Version,
        [string]$NupkgOutputPath = $null
	)

    Write-Host "Create package name: $Name with version: $Version"

    $p = New-ClassLibrary $Name
    $p.Properties.Item("Company").Value = "Some Company"
    $p.Save()

    $output = (Get-PropertyValue $p FullPath)

    if($NupkgOutputPath)
    {
        $output = $NupkgOutputPath
    }
    
    Write-Host "---------------------------"
    Write-Host $NupkgOutputPath
    Write-Host $output
    & $context.NuGetExe pack $p.FullName -build -Properties version=$Version -OutputDirectory $output

    $packageFile = Get-ChildItem $output -Filter *.nupkg
    Assert-NotNull $packageFile
    return $packageFile
}

function Test-InstallPackageAddPackagesConfigFileToProject
{
    param($context)

    # Arrange
    $p = New-ConsoleApplication

    $projectPath = $p.Properties.Item("FullPath").Value

    $packagesConfigPath = Join-Path $projectPath 'packages.config'
    # Write a file to disk, but do not add it to project
    '<packages>
        <package id="Contoso.MVC.ASP" version="1.0.0" targetFramework="net461" />
        <package id="Contoso.Opensource.Buffers" version="2.0.0" targetFramework="net461" />
</packages>' | out-file $packagesConfigPath

    $solutionDirectory = Split-Path -Path $projectPath -Parent
    $opensourceRepo = Join-Path $solutionDirectory "opensourceRepo"
    Write-Host "111111111111" $opensourceRepo
    $privateRepo = Join-Path $solutionDirectory "privateRepo"
    Write-Host "22222222222" $privateRepo

    $nugetConfigPath = Join-Path $solutionDirectory 'nuget.config'

    '<?xml version="1.0" encoding="utf-8"?>
    <configuration>
        <packageSources>
        <clear />
        <add key="PublicRepository" value="' + $opensourceRepo + '" />
        <add key="PrivateRepository" value="' + $privateRepo + '" />
        </packageSources>
        <packageNamespaces>
            <packageSource key="PublicRepository"> 
                <namespace id="Contoso.Opensource.*" />
            </packageSource>
            <packageSource key="PrivateRepository">
                <namespace id="Contoso.MVC.*" />
            </packageSource>
        </packageNamespaces>
    </configuration>' | out-file $nugetConfigPath

    $contosoMVCAsp1Package = GeneratePackage "Contoso.MVC.ASP" "1.0.0" $privateRepo
    $contosoOpensourceBuffers1Package = GeneratePackage "Contoso.Opensource.Buffers" "1.0.0" $opensourceRepo
    $foo1Package = GeneratePackage "Foo" "1.0.0" $opensourceRepo
    # <?xml version="1.0" encoding="utf-8"?>
    # <packages>
    #   <package id="Contoso.MVC.ASP" version="1.0.0" targetFramework="net461" />
    #   <package id="Contoso.Opensource.Buffers" version="1.0.0" targetFramework="net461" />
    # </packages>
    # Act

    # Act
    Build-Solution
    # # install-package SkypePackage -projectName $p.Name -source $context.RepositoryRoot

    # # Assert
    # Assert-Package $p Contoso.MVC.ASP

    # Rename-Item "Newtonsoft.Json.12.0.1.nupkg" "Newtonsoft.Json.12.0.1.nupkg.zip"
    # Expand-Archive "Newtonsoft.Json.12.0.1.nupkg.zip"

    # $xmlFile = [xml](Get-Content $packagesConfigPath)
    # Assert-AreEqual 2 $xmlFile.packages.package.Count
    # Assert-AreEqual 'jquery' $xmlFile.packages.package[0].Id
    # Assert-AreEqual 'SkypePackage' $xmlFile.packages.package[1].Id
}

# function Test-WebsiteWillNotDuplicateConfigOnReInstall {
#     # Arrange
#     $p = New-WebSite

#     # Act
#     Install-Package elmah -Project $p.Name -Version 1.1
#     $item = Get-ProjectItem $p packages.config
#     $item.Delete()
#     Install-Package elmah -Project $p.Name -Version 1.1

#     # Assert
#     $config = [xml](Get-Content (Get-ProjectItemPath $p web.config))
#     Assert-AreEqual 4 $config.configuration.configSections.sectionGroup.section.count
# }

# function Test-InstallPackagePersistTargetFrameworkToPackagesConfig
# {
#     param($context)

#     # Arrange
#     $p = New-ClassLibrary

#     # Act
#     $p | Install-Package PackageA -Source $context.RepositoryPath

#     # Assert
#     Assert-Package $p 'packageA'
#     Assert-Package $p 'packageB'

#     $content = [xml](Get-Content (Get-ProjectItemPath $p 'packages.config'))

#     $entryA = $content.packages.package[0]
#     $entryB = $content.packages.package[1]

#     Assert-True 'net40' -ne $entryA.targetFramework -or 'net4' -ne $entryA.targetFramework
#     Assert-True 'net40' -ne $entryB.targetFramework -or 'net4' -ne $entryB.targetFramework
# }

# function Test-InstallPackageAddPackagesConfigFileToProject
# {
#     param($context)

#     # Arrange
#     $p = New-ConsoleApplication

#     $projectPath = $p.Properties.Item("FullPath").Value

#     $packagesConfigPath = Join-Path $projectPath 'packages.config'

#     # Write a file to disk, but do not add it to project
#     '<packages><package id="jquery" version="2.0" /></packages>' | out-file $packagesConfigPath

#     # Act
#     install-package SkypePackage -projectName $p.Name -source $context.RepositoryRoot

#     # Assert
#     Assert-Package $p SkypePackage

#     $xmlFile = [xml](Get-Content $packagesConfigPath)
#     Assert-AreEqual 2 $xmlFile.packages.package.Count
#     Assert-AreEqual 'jquery' $xmlFile.packages.package[0].Id
#     Assert-AreEqual 'SkypePackage' $xmlFile.packages.package[1].Id
# }

# function Test-InstallPackagePreservesProjectConfigFile
# {
#     param($context)

#     # Arrange
#     $p = New-ClassLibrary "CoolProject"

#     $projectPath = $p.Properties.Item("FullPath").Value
#     $packagesConfigPath = Join-Path $projectPath 'packages.CoolProject.config'

#     # create file and add to project
#     $newFile = New-Item $packagesConfigPath -ItemType File
#     '<packages></packages>' > $newFile
#     $p.ProjectItems.AddFromFile($packagesConfigPath)

#     # Act
#     $p | Install-Package PackageWithFolder -source $context.RepositoryRoot

#     # Assert
#     Assert-Package $p PackageWithFolder
#     Assert-NotNull (Get-ProjectItem $p 'packages.CoolProject.config')
#     Assert-Null (Get-ProjectItem $p 'packages.config')
# }

# --------------------------
# function Test-InstallPackageToWebsitePreservesProjectConfigFile
# {
#     param($context)

#     # Arrange
#     $p = New-Website "CoolProject"
#     $packagesConfigFileName = "packages.CoolProject.config"
#     if ((Get-VSVersion) -gt '10.0')
#     {
#         # on dev 11.0 etc, the project name could be something lkie
#         # "CoolProject(12)". So we need to get the project name
#         # to construct the packages config file name.
#         $packagesConfigFileName = "packages.$($p.Name).config"
#     }

#     $projectPath = $p.Properties.Item("FullPath").Value
#     $packagesConfigPath = Join-Path $projectPath $packagesConfigFileName

#     # create file and add to project
#     $newFile = New-Item $packagesConfigPath -ItemType File
#     '<packages></packages>' > $newFile
#     $p.ProjectItems.AddFromFile($packagesConfigPath)

#     # Act
#     $p | Install-Package PackageWithFolder -source $context.RepositoryRoot

#     # Assert
#     Assert-Package $p PackageWithFolder
#     Assert-NotNull (Get-ProjectItem $p $packagesConfigFileName)
#     Assert-Null (Get-ProjectItem $p 'packages.config')
# }

# function Test-InstallPackageAddMoreEntriesToProjectConfigFile
# {
#     param($context)

#     # Arrange
#     $p = New-ClassLibrary "CoolProject"

#     $p | Install-Package PackageWithContentFile -source $context.RepositoryRoot

#     $file = Get-ProjectItem $p 'packages.config'
#     Assert-NotNull $file

#     # rename it
#     $file.Name = 'packages.CoolProject.config'

#     # Act
#     $p | Install-Package PackageWithFolder -source $context.RepositoryRoot

#     # Assert
#     Assert-Package $p PackageWithFolder
#     Assert-Package $p PackageWithContentFile

#     Assert-NotNull (Get-ProjectItem $p 'packages.CoolProject.config')
#     Assert-Null (Get-ProjectItem $p 'packages.config')
# }

# # Tests that when -DependencyVersion HighestPatch is specified, the dependency with
# # the largest patch number is installed
# function Test-InstallPackageWithDependencyVersionHighestPatch
# {
#     param($context)

#     # A depends on B >= 1.0.0
#     # Available versions of B are: 1.0.0, 1.0.1, 1.2.0, 1.2.1, 2.0.0, 2.0.1

#     # Arrange
#     $p = New-ClassLibrary

#     # Act
#     $p | Install-Package A -Source $context.RepositoryPath -DependencyVersion HighestPatch

#     # Assert
#     Assert-Package $p A 1.0
#     Assert-Package $p B 1.0.1
# }

# # Tests that when -DependencyVersion HighestPatch is specified, the dependency with
# # the lowest major, highest minor, highest patch is installed
# function Test-InstallPackageWithDependencyVersionHighestMinor
# {
#     param($context)

#     # A depends on B >= 1.0.0
#     # Available versions of B are: 1.0.0, 1.0.1, 1.2.0, 1.2.1, 2.0.0, 2.0.1

#     # Arrange
#     $p = New-ClassLibrary

#     # Act
#     $p | Install-Package A -Source $context.RepositoryPath -DependencyVersion HighestMinor

#     # Assert
#     Assert-Package $p A 1.0
#     Assert-Package $p B 1.2.1
# }

# # Tests that when -DependencyVersion Highest is specified, the dependency with
# # the highest version installed
# function Test-InstallPackageWithDependencyVersionHighest
# {
#     param($context)

#     # A depends on B >= 1.0.0
#     # Available versions of B are: 1.0.0, 1.0.1, 1.2.0, 1.2.1, 2.0.0, 2.0.1

#     # Arrange
#     $p = New-ClassLibrary

#     # Act
#     $p | Install-Package A -Source $context.RepositoryPath -DependencyVersion Highest

#     # Assert
#     Assert-Package $p A 1.0
#     Assert-Package $p B 2.0.1
# }

# # Tests that when -DependencyVersion is lowest, the dependency with
# # the smallest patch number is installed
# function Test-InstallPackageWithDependencyVersionLowest
# {
#     param($context)

#     # A depends on B >= 1.0.0
#     # Available versions of B are: 1.0.0, 1.0.1, 1.2.0, 1.2.1, 2.0.0, 2.0.1

#     # Arrange
#     $p = New-ClassLibrary

#     # Act
#     $p | Install-Package A -Source $context.RepositoryPath -DependencyVersion Lowest

#     # Assert
#     Assert-Package $p A 1.0
#     Assert-Package $p B 1.0.0
# }

# # Tests the case when DependencyVersion is specified in nuget.config
# function Test-InstallPackageWithDependencyVersionHighestInNuGetConfig
# {
#     param($context)

#     # Arrange
#     Check-NuGetConfig

#     $componentModel = Get-VSComponentModel
#     $setting = $componentModel.GetService([NuGet.Configuration.ISettings])

#     try {
#         # Arrange
#         $p = New-ClassLibrary

#         $setting.AddOrUpdate('config', [NuGet.Configuration.AddItem]::new('dependencyversion', 'HighestPatch'))

#         # Act
#         $p | Install-Package jquery.validation -version 1.10

#         # Assert
#         Assert-Package $p jquery.validation 1.10
#         Assert-Package $p jquery 1.4.4
#     }
#     finally {
#         $setting.AddOrUpdate('config', [NuGet.Configuration.AddItem]::new('dependencyversion', $null))
#     }
# }

# # Tests that when -DependencyVersion is not specified, the dependency with
# # the smallest patch number is installed
# function Test-InstallPackageWithoutDependencyVersion
# {
#     param($context)

#    # A depends on B >= 1.0.0
#     # Available versions of B are: 1.0.0, 1.0.1, 1.2.0, 1.2.1, 2.0.0, 2.0.1

#     # Arrange
#     $p = New-ClassLibrary

#     # Act
#     $p | Install-Package A -Source $context.RepositoryPath

#     # Assert
#     Assert-Package $p A 1.0
#     Assert-Package $p B 1.0.0
# }

# # Tests that passing in online path to a packages.config file to
# # Install-Package works.
# function Test-InstallPackagesConfigOnline
# {
#     param($context)

#     # Arrange
#     $p = New-ClassLibrary

#     # Act
#     $p | Install-Package Newtonsoft.Json
#     $p | Install-Package https://raw.githubusercontent.com/NuGet/json-ld.net/7dc9becb263a7210ebcd2f571c2a7a07409c240a/src/JsonLD/packages.config

#     # Assert
#     Assert-Package $p Newtonsoft.Json 4.0.1
# }

# # Tests that passing in local path to a packages.config file to
# # Install-Package works.
# function Test-InstallPackagesConfigLocal
# {
#     param($context)

#     # Arrange
#     $p = New-ClassLibrary
#     $pathToPackagesConfig = Join-Path $context.RepositoryRoot "InstallPackagesConfigLocal\packages.config"

#     # Act
#     $p | Install-Package $pathToPackagesConfig

#     # Assert
#     Assert-Package $p jQuery.validation 1.13.1
#     Assert-Package $p jQuery 2.1.3
#     Assert-Package $p EntityFramework 6.1.3-beta1
# }

# # Tests that passing in online path to a .nupkg file to
# # Install-Package works.
# function Test-InstallPackagesNupkgOnline
# {
#     param($context)

#     # Arrange
#     $p = New-ClassLibrary

#     # Act
#     $p | Install-package https://globalcdn.nuget.org/packages/microsoft.aspnet.mvc.4.0.20505.nupkg

#     # Assert
#     Assert-Package $p microsoft.aspnet.mvc 4.0.20505.0
#     Assert-Package $p microsoft.aspnet.webpages 2.0.20505
#     Assert-Package $p microsoft.aspnet.razor 2.0.20505
#     Assert-Package $p microsoft.web.infrastructure 1.0.0
# }

# # Tests that passing in local path to a .nupkg file to
# # Install-Package works.
# function Test-InstallPackagesNupkgLocal
# {
#     param($context)

#     # Arrange
#     $p = New-ClassLibrary
#     $pathToPackagesNupkg = Join-Path $context.RepositoryRoot "PackageWithFolder.1.0.nupkg"

#     # Act
#     $p | Install-Package $pathToPackagesNupkg

#     # Assert
#     Assert-Package $p PackageWithFolder 1.0
# }

# function Test-InstallPackageMissingPackage {
#     # Arrange
#     # create project and install package
#     $proj = New-ClassLibrary
#     $proj | Install-Package Castle.Core -Version 1.2.0
#     Assert-Package $proj Castle.Core 1.2.0

#     # delete the packages folder
#     $packagesDir = Get-PackagesDir
#     RemoveDirectory $packagesDir
#     Assert-False (Test-Path $packagesDir)

#     # Act
#     Install-Package Castle.Core -Version 2.5.1

#     # Assert
#     Assert-Package $proj Castle.Core 2.5.1
# }

# function Test-InstallPackageMissingPackageNoConsent {
#     try {
#         [NuGet.PackageManagement.VisualStudio.SettingsHelper]::Set('PackageRestoreConsentGranted', 'false')
#         [NuGet.PackageManagement.VisualStudio.SettingsHelper]::Set('PackageRestoreIsAutomatic', 'false')

#         # Arrange
#         # create project and install package
#         $proj = New-ClassLibrary
#         $proj | Install-Package PackageWithContentFile -Source $context.RepositoryRoot -Version 1.0.0.0
#         Assert-Package $proj PackageWithContentFile 1.0

#         # delete the packages folder
#         $packagesDir = Get-PackagesDir
#         RemoveDirectory $packagesDir
#         Assert-False (Test-Path $packagesDir)

#         # Act
#         # Assert
#         Assert-Throws { Install-Package PackageWithContentFile } "Some NuGet packages are missing from the solution. The packages need to be restored in order to build the dependency graph. Restore the packages before performing any operations."
#     }
#     finally {
#         [NuGet.PackageManagement.VisualStudio.SettingsHelper]::Set('PackageRestoreConsentGranted', 'true')
#         [NuGet.PackageManagement.VisualStudio.SettingsHelper]::Set('PackageRestoreIsAutomatic', 'true')
#     }
# }

# function Test-InstallPackageWithScriptAddImportFile
# {
#     param($context)

#     # Arrange
#     $p = New-ClassLibrary

#     #Act
#     $p | Install-Package Microsoft.Bcl.build -version 1.0.14
#     Build-Solution

#     # Assert
#     $errorlist = Get-Errors
#     Assert-AreEqual 0 $errorlist.Count
# }

# # Temporarily disable this test
# function Disable-Test-InstallPackageInCpsApp
# {
#     param($context)

#     # Arrange
#     $p = New-CpsApp "CpsProject"

#     #Act
#     $p | Install-Package GoogleAnalyticsTracker.Core -version 3.2.0

#     # Assert
#     $item = Get-ProjectItem $p packages.config
#     Assert-NotNull $item
# }

# function Test-InstallPackageWithEscapedSymbolInPath()
# {
#     param($context)

#     # Arrange
#     $p = New-ClassLibrary

#     #Act
#     $p | Install-Package Xam.Plugin.Connectivity -version 1.0.2

#     # Assert
#     Assert-Package $p Xam.Plugin.Connectivity
# }

# function Test-InstallPackageWithRootNamespaceInPPFile {
#     param(
#         $context
#     )

#     # Arrange
#     $p = New-ClassLibrary "testProject"

#     # Act
#     Install-Package PackageWithRootNamespaceFileTransform -Source $context.RepositoryRoot

#     # Assert
#     Assert-NotNull (Get-ProjectItem $p foo.cs)
#     $path = (Get-ProjectItemPath $p foo.cs)
#     $content = [System.IO.File]::ReadAllText($path)
#     Assert-True ($content.Contains("namespace testProject"))
# }
