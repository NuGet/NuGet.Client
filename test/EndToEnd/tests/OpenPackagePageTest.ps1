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

# Work around server bug https://github.com/NuGet/NuGetGallery/issues/2476, where V2 report Abuse Url is not correct.
# TODO: Remove the replace code when server bug is fixed.
function Test-OpenPackagePageOpenReportAbuseUrlIfReportAbuseParameterIsSet {
    # Act
    $p = Open-PackagePage elmah -Report -WhatIf -PassThru -Version 1.1
	$reportAbuseUrl = $p.OriginalString.Replace('package/ReportAbuse/elmah/1.1.0', 'packages/elmah/1.1.0/ReportAbuse')
	$expectedString = 'https://www.nuget.org/packages/elmah/1.1.0/ReportAbuse'
    
    # Assert
    Assert-AreEqual $expectedString $reportAbuseUrl
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
    # For nuget.org, there is a bug that the project Url was saved to Database with an extra / at the end.
	# TODO: Remove the if else condition below when bug https://github.com/NuGet/NuGetGallery/issues/2409 is fixed.
	if ($SourceNuGet -eq 'nuget.org')
	{
		$source = 'nUGet.OrG'  # keep the coverage that the source name is case insensitive
		$expectedUrl = 'http://elmah.googlecode.com'
	}
	else 
	{
	    $source = $SourceNuGet
	    $expectedUrl = 'http://elmah.googlecode.com'
	}

    # Act
    $p = Open-PackagePage 'elmah' -Source $source -WhatIf -PassThru

    # Assert
    Assert-AreEqual $expectedUrl $p.OriginalString

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