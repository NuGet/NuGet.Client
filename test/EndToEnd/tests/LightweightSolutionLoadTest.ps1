function Test-DeferredUnitTestProjectGetInstalledPackage {
    [SkipTestForVS14()]
    param()

    $projectPC = New-ClassLibrary
    $projectPJ = New-Project BuildIntegratedClassLibrary
    $projectPR = New-Project PackageReferenceClassLibrary

    $projectT = New-Project UnitTestProject

    Add-ProjectReference $projectT $projectPC
    Add-ProjectReference $projectT $projectPJ
    Add-ProjectReference $projectT $projectPR

    # Build to restore
    Build-Solution

    $projectT = $projectT | Select-Object UniqueName, ProjectName

    Enable-LightweightSolutionLoad -Reload

    # Act
    $packageIds = Get-InstalledPackage | Select-Object -ExpandProperty Id

    Assert-True ($packageIds -contains 'MSTest.TestAdapter') -Message 'Test extension package is not found'
    Assert-True ($projectT | Test-Project -IsDeferred) -Message 'Test project should stay in deferred mode'
}

function Test-DeferredPackagesConfigProjectInstallPackage {
    [SkipTestForVS14()]
    param()

    $projectT = New-ClassLibrary | Select-Object UniqueName, ProjectName

    Enable-LightweightSolutionLoad -Reload

    # Act
    $projectT | Install-Package NuGet.Versioning -Version 1.0.7

    Assert-False ($projectT | Test-Project -IsDeferred) -Message 'Test project should not stay in deferred mode'
    Assert-True ($projectT | Test-InstalledPackage -Id NuGet.Versioning -Version 1.0.7) -Message 'Test package should be installed'
}

function Test-DeferredPackagesConfigProjectUninstallPackage {
    [SkipTestForVS14()]
    param()

    $projectT = New-ClassLibrary | Select-Object UniqueName, ProjectName
    $projectT | Install-Package NuGet.Versioning -Version 1.0.7

    Assert-True ($projectT | Test-InstalledPackage -Id NuGet.Versioning -Version 1.0.7) -Message 'Test package should be installed'

    Enable-LightweightSolutionLoad -Reload

    # Act
    $projectT | Uninstall-Package NuGet.Versioning -Version 1.0.7

    Assert-False ($projectT | Test-Project -IsDeferred) -Message 'Test project should not stay in deferred mode'
    Assert-False ($projectT | Test-InstalledPackage -Id NuGet.Versioning) -Message 'Test package should be uninstalled'
}

function Test-DeferredPackagesConfigProjectUpdatePackage {
    [SkipTestForVS14()]
    param(
        $context
    )

    $projectT = New-ClassLibrary | Select-Object UniqueName, ProjectName
    $projectT | Install-Package TestUpdatePackage -Source $context.RepositoryRoot -Version 1.0.0.0

    Assert-True ($projectT | Test-InstalledPackage -Id TestUpdatePackage -Version 1.0.0.0) -Message 'Test package should be installed'

    Enable-LightweightSolutionLoad -Reload

    # Act
    $projectT | Update-Package TestUpdatePackage -Source $context.RepositoryRoot

    Assert-False ($projectT | Test-Project -IsDeferred) -Message 'Test project should not stay in deferred mode'
    Assert-True ($projectT | Test-InstalledPackage -Id TestUpdatePackage -Version 2.0.0.0) -Message 'Test package should be updated'
}

function Test-DeferredPackageReferenceProjectInstallPackage {
    [SkipTestForVS14()]
    param()

    $projectT = New-Project PackageReferenceClassLibrary | Select-Object UniqueName, ProjectName

    Enable-LightweightSolutionLoad -Reload

    # Act
    $projectT | Install-Package NuGet.Versioning -Version 1.0.7

    Assert-False ($projectT | Test-Project -IsDeferred) -Message 'Test project should not stay in deferred mode'
    Assert-True ($projectT | Test-InstalledPackage -Id NuGet.Versioning -Version 1.0.7) -Message 'Test package should be installed'
}

function Test-DeferredPackageReferenceProjectUninstallPackage {
    [SkipTestForVS14()]
    param()

    $projectT = New-Project PackageReferenceClassLibrary | Select-Object UniqueName, ProjectName
    $projectT | Install-Package NuGet.Versioning -Version 1.0.7

    Assert-True ($projectT | Test-InstalledPackage -Id NuGet.Versioning -Version 1.0.7) -Message 'Test package should be installed'

    Enable-LightweightSolutionLoad -Reload

    # Act
    $projectT | Uninstall-Package NuGet.Versioning -Version 1.0.7

    Assert-False ($projectT | Test-Project -IsDeferred) -Message 'Test project should not stay in deferred mode'
    Assert-False ($projectT | Test-InstalledPackage -Id NuGet.Versioning) -Message 'Test package should be uninstalled'
}

