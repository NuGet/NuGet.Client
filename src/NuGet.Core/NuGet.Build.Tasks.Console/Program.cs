// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace NuGet.Build.Tasks.Console
{
    /// <summary>
    /// Represents the main entry point to the console application.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// A <see cref="T:char[]" /> containing the equals sign '=' to be used to split key/value pairs that are separated by it.
        /// </summary>
        private static readonly char[] EqualSign = { '=' };

        /// <summary>
        /// A <see cref="T:char[]" /> containing the semicolon ';' to be used to split key/value pairs that are separated by it.
        /// </summary>
        private static readonly char[] Semicolon = { ';' };

        /// <summary>
        /// The main entry point to the console application.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns><code>0</code> if the application ran successfully with no errors, otherwise <code>1</code>.</returns>
        public static async Task<int> Main(string[] args)
        {
            var debug = IsDebug();

            if (debug)
            {
#if IS_CORECLR
                System.Console.WriteLine($"Waiting for debugger to attach to Process ID: {Process.GetCurrentProcess().Id}");

                while (!Debugger.IsAttached)
                {
                    System.Threading.Thread.Sleep(100);
                }

                Debugger.Break();
#else
                Debugger.Launch();
#endif
            }

            // Parse command-line arguments
            if (!TryParseArguments(args, out (Dictionary<string, string> Options, FileInfo MSBuildExeFilePath, string EntryProjectFilePath, Dictionary<string, string> MSBuildGlobalProperties) arguments))
            {
                return 1;
            }

            // Enable MSBuild feature flags
            MSBuildFeatureFlags.MSBuildExeFilePath = arguments.MSBuildExeFilePath.FullName;
            MSBuildFeatureFlags.EnableCacheFileEnumerations = true;
            MSBuildFeatureFlags.LoadAllFilesAsReadonly = true;
            MSBuildFeatureFlags.SkipEagerWildcardEvaluations = true;

            // Only wire up an AssemblyResolve event handler if being debugged or running under a unit test
            if (debug || string.Equals(Environment.GetEnvironmentVariable("UNIT_TEST_RESTORE_TASK"), bool.TrueString, StringComparison.OrdinalIgnoreCase))
            {
                // The App.config contains relative paths to MSBuild which won't work for locally built copies so an AssemblyResolve event
                // handler is used in order to locate the MSBuild assemblies
                string msbuildDirectory = arguments.MSBuildExeFilePath.DirectoryName;

                AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
                {
                    var assemblyName = new AssemblyName(resolveArgs.Name);

                    var path = Path.Combine(msbuildDirectory, $"{assemblyName.Name}.dll");

                    return File.Exists(path) ? Assembly.LoadFrom(path) : null;
                };
            }

            using (var dependencyGraphSpecGenerator = new MSBuildStaticGraphRestore(debug: debug))
            {
                return await dependencyGraphSpecGenerator.RestoreAsync(arguments.EntryProjectFilePath, arguments.MSBuildGlobalProperties, arguments.Options) ? 0 : 1;
            }
        }

        /// <summary>
        /// Determines if a user specified that the current process is being debugged.
        /// </summary>
        /// <returns><code>true</code> if the user specified to debug the current process, otherwise <code>false</code>.</returns>
        private static bool IsDebug()
        {
            return string.Equals(Environment.GetEnvironmentVariable("DEBUG_RESTORE_TASK"), bool.TrueString, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses a semicolon delimited list of equal sign separated key value pairs.
        /// </summary>
        /// <param name="value">The string containing a semicolon delimited list of key value pairs to parse.</param>
        /// <returns>A <see cref="Dictionary{String,String}" /> containing the list of items as key value pairs.</returns>
        private static Dictionary<string, string> ParseSemicolonDelimitedListOfKeyValuePairs(string value)
        {
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in value
                .Split(Semicolon, StringSplitOptions.RemoveEmptyEntries)
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Select(i => i.Split(EqualSign, 2))
                .Where(i => i.Length == 2 && !string.IsNullOrWhiteSpace(i[0]) && !string.IsNullOrWhiteSpace(i[1])))
            {
                properties[pair[0].Trim()] = pair[1].Trim();
            }

            return properties;
        }

        /// <summary>
        /// Parses command-line arguments.
        /// </summary>
        /// <param name="args">A <see cref="T:string[]" /> containing the process command-line arguments.</param>
        /// <param name="arguments">A <see cref="T:Tuple&lt;Dictionary&lt;string, string&gt;, FileInfo, string, Dictionary&lt;string, string&gt;&gt;" /> that receives the parsed command-line arguments.</param>
        /// <returns><code>true</code> if the arguments were successfully parsed, otherwise <code>false</code>.</returns>
        private static bool TryParseArguments(string[] args, out (Dictionary<string, string> Options, FileInfo MSBuildExeFilePath, string EntryProjectFilePath, Dictionary<string, string> MSBuildGlobalProperties) arguments)
        {
            if (args.Length != 4)
            {
                arguments = (null, null, null, null);

                return false;
            }

            try
            {
                var options = ParseSemicolonDelimitedListOfKeyValuePairs(args[0]);
                var msbuildExeFilePath = new FileInfo(args[1]);
                var entryProjectFilePath = args[2];
                var globalProperties = ParseSemicolonDelimitedListOfKeyValuePairs(args[3]);

                arguments = (options, msbuildExeFilePath, entryProjectFilePath, globalProperties);

                // Command-line is correct if no exceptions were thrown and the MSBuild path exists and an entry project were specified
                return msbuildExeFilePath.Exists && !string.IsNullOrWhiteSpace(entryProjectFilePath);
            }
            catch (Exception)
            {
                arguments = (null, null, null, null);

                return false;
            }
        }
    }
}
