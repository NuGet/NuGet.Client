function Test-NuGet1
{
    param($context)

    # Arrange
    $p = New-ConsoleApplication

    $projectDirectoryPath = $p.Properties.Item("FullPath").Value
    $projectName = Split-Path -Path $projectDirectoryPath -Leaf
    $projectPath = Join-Path $projectDirectoryPath $projectName".csproj"

    $packagesConfigPath = Join-Path $projectDirectoryPath 'packages.config'
    # Write a file to disk, but do not add it to project
    '<packages>
        <package id="Contoso.MVC.ASP" version="1.0.0" targetFramework="net461" />
        <package id="Contoso.Opensource.Buffers" version="2.0.0" targetFramework="net461" />
        <package id="Foo" version="1.0.0" targetFramework="net461" />
</packages>' | out-file $packagesConfigPath

    $solutionDirectory = Split-Path -Path $projectDirectoryPath -Parent
    $opensourceRepo = Join-Path $solutionDirectory "opensourceRepo"
    $privateRepo = Join-Path $solutionDirectory "privateRepo"

    $nugetConfigPath = Join-Path $solutionDirectory 'nuget.config'

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

	$settingFileContent -f $opensourceRepo,$privateRepo | Out-File -Encoding "UTF8" $nugetConfigPath

    CreateTestPackage "Contoso.MVC.ASP" "1.0.0" $privateRepo
    CreateTestPackage "Contoso.Opensource.Buffers" "2.0.0" $opensourceRepo
    CreateTestPackage "Foo" "1.0.0" $opensourceRepo
    # Act
    & $context.NuGetExe restore -SolutionDirectory $solutionDirectory $projectPath

    # Assert
    $packagesFolder = Join-Path $solutionDirectory "packages"
    $contosoMVCNupkgFolder = Join-Path $packagesFolder "Contoso.MVC.ASP.1.0.0"
    Assert-PathExists(Join-Path $contosoMVCNupkgFolder "Contoso.MVC.ASP.1.0.0.nupkg")
    $contosoOpenSourceNupkgFolder = Join-Path $packagesFolder "Contoso.Opensource.Buffers.2.0.0"
    Assert-PathExists(Join-Path $contosoOpenSourceNupkgFolder "Contoso.Opensource.Buffers.2.0.0.nupkg")

    $errorlist = Get-Errors
    Assert-AreEqual 0 $errorlist.Count    

    $warninglist = Get-Warnings
    Assert-AreEqual 0 $warninglist.Count 
}

