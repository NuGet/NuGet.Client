function Test-PackageNamespaceRestore-WithSingleFeed
{
    param($context)

    # Arrange
    $repoDirectory = $context.RepositoryRoot
    $nugetConfigPath = Join-Path $OutputPath 'nuget.config'

    $settingFileContent =@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
    <clear />
    <add key="ReadyPackages" value="{0}" />
    </packageSources>
    <packageNamespaces>
        <packageSource key="ReadyPackages">
            <namespace id="Soluti*" />
        </packageSource>
    </packageNamespaces>
</configuration>
"@
  
    try {
        # We have to create config file before creating solution, otherwise it's not effective for new solutions.
        $settingFileContent -f $repoDirectory | Out-File -Encoding "UTF8" $nugetConfigPath
    
        $p = New-ConsoleApplication
    
        $projectDirectoryPath = $p.Properties.Item("FullPath").Value
        $packagesConfigPath = Join-Path $projectDirectoryPath 'packages.config'
        $projectDirectoryPath = $p.Properties.Item("FullPath").Value
        $solutionDirectory = Split-Path -Path $projectDirectoryPath -Parent   
        # Write a file to disk, but do not add it to project
        '<packages>
            <package id="SolutionLevelPkg" version="1.0.0" targetFramework="net461" />
    </packages>' | out-file $packagesConfigPath
    
        # Act
        Build-Solution
    
        # Assert
        $packagesFolder = Join-Path $solutionDirectory "packages"
        $solutionLevelPkgNupkgFolder = Join-Path $packagesFolder "SolutionLevelPkg.1.0.0"
        Assert-PathExists(Join-Path $solutionLevelPkgNupkgFolder "SolutionLevelPkg.1.0.0.nupkg")
        
        $errorlist = Get-Errors
        Assert-AreEqual 0 $errorlist.Count
    }
    finally {
        Remove-Item $nugetConfigPath
    }
}

function Test-PackageNamespaceRestore-WithMultipleFeedsWithIdenticalPackages-RestoresCorrectPackage
{
    param($context)

    # Arrange
    $repoDirectory = Join-Path $OutputPath "CustomPackages"
    $opensourceRepo = Join-Path $repoDirectory "opensourceRepo"
    $privateRepo = Join-Path $repoDirectory "privateRepo"
    $nugetConfigPath = Join-Path $OutputPath 'nuget.config'

	$settingFileContent =@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
    <clear />
    <add key="OpensourceRepository" value="{0}" />
    <add key="PrivateRepository" value="{1}" />
    </packageSources>
    <packageNamespaces>
        <packageSource key="PrivateRepository">
            <namespace id="Contoso.MVC.*" />
        </packageSource>
    </packageNamespaces>
</configuration>
"@
    try {
        # We have to create config file before creating solution, otherwise it's not effective for new solutions.
        $settingFileContent -f $opensourceRepo,$privateRepo | Out-File -Encoding "UTF8" $nugetConfigPath

        $p = New-ConsoleApplication

        $projectDirectoryPath = $p.Properties.Item("FullPath").Value
        $packagesConfigPath = Join-Path $projectDirectoryPath 'packages.config'
        $projectDirectoryPath = $p.Properties.Item("FullPath").Value
        $solutionDirectory = Split-Path -Path $projectDirectoryPath -Parent   
        # Write a file to disk, but do not add it to project
        '<packages>
            <package id="Contoso.MVC.ASP" version="1.0.0" targetFramework="net461" />
    </packages>' | out-file $packagesConfigPath

        CreateCustomTestPackage "Contoso.MVC.ASP" "1.0.0" $privateRepo "Thisisfromprivaterepo.txt"
        CreateCustomTestPackage "Contoso.MVC.ASP" "1.0.0" $opensourceRepo "Thisisfromopensourcerepo.txt"

        # Act
        Build-Solution

        # Assert   
        $packagesFolder = Join-Path $solutionDirectory "packages"
        $contosoNupkgFolder = Join-Path $packagesFolder "Contoso.MVC.ASP.1.0.0"
        Assert-PathExists(Join-Path $contosoNupkgFolder "Contoso.MVC.ASP.1.0.0.nupkg")
        # Make sure name squatting package from public repo not restored.
        $contentFolder = Join-Path $contosoNupkgFolder "content"
        Assert-PathExists(Join-Path $contentFolder "Thisisfromprivaterepo.txt")

        $errorlist = Get-Errors
        Assert-AreEqual 0 $errorlist.Count
    }
    finally {
        Remove-Item -Recurse -Force $repoDirectory
        Remove-Item $nugetConfigPath
    }
}

