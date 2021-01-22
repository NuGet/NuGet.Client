# Ensure all packages are created before publishing

[CmdletBinding(SupportsShouldProcess=$True)]
Param (
    [string]$NupkgOutputPath
)

# The list of projects need to be packed.
$ProjectsShouldPack = @(
"NuGet.CommandLine",
"NuGet.Indexing",
"NuGet.SolutionRestoreManager.Interop",
"NuGet.VisualStudio.Contracts",
"NuGet.VisualStudio",
"Microsoft.Build.NuGetSdkResolver",
"NuGet.Build.Tasks.Console",
"NuGet.Build.Tasks.Pack",
"NuGet.Build.Tasks",
"NuGet.CommandLine.XPlat",
"NuGet.Commands",
"NuGet.Common",
"NuGet.Configuration",
"NuGet.Credentials",
"NuGet.DependencyResolver.Core",
"NuGet.Frameworks",
"NuGet.LibraryModel",
"NuGet.PackageManagement",
"NuGet.Packaging.Core",
"NuGet.Packaging.Extraction",
"NuGet.Packaging",
"NuGet.ProjectModel",
"NuGet.Protocol",
"NuGet.Resolver",
"NuGet.Versioning")


$ErrorMessage = ""
$ErrorCount = 0

ForEach ($ProjectShouldPack in $ProjectsShouldPack) {

    $ProjectShouldPack = $ProjectShouldPack.trim()

    $PackagesName = Get-ChildItem $NupkgOutputPath -Filter *.nupkg -Name

    $FoundNupkg = $false

    Foreach ($PackageName in $PackagesName) {

        if ( ($PackageName -match "${ProjectShouldPack}.[0-9][0-9a-zA-Z.-]*.nupkg") -and -not($PackageName -match "${ProjectShouldPack}.[0-9][0-9a-zA-Z.-]*.symbols.nupkg") ) {
            $FoundNupkg = $true
            Write-Host "$ProjectShouldPack is packed as : $PackageName" -ForegroundColor Cyan
            break
        }
    }

    if (-not($FoundNupkg))
    {
        $ErrorMessage = $ErrorMessage + "$ProjectShouldPack "
        $ErrorCount = $ErrorCount + 1
    }

}

If ($ErrorCount -ne 0)
{
    $ErrorMessage = "The following project(s) are not packed in $NupkgOutputPath : " + $ErrorMessage
    Write-Error "[FATAL] $ErrorMessage"
    Exit 1
}
else
{
    Write-Host "All projects are packed in : $NupkgOutputPath" -ForegroundColor Cyan
    Exit 0
}
