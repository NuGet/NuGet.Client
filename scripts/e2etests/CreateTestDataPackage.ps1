<#
.SYNOPSIS
Creates a package containing NuGet client EndToEnd test data.

.PARAMETER configuration
The build configuration.  The default value is 'debug'.

.PARAMETER toolsetVersion
The toolset version.  The default value is 15.

.PARAMETER outputDirectoryPath
The output directory where the test data package will be created.  The default value is the current directory.

.EXAMPLE
.\CreateTestDataPackage.ps1 -configuration 'debug' -toolsetVersion 15 -outputDirectoryPath 'C:\git\NuGet.Client\artifacts\nupkgs'
#>

[CmdletBinding()]
param (
    [ValidateSet('debug', 'release')]
    [string] $configuration = 'debug',
    [ValidateSet(15, 16)]
    [int] $toolsetVersion = 15,
    [string] $outputDirectoryPath = $PWD
)

Function Get-DirectoryPath([string[]] $pathParts)
{
    Return [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($pathParts))
}

Set-Variable repositoryRootDirectoryPath -Option Constant -Value $(Get-DirectoryPath($PSScriptRoot, '..', '..'))

Function Get-Directory([string[]] $pathParts)
{
    $directoryPath = Get-DirectoryPath($pathParts)

    Return [System.IO.DirectoryInfo]::new($directoryPath)
}

Function Get-File([string[]] $pathParts)
{
    $filePath = [System.IO.Path]::Combine($pathParts)

    $file = [System.IO.FileInfo]::new($filePath)

    If (-Not $file.Exists)
    {
        throw [System.IO.FileNotFoundException]::new("Could not find $($file.Name) at $($file.FullName).  Please build first.", $file.FullName)
    }

    Return $file
}

Function Get-GenerateTestPackagesFile()
{
    Return Get-File($repositoryRootDirectoryPath, 'artifacts', 'GenerateTestPackages', "$toolsetVersion.0", 'bin', $configuration, 'net472', 'GenerateTestPackages.exe')
}

Function Get-NuGetFile()
{
    Return Get-File($repositoryRootDirectoryPath, 'artifacts', 'VS15', 'nuget.exe')
}

Function Create-TestPackages()
{
    $generateTestPackagesFile = Get-GenerateTestPackagesFile
    $testPackagesDirectory = Get-Directory($repositoryRootDirectoryPath, 'test', 'EndToEnd', 'Packages')

    $testDirectoryPaths = [System.IO.Directory]::GetDirectories($testPackagesDirectory.FullName)

    $testDirectoryPaths | %{
        $testDirectoryPath = $_
        $packagesDirectory = Get-Directory($testDirectoryPath, 'Packages')
        $assembliesDirectory = Get-Directory($testDirectoryPath, 'Assemblies')
        $recursive = $True

        If ($packagesDirectory.Exists)
        {
            $packagesDirectory.Delete($recursive)
        }

        If ($assembliesDirectory.Exists)
        {
            $assembliesDirectory.Delete($recursive)
        }

        Get-ChildItem $testDirectoryPath\* -Include *.dgml,*.nuspec | %{
            Write-Host "Running $($generateTestPackagesFile.Name) on $($_.FullName)...  " -NoNewLine

            $process = Start-Process `
                -FilePath $generateTestPackagesFile.FullName `
                -WorkingDirectory $testDirectoryPath `
                -WindowStyle Hidden `
                -PassThru `
                -Wait `
                -ArgumentList $_.FullName

            If ($process.ExitCode -eq 0)
            {
                Write-Host 'Success.'
            }
            else
            {
                Write-Error "Failed.  Exit code is $($process.ExitCode)."
            }
        }

        If ($assembliesDirectory.Exists)
        {
            $assembliesDirectory.Delete($recursive)
        }
    }
}

Function Create-TestDataPackage()
{
    $nuspecFile = Get-File($repositoryRootDirectoryPath, 'test', 'EndToEnd', 'NuGet.Client.EndToEnd.TestData.nuspec')
    $outputDirectoryPath = [System.IO.Path]::Combine($repositoryRootDirectoryPath, 'artifacts', 'nupkgs')
    $nupkgFilePath = [System.IO.Path]::Combine($outputDirectoryPath, 'NuGet.Client.EndToEnd.TestData.nupkg')
    $nugetFile = Get-NuGetFile

    $process = Start-Process `
        -FilePath $nugetFile.FullName `
        -WorkingDirectory $nuspecFile.DirectoryName `
        -WindowStyle Hidden `
        -PassThru `
        -Wait `
        -ArgumentList 'pack', $nuspecFile.FullName, "-OutputDirectory `"$outputDirectoryPath`"", '-NoDefaultExcludes', '-NonInteractive'

    If ($process.ExitCode -eq 0)
    {
        Write-Host "Created test data package $nupkgFilePath."
    }
    else
    {
        Write-Error "$($nugetFile.Name) failed.  Exit code is $($process.ExitCode)."
    }
}

Create-TestPackages
Create-TestDataPackage