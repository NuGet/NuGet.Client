using NuGet.Frameworks;

namespace NuGet.ProjectModel
{
    public static class PackageSpecExtensions
    {
        public static TargetFrameworkInformation GetTargetFramework(this PackageSpec project, NuGetFramework targetFramework)
        {
            var frameworkInfo = NuGetFrameworkUtility.GetNearest(project.TargetFrameworks, 
                                                                 targetFramework, 
                                                                 item => item.FrameworkName);

            return frameworkInfo ?? new TargetFrameworkInformation();
        }
    }
}