using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Test.Apex;

namespace NuGetClient.Test.Integration.Support
{
    internal class CompositeTypeConstraint : TypeConstraint
    {
        private List<ITypeConstraint> constraints;

        public CompositeTypeConstraint(IList<ITypeConstraint> constraints)
        {
            this.constraints = constraints.ToList();
        }

        public override string DisplayName
        {
            get
            {
                string name = "Composite Constraints: " + string.Join(", ", this.constraints.Select(c => c.DisplayName));
                return name;
            }
        }

        /// <summary>
        /// Validates against all constraints to see if ANY allow it.
        /// </summary>
        /// <param name="type"></param>
        /// <returns>True if any constraint returns true</returns>
        public override bool Validate(Type type)
        {
            return this.constraints != null && this.constraints.Any(c => c.Validate(type));
        }
    }
}
