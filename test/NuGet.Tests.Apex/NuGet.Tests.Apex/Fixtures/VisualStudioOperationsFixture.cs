// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Test.Apex;
using Microsoft.Test.Apex.Services;
using Microsoft.Test.Apex.VisualStudio;

namespace NuGet.Tests.Apex
{
    public class VisualStudioOperationsFixture
    {
        private VisualStudioHostConfiguration _visualStudioHostConfiguration;
        private readonly IOperations _operations;
        private readonly IAssertionVerifier _verifier;
        private readonly ITestLogger _testLogger;
        private IList<string> _nugetTestContracts = new List<string> {"NuGet.PackageManagement.UI.TestContract.dll", "NuGet.Console.TestContract.dll"};

        public VisualStudioOperationsFixture()
        {
            if (!Microsoft.Test.Apex.Operations.IsConfigured)
            {
                Microsoft.Test.Apex.Operations.Configure(new NuGetTestOperationConfiguration());
            }

            _operations = Microsoft.Test.Apex.Operations.Current;
            _verifier = _operations.Get<IAssertionVerifier>();
            _verifier.AssertionDelegate = FailAction;
            _verifier.AssertOnFirstFailure = true;
            _testLogger = _operations.Get<ITestLogger>();
        }

        internal VisualStudioHostConfiguration VisualStudioHostConfiguration
        {
            get
            {
                if (_visualStudioHostConfiguration == null)
                {
                    _visualStudioHostConfiguration = new VisualStudioHostConfiguration();
                    var codeBase = Assembly.GetExecutingAssembly().CodeBase;
                    var uri = new UriBuilder(codeBase);
                    var path = Uri.UnescapeDataString(uri.Path);

                    var assemblyFolder = Path.GetDirectoryName(path);

                    foreach(var testAssembly in _nugetTestContracts)
                    {
                        var assemblyPath = Path.Combine(assemblyFolder, testAssembly);

                        if (File.Exists((assemblyPath)))
                        {
                            _visualStudioHostConfiguration.AddCompositionAssembly(assemblyPath);
                        }
                    }
                    _visualStudioHostConfiguration.AddCompositionAssembly(Assembly.GetExecutingAssembly().Location);
                    _visualStudioHostConfiguration.InProcessHostConstraints = new List<ITypeConstraint>() { new NuGetTypeConstraint() };

                    // Use the same environment to avoid elevation
                    _visualStudioHostConfiguration.InheritProcessEnvironment = true;

                    // Launch in the experimental instance of VS
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NUGET_TEST_APEX_EXP")))
                    {
                        _visualStudioHostConfiguration.CommandLineArguments += " /rootSuffix Exp";
                    }
                }
                return _visualStudioHostConfiguration;
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
                return _verifier.AssertOnFirstFailure;
            }
            set
            {
                _verifier.AssertOnFirstFailure = value;
            }
        }

        public IOperations Operations
        {
            get { return _operations; }
        }

        private void FailAction(string message)
        {
            // Log error
            _testLogger.WriteError(message);

            // throw error for xUnit
            throw new InvalidOperationException(message);
        }
    }
}