function Test-VsPackageInstallerServices-PackageNamespaceInstall-WithSingleFeed-Succeed {
    param(
        $context
    )

    # Arrange
    $repoDirectory = $context.RepositoryRoot
    $nugetConfigPath = Join-Path $OutputPath 'nuget.config'

    $settingFileContent =@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
    <clear />
    <add key="ReadyPackages" value="{0}" />
    </packageSources>
    <packageNamespaces>
        <packageSource key="ReadyPackages">
            <namespace id="Soluti*" />
        </packageSource>
    </packageNamespaces>
</configuration>
"@
  
    try {
        # We have to create config file before creating solution, otherwise it's not effective for new solutions.
        $settingFileContent -f $repoDirectory | Out-File -Encoding "UTF8" $nugetConfigPath
    
        # $p = New-ConsoleApplication
        # Arrange
        $p = New-ClassLibrary

        # Act
        [API.Test.InternalAPITestHook]::InstallLatestPackageApi("SolutionLevelPkg", $false)

        # Assert
        Assert-Package $p SolutionLevelPkg 1.0.0

        $errorlist = Get-Errors
        Assert-AreEqual 0 $errorlist.Count
    }
    finally {
        Remove-Item $nugetConfigPath
    }
}

function Test-VsPackageInstallerServices-PackageNamespaceInstall-WithSingleFeed-Fails {
    param(
        $context
    )

    # Arrange
    $repoDirectory = $context.RepositoryRoot
    $nugetConfigPath = Join-Path $OutputPath 'nuget.config'

    $settingFileContent =@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
    <clear />
    <add key="ReadyPackages" value="{0}" />
    </packageSources>
    <packageNamespaces>
        <packageSource key="SecretPackages">
            <namespace id="Soluti*" />
        </packageSource>
    </packageNamespaces>
</configuration>
"@
  
    try {
        # We have to create config file before creating solution, otherwise it's not effective for new solutions.
        $settingFileContent -f $repoDirectory | Out-File -Encoding "UTF8" $nugetConfigPath
    
        # $p = New-ConsoleApplication
        # Arrange
        $p = New-ClassLibrary

        # Act & Assert
        # Even though SolutionLevelPkg package exist in $repoDirectory since package namespace filter set SolutionLevelPkg can be restored only from SecretPackages repository so it'll fail.
        $exceptionMessage = "Exception calling `"InstallLatestPackageApi`" with `"2`" argument(s): `"Package 'SolutionLevelPkg 1.0.0' is not found in the following primary source(s): '"+ $repoDirectory + "'. Please verify all your online package sources are available (OR) package id, version are specified correctly.`""
        Assert-Throws { [API.Test.InternalAPITestHook]::InstallLatestPackageApi("SolutionLevelPkg", $false)  } $exceptionMessage
        Assert-NoPackage $p SolutionLevelPkg 1.0.0
    }
    finally {
        Remove-Item $nugetConfigPath
    }
}

function Test-VsPackageInstallerServices-PackageNamespaceInstall-WithMultipleFeedsWithIdenticalPackages-RestoresCorrectPackageWithSpecifiedVersion
{
    param($context)

    # Arrange
    $repoDirectory = Join-Path $OutputPath "CustomPackages"
    $opensourceRepo = Join-Path $repoDirectory "opensourceRepo"
    $privateRepo = Join-Path $repoDirectory "privateRepo"
    $nugetConfigPath = Join-Path $OutputPath 'nuget.config'

	$settingFileContent =@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
    <clear />
    <add key="OpensourceRepository" value="{0}" />
    <add key="PrivateRepository" value="{1}" />
    </packageSources>
    <packageNamespaces>
        <packageSource key="PrivateRepository">
            <namespace id="Contoso.MVC.*" />
        </packageSource>
    </packageNamespaces>
</configuration>
"@
    try {
        # We have to create config file before creating solution, otherwise it's not effective for new solutions.
        $settingFileContent -f $opensourceRepo,$privateRepo | Out-File -Encoding "UTF8" $nugetConfigPath

        $p = New-ConsoleApplication

        $projectDirectoryPath = $p.Properties.Item("FullPath").Value
        $packagesConfigPath = Join-Path $projectDirectoryPath 'packages.config'
        $projectDirectoryPath = $p.Properties.Item("FullPath").Value
        $solutionDirectory = Split-Path -Path $projectDirectoryPath -Parent   

        CreateCustomTestPackage "Contoso.MVC.ASP" "1.0.0" $privateRepo "Thisisfromprivaterepo1.txt"
        CreateCustomTestPackage "Contoso.MVC.ASP" "2.0.0" $privateRepo "Thisisfromprivaterepo2.txt"
        CreateCustomTestPackage "Contoso.MVC.ASP" "1.0.0" $opensourceRepo "Thisisfromopensourcerepo1.txt"
        CreateCustomTestPackage "Contoso.MVC.ASP" "2.0.0" $opensourceRepo "Thisisfromopensourcerepo2.txt"

        # Act
        [API.Test.InternalAPITestHook]::InstallPackageApi("Contoso.MVC.ASP", "1.0.0")

        # Assert   
        $packagesFolder = Join-Path $solutionDirectory "packages"
        $contosoNupkgFolder = Join-Path $packagesFolder "Contoso.MVC.ASP.1.0.0"
        Assert-PathExists(Join-Path $contosoNupkgFolder "Contoso.MVC.ASP.1.0.0.nupkg")
        # Make sure name squatting package from public repo not restored.
        $contentFolder = Join-Path $contosoNupkgFolder "content"
        Assert-PathExists(Join-Path $contentFolder "Thisisfromprivaterepo1.txt")

        $errorlist = Get-Errors
        Assert-AreEqual 0 $errorlist.Count
    }
    finally {
        Remove-Item -Recurse -Force $repoDirectory
        Remove-Item $nugetConfigPath
    }
}

