# Nuget specific assert helpers

# Set the locked state of the lock file
function Set-LockFileLocked {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true)]
        [Boolean]$State
    )

    $lockFile = Get-ProjectJsonLockFile $Project

    $lockFile.IsLocked = $State

    Set-ProjectJsonLockFile $Project $LockFile
}

# True if the lock file is locked
function Get-LockFileLocked {
    param(
        [parameter(Mandatory = $true)]
        $Project
    )

    $lockFile = Get-ProjectJsonLockFile $Project

    return $lockFile.IsLocked
}

function Assert-ProjectJsonLockFileExists {
    param(
        [parameter(Mandatory = $true)]
        $Project
    )

    $projectJsonLockFilePath = Get-ProjectJsonLockFilePath $Project

    Assert-PathExists $projectJsonLockFilePath "project.lock.json file does not exist"
}

function Assert-ProjectJsonLockFileDoesNotExist {
    param(
        [parameter(Mandatory = $true)]
        $Project
    )

    $projectJsonLockFilePath = Get-ProjectJsonLockFilePath $Project

    Assert-PathNotExists $projectJsonLockFilePath "project.lock.json file exists"
}

function Assert-ProjectJsonLockFileRuntimeAssembly {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true)]
        [string]$assembly
    )

    $lockFile = Get-ProjectJsonLockFile $Project

    Assert-NotNull $lockFile

    $found = $false

    foreach ($target in $lockFile.Targets) {
        
        foreach ($library in $target.Libraries)
        {
            foreach ($runtimeAssembly in $library.RuntimeAssemblies)
            {  
                if ($runtimeAssembly.Path.Equals($assembly))
                {
                    $found = $true
                }
            }
        }
    }

    Assert-True $found "Runtime assembly $assembly was not found in the lock file for $($Project.Name)"    
}

function Assert-ProjectJsonLockFilePackage {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true)]
        [string]$Id,
        [string]$Version
    )

    $lockFile = Get-ProjectJsonLockFile $Project

    Assert-NotNull $lockFile

    $found = $false

    foreach ($library in $lockFile.Libraries) {
        
        if ($library.Name.ToUpperInvariant().Equals($Id.ToUpperInvariant()))
        {
            if ($Version)
            {
                if ($library.Version.Equals([NuGet.Versioning.NuGetVersion]::Parse($Version)))
                {
                    $found = $true
                }
            }
            else
            {
                $found = $true
            }
        }
    }

    Assert-True $found "Package $Id $Version was not found in the lock file for $($Project.Name)"    
}

function Assert-ProjectJsonLockFilePackageNotFound {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true)]
        [string]$Id
    )

    $lockFile = Get-ProjectJsonLockFile $Project

    Assert-NotNull $lockFile

    $found = $false

    foreach ($library in $lockFile.Libraries) {
        
        if ($library.Name.ToUpperInvariant().Equals($Id.ToUpperInvariant()))
        {
            $found = $true
        }
    }

    Assert-False $found "Package $Id was found in the lock file for $($Project.Name)"    
}

function Assert-ProjectJsonDependency {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true)]
        [string]$Id,
        [string]$Range
    )

    $projectJson = Get-ProjectJsonPackageSpec $Project

    Assert-NotNull $projectJson

    $found = $false

    foreach ($dependency in $projectJson.Dependencies) {
        
        $library = $dependency.LibraryRange

        if ($library.Name.ToUpperInvariant().Equals($Id.ToUpperInvariant()))
        {
            if ($Range)
            {
                if ($library.VersionRange.OriginalString.ToUpperInvariant().Equals($Range.ToUpperInvariant()))
                {
                    $found = $true
                }
            }
            else
            {
                $found = $true
            }
        }
    }

    Assert-True $found "Package $Id $Range is not referenced in $($Project.Name)"    
}

function Assert-ProjectJsonDependencyWithinTargetFramework {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true)]
        [string]$Id,
        [string]$Range
    )

    $projectJson = Get-ProjectJsonPackageSpec $Project

    Assert-NotNull $projectJson

    $found = $false

    foreach ($targetFrameworkInfo in $projectJson.TargetFrameworks) {

        foreach ($dependency in $targetFrameworkInfo.Dependencies) {

			$library = $dependency.LibraryRange

			if ($library.Name.ToUpperInvariant().Equals($Id.ToUpperInvariant()))
			{
				if ($Range)
				{
					if ($library.VersionRange.OriginalString.ToUpperInvariant().Equals($Range.ToUpperInvariant()))
					{
						$found = $true
					}
				}
				else
				{
					$found = $true
				}
			}
		}
    }

    Assert-True $found "Package $Id $Range is not referenced in $($Project.Name)"    
}

