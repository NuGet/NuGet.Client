namespace NuGet.Common
{
    public interface IPackageAssemblyReference : IPackageFile
    {
        string Name
        {
            get;
        }
    }
}
