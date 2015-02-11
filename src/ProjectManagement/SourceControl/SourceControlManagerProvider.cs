namespace NuGet.ProjectManagement
{
    // Helpers ensure that the singleton SourceControlManager is used across the codebase
    public interface ISourceControlManagerProvider
    {
        SourceControlManager GetSourceControlManager();
    }
}
