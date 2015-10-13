using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "push", "PushCommandDescription;DefaultConfigDescription",
        MinArgs = 1, MaxArgs = 2, UsageDescriptionResourceName = "PushCommandUsageDescription",
        UsageSummaryResourceName = "PushCommandUsageSummary", UsageExampleResourceName = "PushCommandUsageExamples")]
    public class PushCommand : Command
    {
        [Option(typeof(NuGetCommand), "PushCommandSourceDescription", AltName = "src")]
        public string Source { get; set; }

        [Option(typeof(NuGetCommand), "CommandApiKey")]
        public string ApiKey { get; set; }

        [Option(typeof(NuGetCommand), "PushCommandTimeoutDescription")]
        public int Timeout { get; set; }

        [Option(typeof(NuGetCommand), "PushCommandDisableBufferingDescription")]
        public bool DisableBuffering { get; set; }

        public override async Task ExecuteCommandAsync()
        {
            // First argument should be the package
            string packagePath = Arguments[0];

            // Don't push symbols by default
            string source = ResolveSource(packagePath, ConfigurationDefaults.Instance.DefaultPushSource);
            var pushEndpoint = await GetPushEndpointAsync(source);

            if (string.IsNullOrEmpty(pushEndpoint))
            {
                var message = string.Format(
                    LocalizedResourceManager.GetString("PushCommand_PushNotSupported"),
                    source);

                Console.LogWarning(message);
                return;
            }

            var apiKey = GetApiKey(pushEndpoint);
            if (string.IsNullOrEmpty(apiKey) && !IsFileSource(pushEndpoint))
            {
                Console.WriteWarning(
                    LocalizedResourceManager.GetString("NoApiKeyFound"),
                    CommandLineUtility.GetSourceDisplayName(pushEndpoint));
            }

            var timeout = TimeSpan.FromSeconds(Math.Abs(Timeout));
            if (timeout.Seconds <= 0)
            {
                timeout = TimeSpan.FromMinutes(5); // Default to 5 minutes
            }

            PushPackage(packagePath, pushEndpoint, apiKey, timeout);

            if (pushEndpoint.Equals(NuGetConstants.DefaultGalleryServerUrl, StringComparison.OrdinalIgnoreCase))
            {
                PushSymbols(packagePath, timeout);
            }
        }

        private async Task<string> GetPushEndpointAsync(string source)
        {
            var packageSource = new Configuration.PackageSource(source);

            var sourceRepositoryProvider = new CommandLineSourceRepositoryProvider(SourceProvider);

            var sourceRepository = sourceRepositoryProvider.CreateRepository(packageSource);
            var pushCommandResource = await sourceRepository.GetResourceAsync<PushCommandResource>();

            if (pushCommandResource != null)
            {
                var pushEndpoint = pushCommandResource.GetPushEndpoint();
                return pushEndpoint;
            }

            return source;
        }

        private string ResolveSource(string packagePath, string configurationDefaultPushSource = null)
        {
            string source = Source;

            if (String.IsNullOrEmpty(source))
            {
                source = SettingsUtility.GetConfigValue(Settings, "DefaultPushSource");
            }

            if (String.IsNullOrEmpty(source))
            {
                source = configurationDefaultPushSource;
            }

            if (!String.IsNullOrEmpty(source))
            {
                source = SourceProvider.ResolveAndValidateSource(source);
            }
            else
            {
                source = packagePath.EndsWith(PackCommand.SymbolsExtension, StringComparison.OrdinalIgnoreCase)
                    ? NuGetConstants.DefaultSymbolServerUrl
                    : NuGetConstants.DefaultGalleryServerUrl;
            }
            return source;
        }

        private void PushSymbols(string packagePath, TimeSpan timeout)
        {
            // Get the symbol package for this package
            string symbolPackagePath = GetSymbolsPath(packagePath);

            // Push the symbols package if it exists
            if (File.Exists(symbolPackagePath))
            {
                string source = NuGetConstants.DefaultSymbolServerUrl;

                // See if the api key exists
                string apiKey = GetApiKey(source);

                if (String.IsNullOrEmpty(apiKey))
                {
                    Console.WriteWarning(
                        LocalizedResourceManager.GetString("Warning_SymbolServerNotConfigured"), 
                        Path.GetFileName(symbolPackagePath), 
                        LocalizedResourceManager.GetString("DefaultSymbolServer"));
                }
                PushPackage(symbolPackagePath, source, apiKey, timeout);
            }
        }

        /// <summary>
        /// Get the symbols package from the original package. Removes the .nupkg and adds .symbols.nupkg
        /// </summary>
        private static string GetSymbolsPath(string packagePath)
        {
            string symbolPath = Path.GetFileNameWithoutExtension(packagePath) + PackCommand.SymbolsExtension;
            string packageDir = Path.GetDirectoryName(packagePath);
            return Path.Combine(packageDir, symbolPath);
        }

        private void PushPackage(string packagePath, string source, string apiKey, TimeSpan timeout)
        {
            var packageServer = new PackageServer(source, CommandLineConstants.UserAgent);
            packageServer.SendingRequest += (sender, e) =>
            {
                Console.LogDebug(String.Format(CultureInfo.CurrentCulture, "{0} {1}", e.Request.Method, e.Request.RequestUri));
            };

            IEnumerable<string> packagesToPush = GetPackagesToPush(packagePath);

            EnsurePackageFileExists(packagePath, packagesToPush);

            foreach (string packageToPush in packagesToPush)
            {
                PushPackageCore(source, apiKey, packageServer, packageToPush, timeout);
            }
        }

        private void PushPackageCore(string source, string apiKey, PackageServer packageServer, string packageToPush, TimeSpan timeout)
        {
            // Push the package to the server
            IPackage package;
            var sourceUri = new Uri(source);
            package = new OptimizedZipPackage(packageToPush);

            string sourceName = CommandLineUtility.GetSourceDisplayName(source);
            Console.WriteLine(LocalizedResourceManager.GetString("PushCommandPushingPackage"), package.GetFullName(), sourceName);

            packageServer.PushPackage(
                apiKey,
                package,
                new FileInfo(packageToPush).Length,
                Convert.ToInt32(timeout.TotalMilliseconds),
                DisableBuffering);
            Console.WriteLine(LocalizedResourceManager.GetString("PushCommandPackagePushed"));
        }

        private static IEnumerable<string> GetPackagesToPush(string packagePath)
        {
            // Ensure packagePath ends with *.nupkg
            packagePath = EnsurePackageExtension(packagePath);
            return PathResolver.PerformWildcardSearch(Environment.CurrentDirectory, packagePath);
        }

        internal static string EnsurePackageExtension(string packagePath)
        {
            if (packagePath.IndexOf('*') == -1)
            {
                // If there's no wildcard in the path to begin with, assume that it's an absolute path.
                return packagePath;
            }
            // If the path does not contain wildcards, we need to add *.nupkg to it.
            if (!packagePath.EndsWith(Constants.PackageExtension, StringComparison.OrdinalIgnoreCase))
            {
                if (packagePath.EndsWith("**", StringComparison.OrdinalIgnoreCase))
                {
                    packagePath = packagePath + Path.DirectorySeparatorChar + '*';
                }
                else if (!packagePath.EndsWith("*", StringComparison.OrdinalIgnoreCase))
                {
                    packagePath = packagePath + '*';
                }
                packagePath = packagePath + Constants.PackageExtension;
            }
            return packagePath;
        }

        private static void EnsurePackageFileExists(string packagePath, IEnumerable<string> packagesToPush)
        {
            if (!packagesToPush.Any())
            {
                throw new CommandLineException(String.Format(CultureInfo.CurrentCulture, LocalizedResourceManager.GetString("UnableToFindFile"), packagePath));
            }
        }

        private string GetApiKey(string source)
        {
            if (!String.IsNullOrEmpty(ApiKey))
            {
                return ApiKey;
            }

            string apiKey = null;

            // Second argument, if present, should be the API Key
            if (Arguments.Count > 1)
            {
                apiKey = Arguments[1];
            }

            // If the user did not pass an API Key look in the config file
            if (String.IsNullOrEmpty(apiKey))
            {
                apiKey = SettingsUtility.GetDecryptedValue(Settings, "apikeys", source);
            }

            return apiKey;
        }

        /// <summary>
        /// Indicates whether the specified source is a file source, such as: \\a\b, c:\temp, etc.
        /// </summary>
        /// <param name="source">The source to test.</param>
        /// <returns>true if the source is a file source; otherwise, false.</returns>
        private static bool IsFileSource(string source)
        {
            Uri uri;
            if (Uri.TryCreate(source, UriKind.RelativeOrAbsolute, out uri))
            {
                return uri.IsFile;
            }
            else
            {
                return false;
            }
        }
    }
}