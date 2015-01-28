function Test-SinglePackageInstallIntoSingleProject {
    # Arrange
    $project = New-ConsoleApplication
    
    # Act
    Install-Package FakeItEasy -ProjectName $project.Name -version 1.8.0
    
    # Assert
    Assert-Reference $project Castle.Core
    Assert-Reference $project FakeItEasy   
    Assert-Package $project FakeItEasy
    Assert-Package $project Castle.Core
    Assert-SolutionPackage FakeItEasy
    Assert-SolutionPackage Castle.Core
}