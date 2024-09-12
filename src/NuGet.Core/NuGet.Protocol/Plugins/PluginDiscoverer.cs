// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Discovers plugins and their operation claims.
    /// </summary>
    public sealed class PluginDiscoverer : IPluginDiscoverer
    {
        private bool _isDisposed;
        private List<PluginFile> _pluginFiles;
        private readonly string _rawPluginPaths;
        private IEnumerable<PluginDiscoveryResult> _results;
        private readonly SemaphoreSlim _semaphore;
        private readonly EmbeddedSignatureVerifier _verifier;
        private readonly IEnvironmentVariableReader _environmentVariableReader = EnvironmentVariableWrapper.Instance;

        internal PluginDiscoverer(string rawPluginPaths, EmbeddedSignatureVerifier verifier, IEnvironmentVariableReader environmentVariableReader) : this(rawPluginPaths, verifier)
        {
            _environmentVariableReader = environmentVariableReader;
        }

        /// <summary>
        /// Instantiates a new <see cref="PluginDiscoverer" /> class.
        /// </summary>
        /// <param name="rawPluginPaths">The raw semicolon-delimited list of supposed plugin file paths.</param>
        public PluginDiscoverer(string rawPluginPaths)
        {
            _rawPluginPaths = rawPluginPaths;
            _semaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1);
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _semaphore.Dispose();

            GC.SuppressFinalize(this);

            _isDisposed = true;
        }

        /// <summary>
        /// Asynchronously discovers plugins.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a
        /// <see cref="IEnumerable{PluginDiscoveryResult}" /> from the target.</returns>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        public async Task<IEnumerable<PluginDiscoveryResult>> DiscoverAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_results != null)
            {
                return _results;
            }

            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                if (_results != null)
                {
                    return _results;
                }

                _pluginFiles = GetPluginFiles(cancellationToken);
                _pluginFiles.AddRange(GetNetToolsPluginFiles());
                var results = new List<PluginDiscoveryResult>();

                for (var i = 0; i < _pluginFiles.Count; ++i)
                {
                    var pluginFile = _pluginFiles[i];

                    var result = new PluginDiscoveryResult(pluginFile);

                    results.Add(result);
                }

                _results = results;
            }
            finally
            {
                _semaphore.Release();
            }

            return _results;
        }

        private List<PluginFile> GetPluginFiles(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePaths = GetPluginFilePaths();

            var files = new List<PluginFile>();

            foreach (var filePath in filePaths)
            {
                var pluginFile = new PluginFile(filePath, new Lazy<PluginFileState>(() =>
                {
                    if (PathValidator.IsValidLocalPath(filePath) || PathValidator.IsValidUncPath(filePath))
                    {
                        return File.Exists(filePath) ? PluginFileState.Valid : PluginFileState.NotFound;
                    }
                    else
                    {
                        return PluginFileState.InvalidFilePath;
                    }
                }));
                files.Add(pluginFile);
            }

            return files;
        }

        /// <summary>
        /// Gets auth plugins installed using dotnet tools. This is done by iterating through each file in directories found int eh
        /// `PATH` environment variable.
        /// The files are also expected to have a name that starts with `nuget-plugin-`
        /// </summary>
        /// <returns></returns>
        private List<PluginFile> GetNetToolsPluginFiles()
        {
            var pluginFiles = new List<PluginFile>();

            string[] paths = Array.Empty<string>();

            // The path to the plugins installed using dotnet tools, should be specified in the NUGET_PLUGIN_PATHS environment variable.
            paths = _environmentVariableReader.GetEnvironmentVariable("NUGET_PLUGIN_PATHS")?.Split(Path.PathSeparator) ?? Array.Empty<string>();

            if (paths.Length == 0)
            {
                // If NUGET_PLUGIN_PATHS is not specified, read all the paths in PATH
                paths = _environmentVariableReader.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
            }

            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    var directoryInfo = new DirectoryInfo(path);
                    var files = directoryInfo.GetFiles("nuget-plugin-*");

                    foreach (var file in files)
                    {
                        if (IsValidPluginFile(file))
                        {
                            var state = new Lazy<PluginFileState>(() => _verifier.IsValid(file.FullName) ? PluginFileState.Valid : PluginFileState.InvalidEmbeddedSignature);
                            PluginFile pluginFile = new PluginFile(file.FullName, state, isDotnetToolsPlugin: true);
                            pluginFiles.Add(pluginFile);
                        }
                    }
                }
            }

            return pluginFiles;
        }

        /// <summary>
        /// Checks whether a file is a valid plugin file for windows/Unix.
        /// Windows: It should be either .bat or  .exe
        /// Unix: It should be executable
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <returns></returns>
        private static bool IsValidPluginFile(FileInfo fileInfo)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return fileInfo.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                       fileInfo.Extension.Equals(".bat", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
#if NET8_0_OR_GREATER
                var fileMode = File.GetUnixFileMode(fileInfo.FullName);

                return fileInfo.Exists &&
                    ((fileMode & UnixFileMode.UserExecute) != 0 ||
                    (fileMode & UnixFileMode.GroupExecute) != 0 ||
                    (fileMode & UnixFileMode.OtherExecute) != 0);
#else
                return fileInfo.Exists && (fileInfo.Attributes & FileAttributes.ReparsePoint) == 0 && IsExecutable(fileInfo);
#endif
            }
        }

#if !NET8_0_OR_GREATER
        /// <summary>
        /// Checks whether a file is executable or not in Unix.
        /// This is done by running bash code: `if [ -x {fileInfo.FullName} ]; then echo yes; else echo no; fi`
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <returns></returns>
        private static bool IsExecutable(FileInfo fileInfo)
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                string output;
                using (var process = new Process())
                {
                    // Use a shell command to check if the file is executable
                    process.StartInfo.FileName = "/bin/bash";
                    process.StartInfo.Arguments = $"if [ -x {fileInfo.FullName} ]; then echo yes; else echo no; fi";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;

                    process.Start();
                    if (!process.WaitForExit(1000) || process.ExitCode != 0)
                    {
                        return false;
                    }

                    output = process.StandardOutput.ReadToEnd().Trim();
                }

                // Check if the output is "yes"
                return output.Equals("yes", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }
#endif

        private IEnumerable<string> GetPluginFilePaths()
        {
            if (string.IsNullOrEmpty(_rawPluginPaths))
            {
                var directories = new List<string> { PluginDiscoveryUtility.GetNuGetHomePluginsPath() };
#if IS_DESKTOP
                // Internal plugins are only supported for .NET Framework scenarios, namely msbuild.exe
                directories.Add(PluginDiscoveryUtility.GetInternalPlugins());
#endif
                return PluginDiscoveryUtility.GetConventionBasedPlugins(directories);
            }

            return _rawPluginPaths.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