function Assert-ProjectJsonDependencyNotFound {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true)]
        [string]$Id
    )

    $projectJson = Get-ProjectJsonPackageSpec $Project

    Assert-NotNull $projectJson

    $found = $false

    foreach ($dependency in $projectJson.Dependencies) {
        
        $library = $dependency.LibraryRange

        if ($library.Name.ToUpperInvariant().Equals($Id.ToUpperInvariant()))
        {
            $found = $true
        }
    }

    Assert-False $found "Package $Id is referenced in $($Project.Name)"    
}

function Get-ProjectJsonPackageSpec {
    param(
        [parameter(Mandatory = $true)]
        $Project
    )
    
    $dir = Split-Path -parent $Project.FullName

    $projectJsonPath = Join-Path $dir "project.json"

    Assert-PathExists $projectJsonPath "project.json file does not exist"

    $stream = [IO.File]::ReadAllText($projectJsonPath)

    return [NuGet.ProjectModel.JsonPackageSpecReader]::GetPackageSpec($stream, $Project.Name, $projectJsonPath)
}

function Get-ProjectJsonLockFile {
    param(
        [parameter(Mandatory = $true)]
        $Project
    )
    
    $projectJsonLockFilePath = Get-ProjectJsonLockFilePath $Project

    Assert-PathExists $projectJsonLockFilePath "project.lock.json file does not exist"

    $lockFileFormat = New-Object 'NuGet.ProjectModel.LockFileFormat'

    return $lockFileFormat.Read($projectJsonLockFilePath)
}

function Set-ProjectJsonLockFile {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true)]
        [NuGet.ProjectModel.LockFile]$LockFile
    )
    
    $projectJsonLockFilePath = Get-ProjectJsonLockFilePath $Project

    $lockFileFormat = New-Object 'NuGet.ProjectModel.LockFileFormat'

    return $lockFileFormat.Write($projectJsonLockFilePath, $LockFile)
}

function Get-ProjectJsonLockFilePath {
    param(
        [parameter(Mandatory = $true)]
        $Project
    )
    
    $dir = Split-Path -parent $Project.FullName

    $projectJsonLockFilePath = Join-Path $dir "project.lock.json"

    return $projectJsonLockFilePath
}

function Remove-ProjectJsonLockFile {
    param(
        [parameter(Mandatory = $true)]
        $Project
    )

    $dir = Split-Path -parent $Project.FullName

    $projectJsonLockFilePath = Join-Path $dir "project.lock.json"

    Assert-PathExists $projectJsonLockFilePath

    Remove-Item $projectJsonLockFilePath

    Assert-PathNotExists $projectJsonLockFilePath
}

function Get-SolutionPackage {
    param(
        [parameter(Mandatory = $true)]
        [string]$Id,
        [string]$Version
    )

    # Get the package entries from the solution
    $packages = Get-Package | ?{ $_.Id -eq $Id }

    if($Version) {
        $actualVersion = [NuGet.Versioning.NuGetVersion]::Parse($Version)
        $packages = $packages | ?{[NuGet.Versioning.NuGetVersion]::Parse($_.Version[0]) -eq $actualVersion }
    }
    
    $packages
}

function Get-PackageConfigName {
    param(
        [parameter(Mandatory = $true)]
        $Project)
    $projectName = Get-ProjectName $Project

    $configName = "packages." + $projectName + ".config"

    # Check for existence of packages.project_name.config
    $configPath = Join-Path (Get-ProjectDir $Project) $configName

    if (-not (Test-Path $configPath))
    {

        # Check for existence on disk of packages.config
        #Assert-PathExists (Join-Path (Get-ProjectDir $Project) "packages.config")

        $configName = "packages.config"
    }

    return $configName
}

