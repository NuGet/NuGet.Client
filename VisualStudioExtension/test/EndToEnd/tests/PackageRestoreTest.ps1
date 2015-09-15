# Tests that packages are restored on build
function Test-PackageRestore-SimpleTest {
    param($context)

    # Arrange
    $p1 = New-ClassLibrary	
    $p1 | Install-Package FakeItEasy -version 1.8.0
    
    $p2 = New-ClassLibrary
    $p2 | Install-Package elmah -Version 1.1

    #$f = New-SolutionFolder 'Folder1'
    $p3 = New-ClassLibrary
    $p3 | Install-Package Newtonsoft.Json -Version 5.0.6

    #$f2 = $f | New-SolutionFolder 'Folder2'
    $p4 = New-ClassLibrary
    $p4 | Install-Package Ninject

    # delete the packages folder
    $packagesDir = Get-PackagesDir
    RemoveDirectory $packagesDir
    Assert-False (Test-Path $packagesDir)

    # Act
    Build-Solution

    # Assert
    Assert-True (Test-Path $packagesDir)
    Assert-Package $p1 FakeItEasy
    Assert-Package $p2 elmah
    Assert-Package $p3 Newtonsoft.Json
    Assert-Package $p4 Ninject
}

# Tests that package restore honors PackageSaveMode in config
<#
function Test-PackageRestore-PackageSaveMode {
    param($context)

    try {
        [NuGet.PackageManagement.VisualStudio.SettingsHelper]::Set('PackageSaveMode', 'nuspec')

        # Arrange
        $p1 = New-ClassLibrary	
        $p1 | Install-Package FakeItEasy -version 1.8.0
    
        # delete the packages folder
        $packagesDir = Get-PackagesDir
        RemoveDirectory $packagesDir
        Assert-False (Test-Path $packagesDir)
    
        # Act
        Build-Solution

        # Assert	    
        # the nuspec file should exist
        $nuspecFile = Join-Path $packagesDir "FakeItEasy.1.8.0\FakeItEasy.1.8.0.nuspec"
        Assert-PathExists $nuspecFile

        # while the nupkg file should not
        $nupkgFile = Join-Path $packagesDir "FakeItEasy.1.8.0\FakeItEasy.1.8.0.nupkg"
        Assert-False (Test-Path $nupkgFile)
    }
    finally {
        [NuGet.PackageManagement.VisualStudio.SettingsHelper]::Set('PackageSaveMode', $null)
    }
    
}
#>

# Tests that package restore works for website project
function Test-PackageRestore-Website {
    param($context)

    # Arrange
    $p = New-WebSite	
    $p | Install-Package JQuery
    
    # delete the packages folder
    $packagesDir = Get-PackagesDir
    Remove-Item -Recurse -Force $packagesDir
    Assert-False (Test-Path $packagesDir)

    # Act
    Build-Solution

    # Assert
    Assert-True (Test-Path $packagesDir)
    Assert-Package $p JQuery
}

# Tests that package restore works for JavaScript Metro project
function Test-PackageRestore-JavaScriptMetroProject {
    param($context)

    if ($dte.Version -eq '10.0') {
        return
    }

    # Arrange
    $p = New-JavaScriptApplication	
    Install-Package JQuery -projectName $p.Name
    
    # delete the packages folder
    $packagesDir = Get-PackagesDir
    Remove-Item -Recurse -Force $packagesDir
    Assert-False (Test-Path $packagesDir)

    # Act
    Build-Solution

    # Assert
    Assert-True (Test-Path $packagesDir)
    Assert-Package $p JQuery
}

# Tests that package restore works for unloaded projects, as long as
# there is at least one loaded project.
function Test-PackageRestore-UnloadedProjects{
    param($context)

    # Arrange
    $p1 = New-ClassLibrary	
    $p1 | Install-Package Microsoft.Bcl.Build -version 1.0.8
    
    $p2 = New-ClassLibrary

    $solutionDir = $dte.Solution.FullName
    $packagesDir = Get-PackagesDir
    $dte.Solution.SaveAs($solutionDir)
    Close-Solution

    # delete the packages folder
    Remove-Item -Recurse -Force $packagesDir
    Assert-False (Test-Path $packagesDir)

    # reopen the solution. Now the project that references Microsoft.Bcl.Build
    # will not be loaded because of missing targets file
    Open-Solution $solutionDir

    # Act
    Build-Solution

    # Assert
    $dir = Join-Path $packagesDir "Microsoft.Bcl.Build.1.0.8"
    Assert-PathExists $dir
}

