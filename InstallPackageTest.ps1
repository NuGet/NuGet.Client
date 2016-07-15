
function Test-InstallPackageWithInvalidLocalSource {
	# Arrange

	# Act & Assert
	Assert-Throws { Install-Package Rules -source c:\temp\data } "Unable to find package 'Rules' at source c:\temp\data. Source not found."
}