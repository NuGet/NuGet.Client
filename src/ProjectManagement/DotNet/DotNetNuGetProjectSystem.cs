using NuGet.Frameworks;

namespace NuGet.ProjectManagement.DotNet
{
    public interface DotNetNuGetProjectSystem
    {
        NuGetFramework TargetFramework { get; }
        string ProjectName { get; }

        /// <summary>
        /// Method called when adding an assembly reference to the project.
        /// </summary>
        /// <param name="referencePath">Physical path to the assembly file relative to the project root.</param>
        void AddReference(string referencePath);

        /// <summary>
        /// Adds an assembly reference to a framework assembly (one in the GAC).
        /// </summary>
        /// <param name="name">name of the assembly</param>
        void AddFrameworkReference(string name);
        void RemoveReference(string name);
        void AddImport(string targetFullPath, ImportLocation location);
        void RemoveImport(string targetFullPath);

        // LIKELY, THERE HAS TO MORE STUFF HERE like 'IsSupportedFile' and 'IsBindingRedirectsEnabled'
        // IMO, there are hacks introduced to special case based on project systems like 'websites' and 'silverlight'
    }

    public enum ImportLocation
    {
        Top,
        Bottom
    }
}
