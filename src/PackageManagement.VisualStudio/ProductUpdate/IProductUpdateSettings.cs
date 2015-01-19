using System;
namespace NuGet.PackageManagement.VisualStudio
{
    public interface IProductUpdateSettings
    {
        bool ShouldCheckForUpdate { get; set; }
    }
}
