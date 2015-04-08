using System.ComponentModel.Composition;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(IPackageRestoreManager))]
    internal class VSPackageRestoreManager : PackageRestoreManager
    {
        public VSPackageRestoreManager()
            : this(
                ServiceLocator.GetInstance<ISourceRepositoryProvider>(),
                ServiceLocator.GetInstance<ISettings>(),
                ServiceLocator.GetInstance<ISolutionManager>())
        {
        }

        public VSPackageRestoreManager(ISourceRepositoryProvider sourceRepositoryProvider, ISettings settings, ISolutionManager solutionManager)
            : base(sourceRepositoryProvider, settings, solutionManager)
        {
        }
    }
}
