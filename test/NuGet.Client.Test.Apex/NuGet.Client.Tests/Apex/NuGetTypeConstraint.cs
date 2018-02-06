using System;
using Microsoft.Test.Apex;

namespace NuGetClient.Test.Integration.Apex
{
    public class NuGetTypeConstraint : TypeConstraint
    {
        public override bool Validate(Type type)
        {
            return true;
        }
    }
}
