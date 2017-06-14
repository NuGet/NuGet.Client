## Skipping these Tests because current E2E machines (Win2016 Server) do not allow creating new UWP projects.

# # basic create for uwp package ref based project
# function Test-UwpPackageRefClassLibraryCreate {

#     # Arrange & Act
#     $project = New-UwpPackageRefClassLibrary UwpLibrary1

#     # Assert
#     Assert-NetCoreProjectCreation $project
# }

# # install package test for uwp legacy csproj package ref
# function Test-UwpPackageRefClassLibInstallPackage {

#     # Arrange
#     $project = New-UwpPackageRefClassLibrary UwpLibrary1
#     $id = 'Nuget.versioning'
#     $version = '3.5.0'
#     Assert-NetCoreProjectCreation $project

#     # Act
#     Install-Package $id -ProjectName $project.Name -version $version
#     $project.Save($project.FullName)

#     # Assert
#     Assert-NetCorePackageInstall $project $id $version
# }