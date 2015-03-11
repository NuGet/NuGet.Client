function Test-FindPackageByIdjQuery {
    # Act
    $packages = Find-Package jQuery
    
    # Assert
    Assert-True $packages.Count -gt 0 "Find-Package cmdlet does not returns any package"
}

function Test-FindPackageByIdMVC {
    # Act
    $packages = Find-Package microsoft.aspnet.mvc
    
    # Assert
    Assert-True $packages.Count -gt 0 "Find-Package cmdlet does not returns any package"
}

function Test-FindPackageByIdaspnet {
    # Act
    $packages = Find-Package aspnet
    
    # Assert
    Assert-True $packages.Count -gt 0 "Find-Package cmdlet does not returns any package"
}

function Test-FindPackageByIdWildcard {
    # Act
    $packages = Find-Package *aspnet*
    
    # Assert
	Assert-NotNull $packages
    Assert-True $packages.Count -gt 0 "Find-Package cmdlet does not returns any package"
}

function Test-FindPackageByIdAndVersion {
    # Act
    $packages = Find-Package entityframework -version 6.1.2
    $version = [NuGet.Versioning.NuGetVersion]::Parse("6.1.2")
    
    # Assert
	Assert-True $packages[0].Versions[0] -eq $version
    Assert-True $packages[0].Id -eq "EntityFramework"
}

function Test-FindPackageByIdAndPrereleaseVersion {
    # Act 1
    $packages = Find-Package TestPackage.AlwaysPrerelease
    
    # Assert 1
	Assert-True $packages.Count -eq 0

	# Act 2
    $packages = Find-Package TestPackage.AlwaysPrerelease -Pre
    
    # Assert 2
	Assert-True $packages[0].Count -ne 0
    Assert-True $packages[0].Id -eq TestPackage.AlwaysPrerelease
	Assert-True $packages[0].Versions[0].ToString() -eq "5.0.0-beta"
}

function Test-FindPackageByIdExactMatch {
    # Act 
    $packages = Find-Package TestPackage.OverwriteTest -ExactMatch
    
    # Assert 
	Assert-True $packages[0].Count -eq 1
    Assert-True $packages[0].Id -eq TestPackage.OverwriteTest
	Assert-True $packages[0].Versions[0].ToString() -eq "1.0.0"
}

function Test-FindPackageByIdWithAllVersions {
    # Act 
    $packages = Find-Package elmah.io -allversions
    
    # Assert 
	Assert-True $packages[0].Count -gt 1
    Assert-True $packages[0].Id -eq elmah.io
	Assert-True $packages[0].Versions.Count -gt 4
}

function Test-FindPackageByIdWithFirstAndSkip {
    # Act 1
    $packages = Find-Package elmah -First 5
    
    # Assert 1
	Assert-True $packages[0].Count -eq 5

	# Act 2
    $packages = Find-Package elmah -Skip 15
    
    # Assert 2
	Assert-True $packages[0].Count -gt 0 And $packages[0].Count -lt 5

	# Act 3
    $packages = Find-Package elmah -First 5 -Skip 5
    
    # Assert 3
	Assert-True $packages[0].Count -gt 0 And $packages[0].Count -lt 10
}