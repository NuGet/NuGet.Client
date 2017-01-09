param($installPath, $toolsPath, $package, $project)

$vsRef = $project.Object.References.Item("NuGet.SolutionRestoreManager.Interop")
if ($vsRef -and !$vsRef.EmbedInteropTypes)
{
    $vsRef.EmbedInteropTypes = $true
}