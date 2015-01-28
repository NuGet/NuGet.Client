# Nuget specific assert helpers

function Get-SolutionPackage {
    param(
        [parameter(Mandatory = $true)]
        [string]$Id,
        [string]$Version
    )

    # Get the package entries from the solution
    $packages = Get-Package | ?{ $_.Id -eq $Id }

    if($Version) {
        $actualVersion = [NuGet.SemanticVersion]::Parse($Version)
        $packages = $packages | ?{[NuGet.SemanticVersion]::Parse($_.Version[0]) -eq $actualVersion }
    }
    
    $packages
}

function Get-ProjectRepository {
    param(
        [parameter(Mandatory = $true)]
        $Project
    )
    
    $packageManager = $host.PrivateData.packageManagerFactory.CreatePackageManager()    
    $fileSystem = New-Object NuGet.PhysicalFileSystem((Get-ProjectDir $Project))
    New-Object NuGet.PackageReferenceRepository($fileSystem, (Get-ProjectName $Project), $packageManager.LocalRepository)
}

function Get-ProjectPackageReferences {
    param(
        [parameter(Mandatory = $true)]
        $Project
    )

    $packageReferenceFile = New-Object NuGet.PackageReferenceFile((Get-ProjectItemPath $Project 'packages.config'))
    $packageReferencesEnumerable = $packageReferenceFile.GetPackageReferences()
    $packageReferences = @()

    # In powershell, It is easier to simply iterate over the enumerable and create an array
    # than to try and use the extension method ToArray in System.Linq
    foreach($packageReference in $packageReferencesEnumerable)
    {
        $packageReferences += $packageReference
    }

    return ,$packageReferences
}

function Get-ProjectPackage {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true)]
        [string]$Id,
        [string]$Version
    )
    
    $repository = Get-ProjectRepository $Project
        
    # We can't call the nuget methods since powershell gets confused with overload resolution
    $packages = $repository.GetPackages() | ?{ $_.Id -eq $Id }    
    
    if($Version) {
        $actualVersion = [NuGet.SemanticVersion]::Parse($Version)
        $packages = $packages | ?{ $_.Version -eq $actualVersion }
    }
    
    $packages
}

function Add-PackageConstraint {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true)]
        [string]$Id,
        [string]$Constraint
    )

   $path = (Get-ProjectItemPath $Project packages.config)
   $packagesConfig = [xml](Get-Content $path)
   $reference = $packagesConfig.packages.package | ?{ $_.Id -eq $Id } | Select -First 1
   Assert-NotNull $reference "Unable to find package $Id"
   $reference.SetAttribute("allowedVersions", $Constraint)
   $packagesConfig.Save($path)
}

function Assert-Package {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true)]
        [string]$Id,
        [string]$Version
    )

    $projectName = Get-ProjectName $Project

    $configName = "packages." + $projectName + ".config"

    # Check for existence of packages.project_name.config
    $configPath = Join-Path (Get-ProjectDir $Project) $configName

    if (-not (Test-Path $configPath)) 
    {
        # Check for existence on disk of packages.config
        Assert-PathExists (Join-Path (Get-ProjectDir $Project) "packages.config")

        $configName = "packages.config"
    }

    
    # Check for the project item
    Assert-NotNull (Get-ProjectItem $Project $configName) "$configName does not exist in $projectName"
        
    $repository = Get-ProjectRepository $Project
    
    Assert-NotNull $repository "Unable to find the project repository"
    
    if($Version) {
        $actualVersion = [NuGet.SemanticVersion]::Parse($Version)
    }
    
    Assert-NotNull ([NuGet.PackageRepositoryExtensions]::Exists($repository, $Id, $actualVersion)) "Package $Id $Version is not referenced in $projectName"
}

function Assert-NoPackage {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true)]
        [string]$Id,
        [string]$Version
    )
    
    # Check for existance on disk of packages.config
    if ((Join-Path (Get-ProjectDir $Project) packages.config) -eq $false)
    {
        return
    }
    
    # Check for the project item
    # Assert-NotNull (Get-ProjectItem $Project packages.config) "packages.config does not exist in $($Project.Name)"
    
    $repository = Get-ProjectRepository $Project
    if (!$repository) 
    {
        return
    }
    
    if($Version) {
        $actualVersion = [NuGet.SemanticVersion]::Parse($Version)
    }
    
    Assert-False ([NuGet.PackageRepositoryExtensions]::Exists($repository, $Id, $actualVersion)) "Package $Id $Version is referenced in $($Project.Name)"
}

function Assert-SolutionPackage {
    param(
        [parameter(Mandatory = $true)]
        [string]$Id,
        [string]$Version
    )
    
    # Make sure the packages directory exists
    Assert-PathExists (Get-PackagesDir) "The packages directory doesn't exist"
    
    $packages = Get-SolutionPackage $Id $Version
    
    if(!$packages -or $packages.Count -eq 0) {
        Assert-Fail "Package $Id $Version does not exist at solution level"
    }
}

function Assert-NoSolutionPackage {
    param(
        [parameter(Mandatory = $true)]
        [string]$Id,
        [parameter(Mandatory = $true)]
        [string]$Version
    )
    
    # Make sure the packages directory exists
    $packagesDir = Get-PackagesDir
    if (-not (Test-Path $packagesDir))
    {
        return
    }

    $thisPackageDir = Join-Path $packagesDir ($Id + "." + $Version)
    if (-not (Test-Path $thisPackageDir))
    {
        return
    }

    # if there is a .deleteme file next to the folder, consider the installation succeeds
    $deleteMeFile = $thisPackageDir + ".deleteme"
    if (Test-Path $deleteMeFile)
    {
        return
    } 
    
    Assert-Fail "Package $Id $Version does EXIST at solution level"
}

function Assert-ProjectImport {
    param(
        [parameter(Mandatory = $true)]
        $project,
        [parameter(Mandatory = $true)]
        [string]$importFile
    )

    $project.Save()
    $doc = [xml](Get-Content $project.FullName)

    if (!$doc.Project.Import)
    {
        Assert-Fail "Project $($project.Name) does NOT contain the import file `'$importFile`'"
    }

    $matchedImports = @($doc.Project.Import) | ? { $_.Project -eq $importFile }
    if (!$matchedImports)
    {
        Assert-Fail "Project $($project.Name) does NOT contain the import file `'$importFile`'"
    }
}

function Assert-NoProjectImport {
    param(
        [parameter(Mandatory = $true)]
        $project,
        [parameter(Mandatory = $true)]
        [string]$importFile
    )

    $project.Save()
    $doc = [xml](Get-Content $project.FullName)

    if ($doc.Project.Import)
    {
        $matchedImports = @($doc.Project.Import) | ? { $_.Project -eq $importFile }
        if ($matchedImports)
        {
            Assert-Fail "Project $($project.Name) does contain the import file `'$importFile`'"
        }
    }
}

function Get-PackagesDir {
    # TODO: Handle when the package location changes
    Join-Path (Get-SolutionDir) packages
}