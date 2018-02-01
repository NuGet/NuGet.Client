using System;
using Microsoft.Test.Apex;
using Microsoft.Test.Apex.Hosts;
using Microsoft.Test.Apex.VisualStudio;
using NuGetClientTestContracts;


namespace Apex.NuGetClient.Host
{
    public class NuGetClientInProcessTypeConstraint : TypeConstraint
    {
        public NuGetClientInProcessTypeConstraint(Type hostType, VisualStudioHostConfiguration config = null)
        {
            this.Host = hostType;
            this.Config = config;
        }

        private VisualStudioHostConfiguration Config { get; set; }

        private Type Host { get; set; }

        public override bool Validate(Type type)
        {
            return ReflectionHelpers.TypeIsDerivedFrom(type, typeof(INuGetClientTestContract)) ||
                this.InProcessHostConstraint(type);
        }

        private bool InProcessHostConstraint(Type type)
        {
            object[] attributes = type.GetCustomAttributes(typeof(CreateInProcessAttribute), true);

            if (attributes.Length > 0)
            {
                foreach (CreateInProcessAttribute attribute in attributes)
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