# Tests that an error will be generated if package restore fails
function Test-PackageRestore-ErrorMessage {
    param($context)

    # Arrange
    $p = New-ClassLibrary	
    Install-Package -Source $context.RepositoryRoot -Project $p.Name NonStrongNameB
    
    # delete the packages folder
    $packagesDir = Get-PackagesDir
    Remove-Item -Recurse -Force $packagesDir
    Assert-False (Test-Path $packagesDir)

    # Act
    # package restore will fail because the source $context.RepositoryRoot is not
    # listed in the settings.
    Build-Solution

    # Assert
    $errorlist = Get-Errors
    Assert-AreEqual 1 $errorlist.Count

    $error = $errorlist[$errorlist.Count-1]
    Assert-True ($error.Description.Contains('NuGet Package restore failed for project'))

    $output = GetBuildOutput
    Assert-True ($output.Contains('NuGet package restore failed.'))
}

# Tests that output does not contain package restore finished
# when there are no missing packages
function Test-PackageRestore-PackageAlreadyInstalled {
    param($context)

    # Arrange
    $p = New-ClassLibrary	
    $p | Install-Package jQuery.Validation

    # Act
    # package restore will just exit as there are no missing packages
    Build-Solution

    # Assert
    $output = GetBuildOutput
    Assert-True ($output.Contains('All packages are already installed and there is nothing to restore.'))
	Assert-False ($output.Contains('NuGet package restore finished.'))
}

# Test that package restore will check for missing packages when consent is not granted,
# while IsAutomatic is true.
function Test-PackageRestore-CheckForMissingPackages {
    param($context)

    # Arrange
    $p1 = New-ClassLibrary	
    $p1 | Install-Package Newtonsoft.Json -Version 5.0.6
    
    $f = New-SolutionFolder 'Folder1'
    $p2 = $f | New-ClassLibrary
    $p2 | Install-Package elmah -Version 1.1

    $f2 = $f | New-SolutionFolder 'Folder2'
    $p3 = $f2 | New-ClassLibrary
    $p3 | Install-Package Ninject

    # delete the packages folder
    $packagesDir = Get-PackagesDir
    RemoveDirectory $packagesDir
    Assert-False (Test-Path $packagesDir)
    
    try {
        [NuGet.PackageManagement.VisualStudio.SettingsHelper]::Set('PackageRestoreConsentGranted', 'false')
        [NuGet.PackageManagement.VisualStudio.SettingsHelper]::Set('PackageRestoreIsAutomatic', 'true')

        # Act
        Build-Solution

        # Assert
        $errorlist = Get-Errors
        Assert-AreEqual 1 $errorlist.Count

        $error = $errorlist[$errorlist.Count-1]
        Assert-True ($error.Description.Contains('One or more NuGet packages need to be restored but couldn''t be because consent has not been granted.'))
        Assert-True ($error.Description.Contains('Newtonsoft.Json 5.0.6'))
        Assert-True ($error.Description.Contains('elmah 1.1'))
        Assert-True ($error.Description.Contains('Ninject'))
    }
    finally {
        [NuGet.PackageManagement.VisualStudio.SettingsHelper]::Set('PackageRestoreConsentGranted', 'true')
        [NuGet.PackageManagement.VisualStudio.SettingsHelper]::Set('PackageRestoreIsAutomatic', 'true')
    }
}	

# Tests that package restore is a no-op when setting PackageRestoreIsAutomatic is false.
function Test-PackageRestore-IsAutomaticIsFalse {
    param($context)

    # Arrange
    $p1 = New-ClassLibrary	
    $p1 | Install-Package FakeItEasy -version 1.8.0
    
    $p2 = New-ClassLibrary
    $p2 | Install-Package elmah -Version 1.1

    $f = New-SolutionFolder 'Folder1'
    $p3 = $f | New-ClassLibrary
    $p3 | Install-Package Newtonsoft.Json -Version 5.0.6

    # delete the packages folder
    $packagesDir = Get-PackagesDir
    RemoveDirectory $packagesDir
    Assert-False (Test-Path $packagesDir)

    try {
        [NuGet.PackageManagement.VisualStudio.SettingsHelper]::Set('PackageRestoreIsAutomatic', 'false')

        # Act
        Build-Solution

        # Assert		
        Assert-False (Test-Path $packagesDir)
    }
    finally {
        [NuGet.PackageManagement.VisualStudio.SettingsHelper]::Set('PackageRestoreConsentGranted', 'true')
        [NuGet.PackageManagement.VisualStudio.SettingsHelper]::Set('PackageRestoreIsAutomatic', 'true')
    }
}

