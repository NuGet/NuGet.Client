function Test-OpenPackagePageOpenProjectUrlByDefault {
    param(
        $context
    )

    # Act
    $p = Open-PackagePage 'OpenPackagePageTestPackage' -Source $context.RepositoryRoot -WhatIf -PassThru

    # Assert
    Assert-AreEqual 'http://codeplex.com' $p.OriginalString
}

function Test-OpenPackagePageOpenLicenseUrlIfLicenseParameterIsSet {
    param(
        $context
    )

    # Act
    $p = Open-PackagePage 'OpenPackagePageTestPackage' -Source $context.RepositoryRoot -License -WhatIf -PassThru

    # Assert
    Assert-AreEqual 'http://bing.com' $p.OriginalString
}

function Test-OpenPackagePageOpenReportAbuseUrlIfReportAbuseParameterIsSet {
    # Act
    $p = Open-PackagePage elmah -Report -WhatIf -PassThru -Version 1.1

	if ($SourceNuGet.Contains('api/v2'))
    {
		$expectedString = $SourceNuGet.Replace('/api/v2', '') + '/Package/ReportAbuse/elmah/1.1.0'
	}
	else 
	{
		$expectedString = 'https://www.nuget.org/Package/ReportAbuse/elmah/1.1.0'
	}
    
    # Assert
    Assert-AreEqual $expectedString $p.OriginalString
}

function Test-OpenPackagePageFailsIfIdIsSetToTheWrongValue {
    param(
        $context
    )

    # Act & Assert

    Assert-Throws { 
        Open-PackagePage 'OpenPackagePageTestPackage_Wrong' -Source $context.RepositoryRoot
    } "Package with the Id 'OpenPackagePageTestPackage_Wrong' is not found in the specified source."
}

function Test-OpenPackagePageFailsIfVersionIsSetToTheWrongValue {
    param(
        $context
    )

    # Act & Assert

    Assert-Throws { 
        Open-PackagePage 'OpenPackagePageTestPackage' -Version 4.2 -Source $context.RepositoryRoot
    } "Package with the Id 'OpenPackagePageTestPackage' and version '4.2' is not found in the specified source."
}

function Test-OpenPackagePageFailsIfReportUrlIsNotAvailable {
    param(
        $context
    )

    # Act & Assert

    Assert-Throws { 
        Open-PackagePage 'OpenPackagePageTestPackage' -Report -Source $context.RepositoryRoot
    } "The package 'OpenPackagePageTestPackage 1.0' does not provide the requested URL."
}

function Test-OpenPackagePageFailsIfProjectUrlIsNotAvailable {
    param(
        $context
    )

    # Act & Assert

    Assert-Throws { 
        Open-PackagePage 'PackageWithGacReferences' -Source $context.RepositoryRoot
    } "The package 'PackageWithGacReferences 1.0' does not provide the requested URL."
}

function Test-OpenPackagePageFailsIfLicenseUrlIsNotAvailable {
    param(
        $context
    )

    # Act & Assert

    Assert-Throws { 
        Open-PackagePage 'PackageWithGacReferences' -License -Source $context.RepositoryRoot
    } "The package 'PackageWithGacReferences 1.0' does not provide the requested URL."
}

function Test-OpenPackagePageAcceptSourceName {
	if ($SourceNuGet -eq 'nuget.org')
	{
		$source = 'nUGet.OrG'  # keep the coverage that the source name is case insensitive
	}
	else 
	{
	   $source = $SourceNuGet
	}

    # Act
    $p = Open-PackagePage 'elmah' -Source $source -WhatIf -PassThru

    # Assert
    Assert-AreEqual 'http://elmah.googlecode.com' $p.OriginalString

    # Act
    $p = Open-PackagePage 'elmah' -License -Source $source -WhatIf -PassThru

    # Assert
    Assert-AreEqual 'http://www.apache.org/licenses/LICENSE-2.0' $p.OriginalString
}

function OpenPackagePageAcceptAllAsSourceName {
    # Act
    $p = Open-PackagePage 'elmah' -version 1.1 -Source 'All' -WhatIf -PassThru

    # Assert
    Assert-AreEqual 'http://elmah.googlecode.com/' $p.OriginalString

    # Act
    $p = Open-PackagePage 'elmah' -Version 1.1 -License -Source 'All' -WhatIf -PassThru

    # Assert
    Assert-AreEqual 'http://www.apache.org/licenses/LICENSE-2.0' $p.OriginalString
}