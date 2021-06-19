function Test-PackageNameSpaceRestore
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
    <add key="PublicRepository" value="{0}" />
    <add key="PrivateRepository" value="{1}" />
    </packageSources>
    <packageNamespaces>
        <packageSource key="PublicRepository"> 
            <namespace id="Contoso.Opensource.*" />
        </packageSource>
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
            <package id="Contoso.Opensource.Buffers" version="2.0.0" targetFramework="net461" />
            <package id="Foo" version="1.0.0" targetFramework="net461" />
    </packages>' | out-file $packagesConfigPath
    
    
        CreateCustomTestPackage "Contoso.MVC.ASP" "1.0.0" $privateRepo
        CreateCustomTestPackage "Contoso.Opensource.Buffers" "2.0.0" $opensourceRepo
        CreateCustomTestPackage "Foo" "1.0.0" $opensourceRepo
    
        # Act
        Build-Solution
    
        # Assert   
    
        $packagesFolder = Join-Path $solutionDirectory "packages"
        $contosoMVCNupkgFolder = Join-Path $packagesFolder "Contoso.MVC.ASP.1.0.0"
        Assert-PathExists(Join-Path $contosoMVCNupkgFolder "Contoso.MVC.ASP.1.0.0.nupkg")
        $contosoOpenSourceNupkgFolder = Join-Path $packagesFolder "Contoso.Opensource.Buffers.2.0.0"
        Assert-PathExists(Join-Path $contosoOpenSourceNupkgFolder "Contoso.Opensource.Buffers.2.0.0.nupkg")
        
        $errorlist = Get-Errors
        Assert-AreEqual 0 $errorlist.Count
    }
    finally {
        Remove-Item -Recurse -Force $repoDirectory
        Remove-Item $nugetConfigPath
    }

}

function Test-PackageNameSpaceIdenticalPackageIdsRestoreCorrectPackage
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

function Test-NoPackageNameSpaceSectionRestoreNormally
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
