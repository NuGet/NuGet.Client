function Test-BindingRedirectDoesNotAddToSilverlightProject {
    param(
        $context
    )
    # Arrange
    $c = New-SilverlightApplication

    # Act
    $c | Install-Package TestSL -Version 1.0 -Source $context.RepositoryPath

    # Assert
    $c | %{ Assert-Reference $_ TestSL 1.0.0.0; 
            Assert-Reference $_ HostSL 1.0.1.0; }

    Assert-NoBindingRedirect $c app.config HostSL '0.0.0.0-1.0.1.0' '1.0.1.0'
}

function Test-InstallPackageRespectReferencesAccordingToDifferentFrameworks
{
    param ($context)

    # Arrange
    $p1 = New-SilverlightClassLibrary
    $p2 = New-ConsoleApplication

    # Act
    ($p1, $p2) | Install-Package RefPackage -Source $context.RepositoryPath

    # Assert
    Assert-Package $p1 'RefPackage'
    Assert-Reference $p1 'fear'
    Assert-Null (Get-AssemblyReference $p1 'mafia')

    Assert-Package $p2 'RefPackage'
    Assert-Reference $p2 'one'
    Assert-Reference $p2 'three'
    Assert-Null (Get-AssemblyReference $p2 'two')
}
