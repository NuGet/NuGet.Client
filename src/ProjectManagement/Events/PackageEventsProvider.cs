using System.ComponentModel.Composition;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Provider for the PackageEvents singleton
    /// </summary>
    [Export(typeof(IPackageEventsProvider))]
    public class PackageEventsProvider : IPackageEventsProvider
    {
        private static PackageEvents _instance;

        public PackageEventsProvider()
        {

        }

        public PackageEvents GetPackageEvents()
        {
            return Instance;
        }

        internal static PackageEvents Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PackageEvents();
                }

                return _instance;
            }
        }
    }
}
