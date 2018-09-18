// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Common;

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
        public string Username { get; set; }

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
            if (string.IsNullOrEmpty(Name))
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandNameRequired"));
            }

            var packageSource = SourceProvider.GetPackageSourceByName(Name);
            if (packageSource == null)
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandNoMatchingSourcesFound"), Name);
            }

            if (enabled && !packageSource.IsEnabled)
            {
                SourceProvider.EnablePackageSource(Name);
            }
            else if (!enabled && packageSource.IsEnabled)
            {
                SourceProvider.DisablePackageSource(Name);
            }

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

            var source = SourceProvider.GetPackageSourceByName(Name);
            if (source == null)
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandNoMatchingSourcesFound"), Name);
            }

            SourceProvider.RemovePackageSource(Name);
            Console.WriteLine(LocalizedResourceManager.GetString("SourcesCommandSourceRemovedSuccessfully"), Name);
        }

        private void AddNewSource()
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
            var existingSourceWithName = SourceProvider.GetPackageSourceByName(Name);
            if (existingSourceWithName != null)
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandUniqueName"));
            }
            var existingSourceWithSource = SourceProvider.GetPackageSourceBySource(Source);
            if (existingSourceWithSource != null)
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandUniqueSource"));
            }

            var newPackageSource = new Configuration.PackageSource(Source, Name);

            if (!string.IsNullOrEmpty(Username))
            {
                var credentials = Configuration.PackageSourceCredential.FromUserInput(
                    Name,
                    Username,
                    Password,
                    StorePasswordInClearText);
                newPackageSource.Credentials = credentials;
            }

            SourceProvider.AddPackageSource(newPackageSource);
            Console.WriteLine(LocalizedResourceManager.GetString("SourcesCommandSourceAddedSuccessfully"), Name);
        }

        private void UpdatePackageSource()
        {
            if (string.IsNullOrEmpty(Name))
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandNameRequired"));
            }

            var existingSource = SourceProvider.GetPackageSourceByName(Name);
            if (existingSource == null)
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandNoMatchingSourcesFound"), Name);
            }

            if (!string.IsNullOrEmpty(Source) && !existingSource.Source.Equals(Source, StringComparison.OrdinalIgnoreCase))
            {
                if (!PathValidator.IsValidSource(Source))
                {
                    throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandInvalidSource"));
                }

                // If the user is updating the source, verify we don't have a duplicate.
                var duplicateSource = SourceProvider.GetPackageSourceBySource(Source);
                if (duplicateSource != null)
                {
                    throw new CommandLineException(LocalizedResourceManager.GetString("SourcesCommandUniqueSource"));
                }
                existingSource = new Configuration.PackageSource(Source, existingSource.Name);
            }

            ValidateCredentials();

            if (!string.IsNullOrEmpty(Username))
            {
                var credentials = Configuration.PackageSourceCredential.FromUserInput(
                    Name,
                    Username,
                    Password,
                    StorePasswordInClearText);
                existingSource.Credentials = credentials;
            }

            SourceProvider.UpdatePackageSource(existingSource, updateCredentials: existingSource.Credentials != null, updateEnabled: false);

            Console.WriteLine(LocalizedResourceManager.GetString("SourcesCommandUpdateSuccessful"), Name);
        }

        private void ValidateCredentials()
        {
            var isUsernameEmpty = string.IsNullOrEmpty(Username);
            var isPasswordEmpty = string.IsNullOrEmpty(Password);

            if (isUsernameEmpty ^ isPasswordEmpty)
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