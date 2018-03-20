using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;

namespace NuGet.CommandLine
{
    public enum SourcesListFormat
    {
        Detailed,
        Short
    }

    [Command(typeof(NuGetCommand), "sources", "SourcesCommandDescription", UsageSummaryResourceName = "SourcesCommandUsageSummary",
        MinArgs = 0, MaxArgs = 1)]
    public class SourcesCommand : Command
    {
        [Option(typeof(NuGetCommand), "SourcesCommandNameDescription")]
        public string Name { get; set; }

        [Option(typeof(NuGetCommand), "SourcesCommandSourceDescription", AltName = "src")]
        public string Source { get; set; }

        [Option(typeof(NuGetCommand), "SourcesCommandUserNameDescription")]
        public string UserName { get; set; }

        [Option(typeof(NuGetCommand), "SourcesCommandPasswordDescription")]
        public string Password { get; set; }

        [Option(typeof(NuGetCommand), "SourcesCommandStorePasswordInClearTextDescription")]
        public bool StorePasswordInClearText { get; set; }

        [Option(typeof(NuGetCommand), "SourcesCommandFormatDescription")]
        public SourcesListFormat Format { get; set; }

        [Option(typeof(NuGetCommand), "SourcesCommandTrustDescription")]
        public bool Trust { get; set; }

        public override async Task ExecuteCommandAsync()
        {
            if (SourceProvider == null)
            {
                throw new InvalidOperationException(LocalizedResourceManager.GetString("Error_SourceProviderIsNull"));
            }

            // Convert to update
            var action = Arguments.FirstOrDefault();

            // TODO: Change these in to switches so we don't have to parse them here.
            if (string.IsNullOrEmpty(action) || action.Equals("List", StringComparison.OrdinalIgnoreCase))
            {
                switch (Format)
                {
                    case SourcesListFormat.Short:
                        PrintRegisteredSourcesShort();
                        break;
                    default:
                        PrintRegisteredSourcesDetailed();
                        break;
                }
            }
            else if (action.Equals("Add", StringComparison.OrdinalIgnoreCase))
            {
                await AddNewSourceAsync();
            }
            else if (action.Equals("Remove", StringComparison.OrdinalIgnoreCase))
            {
                RemoveSource();
            }
            else if (action.Equals("Enable", StringComparison.OrdinalIgnoreCase))
            {
                EnableOrDisableSource(enabled: true);
            }
            else if (action.Equals("Disable", StringComparison.OrdinalIgnoreCase))
            {
                EnableOrDisableSource(enabled: false);
            }
            else if (action.Equals("Update", StringComparison.OrdinalIgnoreCase))
            {
                await UpdatePackageSourceAsync();
            }
        }

        private void EnableOrDisableSource(bool enabled)
        {
            if (string.IsNullOrEmpty(Name))
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandNameRequired"));
            }

            var sourceList = SourceProvider.LoadPackageSources().ToList();
            var existingSource = sourceList.Where(ps => string.Equals(Name, ps.Name, StringComparison.OrdinalIgnoreCase));
            if (!existingSource.Any())
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandNoMatchingSourcesFound"), Name);
            }

            foreach (var source in existingSource)
            {
                source.IsEnabled = enabled;
            }

