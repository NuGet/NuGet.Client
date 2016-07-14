using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Test.Utility;

namespace NuGet.CommandLine.Test.Caching
{
    public class NuGetExe : INuGetExe
    {
        private static ConcurrentDictionary<string, Task<NuGetExe>> _verifiedNuGetExe
            = new ConcurrentDictionary<string, Task<NuGetExe>>();

        private NuGetExe(string pathToExe)
        {
            PathToExe = pathToExe;
        }

        public string PathToExe { get; }

        public bool Debug { get; set; }

        public CommandRunnerResult Execute(CachingTestContext context, string args)
        {
            var timeout = 60 * 1000 * 1;
            if (Debug)
            {
                args += " --debug -Verbosity detailed";
                timeout *= 60;
            }

            return CommandRunner.Run(
                PathToExe,
                context.WorkingPath,
                args,
                timeOutInMilliseconds: timeout,
                waitForExit: true,
                environmentVariables: new Dictionary<string, string>
                {
                    { "NUGET_PACKAGES", context.GlobalPackagesPath },
                    { "NUGET_HTTP_CACHE_PATH", context.IsolatedHttpCachePath }
                });
        }

        public string GetHttpCachePath(CachingTestContext context)
        {
            var result = Execute(context, "locals http-cache -list");

            var stdout = result.Item2.Trim();

            // Example:
            //   stdout = http-cache: C:\Users\jver\AppData\Local\NuGet\v3-cache
            //   path   = C:\Users\jver\AppData\Local\NuGet\v3-cache
            var path = stdout.Split(new[] { ':' }, 2)[1].Trim();

            return path;
        }

        public static async Task<NuGetExe> Get320Async()
        {
            return await DownloadNuGetExeAsync(
                "https://dist.nuget.org/win-x86-commandline/v3.2.0/nuget.exe",
                "nuget.3.2.0.exe");
        }

        public static async Task<NuGetExe> Get330Async()
        {
            return await DownloadNuGetExeAsync(
                "https://dist.nuget.org/win-x86-commandline/v3.3.0/nuget.exe",
                "nuget.3.3.0.exe");
        }

        public static async Task<NuGetExe> Get340RcAsync()
        {
            return await DownloadNuGetExeAsync(
                "https://dist.nuget.org/win-x86-commandline/v3.4.0-rc/nuget.exe",
                "nuget.3.4.3-rc.exe");
        }

        public static async Task<NuGetExe> Get343Async()
        {
            return await DownloadNuGetExeAsync(
                "https://dist.nuget.org/win-x86-commandline/v3.4.3/nuget.exe",
                "nuget.3.4.3.exe");
        }

        public static async Task<NuGetExe> Get344Async()
        {
            return await DownloadNuGetExeAsync(
                "https://dist.nuget.org/win-x86-commandline/v3.4.4/NuGet.exe",
                "nuget.3.4.4.exe");
        }

        public static async Task<NuGetExe> Get350Beta2Async()
        {
            return await DownloadNuGetExeAsync(
                "https://dist.nuget.org/win-x86-commandline/v3.5.0-beta2/NuGet.exe",
                "nuget.3.5.0-beta2.exe");
        }

        public static async Task<NuGetExe> Get350Rc1Async()
        {
            return await DownloadNuGetExeAsync(
                "https://dist.nuget.org/win-x86-commandline/v3.5.0-rc1/NuGet.exe",
                "nuget.3.5.0-rc1.exe");
        }

        public static NuGetExe GetBuiltNuGetExe()
        {
            return new NuGetExe(Util.GetNuGetExePath());
        }

        private static async Task<NuGetExe> DownloadNuGetExeAsync(string requestUri, string fileName)
        {
            var temp = NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp);
            var path = Path.Combine(temp, fileName);

            return await _verifiedNuGetExe.GetOrAdd(
                path,
                thisPath => ConcurrencyUtilities.ExecuteWithFileLockedAsync(
                    thisPath,
                    async token =>
                    {
                        if (File.Exists(thisPath))
                        {
                            // Make sure we can run the executable.
                            var helpResult = CommandRunner.Run(
                                    thisPath,
                                    ".",
                                    "help",
                                    waitForExit: true);

                            if (helpResult.Item1 == 0)
                            {
                                return new NuGetExe(thisPath);
                            }
                        }

                        // Download the executable.
                        using (var httpClient = new System.Net.Http.HttpClient())
                        using (var stream = await httpClient.GetStreamAsync(requestUri))
                        using (var fileStream = new FileStream(thisPath, FileMode.Create))
                        {
                            await stream.CopyToAsync(fileStream);
                        }

                        return new NuGetExe(thisPath);
                    },
                    CancellationToken.None));
        }
    }
}
