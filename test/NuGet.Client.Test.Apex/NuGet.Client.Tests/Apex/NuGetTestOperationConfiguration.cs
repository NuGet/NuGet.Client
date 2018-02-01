using System;
using System.Collections.Generic;
using Microsoft.Test.Apex;

namespace NuGetClient.Test.Integration.Apex
{
    internal class NuGetTestOperationConfiguration : OperationsConfiguration
    {
        private readonly IEnumerable<string> additionalAssemblies;

        internal NuGetTestOperationConfiguration(IEnumerable<string> additionalAssemblies)
        {
            this.additionalAssemblies = additionalAssemblies;
        }

        public override IEnumerable<string> CompositionAssemblies
        {
            get
            {
                List<string> baseAssemblies = new List<string>(base.CompositionAssemblies);
                baseAssemblies.Add(new Uri(typeof(NuGetTestOperationConfiguration).Assembly.CodeBase).LocalPath);

                if (this.additionalAssemblies != null)
                {
                    baseAssemblies.AddRange(this.additionalAssemblies);
                }

                return baseAssemblies;
            }
        }
        protected override Type Verifier
        {
            get
            {
                return typeof(IAssertionVerifier);
            }
        }
    }
}
