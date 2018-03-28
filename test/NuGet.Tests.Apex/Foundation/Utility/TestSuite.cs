using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Tests.Foundation.TestAttributes.Context;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Tests.Foundation.Utility
{
    /// <summary>
    /// Loads a test suite definition from a file at a location specified by an environment variable.
    /// </summary>
    public class TestSuite
    {
        /// <summary>
        /// The environment variable containing the path to the suite definition file.
        /// </summary>
        public const string DesignToolsTestSuiteString = "NuGetClient_SuiteFile";

        private static readonly TestSuite currentSuite;

        private readonly List<ContextedTestSuiteEntry> tests;

        static TestSuite()
        {
            string suiteFile = Environment.GetEnvironmentVariable(TestSuite.DesignToolsTestSuiteString);
            if (!string.IsNullOrEmpty(suiteFile))
            {
                Console.WriteLine(string.Format("Using SuiteFile: {0}", suiteFile));

                using (StreamReader reader = new StreamReader(suiteFile))
                {
                    TestSuite.currentSuite = new TestSuite(reader);
                }
            }
        }

        /// <summary>
        /// The suite currently specified by the process environment.
        /// </summary>
        public static TestSuite Current { get { return TestSuite.currentSuite; } }

        /// <summary>
        /// A list of the tests parsed from the file, ordered by Product.
        /// </summary>
        public virtual IEnumerable<ContextedTestSuiteEntry> Tests { get { return this.tests; } }

        public TestSuite()
        {
            this.tests = new List<ContextedTestSuiteEntry>();
        }

        public TestSuite(TextReader inputReader)
        {
            this.tests = new List<ContextedTestSuiteEntry>();
            JsonSerializer serializer = new JsonSerializer();
            using (JsonTextReader jsonReader = new JsonTextReader(inputReader))
            {
                JArray tests = null;
                tests = serializer.Deserialize<JArray>(jsonReader);
                foreach (var test in tests)
                {
                    var name = test.Value<string>("Name");
                    PlatformIdentifier platform;
                    PlatformVersion version;
                    Product product;
                    CodeLanguage language;
                    ActiveSolutionConfiguration solutionConfiguration;
                    BuildMethod buildMethod;
                    if (!string.IsNullOrEmpty(test.Value<string>("Skip"))) continue;
                    if (!Enum.TryParse<PlatformIdentifier>(test.Value<string>("Platform"), out platform)) continue;
                    if (!Enum.TryParse<PlatformVersion>(test.Value<string>("PlatformVersion"), out version)) continue;
                    if (!Enum.TryParse<Product>(test.Value<string>("Product"), out product)) continue;
                    if (!Enum.TryParse<CodeLanguage>(test.Value<string>("Language"), out language)) language = CodeLanguage.UnspecifiedLanguage;
                    if (!Enum.TryParse<ActiveSolutionConfiguration>(test.Value<string>("SolutionConfiguration"), out solutionConfiguration)) solutionConfiguration = ActiveSolutionConfiguration.UnspecifiedConfiguration;
                    if (!Enum.TryParse<BuildMethod>(test.Value<string>("BuildMethod"), out buildMethod)) buildMethod = BuildMethod.UnspecifiedBuildMethod;
                    this.tests.Add(new ContextedTestSuiteEntry()
                    {
                        Name = name,
                        Context = new Context(platform, version, product, language, solutionConfiguration, buildMethod),
                        
                    });
                }
            }
            this.tests = tests.OrderBy(test => test.Context.Product).ToList();
        }
    }
}
