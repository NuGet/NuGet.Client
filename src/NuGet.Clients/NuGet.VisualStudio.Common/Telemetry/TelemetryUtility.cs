// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Windows.Documents;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.VisualStudio.Telemetry
{
    public static class TelemetryUtility
    {
        public static string CreateFileAndForgetEventName(string typeName, string memberName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(typeName));
            }

            if (string.IsNullOrEmpty(memberName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(memberName));
            }

            return $"{VSTelemetrySession.VSEventNamePrefix}fileandforget/{typeName}/{memberName}";
        }

        public static void EmitException(string className, string methodName, Exception exception)
        {
            if (className == null)
            {
                throw new ArgumentNullException(nameof(className));
            }
            if (methodName == null)
            {
                throw new ArgumentNullException(nameof(methodName));
            }
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            TelemetryEvent ToTelemetryEvent(Exception e, string name)
            {
                TelemetryEvent te = new TelemetryEvent(name);
                te["Message"] = e.Message;
                te["ExceptionType"] = e.GetType().FullName;
                te["StackTrace"] = e.StackTrace;

                if (e is AggregateException aggregateException)
                {
                    var exceptions =
                        aggregateException.InnerExceptions
                        .Select(ie => ToTelemetryEvent(ie, name: null))
                        .ToList();
                    te.ComplexData["InnerExceptions"] = exceptions;
                }
                else if (e.InnerException != null)
                {
                    var inner = ToTelemetryEvent(e.InnerException, name: null);
                    te.ComplexData["InnerException"] = inner;
                }

                return te;
            }

            TelemetryEvent telemetryEvent = ToTelemetryEvent(exception, $"errors/{className}.{methodName}");
            TelemetryActivity.EmitTelemetryEvent(telemetryEvent);
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
        public static bool IsNuGetOrg(PackageSource source)
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
