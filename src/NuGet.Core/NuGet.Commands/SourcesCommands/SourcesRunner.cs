// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using NuGet.Common;

namespace NuGet.Commands
{
    /// <summary>
    /// Shared code to run the "sources" command from the command line projects
    /// </summary>
    public static class SourcesRunner
    {
        public static void Run(SourcesArgs args)
        {
            switch (args.Action)
            {
                case SourcesAction.List:
                    switch (args.Format)
                    {
                        case SourcesListFormat.Short:
                            PrintRegisteredSourcesShort(args);
                            break;
                        case SourcesListFormat.Detailed:
                        default:
                            PrintRegisteredSourcesDetailed(args);
                            break;
                    }
                    break;
                case SourcesAction.Add:
                    AddNewSource(args);
                    break;
                case SourcesAction.Remove:
                    RemoveSource(args);
                    break;
                case SourcesAction.Enable:
                    EnableOrDisableSource(args, enabled: true);
                    break;
                case SourcesAction.Disable:
                    EnableOrDisableSource(args, enabled: false);
                    break;
                case SourcesAction.Update:
                    UpdatePackageSource(args);
                    break;
                default: // args.Action must be one of the values above.
                    throw new InvalidEnumArgumentException("Action");
            }
        }

        private static void EnableOrDisableSource(SourcesArgs args, bool enabled)
        {
            if (string.IsNullOrEmpty(args.Name))
            {
                throw new CommandException(Strings.SourcesCommandNameRequired);
            }

            var packageSource = args.SourceProvider.GetPackageSourceByName(args.Name);
            if (packageSource == null)
            {
                throw new CommandException(Strings.SourcesCommandNoMatchingSourcesFound, args.Name);
            }

            if (enabled && !packageSource.IsEnabled)
            {
                args.SourceProvider.EnablePackageSource(args.Name);
            }
            else if (!enabled && packageSource.IsEnabled)
            {
                args.SourceProvider.DisablePackageSource(args.Name);
            }

            if (enabled)
            {
                args.LogMinimal(string.Format(CultureInfo.CurrentCulture,
                    Strings.SourcesCommandSourceEnabledSuccessfully, args.Name));
            }
            else
            {
                args.LogMinimal(string.Format(CultureInfo.CurrentCulture,
                    Strings.SourcesCommandSourceDisabledSuccessfully, args.Name));
            }
        }

        private static void RemoveSource(SourcesArgs args)
        {
            if (string.IsNullOrEmpty(args.Name))
            {
                throw new CommandException(Strings.SourcesCommandNameRequired);
            }

            // Check to see if we already have a registered source with the same name or source
            var source = args.SourceProvider.GetPackageSourceByName(args.Name);
            if (source == null)
            {
                throw new CommandException(Strings.SourcesCommandNoMatchingSourcesFound, args.Name);
            }

            args.SourceProvider.RemovePackageSource(args.Name);
            args.LogMinimal(string.Format(CultureInfo.CurrentCulture,
                Strings.SourcesCommandSourceRemovedSuccessfully, args.Name));
        }

        private static void AddNewSource(SourcesArgs args)
        {
            if (string.IsNullOrEmpty(args.Name))
            {
                throw new CommandException(Strings.SourcesCommandNoMatchingSourcesFound);
            }

            if (string.Equals(args.Name, Strings.ReservedPackageNameAll))
            {
                throw new CommandException(Strings.SourcesCommandAllNameIsReserved);
            }

            if (string.IsNullOrEmpty(args.Source))
            {
                throw new CommandException(Strings.SourcesCommandSourceRequired);
            }

            // Make sure that the Source given is a valid one.
            if (!PathValidator.IsValidSource(args.Source))
            {
                throw new CommandException(Strings.SourcesCommandInvalidSource);
            }

            ValidateCredentials(args);

            // Check to see if we already have a registered source with the same name or source
            var existingSourceWithName = args.SourceProvider.GetPackageSourceByName(args.Name);
            if (existingSourceWithName != null)
            {
                throw new CommandException(Strings.SourcesCommandUniqueName);
            }
            var existingSourceWithSource = args.SourceProvider.GetPackageSourceBySource(args.Source);
            if (existingSourceWithSource != null)
            {
                throw new CommandException(Strings.SourcesCommandUniqueSource);
            }

            var newPackageSource = new Configuration.PackageSource(args.Source, args.Name);

            if (!string.IsNullOrEmpty(args.Username))
            {
                var credentials = Configuration.PackageSourceCredential.FromUserInput(
                    args.Name,
                    args.Username,
                    args.Password,
                    args.StorePasswordInClearText,
                    args.ValidAuthenticationTypes);
                newPackageSource.Credentials = credentials;
            }

            args.SourceProvider.AddPackageSource(newPackageSource);
            args.LogMinimal(string.Format(CultureInfo.CurrentCulture,
                    Strings.SourcesCommandSourceAddedSuccessfully, args.Name));
        }

