// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// Do not manually edit this autogenerated file:
// instead modify the neighboring .tt file (text template) and/or NuGet.CommandLine.Xplat\Commands\SystemCommandLine\Commands.xml (data file),
// then re-execute the text template via "run custom tool" on VS context menu for .tt file, or via dotnet-t4 global tool.

using System;
using System.CommandLine;
using NuGet.Commands;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat.Commands
{
    internal partial class ListVerbParser
    {
        internal static Func<ILogger> GetLoggerFunction;

        internal static Command Register(Command app, Func<ILogger> getLogger)
        {
            var ListCmd = new Command(name: "list", description: Strings.List_Description);

            // Options directly under the verb 'list'

            // noun sub-command: list source
            var SourceCmd = new Command(name: "source", description: Strings.ListSourceCommandDescription);

            // Options under sub-command: list source
            var Format_Option = new Option<string>(name: "--format", description: Strings.SourcesCommandFormatDescription)
            {
                Arity = ArgumentArity.ZeroOrOne,
            };
            SourceCmd.Add(Format_Option);
            var Configfile_Option = new Option<string>(name: "--configfile", description: Strings.Option_ConfigFile)
            {
                Arity = ArgumentArity.ZeroOrOne,
            };
            SourceCmd.Add(Configfile_Option);
            // Create handler delegate handler for SourceCmd
            SourceCmd.SetHandler((
                  format
                , configfile
            ) =>
            {
                var args = new ListSourceArgs()
                {
                    Format = format,
                    Configfile = configfile,
                }; // end of args assignment

                ListSourceRunner.Run(args, getLogger);
            }, Format_Option, Configfile_Option); // End handler of SourceCmd

            ListCmd.AddCommand(SourceCmd);

            GetLoggerFunction = getLogger;
            app.AddCommand(ListCmd);

            return ListCmd;
        } // End noun method
    } // end class

    internal partial class PushCommandParser
    {
        internal static Func<ILogger> GetLoggerFunction;

        internal static Command Register(Command app, Func<ILogger> getLogger)
        {
            var PushCmd = new Command(name: "push", description: Strings.Push_Description);

            // Options directly under the verb 'push'
            var ForceEnglishOutput_Option = new Option<bool>(name: "--force-english-output", description: Strings.ForceEnglishOutput_Description)
            {
                Arity = ArgumentArity.Zero,
            };
            PushCmd.Add(ForceEnglishOutput_Option);
            var Source_Option = new Option<string>(aliases: new[] { "-s", "--source" }, description: Strings.Source_Description)
            {
                Arity = ArgumentArity.ZeroOrOne,
            };
            PushCmd.Add(Source_Option);
            var SymbolSource_Option = new Option<string>(aliases: new[] { "-ss", "--symbol-source" }, description: Strings.SymbolSource_Description)
            {
                Arity = ArgumentArity.ZeroOrOne,
            };
            PushCmd.Add(SymbolSource_Option);
            var Timeout_Option = new Option<int>(aliases: new[] { "-t", "--timeout" }, description: Strings.Push_Timeout_Description)
            {
                Arity = ArgumentArity.ZeroOrOne,
            };
            PushCmd.Add(Timeout_Option);
            var ApiKey_Option = new Option<string>(aliases: new[] { "-k", "--api-key" }, description: Strings.ApiKey_Description)
            {
                Arity = ArgumentArity.ZeroOrOne,
            };
            PushCmd.Add(ApiKey_Option);
            var SymbolApiKey_Option = new Option<string>(aliases: new[] { "-sk", "--symbol-api-key" }, description: Strings.SymbolApiKey_Description)
            {
                Arity = ArgumentArity.ZeroOrOne,
            };
            PushCmd.Add(SymbolApiKey_Option);
            var DisableBuffering_Option = new Option<bool>(aliases: new[] { "-d", "--disable-buffering" }, description: Strings.DisableBuffering_Description)
            {
                Arity = ArgumentArity.Zero,
            };
            PushCmd.Add(DisableBuffering_Option);
            var NoSymbols_Option = new Option<bool>(aliases: new[] { "-n", "--no-symbols" }, description: Strings.NoSymbols_Description)
            {
                Arity = ArgumentArity.Zero,
            };
            PushCmd.Add(NoSymbols_Option);
            var PackagePaths_Argument = new Argument<string>(name: "package-paths", description: Strings.Push_Package_ApiKey_Description)
            {
                Arity = ArgumentArity.OneOrMore,
            };
            PushCmd.Add(PackagePaths_Argument);
            var NoServiceEndpoint_Option = new Option<bool>(name: "--no-service-endpoint", description: Strings.NoServiceEndpoint_Description)
            {
                Arity = ArgumentArity.Zero,
            };
            PushCmd.Add(NoServiceEndpoint_Option);
            var Interactive_Option = new Option<bool>(name: "--interactive", description: Strings.NuGetXplatCommand_Interactive)
            {
                Arity = ArgumentArity.Zero,
            };
            PushCmd.Add(Interactive_Option);
            var SkipDuplicate_Option = new Option<bool>(name: "--skip-duplicate", description: Strings.PushCommandSkipDuplicateDescription)
            {
                Arity = ArgumentArity.Zero,
            };
            PushCmd.Add(SkipDuplicate_Option);
            //PushCmd.SetHandler(PushHandlerAsync, ForceEnglishOutput_Option, Source_Option, SymbolSource_Option, Timeout_Option, ApiKey_Option, SymbolApiKey_Option, DisableBuffering_Option, NoSymbols_Option, PackagePaths_Argument, NoServiceEndpoint_Option, Interactive_Option, SkipDuplicate_Option);

            GetLoggerFunction = getLogger;
            app.AddCommand(PushCmd);

            return PushCmd;
        } // End noun method
    } // end class

} // end namespace
