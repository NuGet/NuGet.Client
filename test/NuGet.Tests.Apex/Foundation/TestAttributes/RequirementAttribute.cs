using System;
using NuGet.Tests.Foundation.Requirements;

namespace NuGet.Tests.Foundation.TestAttributes
{
    /// <summary>
    /// Specifies a runtime environment requirement for this test to execute successfully.
    /// The harness is responsible for interpreting this requirement, and may interpret it
    /// differently depending on the runtime environment.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class RequirementAttribute : TraitAttribute
    {
        public Requirement Requirement { get; private set; }

        public RequirementAttribute(Requirement requirement) :
            base("Requirement", requirement.ToString())
        {
            this.Requirement = requirement;
        }
    }
}
