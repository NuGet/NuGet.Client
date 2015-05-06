function Test-PackageManagerServicesAreAvailableThroughMEF {
    # Arrange
    $cm = Get-VsComponentModel

    # Act
    $installer = $cm.GetService([NuGet.VisualStudio.IVsPackageInstaller])
    $installerServices = $cm.GetService([NuGet.VisualStudio.IVsPackageInstallerServices])
    $installerEvents = $cm.GetService([NuGet.VisualStudio.IVsPackageInstallerEvents])

    # Assert
    Assert-NotNull $installer
    Assert-NotNull $installerServices
    Assert-NotNull $installerEvents
}

function Test-VsPackageInstallerServices {
    param(
        $context
    )

    # Arrange
    $p = New-WebApplication
    $cm = Get-VsComponentModel
    $installerServices = $cm.GetService([NuGet.VisualStudio.IVsPackageInstallerServices])
    
    # Act
    $p | Install-Package jquery -Version 1.5 -Source $context.RepositoryPath
    $packages = @($installerServices.GetInstalledPackages())

    # Assert
    Assert-NotNull $packages
    Assert-AreEqual 1 $packages.Count
    Assert-AreEqual jQuery $packages[0].Id
}

function Test-GetInstalledPackagesMultipleProjectsSameVersion {
    param($context)

    # Arrange
    $p = New-WebApplication
    $p | Install-Package jquery -Version 1.5 -Source $context.RepositoryPath
    $p = New-WebApplication
    $p | Install-Package jquery -Version 1.5 -Source $context.RepositoryPath

    $cm = Get-VsComponentModel
    $installerServices = $cm.GetService([NuGet.VisualStudio.IVsPackageInstallerServices])
	
    # Act
    $packages = @($installerServices.GetInstalledPackages())

    # Assert
    Assert-NotNull $packages
    Assert-AreEqual 1 $packages.Count
    Assert-AreEqual jQuery $packages[0].Id
    Assert-AreEqual 1.5 $packages[0].VersionString
}