# Test that during package restore, all sources are used.
function Test-PackageRestore-AllSourcesAreUsed {
    param($context)
    
    $tempDirectory = $Env:temp
    $source1 = Join-Path $tempDirectory ([System.IO.Path]::GetRandomFileName())
    $source2 = Join-Path $tempDirectory ([System.IO.Path]::GetRandomFileName())

    try {
        # Arrange		
        New-Item $source1 -ItemType directory
        New-Item $source2 -ItemType directory
        [NuGet.PackageManagement.VisualStudio.SettingsHelper]::AddSource('testSource1', $source1);
        [NuGet.PackageManagement.VisualStudio.SettingsHelper]::AddSource('testSource2', $source2);	
        CreateTestPackage 'p1' '1.0' $source1
        CreateTestPackage 'p2' '1.0' $source2
        
        # Arrange
        # create project and install packages
        $proj = New-ClassLibrary
        $proj | Install-Package p1 -source testSource1
        $proj | Install-Package p2 -source testSource2
        Assert-Package $proj p1
        Assert-Package $proj p2

        # Arrange
        # delete the packages folder
        $packagesDir = Get-PackagesDir
        RemoveDirectory $packagesDir
        Assert-False (Test-Path $packagesDir)

        # Act
        Build-Solution

        # Assert
        # both p1 and p2 are restored
        Assert-True (Test-Path (Join-Path $packagesDir 'p1.1.0' ))
        Assert-True (Test-Path (Join-Path $packagesDir 'p2.1.0' ))

        Write-Host 'It is done!'
    }
    finally
    {
        [NuGet.PackageManagement.VisualStudio.SettingsHelper]::RemoveSource('testSource1')
        [NuGet.PackageManagement.VisualStudio.SettingsHelper]::RemoveSource('testSource2')		
        RemoveDirectory $source1
        RemoveDirectory $source2

        # change active package source to "All"
        # $componentService = Get-VSComponentModel
        #$packageSourceProvider = $componentService.GetService([NuGet.PackageManagement.VisualStudio.IVsPackageSourceProvider])
        #$packageSourceProvider.ActivePackageSource = [NuGet.PackageManagement.VisualStudio.AggregatePackageSource]::Instance
    }
}

# Create a test package 
function CreateTestPackage {
    param(
        [string]$id,
        [string]$version,
        [string]$outputDirectory
    )

    $builder = New-Object NuGet.PackageBuilder
    $builder.Authors.Add("test_author")
    $builder.Id = $id
    $builder.Version = New-Object NuGet.SemanticVersion($version)
    $builder.Description = "description" 

    # add one content file
    $tempFile = [IO.Path]::GetTempFileName()
    "test" >> $tempFile
    $packageFile = New-Object NuGet.PhysicalPackageFile
    $packageFile.SourcePath = $tempFile
    $packageFile.TargetPath = "content\$id-test1.txt"
    $builder.Files.Add($packageFile)

    # create the package file
    $outputFileName = Join-Path $outputDirectory "$id.$version.nupkg"
    $outputStream = New-Object IO.FileStream($outputFileName, [System.IO.FileMode]::Create)
    try {
        $builder.Save($outputStream)
    }
    finally
    {
        $outputStream.Dispose()
        Remove-Item $tempFile
    }
}

function GetBuildOutput { 
    $dte2 = Get-Interface $dte ([EnvDTE80.DTE2])
    $buildPane = $dte2.ToolWindows.OutputWindow.OutputWindowPanes.Item("Build")
    $doc = $buildPane.TextDocument
    $sel = $doc.Selection
    $sel.StartOfDocument($FALSE)
    $sel.EndOfDocument($TRUE)
    $sel.Text
}

function RemoveDirectory {
    param($dir)

    $iteration = 0
    while ($iteration++ -lt 10)
    {
        if (Test-Path $dir)
        {
            Remove-Item -Recurse -Force $dir -ErrorAction SilentlyContinue
        }
        else 
        {
            break;
        }
    }
}