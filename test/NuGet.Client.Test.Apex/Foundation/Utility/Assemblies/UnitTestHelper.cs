using System;
using System.Linq;

namespace NuGetClient.Test.Foundation.Utility.Assemblies
{
    public static class UnitTestHelper
    {
#if DEBUG
        private static string[] unitTestAssemblies = { "Microsoft.Expression.Framework.UnitTests", "xunit", "DesignTool.Tests", "NuGetClient.Test.Foundation" };
        private static string[] blendUnitTestAssemblies = { "Microsoft.Expression.Blend.UnitTests" };
        private static string[] foundationTestAssemblies = { "NuGetClient.Test.Foundation" };
#endif
        public static bool IsUnitTestEnvironment { get; private set; }
        public static bool IsBlendUnitTestEnvironment { get; private set; }
        public static bool IsTestFoundationEnvironment { get; private set; }

        static UnitTestHelper()
        {
#if DEBUG
            // Check if this is unit test environment. 
            IsBlendUnitTestEnvironment = AreAssembliesLoaded(blendUnitTestAssemblies);
            IsUnitTestEnvironment = IsBlendUnitTestEnvironment || AreAssembliesLoaded(unitTestAssemblies);
            IsTestFoundationEnvironment = AreAssembliesLoaded(foundationTestAssemblies);
#else
            IsBlendUnitTestEnvironment = false;
            IsUnitTestEnvironment = false;
            IsTestFoundationEnvironment = false;
#endif
        }

#if DEBUG
        private static bool AreAssembliesLoaded(string[] assemblies)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).Intersect(assemblies).Any();
        }
#endif
    }
}
