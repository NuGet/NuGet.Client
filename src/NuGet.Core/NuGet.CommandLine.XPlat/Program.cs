// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat
{
    public class Program
    {
        private const string DebugOption = "--debug";

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

            var logLevel = XPlatUtility.GetLogLevel(verbosity);
            log.SetLogLevel(logLevel);

            XPlatUtility.SetConnectionLimit();

            XPlatUtility.SetUserAgent();

            // This method has no effect on .NET Core.
            NetworkProtocolUtility.ConfigureSupportedSslProtocols();

            // Register commands
            DeleteCommand.Register(app, () => log);
            PackCommand.Register(app, () => log);
            PushCommand.Register(app, () => log);
            RestoreCommand.Register(app, () => log);

            app.OnExecute(() =>
            {
                app.ShowHelp();

                return 0;
            });

            int exitCode = 0;

            try
            {
                exitCode = app.Execute(args);
            }
            catch (Exception e)
            {
                // Log the error
                log.LogError(ExceptionUtilities.DisplayMessage(e));

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
    }
}
