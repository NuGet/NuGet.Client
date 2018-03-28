using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Tests.Foundation.Requirements;
using NuGet.Tests.Foundation.Utility;
using Xunit.Abstractions;

namespace NuGet.Tests.Foundation.TestAttributes.Context
{
    public class ContextHelpers
    {
        public const string BlissTestPlatformIdentifierString = "BlissTest_PlatformId";
        public const string BlissTestPlatformVersionString = "BlissTest_PlatformVersion";
        public const string BlissTestProductString = "BlissTest_Product";
        public const string RegistryPath = @"Software\Microsoft\VisualStudio\Bliss\TestSettings\Context";

        private static ContextHelpers instance;

        protected ContextHelpers()
        {
        }

        public static ContextHelpers Instance
        {
            get
            {
                if (ContextHelpers.instance == null)
                {
                    ContextHelpers.instance = new ContextHelpers();
                }

                return ContextHelpers.instance;
            }
        }

        public virtual IEnumerable<ContextTraitAttribute> GetContextTraits(ITestMethod method)
        {
            List<ContextTraitAttribute> traits = new List<ContextTraitAttribute>();

            traits.AddRange(method.Method.GetCustomAttributes(typeof(ContextTraitAttribute)).Select(attribInfo => (ContextTraitAttribute)(attribInfo as IReflectionAttributeInfo).Attribute));
            traits.AddRange(method.TestClass.Class.GetCustomAttributes(typeof(ContextTraitAttribute)).Select(attribInfo => (ContextTraitAttribute)(attribInfo as IReflectionAttributeInfo).Attribute));

            IEnumerable<ContextTraitAttribute> distinctTraits = traits.Distinct().ToList();
            if (distinctTraits.Count() != traits.Count)
            {
                throw new InvalidOperationException(String.Format("There are duplicate contexts defined on Class {0} and Method {1}", method.TestClass.Class, method));
            }

            return distinctTraits;
        }

        public virtual IEnumerable<ContextExclusionTraitAttribute> GetContextExclusionTraits(ITestMethod method)
        {
            List<ContextExclusionTraitAttribute> traits = new List<ContextExclusionTraitAttribute>();

            traits.AddRange(method.Method.GetCustomAttributes(typeof(ContextExclusionTraitAttribute)).Select(attribInfo => (ContextExclusionTraitAttribute)(attribInfo as IReflectionAttributeInfo).Attribute));
            traits.AddRange(method.TestClass.Class.GetCustomAttributes(typeof(ContextExclusionTraitAttribute)).Select(attribInfo => (ContextExclusionTraitAttribute)(attribInfo as IReflectionAttributeInfo).Attribute));

            IEnumerable<ContextExclusionTraitAttribute> distinctTraits = traits.Distinct().ToList();
            if (distinctTraits.Count() != traits.Count)
            {
                throw new InvalidOperationException(String.Format("There are duplicate context exclusions defined on Class {0} and Method {1}", method.TestClass.Class, method));
            }

            return distinctTraits;
        }

        public virtual IEnumerable<Context> GenerateContextMatrix(IEnumerable<ContextTraitAttribute> traits, IEnumerable<ContextExclusionTraitAttribute> exclusionTraits, Context defaultContext)
        {
            var groupedTraits =
                from trait in traits
                group trait by trait.GetType() into typeGroup
                select typeGroup;

            List<Context> contexts = new List<Context>();
            foreach (var typeGroup in groupedTraits)
            {
                List<Context> existingContexts = contexts;
                contexts = new List<Context>();

                foreach (var trait in typeGroup)
                {
                    if (existingContexts.Count == 0)
                    {
                        // No existing traits, just add
                        contexts.Add(trait.CreateContext(defaultContext));
                    }
                    else
                    {
                        // Add for each existing context
                        foreach (var existingContext in existingContexts)
                        {
                            contexts.Add(trait.CreateContext(existingContext));
                        }
                    }
                }
            }

            foreach (ContextExclusionTraitAttribute contextExclusionTrait in exclusionTraits)
            {
                contexts.RemoveAll(c => contextExclusionTrait.ShouldExcludeContext(c));
            }

            return contexts;
        }

        public virtual Context GetDefaultContextState(ITestMethod method)
        {
            var defaultAttributeType = method.TestClass.Class.GetCustomAttributes(typeof(ContextDefaultStateAttribute)).FirstOrDefault();
            if (defaultAttributeType == null)
            {
                return new Context();
            }

            return ((ContextDefaultStateAttribute)((IReflectionAttributeInfo)defaultAttributeType).Attribute).Context;
        }

