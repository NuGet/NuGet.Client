using System;
using System.Globalization;
using Microsoft.Test.Apex;
using Microsoft.Test.Apex.Hosts;
using NuGetClientTestContracts;

namespace Apex.NuGetClient.Host
{
    public class PackageManageUIExportToHostTypeConstraint : TypeConstraint
    {
        public PackageManageUIExportToHostTypeConstraint(Type hostType)
        {
            if (!ReflectionHelpers.TypeIsDerivedFrom(hostType, typeof(Microsoft.Test.Apex.Hosts.Host)))
            {
                throw new ArgumentException(
                    String.Format(CultureInfo.InvariantCulture, "Host type must derive from '{0}'", typeof(Microsoft.Test.Apex.Hosts.Host).FullName),
                    "hostType");
            }

            this.Host = hostType;
        }

        private Type Host
        {
            get;
            set;
        }

        public override bool Validate(Type type)
        {
            return ((ReflectionHelpers.TypeIsDerivedFrom(type, typeof(IPackageManageUIHostTestContract)) || ReflectionHelpers.TypeIsDerivedFrom(type, typeof(IPackageManageUITestContract))))
                || this.ExportsToHostConstraint(type);
        }

        private bool ExportsToHostConstraint(Type type)
        {
            if (!Attribute.IsDefined(type, typeof(ProvidesHostExtensionAttribute), true))
            {
                return false;
            }

            object[] attributes = type.GetCustomAttributes(typeof(ProvidesHostExtensionAttribute), true);

            if (attributes.Length > 0)
            {
                foreach (ProvidesHostExtensionAttribute attribute in attributes)
                {
                    if (ReflectionHelpers.TypeIsDerivedFrom(this.Host, attribute.Host))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
