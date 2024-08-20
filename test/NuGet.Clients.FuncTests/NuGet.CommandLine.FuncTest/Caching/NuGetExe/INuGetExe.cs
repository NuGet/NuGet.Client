using NuGet.Test.Utility;

namespace NuGet.CommandLine.Test.Caching
{
    public interface INuGetExe
    {
        void ClearHttpCache(CachingTestContext context);
        string GetHttpCachePath(CachingTestContext context);
        CommandRunnerResult Execute(CachingTestContext context, string args);
    }
}
