namespace NuGet.PackageManagement
{
    public class PackageRestoreResult
    {
        public bool Restored { get; }
        public PackageRestoreResult(bool restored)
        {
            Restored = restored;
        }
    }
}
