using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Test.Apex;

namespace NuGet.Tests.Apex
{
    public class NuGetTypeConstraint : TypeConstraint
    {
        public override bool Validate(Type type)
        {
            return true;
        }
    }
}
