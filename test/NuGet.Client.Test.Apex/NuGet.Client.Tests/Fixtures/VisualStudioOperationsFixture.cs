using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Test.Apex;
using Microsoft.Test.Apex.Services;
using Microsoft.Test.Apex.VisualStudio;
using NuGetClient.Test.Integration.Apex;

namespace NuGetClient.Test.Integration.Fixtures
{
    public class VisualStudioOperationsFixture
    {
        private VisualStudioHostConfiguration visualStudioHostConfiguration;
        private readonly IOperations operations;
        private readonly IAssertionVerifier verifier;
        private readonly ITestLogger testLogger;

        private static readonly string[] DefaultAssembliesExportingTypes = new string[]
            {
                "Apex.NuGetClient.dll",
                "NuGetClientTestContracts.dll",
                "Microsoft.Test.Apex.ApplicationDeployment.dll",
            };
        private static readonly string[] ProbingPaths = new string[]
            {
                Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath),
                    Util.TestRequirementFiles.InstallationPath,
                    Util.TestRequirementFiles.NuGetClientTestBinariesPath,
            };

        private static List<string> compositionAssembliesCache;

        private IList<string> nugetTestContracts = new List<string> { "NuGetClientTestContracts.dll", "Apex.NuGetClient.dll"};

        public IOperations FixtureOperations
        {
            get { return operations; }
        }
        public VisualStudioOperationsFixture()
        {
            if (!Microsoft.Test.Apex.Operations.IsConfigured)
            {
                Microsoft.Test.Apex.Operations.Configure(new NuGetTestOperationConfiguration(this.CompositionAssemblies));
            }

            operations = Microsoft.Test.Apex.Operations.Current;
            verifier = operations.Get<IAssertionVerifier>();
            verifier.AssertionDelegate = FailAction;
            testLogger = operations.Get<ITestLogger>();
        }

        internal VisualStudioHostConfiguration VisualStudioHostConfiguration
        {
            get
            {
                //if (visualStudioHostConfiguration == null)
                //{
                //    visualStudioHostConfiguration = new InternalVisualStudioHostConfiguration();

                //    visualStudioHostConfiguration.InProcessHostConstraints = new List<ITypeConstraint>
                //    {
                //        // Provide a composite constraint to allow multiple constraints to filter types.
                //            // All InProcessHostConstraints must return TRUE for the type to be allowed; our
                //            // composite type allows ANY of our constraints to contribute our TRUE or FALSE vote.
                //            new CompositeTypeConstraint(new List<ITypeConstraint>{
                //                new NuGetClientInProcessTypeConstraint(typeof(VisualStudioHost), this.visualStudioHostConfiguration)})
                //    };
                //    foreach (string compositionAssembly in this.CompositionAssemblies)
                //    {
                //        visualStudioHostConfiguration.AddCompositionAssembly(Assembly.GetExecutingAssembly().Location);
                //    }
                //}

                //return visualStudioHostConfiguration;

                if (visualStudioHostConfiguration == null)
                {
                    visualStudioHostConfiguration = new VisualStudioHostConfiguration();
                    var codeBase = Assembly.GetExecutingAssembly().CodeBase;
                    var uri = new UriBuilder(codeBase);
                    var path = Uri.UnescapeDataString(uri.Path);

                    var assemblyFolder = Path.GetDirectoryName(path);

                    foreach (var testAssembly in nugetTestContracts)
                    {
                        var assemblyPath = Path.Combine(assemblyFolder, testAssembly);

                        if (File.Exists((assemblyPath)))
                        {
                            visualStudioHostConfiguration.AddCompositionAssembly(assemblyPath);
                        }
                    }
                    visualStudioHostConfiguration.AddCompositionAssembly(Assembly.GetExecutingAssembly().Location);

                    visualStudioHostConfiguration.InProcessHostConstraints = new List<ITypeConstraint>() { new NuGetTypeConstraint() };
                }
                return visualStudioHostConfiguration;
            }
        }
        private IEnumerable<string> CompositionAssemblies
        {
            get
            {
                if (this.CachedCompositionAssemblies == null)
                {
                    List<string> compositionAssemblies = new List<string>();

                    foreach (string compositionAssembly in this.AssembliesExportingTypes)
                    {
                        foreach (string location in ProbingPaths)
                        {
                            string testContractAssembly = Path.Combine(location, compositionAssembly);
                            if (File.Exists(testContractAssembly))
                            {
                                compositionAssemblies.Add(testContractAssembly);
                                break;
                            }
                        }
                    }
                    compositionAssemblies.Add(new Uri(typeof(VisualStudioOperationsFixture).Assembly.CodeBase).LocalPath);
                    this.CachedCompositionAssemblies = compositionAssemblies.Distinct().ToList();
                }
                return this.CachedCompositionAssemblies;
            }
        }

        protected virtual string[] AssembliesExportingTypes => VisualStudioOperationsFixture.DefaultAssembliesExportingTypes;

        protected virtual List<string> CachedCompositionAssemblies
        {
            get
            {
                return VisualStudioOperationsFixture.compositionAssembliesCache;
            }

            set
            {
                VisualStudioOperationsFixture.compositionAssembliesCache = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the test should fail on the 
        /// first assert failure or continue executing as far as possible.
        /// The default value is false.
        /// </summary>
        public bool FailTestOnFirstFailure
        {
            get
            {
                return verifier.AssertOnFirstFailure;
            }
            set
            {
                verifier.AssertOnFirstFailure = false;
            }
        }

        private void FailAction(string message)
        {
            //Log error
            testLogger.WriteError(message);

            //throw error for xUnit
            throw new InvalidOperationException(message);
        }

        public IOperations Operations
        {
            get { return this.operations; }
        }

        /// <summary>
        /// Use the new env var for telling Omni which VS instance to use
        /// </summary>
        private class InternalVisualStudioHostConfiguration : VisualStudioHostConfiguration
        {
            protected static readonly TimeSpan InternalRemoteObjectLeaseTime = TimeSpan.FromHours(1);
            protected static readonly TimeSpan InternalRemoteSingletonObjectLeaseTime = TimeSpan.Zero;

            public InternalVisualStudioHostConfiguration()
                : base()
            {
                this.RemoteObjectLeaseTime = InternalRemoteObjectLeaseTime;
                this.RemoteSingletonObjectLeaseTime = InternalRemoteSingletonObjectLeaseTime;
            }
        }
    }
}
