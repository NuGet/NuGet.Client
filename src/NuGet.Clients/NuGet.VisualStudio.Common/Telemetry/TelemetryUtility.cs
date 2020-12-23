// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Telemetry;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.Configuration;

using AsyncLazyBool = Microsoft.VisualStudio.Threading.AsyncLazy<bool>;
using Task = System.Threading.Tasks.Task;

namespace NuGet.VisualStudio.Telemetry
{
    public static class TelemetryUtility
    {
        public static async Task PostFaultAsync(Exception e, string callerClassName, [CallerMemberName] string callerMemberName = null)
        {
            if (e == null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            var caller = $"{callerClassName}.{callerMemberName}";
            var description = $"{e.GetType().Name} - {e.Message}";

            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var fault = new FaultEvent($"{VSTelemetrySession.VSEventNamePrefix}Fault", description, FaultSeverity.General, e, gatherEventDetails: null);
            fault.Properties[$"{VSTelemetrySession.VSPropertyNamePrefix}Fault.Caller"] = caller;
            TelemetryService.DefaultSession.PostEvent(fault);

            if (await IsShellAvailable.GetValueAsync())
            {
                ActivityLog.TryLogError(caller, description);
            }
        }

        private static readonly AsyncLazyBool IsShellAvailable = new AsyncLazyBool(IsShellAvailableAsync, NuGetUIThreadHelper.JoinableTaskFactory);

        private static async Task<bool> IsShellAvailableAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            return ServiceProvider.GlobalProvider.GetService(typeof(SVsShell)) != null;
        }

        /// <summary>
        /// True if the source is http and ends with index.json
        /// </summary>
        public static bool IsHttpV3(PackageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.IsHttp &&
                (source.Source.EndsWith("index.json", StringComparison.OrdinalIgnoreCase)
                || source.ProtocolVersion == 3);
        }

        /// <summary>
        /// True if the source is HTTP and has a *.nuget.org or nuget.org host.
        /// </summary>
        public static bool IsNuGetOrg(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentNullException(nameof(source));
            }

            bool isHttp = source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                          source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            if (!isHttp)
            {
                return false;
            }

            var uri = UriUtility.TryCreateSourceUri(source, UriKind.Absolute);
            if (uri == null)
            {
                return false;
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(uri.Host, "nuget.org")
                || uri.Host.EndsWith(".nuget.org", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// True if the source is an Azure Artifacts (DevOps) feed
        /// </summary>
        public static bool IsAzureArtifacts(PackageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!source.IsHttp)
            {
                return false;
            }

            var uri = source.TrySourceAsUri;
            if (uri == null)
            {
                return false;
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(uri.Host, "pkgs.dev.azure.com")
                || uri.Host.EndsWith(".pkgs.visualstudio.com", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// True if the source is a GitHub Package Repository (GPR) feed
        /// </summary>
        public static bool IsGitHub(PackageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!source.IsHttp)
            {
                return false;
            }

            var uri = source.TrySourceAsUri;
            if (uri == null)
            {
                return false;
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(uri.Host, "nuget.pkg.github.com"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// True if the source is the Visual Studio Offline feed
        /// </summary>
        public static bool IsVsOfflineFeed(PackageSource source)
        {
            return IsVsOfflineFeed(source, ExpectedVsOfflinePackagesPath.Value);
        }

        internal static bool IsVsOfflineFeed(PackageSource source, string expectedVsOfflinePackagesPath)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!source.IsLocal)
            {
                return false;
            }

            return expectedVsOfflinePackagesPath != null &&
                StringComparer.OrdinalIgnoreCase.Equals(expectedVsOfflinePackagesPath, source.Source?.TrimEnd('\\'));
        }

        private static readonly Lazy<string> ExpectedVsOfflinePackagesPath = new Lazy<string>(() =>
        {
            if (!RuntimeEnvironmentHelper.IsWindows)
            {
                return null;
            }

            try
            {
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                return Path.Combine(programFiles, "Microsoft SDKs", "NuGetPackages");
            }
            catch
            {
                // Ignore this check if we fail for any reason to generate the path.
                return null;
            }
        });
    }
}
