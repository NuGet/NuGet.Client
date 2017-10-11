using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Test.Apex;

namespace NuGet.Tests.Apex
{
    internal class NuGetTestOperationConfiguration : OperationsConfiguration
    {
        protected override Type Verifier
        {
            get
            {
                return typeof(IAssertionVerifier);
            }
        }
    }
}
