using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Tests.Foundation.TestAttributes.Context;
using NuGet.Tests.Foundation.Utility;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using NuGet.Tests.Foundation.TestCommands.Context;

namespace NuGet.Tests.Foundation.TestAttributes
{
    [AttributeUsageAttribute(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("Foundation.ContextDataDrivenTestDiscoverer", "Foundation")]
    public class DataDrivenTestAttribute : TheoryAttribute
    {
    }

    public class ContextDataDrivenTestDiscoverer : IXunitTestCaseDiscoverer
    {
        readonly IMessageSink diagnosticMessageSink;
        TestMethodDisplay defaultDisplay = TestMethodDisplay.Method;

        public ContextDataDrivenTestDiscoverer(IMessageSink diagnosticMessageSink)
        {
            this.diagnosticMessageSink = diagnosticMessageSink;
        }

        public IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
        {
            try
            {
                TestSuite suite = TestSuite.Current;
                if (suite != null)
                {
                    if (!suite.Tests.Any(suiteTest => testMethod.Method.Name == suiteTest.Name))
                    {
                        return Enumerable.Empty<ContextBaseTestCase>();
                    }
                }

                discoveryOptions.SetValue<bool>("xunit.discovery.PreEnumerateTheories", true);

                // Special case Skip, because we want a single Skip (not one per data item), and a skipped test may
                // not actually have any data (which is quasi-legal, since it's skipped).
                if (factAttribute.GetNamedArgument<string>("Skip") != null)
                {
                    return new IXunitTestCase[] { new XunitTestCase(diagnosticMessageSink, defaultDisplay, testMethod) };
                }

                List<IXunitTestCase> contextTests = new List<IXunitTestCase>();
                IEnumerable<Context.Context> contexts = ContextHelpers.Instance.GenerateTestCommands(testMethod);

                if (discoveryOptions.PreEnumerateTheoriesOrDefault())
                {
                    IEnumerable<IAttributeInfo> dataAttributes = testMethod.Method.GetCustomAttributes(typeof(DataAttribute));
                    foreach (var dataAttribute in dataAttributes)
                    {
                        IAttributeInfo discovererAttribute = dataAttribute.GetCustomAttributes(typeof(DataDiscovererAttribute)).First();
                        IDataDiscoverer discoverer = ExtensibilityPointFactory.GetDataDiscoverer(diagnosticMessageSink, discovererAttribute);
                        IEnumerable<object[]> dataRows = discoverer.GetData(dataAttribute, testMethod.Method);

                        if (discoverer.SupportsDiscoveryEnumeration(dataAttribute, testMethod.Method)
                            && dataRows.All(dataRow => CanSerializeObject(dataRow)))
                        {
                            foreach (object[] dataRow in dataRows)
                            {
                                AddTestWithData(dataRow, testMethod, contexts, ref contextTests);
                            }
                        }
                        else
                        {
                            AddTestWithData(null, testMethod, contexts, ref contextTests);
                        }
                    }

                    if (contextTests.Count == 0)
                    {
                        contextTests.Add(new ExecutionErrorTestCase(diagnosticMessageSink, defaultDisplay, testMethod, string.Format("No data found for {0}.{1}", testMethod.TestClass.Class.Name, testMethod.Method.Name)));
                    }

                    return contextTests;
                }

                return new IXunitTestCase[] { new ContextTheoryTestCase(diagnosticMessageSink, defaultDisplay, testMethod, Context.Context.EmptyContext) };
            }
            catch (Exception e)
            {
                return new IXunitTestCase[] { new ExecutionErrorTestCase(diagnosticMessageSink, Xunit.Sdk.TestMethodDisplay.Method, testMethod, $"Test discovery failed with exception: {e.ToString()}") };
            }
        }

        private void AddTestWithData(object[] dataRow, ITestMethod testMethod, IEnumerable<Context.Context> contexts, ref List<IXunitTestCase> contextTests)
        {
            if (contexts.Any())
            {
                if (dataRow != null)
                {
                    contextTests.AddRange(contexts.Select(c => new ContextTestCase(diagnosticMessageSink, defaultDisplay, testMethod, c, dataRow)));
                }
                else
                {
                    contextTests.AddRange(contexts.Select(c => new ContextTheoryTestCase(diagnosticMessageSink, defaultDisplay, testMethod, c)));
                }
            }
            else
            {
                if (dataRow != null)
                {
                    contextTests.Add(new ContextTestCase(diagnosticMessageSink, defaultDisplay, testMethod, Context.Context.EmptyContext, dataRow));
                }
                else
                {
                    contextTests.Add(new ContextTheoryTestCase(diagnosticMessageSink, defaultDisplay, testMethod, Context.Context.EmptyContext));
                }
            }
        }


        // These restrictions come from xUnit, if an object is not one of these types, they will not show up in the Test Explorer window
        static readonly Type[] supportedSerializationTypes = new[] {
            typeof(IXunitSerializable),
            typeof(char),           typeof(char?),
            typeof(string),
            typeof(Type),
            typeof(byte),           typeof(byte?),
            typeof(short),          typeof(short?),
            typeof(ushort),         typeof(ushort?),
            typeof(int),            typeof(int?),
            typeof(uint),           typeof(uint?),
            typeof(long),           typeof(long?),
            typeof(ulong),          typeof(ulong?),
            typeof(float),          typeof(float?),
            typeof(double),         typeof(double?),
            typeof(decimal),        typeof(decimal?),
            typeof(bool),           typeof(bool?),
            typeof(DateTime),       typeof(DateTime?),
            typeof(DateTimeOffset), typeof(DateTimeOffset?),
        };

        private static bool CanSerializeObject(object value)
        {
            if (value == null)
            {
                return true;
            }

            var valueType = value.GetType();
            if (valueType.IsArray)
            {
                return ((Array)value).Cast<object>().All(CanSerializeObject);
            }

            if (valueType.IsEnum || (valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(Nullable<>) && valueType.GetGenericArguments()[0].IsEnum))
            {
                return true;
            }

            if (supportedSerializationTypes.Any(supportedType => supportedType.IsAssignableFrom(valueType)))
            {
                return true;
            }

            return false;
        }
    }
}
