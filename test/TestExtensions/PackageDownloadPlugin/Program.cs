// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NuGet.Protocol.Plugins;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal static class Program
    {
        private const int _error = 1;
        private const string _expectedArgument = "-plugin";
        private const int _success = 0;

        private static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;

                DebugBreakIfPluginDebuggingIsEnabled();

                if (args.Any(arg => string.Equals(_expectedArgument, arg, StringComparison.OrdinalIgnoreCase)))
                {
                    Run();
                }
                else
                {
                    Console.WriteLine($"Please use the \"{_expectedArgument}\" argument.");

                    return _error;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());

                return _error;
            }

            return _success;
        }

        private static ServiceContainer CreateServiceContainer()
        {
            var serviceContainer = new ServiceContainer();

            serviceContainer.RegisterInstance(PluginConfiguration.Create());
            serviceContainer.RegisterInstance(new CredentialsService());
            serviceContainer.RegisterInstance(new Logger());
            serviceContainer.RegisterInstance(DownloadPackageCache.Create());
            serviceContainer.RegisterInstance(OfflinePackageCache.Create());
            serviceContainer.RegisterInstance(new PackageDownloader(serviceContainer));

            return serviceContainer;
        }

        private static void DebugBreakIfPluginDebuggingIsEnabled()
        {
            var nugetPluginDebug = Environment.GetEnvironmentVariable("NUGET_PLUGIN_DEBUG");

            if (!string.IsNullOrEmpty(nugetPluginDebug))
            {
                Console.WriteLine($"Within {ProtocolConstants.HandshakeTimeout.TotalSeconds} seconds either attach a debugger and continue or cancel the debugger launch request.");

                Debugger.Launch();
            }
        }

        private static void Run()
        {
            var serviceContainer = CreateServiceContainer();
            var plugin = new Plugin(serviceContainer);

            plugin.RunAsync().Wait();
        }
    }
}