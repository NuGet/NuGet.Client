using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// A nuget aware project system containing a .json file instead of a packages.config file
    /// </summary>
    public class BuildIntegratedProjectSystem : BuildIntegratedNuGetProject
    {
        public BuildIntegratedProjectSystem(string jsonConfigPath, IMSBuildNuGetProjectSystem msbuildProjectSystem, string projectName, string uniqueName)
            : base(jsonConfigPath, msbuildProjectSystem)
        {
            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, projectName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, uniqueName);
        }
    }
}
