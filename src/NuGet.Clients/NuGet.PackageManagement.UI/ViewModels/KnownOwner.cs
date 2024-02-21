using System;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public class KnownOwner
    {
        private string _name;
        private Uri _link;

        public KnownOwner(string name, Uri link)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _link = link ?? throw new ArgumentNullException(nameof(link));
        }

        public string Name => _name;

        public Uri Link => _link;
    }
}
