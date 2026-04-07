// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.CommandLine;
using System.Globalization;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Credentials;

namespace NuGet.CommandLine.XPlat
{
    internal static class DeleteCommand
    {
        private static readonly Option<string> SourceOption = new Option<string>("--source", "-s")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = Strings.Source_Description,
        };

        private static readonly Option<bool> NonInteractiveOption = new Option<bool>("--non-interactive")
        {
            Arity = ArgumentArity.Zero,
            Description = Strings.NonInteractive_Description,
        };

        private static readonly Option<string> ApiKeyOption = new Option<string>("--api-key", "-k")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = Strings.ApiKey_Description,
        };

        private static readonly Option<bool> NoServiceEndpointOption = new Option<bool>("--no-service-endpoint")
        {
            Arity = ArgumentArity.Zero,
            Description = Strings.NoServiceEndpoint_Description,
        };

        private static readonly Option<bool> InteractiveOption = new Option<bool>("--interactive")
        {
            Arity = ArgumentArity.Zero,
            Description = Strings.NuGetXplatCommand_Interactive,
        };

        private static readonly Argument<string> PackageIdArgument = new Argument<string>("PackageId")
        {
            Arity = ArgumentArity.ExactlyOne,
            Description = Strings.Delete_PackageIdAndVersion_Description,
        };

        private static readonly Argument<string> PackageVersionArgument = new Argument<string>("PackageVersion")
        {
            Arity = ArgumentArity.ExactlyOne,
            Description = Strings.Delete_PackageIdAndVersion_Description,
        };

        internal static void Register(Command parent, Func<ILoggerWithColor> getLogger)
        {
            var deleteCmd = new Command("delete", Strings.Delete_Description);

            deleteCmd.Options.Add(SourceOption);
            deleteCmd.Options.Add(NonInteractiveOption);
            deleteCmd.Options.Add(ApiKeyOption);
            deleteCmd.Options.Add(NoServiceEndpointOption);
            deleteCmd.Options.Add(InteractiveOption);
            deleteCmd.Arguments.Add(PackageIdArgument);
            deleteCmd.Arguments.Add(PackageVersionArgument);

            deleteCmd.SetAction(async (parseResult, cancellationToken) =>
            {
                string packageId = parseResult.GetValue(PackageIdArgument);
                string packageVersion = parseResult.GetValue(PackageVersionArgument);
                string sourcePath = parseResult.GetValue(SourceOption);
                string apiKeyValue = parseResult.GetValue(ApiKeyOption);
                bool nonInteractiveValue = parseResult.GetValue(NonInteractiveOption);
                bool noServiceEndpoint = parseResult.GetValue(NoServiceEndpointOption);
                bool interactiveValue = parseResult.GetValue(InteractiveOption);

                DefaultCredentialServiceUtility.SetupDefaultCredentialService(getLogger(), !interactiveValue);

#pragma warning disable CS0618 // Type or member is obsolete
                PackageSourceProvider sourceProvider = new PackageSourceProvider(XPlatUtility.GetSettingsForCurrentWorkingDirectory(), enablePackageSourcesChangedEvent: false);
#pragma warning restore CS0618 // Type or member is obsolete

                await DeleteRunner.Run(
                    sourceProvider.Settings,
                    sourceProvider,
                    packageId,
                    packageVersion,
                    sourcePath,
                    apiKeyValue,
                    nonInteractiveValue,
                    noServiceEndpoint,
                    Confirm,
                    getLogger());

                return 0;
            });

            parent.Subcommands.Add(deleteCmd);
        }

        private static bool Confirm(string description)
        {
            ConsoleColor currentColor = ConsoleColor.Gray;
            try
            {
                currentColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.ConsoleConfirmMessage, description));
                var result = Console.ReadLine();
                return result != null && result.StartsWith(Strings.ConsoleConfirmMessageAccept, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Console.ForegroundColor = currentColor;
            }
        }
    }
}
