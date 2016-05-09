// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat
{
    public class Program
    {
        private const string DebugOption = "--debug";
        public static CommandOutputLogger Log { get; set; }

        public static int Main(string[] args)
        {
#if DEBUG
            if (args.Contains(DebugOption))
            {
                args = args.Where(arg => !StringComparer.OrdinalIgnoreCase.Equals(arg, DebugOption)).ToArray();

                while (!Debugger.IsAttached)
                {
                    System.Threading.Thread.Sleep(100);
                }

                Debugger.Break();
            }
#endif

            // First, optionally disable localization.
            if (args.Any(arg => string.Equals(arg, CommandConstants.ForceEnglishOutputOption, StringComparison.OrdinalIgnoreCase)))
            {
                CultureUtility.DisableLocalization();
            }

            var app = new CommandLineApplication();
            app.Name = "nuget3";
            app.FullName = Strings.App_FullName;
            app.HelpOption(XPlatUtility.HelpOption);
            app.VersionOption("--version", typeof(Program).GetTypeInfo().Assembly.GetName().Version.ToString());

            var verbosity = app.Option(XPlatUtility.VerbosityOption, Strings.Switch_Verbosity, CommandOptionType.SingleValue);

            // Options aren't parsed until we call app.Execute(), so look directly for the verbosity option ourselves
            ParseVerbosity(args, verbosity);

            // Set up logging.
            // For tests this will already be set.
            if (Log == null)
            {
                var logLevel = XPlatUtility.GetLogLevel(verbosity);
                Log = new CommandOutputLogger(logLevel);
            }

            XPlatUtility.SetConnectionLimit();

            XPlatUtility.SetUserAgent();

            // This method has no effect on .NET Core.
            NetworkProtocolUtility.ConfigureSupportedSslProtocols();

            // Register commands
            DeleteCommand.Register(app, () => Log);
            PackCommand.Register(app, () => Log);
            PushCommand.Register(app, () => Log);
            RestoreCommand.Register(app, () => Log);

            app.OnExecute(() =>
            {
                app.ShowHelp();

                return 0;
            });

            Log.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.OutputNuGetVersion, app.FullName, app.LongVersionGetter()));

            int exitCode = 0;

            try
            {
                exitCode = app.Execute(args);
            }
            catch (Exception e)
            {
                // Log the error
                Log.LogError(ExceptionUtilities.DisplayMessage(e));

                // Log the stack trace as verbose output.
                Log.LogVerbose(e.ToString());

                exitCode = 1;
            }

            // Limit the exit code range to 0-255 to support POSIX
            if (exitCode < 0 || exitCode > 255)
            {
                exitCode = 1;
            }

            return exitCode;
        }

        private static void ParseVerbosity(string[] args, CommandOption verbosity)
        {
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
                    verbosity.TryParse(option[1]);
                }
                else if (index < args.Length - 1)
                {
                    verbosity.TryParse(args[index + 1]);
                }
                break;
            }
        }
    }
}