function Get-PackagesConfigNuGetProject {
    param(
        [parameter(Mandatory = $true)]
        $Project
    )

    $configName = Get-PackageConfigName $Project
    Write-Host 'Packages config file name is ' $configName
    $packagesConfigFolderPath = Get-ProjectDir $Project
    $metadataDictionary = New-Object 'System.Collections.Generic.Dictionary[string,object]'
    $metadataDictionary.Add('Name', $Project.Name)
    $targetFrameworkMoniker = $project.Properties.Item("TargetFrameworkMoniker").Value
    $nuGetFramework = [NuGet.Frameworks.NuGetFramework]::Parse($targetFrameworkMoniker)
    $metadataDictionary.Add('TargetFramework', $nuGetFramework)
    $packagesConfigNuGetProject = New-Object NuGet.ProjectManagement.PackagesConfigNuGetProject($packagesConfigFolderPath, $metadataDictionary)
    return $packagesConfigNuGetProject
}


function Get-InstalledPackageReferencesFromProject {
    param(
        [parameter(Mandatory = $true)]
        $packagesConfigNuGetProject
    )

    $token = [System.Threading.CancellationToken]::None
    $task = $packagesConfigNuGetProject.GetInstalledPackagesAsync($token)
    $task.Wait()

    $result = ,$task.Result
    Write-Host 'Packages count in packages config file: ' $result.Count
    return $result
}

function Get-ProjectPackageReferences {
    param(
        [parameter(Mandatory = $true)]
        $Project
    )

	$packagesConfigNuGetProject = Get-PackagesConfigNuGetProject $Project
	$packageReferences = @()

	if ($packagesConfigNuGetProject)
	{   
		$packageReferences = Get-InstalledPackageReferencesFromProject($packagesConfigNuGetProject)
	}
    return $packageReferences
}

function Get-ProjectPackage {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true)]
        [string]$Id,
        [string]$Version
    )
    
    $packagesConfigNuGetProject = Get-PackagesConfigNuGetProject $Project
        
    # We can't call the nuget methods since powershell gets confused with overload resolution
    $packages = (Get-InstalledPackageReferencesFromProject $packagesConfigNuGetProject) | ?{ $_.PackageIdentity.Id -eq $Id }    
    
    if($Version) {
        $actualVersion = [NuGet.Versioning.NuGetVersion]::Parse($Version)
        $packages = $packages | ?{ $_.PackageIdentity.Version -eq $actualVersion }
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

    $configName = Get-PackageConfigName $Project
	
	# Check for existence on disk of packages.config
	if($configName -eq "packages.config") {Assert-PathExists (Join-Path (Get-ProjectDir $Project) "packages.config")}

    # Check for the project item
    Assert-NotNull (Get-ProjectItem $Project $configName) "$configName does not exist in $projectName"
        
    $packagesConfigNuGetProject = Get-PackagesConfigNuGetProject $Project
    
    Assert-NotNull $packagesConfigNuGetProject "Unable to find the project repository"
    
    $packagesInPackagesConfig = Get-InstalledPackageReferencesFromProject $packagesConfigNuGetProject
    if($Version) {
        $actualVersion = [NuGet.Versioning.NuGetVersion]::Parse($Version)
        $packageIdentity = New-Object NuGet.Packaging.Core.PackageIdentity($Id, $actualVersion)
        Assert-NotNull ($packagesInPackagesConfig | where { $_.PackageIdentity.Equals($packageIdentity) }) "Package $Id $Version is not referenced in $($Project.Name)"
    }
    else
    {
        Assert-NotNull ($packagesInPackagesConfig | where { $_.PackageIdentity.Id -eq $Id }) "Package $Id is not referenced in $($Project.Name)"
    }    
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
    
    $packagesConfigNuGetProject = Get-PackagesConfigNuGetProject $Project
    if (!$packagesConfigNuGetProject) 
    {
        return
    }
    
    if($Version) {
        $actualVersion = [NuGet.Versioning.NuGetVersion]::Parse($Version)
        $packageIdentity = New-Object NuGet.Packaging.Core.PackageIdentity($Id, $actualVersion)
        Assert-Null ((Get-InstalledPackageReferencesFromProject $packagesConfigNuGetProject) | where { $_.PackageIdentity.Equals($packageIdentity) }) "Package $Id $Version is not referenced in $($Project.Name)"
    }
    else
    {
        Assert-Null ((Get-InstalledPackageReferencesFromProject $packagesConfigNuGetProject | where { $_.PackageIdentity.Id -eq $Id })) "Package $Id is not referenced in $($Project.Name)"
    }
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