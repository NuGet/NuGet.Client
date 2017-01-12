# basic create for .net core template
function Test-NetCore {
    # Arrange
    $project = New-NetCoreConsoleApp ConsoleApp

    # Act
    Install-Package NuGet.Versioning -ProjectName $project.Name -version 1.0.7

    # Assert
    Assert-ProjectJsonDependency $project NuGet.Versioning 1.0.7
    Assert-ProjectJsonLockFilePackage $project NuGet.Versioning 1.0.7
    Assert-ProjectJsonLockFileRuntimeAssembly $project lib/portable-net40+win/NuGet.Versioning.dll
}