function Test-NuGet2
{
    param($context)

    # Arrange
    $p = New-ConsoleApplication

    $projectDirectoryPath = $p.Properties.Item("FullPath").Value
    $projectName = Split-Path -Path $projectDirectoryPath -Leaf
    $projectPath = Join-Path $projectDirectoryPath $projectName".csproj"

    $packagesConfigPath = Join-Path $projectDirectoryPath 'packages.config'
    # Write a file to disk, but do not add it to project
    '<packages>
        <package id="Contoso.MVC.ASP" version="1.0.0" targetFramework="net461" />
</packages>' | out-file $packagesConfigPath

    $solutionDirectory = Split-Path -Path $projectDirectoryPath -Parent
    $opensourceRepo = Join-Path $solutionDirectory "opensourceRepo"
    $privateRepo = Join-Path $solutionDirectory "privateRepo"

    $nugetConfigPath = Join-Path $solutionDirectory 'nuget.config'

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

	$settingFileContent -f $opensourceRepo,$privateRepo | Out-File -Encoding "UTF8" $nugetConfigPath

    CreateTestPackage "Contoso.MVC.ASP" "1.0.0" $privateRepo "Thisisfromprivaterepo.txt"
    CreateTestPackage "Contoso.MVC.ASP" "1.0.0" $opensourceRepo "Thisisfromopensourcerepo.txt"

    # Act
    & $context.NuGetExe restore -SolutionDirectory $solutionDirectory $projectPath

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

function Test-NuGet3
{
    param($context)

    # Arrange
    $p = New-ConsoleApplication

    $projectDirectoryPath = $p.Properties.Item("FullPath").Value
    $projectName = Split-Path -Path $projectDirectoryPath -Leaf
    $projectPath = Join-Path $projectDirectoryPath $projectName".csproj"

    $packagesConfigPath = Join-Path $projectDirectoryPath 'packages.config'
    # Write a file to disk, but do not add it to project
    '<packages>
        <package id="Contoso.MVC.ASP" version="1.0.0" targetFramework="net461" />
</packages>' | out-file $packagesConfigPath

    $solutionDirectory = Split-Path -Path $projectDirectoryPath -Parent
    $opensourceRepo = Join-Path $solutionDirectory "opensourceRepo"
    $privateRepo = Join-Path $solutionDirectory "privateRepo"

    $nugetConfigPath = Join-Path $solutionDirectory 'nuget.config'

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

	$settingFileContent -f $opensourceRepo,$privateRepo | Out-File -Encoding "UTF8" $nugetConfigPath

    CreateTestPackage "Contoso.MVC.ASP" "1.0.0" $privateRepo "Thisisfromprivaterepo.txt"
    CreateTestPackage "Contoso.MVC.ASP" "1.0.0" $opensourceRepo "Thisisfromopensourcerepo.txt"

    # Act
    & $context.NuGetExe restore -SolutionDirectory $solutionDirectory $projectPath

    # Assert
    $packagesFolder = Join-Path $solutionDirectory "packages"
    $contosoNupkgFolder = Join-Path $packagesFolder "Contoso.MVC.ASP.1.0.0"
    Assert-PathExists(Join-Path $contosoNupkgFolder "Contoso.MVC.ASP.1.0.0.nupkg")

    $errorlist = Get-Errors
    Assert-AreEqual 0 $errorlist.Count
}

# function Test-MSBuild1
# {
#     param($context)

#     # Arrange
#     $p = New-ConsoleApplication

#     $projectDirectoryPath = $p.Properties.Item("FullPath").Value
#     $packagesConfigPath = Join-Path $projectDirectoryPath 'packages.config'
#     # Write a file to disk, but do not add it to project
#     '<packages>
#         <package id="Contoso.MVC.ASP" version="1.0.0" targetFramework="net461" />
#         <package id="Contoso.Opensource.Buffers" version="2.0.0" targetFramework="net461" />
#         <package id="Foo" version="1.0.0" targetFramework="net461" />
# </packages>' | out-file $packagesConfigPath

#     $solutionDirectory = Split-Path -Path $projectDirectoryPath -Parent
#     $opensourceRepo = Join-Path $solutionDirectory "opensourceRepo"
#     $privateRepo = Join-Path $solutionDirectory "privateRepo"
#     $nugetConfigPath = Join-Path $solutionDirectory 'nuget.config'

# 	$settingFileContent =@"
# <?xml version="1.0" encoding="utf-8"?>
# <configuration>
#     <packageSources>
#     <clear />
#     <add key="PublicRepository" value="{0}" />
#     <add key="PrivateRepository" value="{1}" />
#     </packageSources>
#     <packageNamespaces>
#         <packageSource key="PublicRepository"> 
#             <namespace id="Contoso.Opensource.*" />
#         </packageSource>
#         <packageSource key="PrivateRepository">
#             <namespace id="Contoso.MVC.*" />
#         </packageSource>
#     </packageNamespaces>
# </configuration>
# "@

# 	$settingFileContent -f $opensourceRepo,$privateRepo | Out-File -Encoding "UTF8" $nugetConfigPath

#     CreateTestPackage "Contoso.MVC.ASP" "1.0.0" $privateRepo "Thisisfromprivaterepo.txt"
#     CreateTestPackage "Contoso.MVC.ASP" "1.0.0" $opensourceRepo "Thisisfromopensourcerepo.txt"
#     CreateTestPackage "Contoso.Opensource.Buffers" "2.0.0" $opensourceRepo
#     CreateTestPackage "Foo" "1.0.0" $opensourceRepo

#     # Act
#     $MSBuildExe = Get-MSBuildExe
#     & "$MSBuildExe" -p:RestorePackagesConfig=true /t:restore    
#     Build-Solution

#     # Assert
#     $packagesFolder = Join-Path $solutionDirectory "packages"
#     $contosoMVCNupkgFolder = Join-Path $packagesFolder "Contoso.MVC.ASP.1.0.0"
#     Assert-PathExists(Join-Path $contosoMVCNupkgFolder "Contoso.MVC.ASP.1.0.0.nupkg")
#     # Make sure name squatting package from public repo not restored.
#     # Assert-PathExists(Join-Path $contentFolder "Thisisfromprivaterepo.txt")
#     $contosoOpenSourceNupkgFolder = Join-Path $packagesFolder "Contoso.Opensource.Buffers.2.0.0"
#     Assert-PathExists(Join-Path $contosoOpenSourceNupkgFolder "Contoso.Opensource.Buffers.2.0.0.nupkg")
#     $fooNupkgFolder = Join-Path $packagesFolder "Foo.1.0.0"
#     Assert-PathExists(Join-Path $fooNupkgFolder "Foo.1.0.0.nupkg")

#     $errorlist = Get-Errors
#     Assert-AreEqual 0 $errorlist.Count    
# }

# function Test-MSBuild2
# {
#     param($context)

#     # Arrange
#     $p = New-ConsoleApplication

#     $projectDirectoryPath = $p.Properties.Item("FullPath").Value

#     $packagesConfigPath = Join-Path $projectDirectoryPath 'packages.config'
#     # Write a file to disk, but do not add it to project
#     '<packages>
#         <package id="Contoso.MVC.ASP" version="1.0.0" targetFramework="net461" />
# </packages>' | out-file $packagesConfigPath

#     $solutionDirectory = Split-Path -Path $projectDirectoryPath -Parent
#     $opensourceRepo = Join-Path $solutionDirectory "opensourceRepo"
#     $privateRepo = Join-Path $solutionDirectory "privateRepo"

#     $nugetConfigPath = Join-Path $solutionDirectory 'nuget.config'

# 	$settingFileContent =@"
# <?xml version="1.0" encoding="utf-8"?>
# <configuration>
#     <packageSources>
#     <clear />
#     <add key="OpensourceRepository" value="{0}" />
#     <add key="PrivateRepository" value="{1}" />
#     </packageSources>
# </configuration>
# "@

# 	$settingFileContent -f $opensourceRepo,$privateRepo | Out-File -Encoding "UTF8" $nugetConfigPath

#     CreateTestPackage "Contoso.MVC.ASP" "1.0.0" $privateRepo "Thisisfromprivaterepo.txt"
#     CreateTestPackage "Contoso.MVC.ASP" "1.0.0" $opensourceRepo "Thisisfromopensourcerepo.txt"

#     # Act
#     $MSBuildExe = Get-MSBuildExe   
#     & "$MSBuildExe" -p:RestorePackagesConfig=true /t:restore
#     Build-Solution

#     # Assert
#     $packagesFolder = Join-Path $solutionDirectory "packages"
#     $contosoNupkgFolder = Join-Path $packagesFolder "Contoso.MVC.ASP.1.0.0"
#     Assert-PathExists(Join-Path $contosoNupkgFolder "Contoso.MVC.ASP.1.0.0.nupkg")

#     $errorlist = Get-Errors
#     Assert-AreEqual 0 $errorlist.Count
# }

# Create a test package 
function CreateTestPackage {
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
    "test" >> $tempFile
    $packageFile = New-Object NuGet.Packaging.PhysicalPackageFile
    $packageFile.SourcePath = $tempFile
    $packageFile.TargetPath = "content\$id-test1.txt"
    $builder.Files.Add($packageFile)

    if($requestAdditionalContent)
    {
        # add one content file
        $tempFile2 = [IO.Path]::GetTempFileName()
        "test" >> $tempFile2        
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