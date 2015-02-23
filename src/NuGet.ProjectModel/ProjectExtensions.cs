using NuGet.Frameworks;

namespace NuGet.ProjectModel
{
    public static class ProjectExtensions
    {
        public static TargetFrameworkInformation GetTargetFramework(this Project project, NuGetFramework targetFramework)
        {
            var frameworkInfo = NuGetFrameworkUtility.GetNearest(project.TargetFrameworks, 
                                                                 targetFramework, 
                                                                 item => item.FrameworkName);

            return frameworkInfo ?? new TargetFrameworkInformation();
        }
    }
}