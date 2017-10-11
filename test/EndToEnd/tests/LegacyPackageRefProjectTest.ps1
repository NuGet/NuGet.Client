# basic create for uwp package ref based project
function Test-UwpPackageRefClassLibraryCreate {

    # Arrange & Act
    $project = New-UwpPackageRefClassLibrary UwpLibrary1

    # Assert
    Assert-NetCoreProjectCreation $project
}

# install package test for uwp legacy csproj package ref
function Test-UwpPackageRefClassLibInstallPackage {

    # Arrange
    $project = New-UwpPackageRefClassLibrary UwpLibrary1
    $id = 'Nuget.versioning'
    $version = '3.5.0'
    Assert-NetCoreProjectCreation $project

    # Act
    Install-Package $id -ProjectName $project.Name -version $version
    $project.Save($project.FullName)

    # Assert
    $packageRefs = @(Get-MsBuildItems $project 'PackageReference')
    Assert-AreEqual 2 $packageRefs.Count
    Assert-AreEqual $packageRefs[1].GetMetadataValue("Identity") 'Nuget.Versioning' 
    Assert-AreEqual $packageRefs[1].GetMetadataValue("Version") '3.5.0'
}