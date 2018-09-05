// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Test.TestExtensions.TestablePlugin
{
    internal static class Program
    {
        private const int _success = 0;
        private const int _error = 1;

        private static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;

                DebugBreakIfPluginDebuggingIsEnabled();

                Arguments parsedArgs;

                if (!Arguments.TryParse(args, out parsedArgs))
                {
                    return _error;
                }

                Start(parsedArgs);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());

                return _error;
            }

            return _success;
        }

        private static void Start(Arguments arguments)
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var responses = new BlockingCollection<Response>())
            {
                Process process;

                if (!TryGetProcess(arguments.TestRunnerProcessId, out process))
                {
                    return;
                }

                using (process)
                {
                    process.Exited += (sender, args) =>
                    {
                        try
                        {
                            cancellationTokenSource.Cancel();
                        }
                        catch (Exception)
                        {
                        }
                    };

                    process.EnableRaisingEvents = true;

                    var responseReceiver = new ResponseReceiver(arguments.PortNumber, responses);

                    using (var testablePlugin = new TestablePlugin(responses))
                    {
                        var tasks = new[]
                        {
                            Task.Factory.StartNew(
                                () => responseReceiver.StartListeningAsync(cancellationTokenSource.Token),
                                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach),
                            testablePlugin.StartAsync(cancellationTokenSource.Token)
                        };

                        Task.WaitAny(tasks);

                        try
                        {
                            cancellationTokenSource.Cancel();
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }
        }

        private static void DebugBreakIfPluginDebuggingIsEnabled()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NUGET_PLUGIN_DEBUG")))
            {
                Debugger.Break();
            }
        }

        private static bool TryGetProcess(int processId, out Process process)
        {
            try
            {
                process = Process.GetProcessById(processId);

                return true;
            }
            catch (Exception)
            {
            }

            process = null;

            return false;
        }
    }
}