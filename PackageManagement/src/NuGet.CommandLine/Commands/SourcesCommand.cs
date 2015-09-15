using System;
using System.Linq;

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

        public override void ExecuteCommand()
        {
            if (SourceProvider == null)
            {
                throw new InvalidOperationException(LocalizedResourceManager.GetString("Error_SourceProviderIsNull"));
            }

            // Convert to update
            var action = Arguments.FirstOrDefault();

            // TODO: Change these in to switches so we don't have to parse them here.
            if (String.IsNullOrEmpty(action) || action.Equals("List", StringComparison.OrdinalIgnoreCase))
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
                AddNewSource();
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
                UpdatePackageSource();
            }
        }

        private void EnableOrDisableSource(bool enabled)
        {
            if (String.IsNullOrEmpty(Name))
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandNameRequired"));
            }

            var sourceList = SourceProvider.LoadPackageSources().ToList();
            var existingSource = sourceList.Where(ps => String.Equals(Name, ps.Name, StringComparison.OrdinalIgnoreCase));
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
            if (String.IsNullOrEmpty(Name))
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandNameRequired"));
            }
            // Check to see if we already have a registered source with the same name or source
            var sourceList = SourceProvider.LoadPackageSources().ToList();
            var matchingSources = sourceList.Where(ps => String.Equals(Name, ps.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!matchingSources.Any())
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandNoMatchingSourcesFound"), Name);
            }

            sourceList.RemoveAll(matchingSources.Contains);
            SourceProvider.SavePackageSources(sourceList);
            Console.WriteLine(LocalizedResourceManager.GetString("SourcesCommandSourceRemovedSuccessfully"), Name);
        }

        private void AddNewSource()
        {
            if (String.IsNullOrEmpty(Name))
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandNameRequired"));
            }
            if (String.Equals(Name, LocalizedResourceManager.GetString("ReservedPackageNameAll")))
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandAllNameIsReserved"));
            }
            if (String.IsNullOrEmpty(Source))
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
            bool hasName = sourceList.Any(ps => String.Equals(Name, ps.Name, StringComparison.OrdinalIgnoreCase));
            if (hasName)
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandUniqueName"));
            }
            bool hasSource = sourceList.Any(ps => String.Equals(Source, ps.Source, StringComparison.OrdinalIgnoreCase));
            if (hasSource)
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandUniqueSource"));
            }

            var newPackageSource = new Configuration.PackageSource(Source, Name) { UserName = UserName, Password = Password, IsPasswordClearText = StorePasswordInClearText };
            sourceList.Add(newPackageSource);
            SourceProvider.SavePackageSources(sourceList);
            Console.WriteLine(LocalizedResourceManager.GetString("SourcesCommandSourceAddedSuccessfully"), Name);
        }

        private void UpdatePackageSource()
        {
            if (String.IsNullOrEmpty(Name))
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandNameRequired"));
            }

            var sourceList = SourceProvider.LoadPackageSources().ToList();
            int existingSourceIndex = sourceList.FindIndex(ps => Name.Equals(ps.Name, StringComparison.OrdinalIgnoreCase));
            if (existingSourceIndex == -1)
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandNoMatchingSourcesFound"), Name);
            }
            var existingSource = sourceList[existingSourceIndex];

            if (!String.IsNullOrEmpty(Source) && !existingSource.Source.Equals(Source, StringComparison.OrdinalIgnoreCase))
            {
                if (!PathValidator.IsValidSource(Source))
                {
                    throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandInvalidSource"));
                }

                // If the user is updating the source, verify we don't have a duplicate.
                bool duplicateSource = sourceList.Any(ps => String.Equals(Source, ps.Source, StringComparison.OrdinalIgnoreCase));
                if (duplicateSource)
                {
                    throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandUniqueSource"));
                }
                existingSource = new Configuration.PackageSource(Source, existingSource.Name);
            }

            ValidateCredentials();

            sourceList.RemoveAt(existingSourceIndex);
            existingSource.UserName = UserName;
            existingSource.Password = Password;
            existingSource.IsPasswordClearText = StorePasswordInClearText;

            sourceList.Insert(existingSourceIndex, existingSource);
            SourceProvider.SavePackageSources(sourceList);
            Console.WriteLine(LocalizedResourceManager.GetString("SourcesCommandUpdateSuccessful"), Name);
        }

        private void ValidateCredentials()
        {
            bool userNameEmpty = String.IsNullOrEmpty(UserName);
            bool passwordEmpty = String.IsNullOrEmpty(Password);

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
            var sourcePadding = new String(' ', 6);
            for (int i = 0; i < sourcesList.Count; i++)
            {
                var source = sourcesList[i];
                var indexNumber = i + 1;
                var namePadding = new String(' ', i >= 9 ? 1 : 2);
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