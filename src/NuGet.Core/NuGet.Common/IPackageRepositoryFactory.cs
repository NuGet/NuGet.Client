namespace NuGet.Common
{
    public interface IPackageRepositoryFactory
    {
        IPackageRepository CreateRepository(string packageSource);
    }
}
