using NuGet.ProjectManagement;
using System;
using System.ComponentModel;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Encapsulates the document model behind the Package Manager document window
    /// </summary>
    /// <remarks>
    /// This class just proxies all calls through to the PackageManagerSession and implements IVsPersistDocData to fit
    /// into the VS model. It's basically an adaptor that turns PackageManagerSession into an IVsPersistDocData so VS is happy.
    /// </remarks>
    public class PackageManagerModel : INotifyPropertyChanged
    {
        public SourceRepositoryProvider Sources { get; private set; }

        public NuGetProject Target { get; private set; }

        public PackageManagerModel(SourceRepositoryProvider sources, NuGetProject target)
        {
            Sources = sources;
            Target = target;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}