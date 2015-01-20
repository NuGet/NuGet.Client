namespace NuGet.VisualStudio
{
    public interface IRepositorySettings
    {
        string RepositoryPath { get; }
        string ConfigFolderPath { get; }
    }
}
