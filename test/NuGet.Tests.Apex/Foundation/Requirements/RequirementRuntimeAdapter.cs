using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace NuGet.Tests.Foundation.Requirements
{
    /// <summary>
    /// This class acts as a mapping layer between the raw MEF ImportMany composition and
    /// consumption via a keyed Requirement lookup, by mapping the IEnumerable input to a
    /// dictionary for ease of lookup.
    /// </summary>
    [Export(typeof(RequirementRuntimeAdapter))]
    internal class RequirementRuntimeAdapter
    {
        private Dictionary<Requirement, IRequirementRuntime> requirements;

        [ImportingConstructor]
        public RequirementRuntimeAdapter([ImportMany] IEnumerable<IRequirementRuntime> requirementRuntimes)
        {
            this.requirements = requirementRuntimes.ToDictionary(r => r.Requirement, r => r);
        }

        public IRequirementRuntime this[Requirement requirement]
        {
            get
            {
                return this.requirements[requirement];
            }
        }

        public bool TryGetValue(Requirement requirement, out IRequirementRuntime runtime)
        {
            return this.requirements.TryGetValue(requirement, out runtime);
        }
    }
}