function Test-GetInstalledPackagesMultipleProjectsDifferentVersion {
    param($context)

    # Arrange
    $p = New-WebApplication
    $p | Install-Package jquery -Version 1.5 -Source $context.RepositoryPath
    $p = New-WebApplication
    $p | Install-Package jquery -Version 1.6 -Source $context.RepositoryPath

    $cm = Get-VsComponentModel
    $installerServices = $cm.GetService([NuGet.VisualStudio.IVsPackageInstallerServices])
	
    # Act
    $packages = @($installerServices.GetInstalledPackages())

    # Assert
    Assert-NotNull $packages
    Assert-AreEqual 2 $packages.Count
    Assert-AreEqual jQuery $packages[0].Id
    Assert-AreEqual 1.5 $packages[0].VersionString
    Assert-AreEqual jQuery $packages[1].Id
    Assert-AreEqual 1.6 $packages[1].VersionString
}
<#
function Test-GetInstalledPackagesMVCTemplate
{
    param($context)

    # Arrange
    $p = New-MvcWebSite
	
    $cm = Get-VsComponentModel
    $installerServices = $cm.GetService([NuGet.VisualStudio.IVsPackageInstallerServices])

    # Act
    $packages = @($installerServices.GetInstalledPackages())

    # Assert
	Write-Host $packages

    Assert-AreEqual 30 $packages.Count
    Assert-AreEqual Microsoft.Web.Infrastructure $packages[0].Id
    Assert-AreEqual 1.0.0.0 $packages[0].VersionString
    Assert-AreEqual jQuery $packages[1].Id
    Assert-AreEqual 1.10.2 $packages[1].VersionString
    Assert-AreEqual Respond $packages[2].Id
    Assert-AreEqual 1.2.0 $packages[2].VersionString
    Assert-AreEqual Modernizr $packages[3].Id
    Assert-AreEqual 2.6.2 $packages[3].VersionString
    Assert-AreEqual bootstrap $packages[4].Id
    Assert-AreEqual 3.0.0 $packages[4].VersionString
    Assert-AreEqual WebGrease $packages[5].Id
    Assert-AreEqual 1.5.2 $packages[5].VersionString
    Assert-AreEqual Antlr $packages[6].Id
    Assert-AreEqual 3.4.1.9004 $packages[6].VersionString
    Assert-AreEqual Newtonsoft.Json $packages[7].Id
    Assert-AreEqual 6.0.4 $packages[7].VersionString
    Assert-AreEqual Microsoft.AspNet.Web.Optimization $packages[8].Id
    Assert-AreEqual 1.1.3 $packages[8].VersionString
    Assert-AreEqual AspNet.ScriptManager.bootstrap $packages[9].Id
    Assert-AreEqual 3.0.0 $packages[9].VersionString
    Assert-AreEqual AspNet.ScriptManager.jQuery $packages[10].Id
    Assert-AreEqual 1.10.2 $packages[10].VersionString
    Assert-AreEqual Microsoft.AspNet.ScriptManager.MSAjax $packages[11].Id
    Assert-AreEqual 5.0.0 $packages[11].VersionString
    Assert-AreEqual Microsoft.AspNet.ScriptManager.WebForms $packages[12].Id
    Assert-AreEqual 5.0.0 $packages[12].VersionString
    Assert-AreEqual Microsoft.AspNet.Web.Optimization.WebForms $packages[13].Id
    Assert-AreEqual 1.1.3 $packages[13].VersionString
    Assert-AreEqual Microsoft.AspNet.FriendlyUrls.Core $packages[14].Id
    Assert-AreEqual 1.0.2 $packages[14].VersionString
    Assert-AreEqual EntityFramework $packages[15].Id
    Assert-AreEqual 6.1.2 $packages[15].VersionString
    Assert-AreEqual Microsoft.AspNet.Identity.Core $packages[16].Id
    Assert-AreEqual 2.2.0 $packages[16].VersionString
    Assert-AreEqual Microsoft.AspNet.Identity.EntityFramework $packages[17].Id
    Assert-AreEqual 2.2.0 $packages[17].VersionString
    Assert-AreEqual Microsoft.AspNet.Identity.Owin $packages[18].Id
    Assert-AreEqual 2.2.0 $packages[18].VersionString
    Assert-AreEqual Microsoft.Owin $packages[19].Id
    Assert-AreEqual 3.0.1 $packages[19].VersionString
    Assert-AreEqual Microsoft.Owin.Host.SystemWeb $packages[20].Id
    Assert-AreEqual 3.0.1 $packages[20].VersionString
    Assert-AreEqual Microsoft.Owin.Security $packages[21].Id
    Assert-AreEqual 3.0.1 $packages[21].VersionString
    Assert-AreEqual Microsoft.Owin.Security.Facebook $packages[22].Id
    Assert-AreEqual 3.0.1 $packages[22].VersionString
    Assert-AreEqual Microsoft.Owin.Security.Cookies $packages[23].Id
    Assert-AreEqual 3.0.1 $packages[23].VersionString
    Assert-AreEqual Microsoft.Owin.Security.Google $packages[24].Id
    Assert-AreEqual 3.0.1 $packages[24].VersionString
    Assert-AreEqual Microsoft.Owin.Security.Twitter $packages[25].Id
    Assert-AreEqual 3.0.1 $packages[25].VersionString
    Assert-AreEqual Microsoft.Owin.Security.MicrosoftAccount $packages[26].Id
    Assert-AreEqual 3.0.1 $packages[26].VersionString
    Assert-AreEqual Owin $packages[27].Id
    Assert-AreEqual 1.0 $packages[27].VersionString
    Assert-AreEqual Microsoft.AspNet.Providers.Core $packages[28].Id
    Assert-AreEqual 2.0.0 $packages[28].VersionString
    Assert-AreEqual Microsoft.Owin.Security.OAuth $packages[29].Id
    Assert-AreEqual 3.0.1 $packages[29].VersionString
}
#>
function Test-VsPackageInstallerEvents {
    param(
        $context
    )

    try {
        # Arrange
        $p = New-WebApplication
        $cm = Get-VsComponentModel
        $installerEvents = $cm.GetService([NuGet.VisualStudio.IVsPackageInstallerEvents])
    
        $global:installing = 0
        $global:installed = 0
        $global:uninstalling = 0
        $global:uninstalled = 0

        $installingHandler = {
            $global:installing++
        }

        $installerEvents.add_PackageInstalling($installingHandler)

        $installedHandler = {
            $global:installed++
        }

        $installerEvents.add_PackageInstalled($installedHandler)

        $uninstallingHandler = {
            $global:uninstalling++
        }

        $installerEvents.add_PackageUninstalling($uninstallingHandler)

        $uninstalledHandler = {
            $global:uninstalled++
        }

        $installerEvents.add_PackageUninstalled($uninstalledHandler)

        # Act
        $p | Install-Package jquery -Version 1.5 -Source $context.RepositoryPath
        $p | Uninstall-Package jquery

        # Assert
        Assert-AreEqual 1 $global:installing
        Assert-AreEqual 1 $global:installed
        Assert-AreEqual 1 $global:uninstalling
        Assert-AreEqual 1 $global:uninstalled
    }
    finally {
        $installerEvents.remove_PackageInstalling($installingHandler)
        $installerEvents.remove_PackageInstalled($installedHandler)
        $installerEvents.remove_PackageUninstalling($uninstallingHandler)
        $installerEvents.remove_PackageUninstalled($uninstalledHandler)

		Remove-Variable "installing"   -scope global
		Remove-Variable "installed"    -scope global
		Remove-Variable "uninstalling" -scope global
		Remove-Variable "uninstalled"  -scope global
    }
}


