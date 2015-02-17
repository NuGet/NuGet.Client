using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    public interface ICommonOperations
    {
        Task OpenFile(string fullPath);
    }
}
