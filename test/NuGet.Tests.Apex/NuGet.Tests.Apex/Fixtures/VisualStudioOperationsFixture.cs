using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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

        public VisualStudioOperationsFixture()
        {
            if (!Microsoft.Test.Apex.Operations.IsConfigured)
            {
                Microsoft.Test.Apex.Operations.Configure(new NuGetTestOperationConfiguration());
            }

            _operations = Microsoft.Test.Apex.Operations.Current;
            _verifier = _operations.Get<IAssertionVerifier>();
            _verifier.AssertionDelegate = FailAction;
            _testLogger = _operations.Get<ITestLogger>();
        }

        internal VisualStudioHostConfiguration VisualStudioHostConfiguration
        {
            get
            {
                if (_visualStudioHostConfiguration == null)
                {
                    _visualStudioHostConfiguration = new VisualStudioHostConfiguration();
                    _visualStudioHostConfiguration.AddCompositionAssembly(Assembly.GetExecutingAssembly().Location);
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
