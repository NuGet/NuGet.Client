using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsPackageSourceProvider))]
    public class VsPackageSourceProvider : IVsPackageSourceProvider
    {
        private readonly IPackageSourceProvider _packageSourceProvider;

        [ImportingConstructor]
        public VsPackageSourceProvider(ISettings settings)
        {
            _packageSourceProvider = new PackageSourceProvider(settings);
            _packageSourceProvider.PackageSourcesSaved += PackageSourcesSaved;
        }

        public IEnumerable<KeyValuePair<string, string>> GetSources(bool includeUnOfficial, bool includeDisabled)
        {
            foreach (PackageSource source in _packageSourceProvider.LoadPackageSources())
            {
                if ((source.IsOfficial || includeUnOfficial) && (source.IsEnabled || includeDisabled))
                {
                    // Name -> Source Uri
                    yield return new KeyValuePair<string, string>(source.Name, source.Source);
                }
            }

            yield break;
        }

        public event EventHandler SourcesChanged;

        private void PackageSourcesSaved(object sender, EventArgs e)
        {
            if (SourcesChanged != null)
            {
                // No information is given in the event args, callers must re-request GetSources
                SourcesChanged(this, new EventArgs());
            }
        }
    }
}
