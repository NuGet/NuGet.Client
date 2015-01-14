using System;
using Microsoft.VisualStudio.Shell;

namespace NuGet.Tools
{
    public class ProvideSearchProviderAttribute : RegistrationAttribute
    {
        private const string ExtensionProvidersKey = "SearchProviders";
        private readonly string _providerTypeGuid;
        private readonly string _providerName; 

        public ProvideSearchProviderAttribute(Type providerType, string providerName)
        {
            if (providerType == null)
            {
                throw new ArgumentNullException("providerType");
            }

            if (String.IsNullOrEmpty(providerName))
            {
                throw new ArgumentException("'providerName' cannot be null or empty.");
            }

            _providerTypeGuid = providerType.GUID.ToString("B");
            _providerName = providerName;
        }

        public override void Register(RegistrationContext context)
        {
            using (RegistrationAttribute.Key key = context.CreateKey(ExtensionProvidersKey))
            {
                using (RegistrationAttribute.Key subKey = key.CreateSubkey(_providerTypeGuid))
                {
                    subKey.SetValue(String.Empty, _providerName);
                    subKey.SetValue("Package", context.ComponentType.GUID.ToString("B"));
                }
            }
        }

        public override void Unregister(RegistrationContext context)
        {
            context.RemoveKey(String.Format(@"{0}\{1}", ExtensionProvidersKey, _providerTypeGuid));
        }
    }
}
