using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public abstract class CertificateSearchItem : SettingItem
    {
        protected CertificateSearchItem()
        {
        }

        internal CertificateSearchItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
        }

        protected override bool CanHaveChildren => true;

        public abstract X509Certificate Search();
    }
}