            SourceProvider.SavePackageSources(sourceList);
            Console.WriteLine(
                enabled ? LocalizedResourceManager.GetString("SourcesCommandSourceEnabledSuccessfully") : LocalizedResourceManager.GetString("SourcesCommandSourceDisabledSuccessfully"),
                Name);
        }

        private void RemoveSource()
        {
            if (string.IsNullOrEmpty(Name))
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandNameRequired"));
            }
            // Check to see if we already have a registered source with the same name or source
            var sourceList = SourceProvider.LoadPackageSources().ToList();
            var matchingSources = sourceList.Where(ps => string.Equals(Name, ps.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!matchingSources.Any())
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandNoMatchingSourcesFound"), Name);
            }

            if (Trust)
            {
                matchingSources.ForEach(p => SourceProvider.DeleteTrustedSource(p.Name));
            }
            else
            {
                var trustedSourceList = matchingSources.Where(p => p.TrustedSource != null).Select(p => UpdateServiceIndexTrustedSource(p));
                SourceProvider.SaveTrustedSources(trustedSourceList);
            }

            sourceList.RemoveAll(matchingSources.Contains);
            SourceProvider.SavePackageSources(sourceList);
            Console.WriteLine(LocalizedResourceManager.GetString("SourcesCommandSourceRemovedSuccessfully"), Name);
        }

        private TrustedSource UpdateServiceIndexTrustedSource(PackageSource source)
        {
            if (source.TrustedSource != null)
            {
                var trustedSource = source.TrustedSource;
                trustedSource.ServiceIndex = new ServiceIndexTrustEntry(source.Source);

                return trustedSource;
            }

            return null;
        }

        private async Task AddNewSourceAsync()
        {
            if (string.IsNullOrEmpty(Name))
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandNameRequired"));
            }
            if (string.Equals(Name, LocalizedResourceManager.GetString("ReservedPackageNameAll")))
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandAllNameIsReserved"));
            }
            if (string.IsNullOrEmpty(Source))
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandSourceRequired"));
            }
            // Make sure that the Source given is a valid one.
            if (!PathValidator.IsValidSource(Source))
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandInvalidSource"));
            }

            ValidateCredentials();

            // Check to see if we already have a registered source with the same name or source
            var sourceList = SourceProvider.LoadPackageSources().ToList();
            var hasName = sourceList.Any(ps => string.Equals(Name, ps.Name, StringComparison.OrdinalIgnoreCase));
            if (hasName)
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandUniqueName"));
            }
            var hasSource = sourceList.Any(ps => string.Equals(Source, ps.Source, StringComparison.OrdinalIgnoreCase));
            if (hasSource)
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandUniqueSource"));
            }

            var newPackageSource = new Configuration.PackageSource(Source, Name);

            if (!string.IsNullOrEmpty(UserName))
            {
                var credentials = Configuration.PackageSourceCredential.FromUserInput(Name, UserName, Password, StorePasswordInClearText);
                newPackageSource.Credentials = credentials;
            }

            if (Trust)
            {
                await UpdateTrustedSourceAsync(newPackageSource);
            }

            sourceList.Add(newPackageSource);
            SourceProvider.SavePackageSources(sourceList);
            Console.WriteLine(LocalizedResourceManager.GetString("SourcesCommandSourceAddedSuccessfully"), Name);
        }

        private async Task UpdatePackageSourceAsync()
        {
            if (string.IsNullOrEmpty(Name))
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandNameRequired"));
            }

            var sourceList = SourceProvider.LoadPackageSources().ToList();
            var existingSourceIndex = sourceList.FindIndex(ps => Name.Equals(ps.Name, StringComparison.OrdinalIgnoreCase));
            if (existingSourceIndex == -1)
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandNoMatchingSourcesFound"), Name);
            }
            var existingSource = sourceList[existingSourceIndex];

            if (!string.IsNullOrEmpty(Source) && !existingSource.Source.Equals(Source, StringComparison.OrdinalIgnoreCase))
            {
                if (!PathValidator.IsValidSource(Source))
                {
                    throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandInvalidSource"));
                }

                // If the user is updating the source, verify we don't have a duplicate.
                var duplicateSource = sourceList.Any(ps => string.Equals(Source, ps.Source, StringComparison.OrdinalIgnoreCase));
                if (duplicateSource)
                {
                    throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandUniqueSource"));
                }
                existingSource = new Configuration.PackageSource(Source, existingSource.Name);
            }

            ValidateCredentials();

            sourceList.RemoveAt(existingSourceIndex);

            if (Trust)
            {
                await UpdateTrustedSourceAsync(existingSource);
            }

            if (!string.IsNullOrEmpty(UserName))
            {
                var credentials = Configuration.PackageSourceCredential.FromUserInput(Name, UserName, Password,
                    storePasswordInClearText: StorePasswordInClearText);
                existingSource.Credentials = credentials;
            }

            sourceList.Insert(existingSourceIndex, existingSource);
            SourceProvider.SavePackageSources(sourceList);
            Console.WriteLine(LocalizedResourceManager.GetString("SourcesCommandUpdateSuccessful"), Name);
        }

        private async Task UpdateTrustedSourceAsync(PackageSource packageSource)
        {
            var sourceRepositoryProvider = new CommandLineSourceRepositoryProvider(SourceProvider);
            var repositorySignatureResource = await sourceRepositoryProvider.CreateRepository(packageSource).GetResourceAsync<RepositorySignatureResource>() ??
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandSourceNotSupportRepoSign"), packageSource.Name);

            var trustedSource = new TrustedSource(packageSource.Name);

            foreach (var cert in repositorySignatureResource.RepositoryCertificateInfos)
            {
                foreach (var fingerprint in cert.Fingerprints)
                {
                    trustedSource.Certificates.Add(new CertificateTrustEntry(fingerprint.Value, cert.Subject, CryptoHashUtility.OidToHashAlgorithmName(fingerprint.Key)));
                }
            }

            packageSource.TrustedSource = trustedSource;
        }

        private void ValidateCredentials()
        {
            var userNameEmpty = string.IsNullOrEmpty(UserName);
            var passwordEmpty = string.IsNullOrEmpty(Password);

            if (userNameEmpty ^ passwordEmpty)
            {
                // If only one of them is set, throw.
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandCredentialsRequired"));
            }
        }

        private void PrintRegisteredSourcesShort()
        {
            foreach (var source in SourceProvider.LoadPackageSources())
            {
                Console.Write(source.IsEnabled ? 'E' : 'D');
                if (source.IsMachineWide)
                {
                    Console.Write('M');
                }
                if (source.IsOfficial)
                {
                    Console.Write('O');
                }
                Console.Write(' ');
                Console.WriteLine(source.Source);
            }
        }

        private void PrintRegisteredSourcesDetailed()
        {
            var sourcesList = SourceProvider.LoadPackageSources().ToList();
            if (!sourcesList.Any())
            {
                Console.WriteLine(LocalizedResourceManager.GetString("SourcesCommandNoSources"));
                return;
            }
            Console.PrintJustified(0, LocalizedResourceManager.GetString("SourcesCommandRegisteredSources"));
            Console.WriteLine();
            var sourcePadding = new string(' ', 6);
            for (var i = 0; i < sourcesList.Count; i++)
            {
                var source = sourcesList[i];
                var indexNumber = i + 1;
                var namePadding = new string(' ', i >= 9 ? 1 : 2);
                Console.WriteLine(
                    "  {0}.{1}{2} [{3}]",
                    indexNumber,
                    namePadding,
                    source.Name,
                    source.IsEnabled ? LocalizedResourceManager.GetString("SourcesCommandEnabled") : LocalizedResourceManager.GetString("SourcesCommandDisabled"));
                Console.WriteLine("{0}{1}", sourcePadding, source.Source);
            }
        }
    }
}