using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public abstract class CertificateSearchItem : SettingItem
    {
        #region Constructors

        protected CertificateSearchItem()
        {
        }

        internal CertificateSearchItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
        }

        #endregion

        #region Properties

        protected override bool CanHaveChildren => true;

        #endregion

        #region Members

        public abstract X509Certificate Search();

        #endregion
    }
}
