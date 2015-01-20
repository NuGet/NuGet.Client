using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(IProductUpdateSettings))]
    public class VsProductUpdateSettings : SettingsManagerBase, IProductUpdateSettings
    {
        private const string SettingsRoot = "NuGet";
        private const string CheckUpdatePropertyName = "ShouldCheckForUpdate";

        public VsProductUpdateSettings() :
            this(ServiceLocator.GetInstance<IServiceProvider>())
        {
        }

        public VsProductUpdateSettings(IServiceProvider serviceProvider) :
            base(serviceProvider)
        {
        }

        public bool ShouldCheckForUpdate
        {
            get
            {
                return ReadInt32(SettingsRoot, CheckUpdatePropertyName, defaultValue: 1) == 1;
            }
            set
            {
                WriteInt32(SettingsRoot, CheckUpdatePropertyName, value ? 1 : 0);
            }
        }
    }
}
