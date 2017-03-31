using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Tests.Apex
{
    public class ApexTestRequirementsFixture : IDisposable
    {
        private static bool isInitialized = false;

        public ApexTestRequirementsFixture()
        {
            if (isInitialized)
            {
                return;
            }

            TestInitialize();
            isInitialized = true;
        }

        public void Dispose()
        {
            TestCleanup();
        }

        private void TestCleanup()
        {
            // test clean up code
        }

        private void TestInitialize()
        {
            // test initialization code
        }

    }
}
