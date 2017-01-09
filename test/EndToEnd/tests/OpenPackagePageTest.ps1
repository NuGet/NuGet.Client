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
    $p = Open-PackagePage elmah -Report -WhatIf -PassThru -Version 1.1.0 -Source $SourceNuGet
	$expectedString = 'https://www.nuget.org/packages/elmah/1.1.0/ReportAbuse'
    
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
    # Act
    $p = Open-PackagePage 'owin' -Version 1.0.0 -Source $SourceNuGet -WhatIf -PassThru

    # Assert
    Assert-AreEqual 'https://github.com/owin-contrib/owin-hosting/' $p.OriginalString

    # Act
    $p = Open-PackagePage 'owin' -Version 1.0.0 -License -Source $SourceNuGet -WhatIf -PassThru

    # Assert
    Assert-AreEqual 'https://github.com/owin-contrib/owin-hosting/blob/master/LICENSE.txt' $p.OriginalString
}