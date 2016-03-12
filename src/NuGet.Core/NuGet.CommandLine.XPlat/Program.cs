﻿// Copyright (c) .NET Foundation. All rights reserved.
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
        public static CommandOutputLogger Log { get; set; }

        public static int Main(string[] args)
        {
#if DEBUG
            if (args.Contains("--debug"))
            {
                args = args.Skip(1).ToArray();
                while (!Debugger.IsAttached)
                {

                }
                Debugger.Break();
            }
#endif

            var app = new CommandLineApplication();
            app.Name = "nuget3";
            app.FullName = Strings.App_FullName;
            app.HelpOption(XPlatUtility.HelpOption);
            app.VersionOption("--version", typeof(Program).GetTypeInfo().Assembly.GetName().Version.ToString());

            var verbosity = app.Option(XPlatUtility.VerbosityOption, Strings.Switch_Verbosity, CommandOptionType.SingleValue);

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

            //register push and delete command
            PushCommand.Register(app, () =>
            {
                return Log;
            });

            DeleteCommand.Register(app, () =>
            {
                return Log;
            });

            RestoreCommand.Register(app, () =>
            {
                return Log;
            });

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 0;
            });

            var exitCode = 0;

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
    }
}
