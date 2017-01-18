# basic create for .net core template
function Test-NetCoreCreateConsoleApp {

    # Arrange & Act
    $project = New-NetCoreConsoleApp ConsoleApp
    
    # Assert
    Assert-NetCoreProjectCreation $project
}

# install package test for .net core
function Test-NetCoreAddPackage {
    
    # Arrange
    $project = New-NetCoreConsoleApp ConsoleApp
    $id = 'Newtonsoft.Json'
    $version = '9.0.1'

    # Act 
    Install-Package $id -ProjectName $project.Name -version $version
    $project.Save($project.FullName)

    # Assert
    Assert-NetCoreProjectCreation $project
    Assert-NetCorePackageInstall $project $id $version
}