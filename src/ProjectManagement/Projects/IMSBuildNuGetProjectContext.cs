namespace NuGet.ProjectManagement
{
    public interface IMSBuildNuGetProjectContext : INuGetProjectContext
    {
        bool SkipAssemblyReferences { get; }
        bool BindingRedirectsDisabled { get; }
    }
}