        public IEnumerable<Context> GenerateTestCommands(ITestMethod method)
        {
            IEnumerable<ContextTraitAttribute> definedTraits = GetContextTraits(method);

            List<Context> contextsToRun = new List<Context>();

            if (!definedTraits.Any())
            {
                return contextsToRun;
            }

            Context defaultContext = this.GetDefaultContextState(method);
            IEnumerable<Context> availableContexts = this.GenerateContextMatrix(definedTraits, GetContextExclusionTraits(method), defaultContext);
            IEnumerable<Context> specifiedContexts = this.GetSpecifiedContexts(method.Method);

            ContextBehavior contextBehavior = ContextBehavior.RunFirstContext;
            if (specifiedContexts != null)
            {
                contextBehavior = ContextBehavior.RunSpecifiedContexts;
            }
            else
            {
                IAttributeInfo attributeInfo = method.TestClass.Class.GetCustomAttributes(typeof(ContextDefaultBehaviorAttribute)).FirstOrDefault();
                if (attributeInfo != null)
                {
                    contextBehavior = attributeInfo.GetNamedArgument<ContextBehavior>("DefaultBehavior");
                }
            }

            switch (contextBehavior)
            {
                case ContextBehavior.RunSpecifiedContexts:
                    IEnumerable<Context> desiredContexts = this.FilterToContexts(specifiedContexts, availableContexts);
                    contextsToRun.AddRange(GetContextsUsingRequirementService(method, desiredContexts).ToList());
                    break;
                case ContextBehavior.RunFirstContext:
                    contextsToRun.Add(availableContexts.First());
                    break;
                case ContextBehavior.RunAllContexts:
                    foreach (Context context in availableContexts)
                    {
                        contextsToRun.Add(context);
                    }
                    break;
                default:
                    throw new InvalidOperationException("Unexpected context behavior");
            }

            return contextsToRun;
        }

        public IEnumerable<Context> GetContextsUsingRequirementService(ITestMethod method, IEnumerable<Context> contexts)
        {
            using (RequirementService rs = this.CreateRequirementService())
            {
                foreach (Context context in contexts)
                {
                    if (rs.SatisfiesRequirements(method.TestClass.Class.GetType(), context) &&
                        rs.SatisfiesRequirements(method.Method.GetType(), context))
                    {
                        yield return context;
                    }
                }
            }
        }

        private static RequirementService requirementService;
        public virtual RequirementService CreateRequirementService()
        {
            return requirementService ?? (requirementService = new RequirementService());
        }

        private IEnumerable<Context> FilterToContexts(IEnumerable<Context> specifiedContexts, IEnumerable<Context> availableContexts)
        {
            List<Context> filteredContexts = new List<Context>();
            foreach (Context availableContext in availableContexts)
            {
                if (specifiedContexts.Any(requiredContext => requiredContext.Matches(availableContext)))
                {
                    filteredContexts.Add(availableContext);
                }
            }

            return filteredContexts;
        }

        public virtual string GetEnvironmentVariable(string variable)
        {
            string value = Environment.GetEnvironmentVariable(variable);
            if (!String.IsNullOrEmpty(value))
            {
                return Environment.ExpandEnvironmentVariables(value);
            }
            else
            {
                return value;
            }
        }

        public virtual string GetRegistrySetting(string setting)
        {
            return RegistryHelpers.RetrieveRegistryValue<string>(RegistryHive.CurrentUser, ContextHelpers.RegistryPath, setting);
        }

        public virtual Context GetSpecifiedContext()
        {
            Context result = new Context();
            result.Platform = this.GetContextProperty(BlissTestPlatformIdentifierString, PlatformIdentifier.UnspecifiedPlatform);
            result.Version = this.GetContextProperty(BlissTestPlatformVersionString, PlatformVersion.UnspecifiedVersion);
            result.Product = this.GetContextProperty(BlissTestProductString, Product.UnspecifiedProduct);

            if (result.Product == Product.UnspecifiedProduct &&
                result.Platform == PlatformIdentifier.UnspecifiedPlatform)
            {
                return null;
            }

            return result;
        }

        public virtual IEnumerable<Context> GetSpecifiedContexts(IMethodInfo method)
        {
            TestSuite suite = TestSuite.Current;
            if (suite != null)
            {
                IEnumerable<ContextedTestSuiteEntry> matchingTests = suite.Tests.Where(test => test.Name == method.Name);
                return matchingTests.Select(suiteTest => suiteTest.Context);
            }

            Context result = new Context();
            result.Platform = this.GetContextProperty(BlissTestPlatformIdentifierString, PlatformIdentifier.UnspecifiedPlatform);
            result.Version = this.GetContextProperty(BlissTestPlatformVersionString, PlatformVersion.UnspecifiedVersion);
            result.Product = this.GetContextProperty(BlissTestProductString, Product.UnspecifiedProduct);
            if (result.Product == Product.UnspecifiedProduct &&
                result.Platform == PlatformIdentifier.UnspecifiedPlatform)
            {
                return null;
            }
            return new Context[] { result };
        }

        private T GetContextProperty<T>(string key, T defaultValue) where T : struct
        {
            string valueAsString = this.GetEnvironmentVariable(key) ?? this.GetRegistrySetting(key);
            T value;
            if (!Enum.TryParse<T>(valueAsString, ignoreCase: true, result: out value))
            {
                value = defaultValue;
            }

            return value;
        }
    }
}
