// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;

namespace NuGet.CommandLine.XPlat.Commands.Why
{
    public static class WhyCommand
    {
        internal static void Register(CommandLineApplication app)
        {
            app.Command("why", whyCmd =>
            {
                whyCmd.Description = Strings.WhyCommand_Description;
            });
        }

        internal static void Register(Command rootCommand, Func<ILoggerWithColor> getLogger)
        {
            Register(rootCommand, getLogger, WhyCommandRunner.ExecuteCommand);
        }

        /// <summary>
        /// This is a temporary API until NuGet migrates all our commands to System.CommandLine, at which time I suspect we'll have a NuGetParser.GetNuGetCommand for all the `dotnet nuget *` commands.
        /// For now, this allows the dotnet CLI to invoke why directly, instead of running NuGet.CommandLine.XPlat as a child process.
        /// </summary>
        /// <param name="rootCommand">The <c>dotnet nuget</c> command handler, to add <c>why</c> to.</param>
        public static void GetWhyCommand(Command rootCommand)
        {
            Register(rootCommand, CommandOutputLogger.Create, WhyCommandRunner.ExecuteCommand);
        }

        internal static void Register(Command rootCommand, Func<ILoggerWithColor> getLogger, Func<WhyCommandArgs, Task<int>> action)
        {
            var whyCommand = new DocumentedCommand("why", Strings.WhyCommand_Description, "https://aka.ms/dotnet/nuget/why");

            Argument<string> path = new Argument<string>("PROJECT|SOLUTION")
            {
                Description = Strings.WhyCommand_PathArgument_Description,
                // We really want this to be zero or one, however, because this is the first argument, it doesn't work.
                // Instead, we need to use a CustomParser to choose if the argument is the path, or the package.
                // In order for the parser to tell us there's more than 1 argument available, we need to tell Argument
                // that it supports more than one, but then in the custom parser we'll make sure we only take at most 1.
                Arity = ArgumentArity.ZeroOrMore,
                CustomParser = ar =>
                {
                    if (HasPathArgument(ar))
                    {
                        var value = ar.Tokens[0];
                        ar.OnlyTake(1);
                        return value.Value;
                    }

                    ar.OnlyTake(0);
                    var currentDirectory = Directory.GetCurrentDirectory();
                    return currentDirectory;

                    bool HasPathArgument(ArgumentResult ar)
                    {
                        // If there's only one argument, it could be the path, or the package.
                        if (ar.Tokens.Count == 1)
                        {
                            var value = ar.Tokens[0].Value;
                            return File.Exists(value) || Directory.Exists(value);
                        }

                        return ar.Tokens.Count > 1;
                    }
                }
            };

            Argument<string> package = new Argument<string>("PACKAGE")
            {
                Description = Strings.WhyCommand_PackageArgument_Description,
                Arity = ArgumentArity.ExactlyOne
            };

            Option<List<string>> frameworks = new Option<List<string>>("--framework", "-f")
            {
                Description = Strings.WhyCommand_FrameworksOption_Description,
                Arity = ArgumentArity.OneOrMore
            };

            Option<string> outputFormat = new Option<string>("--format")
            {
                Description = Strings.WhyCommand_FrameworksOption_Format,
                Arity = ArgumentArity.ZeroOrOne
            };

            Option<string> outputVersion = new Option<string>("--output-version")
            {
                Description = Strings.WhyCommand_FrameworksOption_OutputVersion,
                Arity = ArgumentArity.ZeroOrOne
            };

            HelpOption help = new HelpOption()
            {
                Arity = ArgumentArity.Zero
            };

            whyCommand.Arguments.Add(path);
            whyCommand.Arguments.Add(package);
            whyCommand.Options.Add(frameworks);
            whyCommand.Options.Add(outputFormat);
            whyCommand.Options.Add(outputVersion);
            whyCommand.Options.Add(help);

            whyCommand.SetAction(async (parseResult, cancellationToken) =>
            {
                ILoggerWithColor logger = getLogger();

                try
                {
                    IReportRenderer reportRenderer = GetOutputType(
                        parseResult.GetValue(outputFormat),
                        parseResult.GetValue(outputVersion),
                        logger);

                    WhyCommandArgs whyCommandArgs = new WhyCommandArgs(
                        parseResult.GetValue(path),
                        parseResult.GetValue(package),
                        parseResult.GetValue(frameworks),
                        reportRenderer,
                        logger,
                        cancellationToken);

                    int exitCode = await action(whyCommandArgs);
                    return exitCode;
                }
                catch (ArgumentException ex)
                {
                    logger.LogError(ex.Message);
                    return ExitCodes.InvalidArguments;
                }
            });

            rootCommand.Subcommands.Add(whyCommand);
        }

        private static IReportRenderer GetOutputType(string outputFormatOption, string outputVersionOption, ILoggerWithColor logger)
        {
            ReportOutputFormat outputFormat = ReportOutputFormat.Console;
            if (!string.IsNullOrEmpty(outputFormatOption) &&
                !Enum.TryParse(outputFormatOption, ignoreCase: true, out outputFormat))
            {
                string currentlySupportedFormat = GetEnumValues<ReportOutputFormat>();
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.ListPkg_InvalidOutputFormat,
                        outputFormatOption,
                        currentlySupportedFormat));
            }

            if (outputFormat == ReportOutputFormat.Console)
            {
                if (!string.IsNullOrEmpty(outputVersionOption))
                {
                    throw new ArgumentException(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.ListPkg_OutputVersionNotApplicable));
                }
                return new WhyConsoleRenderer(logger);
            }

            IReportRenderer jsonReportRenderer;

            var currentlySupportedReportVersions = new List<string> { "1" };
            // If customer pass unsupported version then error out instead of defaulting to version probably unsupported by customer machine.
            if (!string.IsNullOrEmpty(outputVersionOption) && !currentlySupportedReportVersions.Contains(outputVersionOption))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.ListPkg_InvalidOutputVersion,
                        outputVersionOption,
                        string.Join(" ,", currentlySupportedReportVersions)));
            }
            else
            {
                jsonReportRenderer = new WhyJsonRenderer(logger);
            }

            return jsonReportRenderer;
        }

        private static string GetEnumValues<T>() where T : Enum
        {
            var enumValues = ((T[])Enum.GetValues(typeof(T)))
               .Select(x => x.ToString());

            return string.Join(", ", enumValues).ToLower(CultureInfo.CurrentCulture);
        }
    }
}
