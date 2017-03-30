using System;
using NuGet.Configuration;
using NuGet.ProjectManagement;

namespace Test.Utility
{
    public class TestSourceControlManagerProvider : ISourceControlManagerProvider
    {
        private TestSourceControlManager TestSourceControlManager { get; }

        public TestSourceControlManagerProvider(TestSourceControlManager testSourceControlManager)
        {
            if (testSourceControlManager == null)
            {
                throw new ArgumentNullException(nameof(testSourceControlManager));
            }

            TestSourceControlManager = testSourceControlManager;
        }

        public SourceControlManager GetSourceControlManager()
        {
            return TestSourceControlManager;
        }
    }
}
