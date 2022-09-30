// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat
{
    public class Program
    {
        private const string DebugOption = "--debug";
        private const string DotnetNuGetAppName = "dotnet nuget";
        private const string DotnetPackageAppName = "NuGet.CommandLine.XPlat.dll package";

        public static int Main(string[] args)
        {
            // Start with a default logger, this will be updated according to the passed in verbosity
            var log = new CommandOutputLogger(LogLevel.Information);
            return MainInternal(args, log);
        }

        /// <summary>
        /// Internal Main. This is used for testing.
        /// </summary>
        public static int MainInternal(string[] args, CommandOutputLogger log)
        {
#if DEBUG
            if (args.Contains(DebugOption))
            {
                args = args.Where(arg => !StringComparer.OrdinalIgnoreCase.Equals(arg, DebugOption)).ToArray();

                Console.WriteLine("Waiting for debugger to attach.");
                Console.WriteLine($"Process ID: {Process.GetCurrentProcess().Id}");

                while (!Debugger.IsAttached)
                {
                    System.Threading.Thread.Sleep(100);
                }

                Debugger.Break();
            }
#endif

            // Optionally disable localization.
            if (args.Any(arg => string.Equals(arg, CommandConstants.ForceEnglishOutputOption, StringComparison.OrdinalIgnoreCase)))
            {
                CultureUtility.DisableLocalization();
            }

            var app = InitializeApp(args);
            args = args
                .Where(e => e != "package")
                .ToArray();

            NuGet.Common.Migrations.MigrationRunner.Run();

            var verbosity = app.Option(XPlatUtility.VerbosityOption, Strings.Switch_Verbosity, CommandOptionType.SingleValue);

            // Options aren't parsed until we call app.Execute(), so look directly for the verbosity option ourselves
            LogLevel logLevel;
            TryParseVerbosity(args, verbosity, out logLevel);
            log.LogLevel = logLevel;

            NetworkProtocolUtility.SetConnectionLimit();

            XPlatUtility.SetUserAgent();

            // This method has no effect on .NET Core.
            NetworkProtocolUtility.ConfigureSupportedSslProtocols();

            // Register commands
            RegisterCommands(app, log);

            app.OnExecute(() =>
            {
                app.ShowHelp();

                return 0;
            });

            log.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.OutputNuGetVersion, app.FullName, app.LongVersionGetter()));

            int exitCode = 0;

            try
            {
                exitCode = app.Execute(args);
            }
            catch (Exception e)
            {
                // Log the error
                if (ExceptionLogger.Instance.ShowStack)
                {
                    log.LogError(e.ToString());
                }
                else
                {
                    log.LogError(ExceptionUtilities.DisplayMessage(e));
                }

                // Log the stack trace as verbose output.
                log.LogVerbose(e.ToString());

                exitCode = 1;
            }

            // Limit the exit code range to 0-255 to support POSIX
            if (exitCode < 0 || exitCode > 255)
            {
                exitCode = 1;
            }

            return exitCode;
        }

        private static CommandLineApplication InitializeApp(string[] args)
        {
            var app = new CommandLineApplication();

            if (args.Any() && args[0] == "package")
            {
                app.Name = DotnetPackageAppName;
            }
            else
            {
                app.Name = DotnetNuGetAppName;
            }
            app.FullName = Strings.App_FullName;
            app.HelpOption(XPlatUtility.HelpOption);
            app.VersionOption("--version", typeof(Program).GetTypeInfo().Assembly.GetName().Version.ToString());

            return app;
        }

        private static void RegisterCommands(CommandLineApplication app, CommandOutputLogger log)
        {
            // Register commands
            if (app.Name == DotnetPackageAppName)
            {
                AddPackageReferenceCommand.Register(app, () => log, () => new AddPackageReferenceCommandRunner());
                RemovePackageReferenceCommand.Register(app, () => log, () => new RemovePackageReferenceCommandRunner());
                ListPackageCommand.Register(app, () => log, () => new ListPackageCommandRunner());
            }
            else
            {
                DeleteCommand.Register(app, () => log);
                PushCommand.Register(app, () => log);
                LocalsCommand.Register(app, () => log);
            }
        }

        /// <summary>
        /// Attempts to parse the desired log verbosity from the arguments. Returns true if the
        /// arguments contains a valid verbosity option. If no valid verbosity option was
        /// specified, the log level is set to a default log level and false is returned.
        /// </summary>
        private static bool TryParseVerbosity(string[] args, CommandOption verbosity, out LogLevel logLevel)
        {
            bool found = false;

            for (var index = 0; index < args.Length; index++)
            {
                var arg = args[index];
                string[] option;
                if (arg.StartsWith("--"))
                {
                    option = arg.Substring(2).Split(new[] { ':', '=' }, 2);
                    if (!string.Equals(option[0], verbosity.LongName, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }
                else if (arg.StartsWith("-"))
                {
                    option = arg.Substring(1).Split(new[] { ':', '=' }, 2);
                    if (!string.Equals(option[0], verbosity.ShortName, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }
                else
                {
                    continue;
                }

                if (option.Length == 2)
                {
                    found = verbosity.TryParse(option[1]);
                }
                else if (index < args.Length - 1)
                {
                    found = verbosity.TryParse(args[index + 1]);
                }

                break;
            }

            logLevel = XPlatUtility.GetLogLevel(verbosity);

            // Reset the parsed value since the application execution expects the option to not be
            // populated yet, as this is a single-valued option.
            verbosity.Values.Clear();

            return found;
        }
    }
}