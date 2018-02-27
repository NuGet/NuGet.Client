using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Tests.Foundation.TestAttributes.Context;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NuGet.Tests.Foundation.TestAttributes
{
    public class NuGetClientTraitDiscoverer : TraitDiscoverer
    {
        public override IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            List<object> args = traitAttribute.GetConstructorArguments().ToList();
            if (args.Count == 1 && args[0].GetType().IsEnum)
            {
                yield return new KeyValuePair<string, string>(args[0].GetType().Name, args[0].ToString());
            }
            else if (((traitAttribute as IReflectionAttributeInfo)?.Attribute as ContextTraitAttribute) != null)
            {
                IReflectionAttributeInfo reflectionAttribute = (IReflectionAttributeInfo)traitAttribute;
                string name = reflectionAttribute.Attribute.GetType().Name.Replace("Attribute", "");

                if (args.Count == 1)
                {
                    yield return new KeyValuePair<string, string>(name, ((string)args[0]));
                }
                else
                {
                    yield return new KeyValuePair<string, string>(name, traitAttribute.ToString());
                }
            }
            else if (args.Count == 1 && traitAttribute as IReflectionAttributeInfo != null)
            {
                IReflectionAttributeInfo reflectionAttribute = (IReflectionAttributeInfo)traitAttribute;
                string argAsString = args[0].ToString().ToLowerInvariant();
                yield return new KeyValuePair<string, string>(reflectionAttribute.Attribute.GetType().Name.Replace("Attribute", ""), argAsString);
            }
            else
            {
                var ctorArgs = args.Cast<string>().ToList();
                yield return new KeyValuePair<string, string>(ctorArgs[0], ctorArgs[1]);
            }
        }
    }

    /// <summary>
    /// Attribute used to decorate a test method with arbitrary name/value pairs ("traits").
    /// </summary>
    /// <remarks>Simple wrapper for XUnit trait to isolate tests. In root namespace for discoverability.</remarks>
    [TraitDiscoverer("NuGet.Tests.Foundation.NuGetClientTraitDiscoverer", "NuGet.Tests.Foundation")]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class TraitAttribute : Attribute, ITraitAttribute
    {
        public string Name { get; private set; }
        public string Value { get; private set; }

        // We put the traits in a static class in the Tests.Foundation root to get consistency and discoverability.
        // Here we create an overload for each trait to translate it into the string value pair. Using typeof()
        // prevents disconnect with type renames.

        public TraitAttribute(Traits.Traits.TestType testType)
                    : this(typeof(Traits.Traits.TestType).Name, testType.ToString())
        {
        }

        public TraitAttribute(Traits.Traits.Team team)
            : this(typeof(Traits.Traits.Team).Name, team.ToString())
        {
        }

        protected TraitAttribute(string name, string value)
        {
            this.Name = name;
            this.Value = value;
        }
    }
}
