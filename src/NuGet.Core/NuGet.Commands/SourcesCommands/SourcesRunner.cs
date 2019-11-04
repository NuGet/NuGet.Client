// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;

namespace NuGet.Commands
{
    public enum SourcesListFormat
    {
        Detailed,
        Short
    }

    /// <summary>
    /// Shared code to run the "sources" command from the command line projects
    /// </summary>
    public static class SourcesRunner
    {
        public static void Run(SourcesArgs args)
        {
            if (args.SourceProvider == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                                                                    Strings.Error_SourceProviderIsNull));
            }

            switch (args.Action)
            {
                case "":
                case null:
                case "list":
                    switch (args.Format)
                    {
                        case "short":
                            PrintRegisteredSourcesShort(args);
                            break;
                        case "detailed":
                        default:
                            PrintRegisteredSourcesDetailed(args);
                            break;
                    }
                    break;
                case "add":
                    AddNewSource(args);
                    break;
                case "remove":
                    RemoveSource(args);
                    break;
                case "enable":
                    EnableOrDisableSource(args, enabled: true);
                    break;
                case "disable":
                    EnableOrDisableSource(args, enabled: false);
                    break;
                case "update":
                    UpdatePackageSource(args);
                    break;
            }
        }

        private static void EnableOrDisableSource(SourcesArgs args, bool enabled)
        {
            if (string.IsNullOrEmpty(args.Name))
            {
                args.LogError(string.Format(CultureInfo.CurrentCulture,
                    Strings.SourcesCommandNameRequired));
            }

            var packageSource = args.SourceProvider.GetPackageSourceByName(args.Name);
            if (packageSource == null)
            {
                args.LogError(string.Format(CultureInfo.CurrentCulture,
                    Strings.SourcesCommandNoMatchingSourcesFound, args.Name));
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
                Console.WriteLine(string.Format(CultureInfo.CurrentCulture,
                    Strings.SourcesCommandSourceEnabledSuccessfully, args.Name));
            }
            else
            {
                Console.WriteLine(string.Format(CultureInfo.CurrentCulture,
                    Strings.SourcesCommandSourceDisabledSuccessfully, args.Name));
            }
        }

        private static void RemoveSource(SourcesArgs args)
        {
            if (string.IsNullOrEmpty(args.Name))
            {
                args.LogError(string.Format(CultureInfo.CurrentCulture,
                    Strings.SourcesCommandNameRequired));
            }

            // Check to see if we already have a registered source with the same name or source
            var sourceList = args.SourceProvider.LoadPackageSources().ToList();

            var source = args.SourceProvider.GetPackageSourceByName(args.Name);
            if (source == null)
            {
                args.LogError(string.Format(CultureInfo.CurrentCulture,
                    Strings.SourcesCommandNoMatchingSourcesFound, args.Name));
            }

            args.SourceProvider.RemovePackageSource(args.Name);
            Console.WriteLine(string.Format(CultureInfo.CurrentCulture,
                Strings.SourcesCommandSourceRemovedSuccessfully, args.Name));
        }

        private static void AddNewSource(SourcesArgs args)
        {
            if (string.IsNullOrEmpty(args.Name))
            {
                args.LogError(string.Format(CultureInfo.CurrentCulture,
                                Strings.SourcesCommandNoMatchingSourcesFound));
            }
            if (string.Equals(args.Name, Strings.ReservedPackageNameAll))
            {
                args.LogError(string.Format(CultureInfo.CurrentCulture,
                                Strings.SourcesCommandAllNameIsReserved));
            }
            if (string.IsNullOrEmpty(args.Source))
            {
                args.LogError(string.Format(CultureInfo.CurrentCulture,
                                Strings.SourcesCommandSourceRequired));
            }

            // Make sure that the Source given is a valid one.
            if (!PathValidator.IsValidSource(args.Source))
            {
                args.LogError(string.Format(CultureInfo.CurrentCulture,
                                 Strings.SourcesCommandInvalidSource));
            }

            ValidateCredentials(args);

            // Check to see if we already have a registered source with the same name or source
            var existingSourceWithName = args.SourceProvider.GetPackageSourceByName(args.Name);
            if (existingSourceWithName != null)
            {
                args.LogError(string.Format(CultureInfo.CurrentCulture,
                                Strings.SourcesCommandUniqueName));
            }
            var existingSourceWithSource = args.SourceProvider.GetPackageSourceBySource(args.Source);
            if (existingSourceWithSource != null)
            {
                args.LogError(string.Format(CultureInfo.CurrentCulture,
                                Strings.SourcesCommandUniqueSource));
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
            Console.WriteLine(string.Format(CultureInfo.CurrentCulture,
                        Strings.SourcesCommandSourceAddedSuccessfully, args.Name));
        }

        private static void UpdatePackageSource(SourcesArgs args)
        {
            if (string.IsNullOrEmpty(args.Name))
            {
                args.LogError(string.Format(CultureInfo.CurrentCulture,
                                Strings.SourcesCommandNameRequired));
            }

            var existingSource = args.SourceProvider.GetPackageSourceByName(args.Name);
            if (existingSource == null)
            {
                args.LogError(string.Format(CultureInfo.CurrentCulture,
                                Strings.SourcesCommandNoMatchingSourcesFound, args.Name));
            }

            if (!string.IsNullOrEmpty(args.Source) && !existingSource.Source.Equals(args.Source, StringComparison.OrdinalIgnoreCase))
            {
                if (!PathValidator.IsValidSource(args.Source))
                {
                    args.LogError(string.Format(CultureInfo.CurrentCulture,
                                Strings.SourcesCommandInvalidSource));
                }

                // If the user is updating the source, verify we don't have a duplicate.
                var duplicateSource = args.SourceProvider.GetPackageSourceBySource(args.Source);
                if (duplicateSource != null)
                {
                    args.LogError(string.Format(CultureInfo.CurrentCulture,
                                Strings.SourcesCommandUniqueSource));
                }

                existingSource = new Configuration.PackageSource(args.Source, existingSource.Name);
            }

            ValidateCredentials(args);

            if (!string.IsNullOrEmpty(args.Username))
            {
                var hasExistingAuthTypes = existingSource.Credentials?.ValidAuthenticationTypes.Any() ?? false;
                if (hasExistingAuthTypes && string.IsNullOrEmpty(args.ValidAuthenticationTypes))
                {
                    Console.WriteLine(string.Format(CultureInfo.CurrentCulture,
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

            Console.WriteLine(string.Format(CultureInfo.CurrentCulture,
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
                args.LogError(string.Format(CultureInfo.CurrentCulture,
                                Strings.SourcesCommandCredentialsRequired));
            }

            if (isPasswordEmpty && !isAuthTypesEmpty)
            {
                // can't specify auth types without credentials
                args.LogError(string.Format(CultureInfo.CurrentCulture,
                                Strings.SourcesCommandCredentialsRequiredWithAuthTypes));
            }


        }

        private static void PrintRegisteredSourcesShort(SourcesArgs args)
        {
            var sourcesList = args.SourceProvider.LoadPackageSources().ToList();
            ValidateSources(sourcesList);

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
                Console.WriteLine(legend + source.Source);
                OutputWarningsForSource(args, source);
            }
        }

        private static void PrintRegisteredSourcesDetailed(SourcesArgs args)
        {
            var sourcesList = args.SourceProvider.LoadPackageSources().ToList();
            if (!sourcesList.Any())
            {
                Console.WriteLine(string.Format(CultureInfo.CurrentCulture,
                                        Strings.SourcesCommandNoSources));
                return;
            }

            // TODO: printjustified 0 ??? -- right translation?
            Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.SourcesCommandRegisteredSources));
            var sourcePadding = new string(' ', 6);
            ValidateSources(sourcesList);
            for (var i = 0; i < sourcesList.Count; i++)
            {
                var source = sourcesList[i];
                var indexNumber = i + 1;
                var namePadding = new string(' ', i >= 9 ? 1 : 2);
                Console.WriteLine(string.Format(
                    "  {0}.{1}{2} [{3}]",
                    indexNumber,
                    namePadding,
                    source.Name,
                    source.IsEnabled ? string.Format(CultureInfo.CurrentCulture, Strings.SourcesCommandEnabled) : string.Format(CultureInfo.CurrentCulture, Strings.SourcesCommandDisabled)));
                Console.WriteLine(string.Format("{0}{1}", sourcePadding, source.Source));
                OutputWarningsForSource(args, source);
            }
        }

        private static void OutputWarningsForSource(SourcesArgs args, PackageSource source)
        {
            foreach (var logCode in source.LogCodes)
            {
                switch (logCode)
                {
                    case NuGetLogCode.NU6500:
                        args.LogWarning(logCode.ToString() + $" : nuget v2 feed is less secure than nuget v3 feed. Please change '{source.Source}' to '" + NuGetConstants.V3FeedUrl + "'");
                        break;
                    case NuGetLogCode.NU6501:
                        args.LogWarning(logCode.ToString() + " : http feeds are not secure, change your '" + source.Source + "' feed to use https protocol");
                        break;
                    case NuGetLogCode.NU6502:
                        args.LogInformation(logCode.ToString() + " : local v3 feeds are faster than v2 feeds");
                        break;
                    case NuGetLogCode.NU6503:
                        args.LogWarning(logCode.ToString() + $" : '{source.Source}' was already defined with a previous entry");
                        break;
                    case NuGetLogCode.NU6504:
                        args.LogWarning(logCode.ToString() + $" : Local feed '{source.Source}' does not exist or is not reachable");
                        break;
                }
            }

            if (source.LogCodes.Any())
            {
                Console.WriteLine();
            }
        }

        private static void ValidateSources(List<PackageSource> sources)
        {
            HashSet<string> sourceStrings = new HashSet<string>();
            foreach (var source in sources)
            {
                var logCodes = source.LogCodes;

                if (source.IsEnabled)
                {
                    if (source.IsLocal && LocalFolderUtility.GetLocalFeedType(source.Source, NullLogger.Instance) == FeedType.FileSystemV2)
                    {
                        logCodes.Add(NuGetLogCode.NU6502);
                    }

                    if (source.IsHttp && HttpClient)

                    // Check for NU6503 - duplicate Sources
                    if (!sourceStrings.Add(source.Source))
                    {
                        logCodes.Add(NuGetLogCode.NU6503);
                    }
                }
            }
        }
    }
}