function Test-VsPackageInstallerServices-PackageNamespaceInstall-WithMultipleFeedsWithIdenticalPackages-RestoresCorrectPackageWithLatestVersion
{
    param($context)

    # Arrange
    $repoDirectory = Join-Path $OutputPath "CustomPackages"
    $opensourceRepo = Join-Path $repoDirectory "opensourceRepo"
    $privateRepo = Join-Path $repoDirectory "privateRepo"
    $nugetConfigPath = Join-Path $OutputPath 'nuget.config'

	$settingFileContent =@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
    <clear />
    <add key="OpensourceRepository" value="{0}" />
    <add key="PrivateRepository" value="{1}" />
    </packageSources>
    <packageNamespaces>
        <packageSource key="PrivateRepository">
            <namespace id="Contoso.MVC.*" />
        </packageSource>
    </packageNamespaces>
</configuration>
"@
    try {
        # We have to create config file before creating solution, otherwise it's not effective for new solutions.
        $settingFileContent -f $opensourceRepo,$privateRepo | Out-File -Encoding "UTF8" $nugetConfigPath

        $p = New-ConsoleApplication

        $projectDirectoryPath = $p.Properties.Item("FullPath").Value
        $packagesConfigPath = Join-Path $projectDirectoryPath 'packages.config'
        $projectDirectoryPath = $p.Properties.Item("FullPath").Value
        $solutionDirectory = Split-Path -Path $projectDirectoryPath -Parent   

        CreateCustomTestPackage "Contoso.MVC.ASP" "1.0.0" $privateRepo "Thisisfromprivaterepo1.txt"
        CreateCustomTestPackage "Contoso.MVC.ASP" "2.0.0" $privateRepo "Thisisfromprivaterepo2.txt"
        CreateCustomTestPackage "Contoso.MVC.ASP" "1.0.0" $opensourceRepo "Thisisfromopensourcerepo1.txt"
        CreateCustomTestPackage "Contoso.MVC.ASP" "2.0.0" $opensourceRepo "Thisisfromopensourcerepo2.txt"

        # Act
        [API.Test.InternalAPITestHook]::InstallLatestPackageApi("Contoso.MVC.ASP", $false)

        # Assert   
        $packagesFolder = Join-Path $solutionDirectory "packages"
        $contosoNupkgFolder = Join-Path $packagesFolder "Contoso.MVC.ASP.2.0.0"
        Assert-PathExists(Join-Path $contosoNupkgFolder "Contoso.MVC.ASP.2.0.0.nupkg")
        # Make sure name squatting package from public repo not restored.
        $contentFolder = Join-Path $contosoNupkgFolder "content"
        Assert-PathExists(Join-Path $contentFolder "Thisisfromprivaterepo2.txt")

        $errorlist = Get-Errors
        Assert-AreEqual 0 $errorlist.Count
    }
    finally {
        Remove-Item -Recurse -Force $repoDirectory
        Remove-Item $nugetConfigPath
    }
}

# Create a custom test package 
function CreateCustomTestPackage {
    param(
        [string]$id,
        [string]$version,
        [string]$outputDirectory,
        [string]$requestAdditionalContent
    )

    $builder = New-Object NuGet.Packaging.PackageBuilder
    $builder.Authors.Add("test_author")
    $builder.Id = $id
    $builder.Version = [NuGet.Versioning.NuGetVersion]::Parse($version)
    $builder.Description = "description" 

    # add one content file
    $tempFile = [IO.Path]::GetTempFileName()
    "temp1" >> $tempFile
    $packageFile = New-Object NuGet.Packaging.PhysicalPackageFile
    $packageFile.SourcePath = $tempFile
    $packageFile.TargetPath = "content\$id-test1.txt"
    $builder.Files.Add($packageFile)

    if($requestAdditionalContent)
    {
        # add one content file
        $tempFile2 = [IO.Path]::GetTempFileName()
        "temp2" >> $tempFile2        
        $packageFile = New-Object NuGet.Packaging.PhysicalPackageFile
        $packageFile.SourcePath = $tempFile2
        $packageFile.TargetPath = "content\$requestAdditionalContent"
        $builder.Files.Add($packageFile)
    }

    if(-not(Test-Path $outputDirectory))
    {
        New-Item -Path $outputDirectory -ItemType Directory
    }

    $outputFileName = Join-Path $outputDirectory "$id.$version.nupkg"
    $outputStream = New-Object IO.FileStream($outputFileName, [System.IO.FileMode]::Create)
    try {
        $builder.Save($outputStream)
    }
    finally
    {
        $outputStream.Dispose()
        Remove-Item $tempFile
        if($tempFile2)
        {
            Remove-Item $tempFile2
        }
    }
}