function Test-DeferredPackageReferenceProjectUpdatePackage {
    [SkipTestForVS14()]
    param(
        $context
    )

    $projectT = New-Project PackageReferenceClassLibrary | Select-Object UniqueName, ProjectName
    $projectT | Install-Package TestUpdatePackage -Source $context.RepositoryRoot -Version 1.0.0.0

    Assert-True ($projectT | Test-InstalledPackage -Id TestUpdatePackage -Version 1.0.0.0) -Message 'Test package should be installed'

    Enable-LightweightSolutionLoad -Reload

    # Act
    $projectT | Update-Package TestUpdatePackage -Source $context.RepositoryRoot

    Assert-False ($projectT | Test-Project -IsDeferred) -Message 'Test project should not stay in deferred mode'
    Assert-True ($projectT | Test-InstalledPackage -Id TestUpdatePackage -Version 2.0.0.0) -Message 'Test package should be updated'
}

function Test-DeferredProjectJsonProjectInstallPackage {
    [SkipTestForVS14()]
    param()

    $projectT = New-Project BuildIntegratedClassLibrary | Select-Object UniqueName, ProjectName

    Enable-LightweightSolutionLoad -Reload

    # Act
    $projectT | Install-Package NuGet.Versioning -Version 1.0.7

    Assert-True ($projectT | Test-Project -IsDeferred) -Message 'Test project should stay in deferred mode'
    Assert-True ($projectT | Test-InstalledPackage -Id NuGet.Versioning) -Message 'Test package should be installed'
}

function Test-DeferredProjectJsonProjectUninstallPackage {
    [SkipTestForVS14()]
    param()

    $projectT = New-Project BuildIntegratedClassLibrary | Select-Object UniqueName, ProjectName
    $projectT | Install-Package NuGet.Versioning -Version 1.0.7

    Assert-True ($projectT | Test-InstalledPackage -Id NuGet.Versioning -Version 1.0.7) -Message 'Test package should be installed'

    Enable-LightweightSolutionLoad -Reload

    # Act
    $projectT | Uninstall-Package NuGet.Versioning -Version 1.0.7

    Assert-True ($projectT | Test-Project -IsDeferred) -Message 'Test project should stay in deferred mode'
    Assert-False ($projectT | Test-InstalledPackage -Id NuGet.Versioning) -Message 'Test package should be uninstalled'
}

function Test-DeferredProjectJsonProjectUpdatePackage {
    [SkipTestForVS14()]
    param(
        $context
    )

    $projectT = New-Project BuildIntegratedClassLibrary | Select-Object UniqueName, ProjectName
    $projectT | Install-Package TestUpdatePackage -Source $context.RepositoryRoot -Version 1.0.0.0

    Assert-True ($projectT | Test-InstalledPackage -Id TestUpdatePackage -Version 1.0.0.0) -Message 'Test package should be installed'

    Enable-LightweightSolutionLoad -Reload

    # Act
    $projectT | Update-Package TestUpdatePackage -Source $context.RepositoryRoot

    Assert-False ($projectT | Test-Project -IsDeferred) -Message 'Test project should not stay in deferred mode'
    Assert-True ($projectT | Test-InstalledPackage -Id TestUpdatePackage -Version 2.0.0.0) -Message 'Test package should be updated'
}

function Test-DeferredNativeProjectInstallPackage {
    [SkipTestForVS14()]
    param()

    $projectT = New-Project NativeConsoleApplication | Select-Object UniqueName, ProjectName

    Enable-LightweightSolutionLoad -Reload

    # Act
    $projectT | Install-Package zlib -IgnoreDependencies

    Assert-False ($projectT | Test-Project -IsDeferred) -Message 'Test project should not stay in deferred mode'
    Assert-True ($projectT | Test-InstalledPackage -Id zlib) -Message 'Test package should be installed'
}

function Test-DeferredNativeProjectUninstallPackage {
    [SkipTestForVS14()]
    param()

    $projectT = New-Project NativeConsoleApplication | Select-Object UniqueName, ProjectName
    $projectT | Install-Package zlib

    Assert-True ($projectT | Test-InstalledPackage -Id zlib) -Message 'Test package should be installed'

    Enable-LightweightSolutionLoad -Reload

    # Act
    $projectT | Uninstall-Package zlib

    Assert-False ($projectT | Test-Project -IsDeferred) -Message 'Test project should not stay in deferred mode'
    Assert-False ($projectT | Test-InstalledPackage -Id zlib) -Message 'Test package should be uninstalled'
}

