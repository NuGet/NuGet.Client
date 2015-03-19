
namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Internal version of the public IVsPackageInstallerEvents
    /// </summary>
    public interface IPackageEventsProvider
    {
        PackageEvents GetPackageEvents();
    }
}
