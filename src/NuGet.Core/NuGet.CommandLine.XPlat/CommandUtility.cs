using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.CommandLine.XPlat
{
    internal class CommandUtility
    {
        // Create a caching source provider with the default settings, the sources will be passed in
        private static Lazy<CachingSourceProvider> _sourceProvider;

        static CommandUtility()
        {
            _sourceProvider = new Lazy<CachingSourceProvider>(() =>
            {
                ISettings settings = Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null);
                PackageSourceProvider packageSourceProvider = new PackageSourceProvider(settings);
                return new CachingSourceProvider(packageSourceProvider);
            });
        }

        public static CachingSourceProvider CachingSourceProvider
        {
            get
            {
                return _sourceProvider.Value;
            }
        }

        public static async Task<PackageUpdateResource> GetPushCommandResource(string source, ISettings settings)
        {
            // Take the passed in source
            IEnumerable<PackageSource> packageSources;
            if (!string.IsNullOrEmpty(source))
            {
                packageSources = new PackageSource[] { new PackageSource(source) };
            }
            else
            {
                var packageSourceProvider = new PackageSourceProvider(settings);

                packageSources = packageSourceProvider.LoadPackageSources().Where(src => src.IsEnabled);
            }

            SourceRepository repo = packageSources.Select(src => CachingSourceProvider.CreateRepository(src))
                .Distinct()
                .ToList().FirstOrDefault();

            if (repo == null)
            {
                throw new InvalidOperationException("We don't have valid repository(TODO use resource strings)");
            }

            return await repo.GetResourceAsync<PackageUpdateResource>();
        }

        public static bool Confirm(bool isNonInteractive, string description)
        {
            if (isNonInteractive)
            {
                return true;
            }

            var currentColor = ConsoleColor.Gray;
            try
            {
                currentColor = System.Console.ForegroundColor;
                System.Console.ForegroundColor = ConsoleColor.Yellow;
                System.Console.Write(String.Format(CultureInfo.CurrentCulture, Strings.ConsoleConfirmMessage, description));
                var result = System.Console.ReadLine();
                return result.StartsWith(Strings.ConsoleConfirmMessageAccept, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                System.Console.ForegroundColor = currentColor;
            }
        }
    }
}