function Test-DeferredProjectInvokeInitScript {
    [SkipTestForVS14()]
    param($Context, $TestCase)

    if (Test-Path function:\Get-WorldB) {
        Remove-Item function:\Get-WorldB
    }

    $projectT = New-Project $TestCase.ProjectTemplate | Select-Object UniqueName, ProjectName

    Enable-LightweightSolutionLoad -Reload

    # Act
    $projectT | Install-Package PackageWithScriptsB -Source $Context.RepositoryRoot

    Assert-True ($projectT | Test-InstalledPackage -Id PackageWithScriptsB) -Message 'Test package should be installed'

    # This asserts init.ps1 gets called
    Assert-True (Test-Path function:\Get-WorldB) -Message 'Test package function should be imported by init.ps1'
}

function TestCases-DeferredProjectInvokeInitScript {
    BuildProjectTemplateTestCases 'ConsoleApplication', 'PackageReferenceClassLibrary', 'BuildIntegratedClassLibrary'
}

function BuildProjectTemplateTestCases([string[]]$ProjectTemplates) {
    $ProjectTemplates | ForEach-Object{
        $testCase = New-Object System.Object
        $testCase | Add-Member -Type NoteProperty -Name ProjectTemplate -Value $_
        $testCase
    }
}

function Test-DeferredProjectGetPackage {
    [SkipTestForVS14()]
    param($Context, $TestCase)

    $projectT = New-Project $TestCase.ProjectTemplate | Select-Object UniqueName, ProjectName
    $projectT | Install-Package NuGet.Versioning -Version 1.0.7

    Assert-True ($projectT | Test-InstalledPackage -Id NuGet.Versioning -Version 1.0.7) -Message 'Test package should be installed'

    Enable-LightweightSolutionLoad -Reload

    # Act
    $packageIds = $projectT | Get-Package | Select-Object -ExpandProperty Id

    Assert-True ($projectT | Test-Project -IsDeferred) -Message 'Test project should stay in deferred mode'
    Assert-True ($packageIds -contains 'NuGet.Versioning') -Message 'Test package should be installed'
}

function TestCases-DeferredProjectGetPackage{
    BuildProjectTemplateTestCases 'ClassLibrary', 'PackageReferenceClassLibrary', 'BuildIntegratedClassLibrary'
}

function Test-DeferredPackageReferenceProjectGetPackageTransitive {
    [SkipTestForVS14()]
    param($Context, $TestCase)

    $projectR = New-Project $TestCase.ProjectTemplate
    $projectT = New-Project PackageReferenceClassLibrary

    $projectT | Add-ProjectReference -ProjectTo $projectR
    $projectR | Install-Package NuGet.Versioning -Version 1.0.7
    Clean-Solution

    $projectT = $projectT | Select-Object UniqueName, ProjectName, FullName

    Enable-LightweightSolutionLoad -Reload

    # Act (Restore)
    Build-Solution

    Assert-True ($projectT | Test-Project -IsDeferred) -Message 'Test project should stay in deferred mode'
    Assert-NetCorePackageInLockFile $projectT NuGet.Versioning 1.0.7
}

function TestCases-DeferredPackageReferenceProjectGetPackageTransitive{
    BuildProjectTemplateTestCases 'ClassLibrary' , 'PackageReferenceClassLibrary', 'BuildIntegratedClassLibrary'
}

function Test-DeferredBuildIntegratedProjectGetPackageTransitive {
    [SkipTestForVS14()]
    param($Context, $TestCase)

    $projectR = New-Project $TestCase.ProjectTemplate
    $projectT = New-Project BuildIntegratedClassLibrary

    $projectT | Add-ProjectReference -ProjectTo $projectR
    $projectR | Install-Package NuGet.Versioning -Version 1.0.7
    Clean-Solution

    $projectT = $projectT | Select-Object UniqueName, ProjectName, FullName

    Enable-LightweightSolutionLoad -Reload

    # Act (Restore)
    Build-Solution

    Assert-True ($projectT | Test-Project -IsDeferred) -Message 'Test project should stay in deferred mode'
    Assert-ProjectJsonLockFilePackage $projectT NuGet.Versioning 1.0.7
}

function TestCases-DeferredBuildIntegratedProjectGetPackageTransitive{
    BuildProjectTemplateTestCases 'PackageReferenceClassLibrary', 'BuildIntegratedClassLibrary'
}

function Test-DeferredProjectClean {

    [SkipTestForVS14()]
    param(
        $context
    )

    $project = New-Project BuildIntegratedClassLibrary Project1
    $project | Install-Package NuGet.Versioning -Version 1.0.7

    Build-Solution

    # Act
    $cacheFile = Get-CacheFilePathFromProjectPath $project.FullName

    Assert-PathExists $cacheFile

    Enable-LightweightSolutionLoad -Reload

    #Act
    Clean-Solution

    #Assert
    Assert-PathNotExists $cacheFile
}