
namespace NuGet.PackageManagement.VisualStudio
{
    public interface ISettingsManager
    {
        ISettingsStore GetReadOnlySettingsStore();
        IWritableSettingsStore GetWritableSettingsStore();
    }
}
