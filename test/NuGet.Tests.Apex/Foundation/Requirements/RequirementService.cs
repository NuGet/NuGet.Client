using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Reflection;
using NuGet.Tests.Foundation.TestAttributes;
using NuGet.Tests.Foundation.TestAttributes.Context;

namespace NuGet.Tests.Foundation.Requirements
{
    /// <summary>
    /// Aggregates available (or explicit) IRequirementRuntime types and provides
    /// a way to test types/methods against them.
    /// </summary>
    public class RequirementService : IDisposable
    {
        [Import]
#pragma warning disable 0649
        // initialized via MEF composition
        private RequirementRuntimeAdapter requirementRuntime;
#pragma warning restore 0649
        private CompositionContainer compositionContainer;

        public RequirementService()
        {
            var catalog = new AggregateCatalog();
            catalog.Catalogs.Add(new AssemblyCatalog(typeof(RequirementService).Assembly));
            this.compositionContainer = new CompositionContainer(catalog);
            this.Compose();
        }

        public RequirementService(CompositionContainer compositionContainer)
        {
            this.compositionContainer = compositionContainer;
            this.Compose();
        }

        public RequirementService(params IRequirementRuntime[] requirementRuntimes)
        {
            this.requirementRuntime = new RequirementRuntimeAdapter(requirementRuntimes);
        }

        private void Compose()
        {
            this.compositionContainer.ComposeParts(this);
        }

        public bool SatisfiesRequirements(Type type, Context context = default(Context))
        {
            if (type == null)
            {
                return false;
            }
            return this.SatisfiesRequirementsImpl(type.GetCustomAttributes<RequirementAttribute>(inherit: true), context);
        }

        public bool SatisfiesRequirements(MethodInfo method, Context context = default(Context))
        {
            if (method == null)
            {
                return false;
            }
            return this.SatisfiesRequirementsImpl(method.GetCustomAttributes<RequirementAttribute>(inherit: true), context);
        }

        private bool SatisfiesRequirementsImpl(IEnumerable<RequirementAttribute> requirements, Context context)
        {
            if (context == default(Context))
            {
                context = new Context();
            }
            foreach (RequirementAttribute requirement in requirements)
            {
                IRequirementRuntime runtime;
                if (!this.requirementRuntime.TryGetValue(requirement.Requirement, out runtime))
                {
                    throw new NotImplementedException(string.Format("No IRequirementRuntime implementation for '{0}' found. Check that your MEF composition defines a compatible type.", requirement.Requirement));
                }
                if (!runtime.SatisfiedInContext(context))
                {
                    return false;
                }
            }
            return true;
        }

        public void Dispose()
        {
            if (this.compositionContainer != null)
            {
                this.compositionContainer.Dispose();
                this.compositionContainer = null;
            }
        }
    }
}