        private static void UpdatePackageSource(SourcesArgs args)
        {
            if (string.IsNullOrEmpty(args.Name))
            {
                throw new CommandException(Strings.SourcesCommandNameRequired);
            }

            var existingSource = args.SourceProvider.GetPackageSourceByName(args.Name);
            if (existingSource == null)
            {
                throw new CommandException(Strings.SourcesCommandNoMatchingSourcesFound, args.Name);
            }

            if (!string.IsNullOrEmpty(args.Source) && !existingSource.Source.Equals(args.Source, StringComparison.OrdinalIgnoreCase))
            {
                if (!PathValidator.IsValidSource(args.Source))
                {
                    throw new CommandException(Strings.SourcesCommandInvalidSource);
                }

                // If the user is updating the source, verify we don't have a duplicate.
                var duplicateSource = args.SourceProvider.GetPackageSourceBySource(args.Source);
                if (duplicateSource != null)
                {
                    throw new CommandException(Strings.SourcesCommandUniqueSource);
                }

                existingSource = new Configuration.PackageSource(args.Source, existingSource.Name);
            }

            ValidateCredentials(args);

            if (!string.IsNullOrEmpty(args.Username))
            {
                var hasExistingAuthTypes = existingSource.Credentials?.ValidAuthenticationTypes.Any() ?? false;
                if (hasExistingAuthTypes && string.IsNullOrEmpty(args.ValidAuthenticationTypes))
                {
                    args.LogMinimal(string.Format(CultureInfo.CurrentCulture,
                        Strings.SourcesCommandClearingExistingAuthTypes, args.Name));
                }

                var credentials = Configuration.PackageSourceCredential.FromUserInput(
                    args.Name,
                    args.Username,
                    args.Password,
                    args.StorePasswordInClearText,
                    args.ValidAuthenticationTypes);
                existingSource.Credentials = credentials;
            }

            args.SourceProvider.UpdatePackageSource(existingSource, updateCredentials: existingSource.Credentials != null, updateEnabled: false);

            args.LogMinimal(string.Format(CultureInfo.CurrentCulture,
                    Strings.SourcesCommandUpdateSuccessful, args.Name));
        }

        private static void ValidateCredentials(SourcesArgs args)
        {
            var isUsernameEmpty = string.IsNullOrEmpty(args.Username);
            var isPasswordEmpty = string.IsNullOrEmpty(args.Password);
            var isAuthTypesEmpty = string.IsNullOrEmpty(args.ValidAuthenticationTypes);

            if (isUsernameEmpty ^ isPasswordEmpty)
            {
                // If only one of them is set, throw.
                throw new CommandException(Strings.SourcesCommandCredentialsRequired);
            }

            if (isPasswordEmpty && !isAuthTypesEmpty)
            {
                // can't specify auth types without credentials
                throw new CommandException(Strings.SourcesCommandCredentialsRequiredWithAuthTypes);
            }
        }

        private static void PrintRegisteredSourcesShort(SourcesArgs args)
        {
            var sourcesList = args.SourceProvider.LoadPackageSources();

            foreach (var source in sourcesList)
            {
                string legend = source.IsEnabled ? "E" : "D";

                if (source.IsMachineWide)
                {
                    legend += "M";
                }
                if (source.IsOfficial)
                {
                    legend += "O";
                }
                legend += " ";
                args.LogMinimal(legend + source.Source);
            }
        }

        private static void PrintRegisteredSourcesDetailed(SourcesArgs args)
        {
            var sourcesList = args.SourceProvider.LoadPackageSources().ToList();
            if (!sourcesList.Any())
            {
                args.LogMinimal(string.Format(CultureInfo.CurrentCulture,
                                    Strings.SourcesCommandNoSources));

                return;
            }

            args.LogMinimal(string.Format(CultureInfo.CurrentCulture, Strings.SourcesCommandRegisteredSources));
            var sourcePadding = new string(' ', 6);
            for (var i = 0; i < sourcesList.Count; i++)
            {
                var source = sourcesList[i];
                var indexNumber = i + 1;
                var namePadding = new string(' ', i >= 9 ? 1 : 2);

                args.LogMinimal(string.Format(
                    "  {0}.{1}{2} [{3}]",
                    indexNumber,
                    namePadding,
                    source.Name,
                    source.IsEnabled ? string.Format(CultureInfo.CurrentCulture, Strings.SourcesCommandEnabled) : string.Format(CultureInfo.CurrentCulture, Strings.SourcesCommandDisabled)));
                args.LogMinimal(string.Format("{0}{1}", sourcePadding, source.Source));
            }
        }
    }
}
