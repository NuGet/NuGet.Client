// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        private readonly string _netCoreAndNetFXPluginPaths;
        private readonly string _nuGetPluginPaths;
        private IEnumerable<PluginDiscoveryResult> _results;
        private readonly SemaphoreSlim _semaphore;
        private readonly EmbeddedSignatureVerifier _verifier;
        private readonly IEnvironmentVariableReader _environmentVariableReader;

        public PluginDiscoverer(EmbeddedSignatureVerifier verifier)
            : this(verifier, EnvironmentVariableWrapper.Instance)
        {
        }

        /// <summary>
        /// Instantiates a new <see cref="PluginDiscoverer" /> class.
        /// </summary>
        /// <param name="rawPluginPaths">The raw semicolon-delimited list of supposed plugin file paths.</param>
        public PluginDiscoverer(string rawPluginPaths)
        /// <param name="verifier">An embedded signature verifier.</param>
        /// <param name="environmentVariableReader"> A reader for environmental varibales. </param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="verifier" /> is <see langword="null" />.</exception>
        internal PluginDiscoverer(EmbeddedSignatureVerifier verifier, IEnvironmentVariableReader environmentVariableReader)
        {
            _rawPluginPaths = rawPluginPaths;
            if (verifier == null)
            {
                throw new ArgumentNullException(nameof(verifier));
            }

            _environmentVariableReader = environmentVariableReader;
#if IS_DESKTOP
            _netCoreAndNetFXPluginPaths = environmentVariableReader.GetEnvironmentVariable(EnvironmentVariableConstants.DesktopPluginPaths);
#else
            _netCoreAndNetFXPluginPaths = environmentVariableReader.GetEnvironmentVariable(EnvironmentVariableConstants.CorePluginPaths);
#endif
            _nuGetPluginPaths = _environmentVariableReader.GetEnvironmentVariable(EnvironmentVariableConstants.PluginPaths);
            _verifier = verifier;
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

                if (!string.IsNullOrEmpty(_netCoreAndNetFXPluginPaths))
                {
                    // NUGET_NETFX_PLUGIN_PATHS, NUGET_NETCORE_PLUGIN_PATHS have been set.
                    var filePaths = _netCoreAndNetFXPluginPaths.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    _pluginFiles = GetPluginFiles(filePaths, cancellationToken);
                }
                else if (!string.IsNullOrEmpty(_nuGetPluginPaths))
                {
                    // NUGET_PLUGIN_PATHS has been set
                    _pluginFiles = GetPluginsInNuGetPluginPaths();
                }
                else
                {
                    // restore to default plugins search.
                    // Search for plugins in %user%/.nuget/plugins
                    var directories = new List<string> { PluginDiscoveryUtility.GetNuGetHomePluginsPath() };
#if IS_DESKTOP
                    // Internal plugins are only supported for .NET Framework scenarios, namely msbuild.exe
                    directories.Add(PluginDiscoveryUtility.GetInternalPlugins());
#endif
                    var filePaths = PluginDiscoveryUtility.GetConventionBasedPlugins(directories);
                    _pluginFiles = GetPluginFiles(filePaths.ToArray(), cancellationToken);

                    // Search for .Net tools plugins in PATH
                    if (_pluginFiles != null)
                    {
                        _pluginFiles.AddRange(GetPluginsInPATH() ?? new List<PluginFile>());
                    }
                    else
                    {
                        _pluginFiles = GetPluginsInPATH() ?? new List<PluginFile>();
                    }
                }

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

        private List<PluginFile> GetPluginFiles(string[] filePaths, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
        /// Retrieves authentication plugins by searching through directories and files specified in the `NuGET_PLUGIN_PATHS`
        /// environment variable. The method looks for files prefixed with 'nuget-plugin-' and verifies their validity for .net tools plugins.
        /// </summary>
        /// <returns>A list of valid <see cref="PluginFile"/> objects representing the discovered plugins.</returns>
        internal List<PluginFile> GetPluginsInNuGetPluginPaths()
        {
            var pluginFiles = new List<PluginFile>();
            string[] paths = _nuGetPluginPaths?.Split(Path.PathSeparator) ?? Array.Empty<string>();

            foreach (var path in paths)
            {
                if (PathValidator.IsValidLocalPath(path) || PathValidator.IsValidUncPath(path))
                {
                    if (File.Exists(path))
                    {
                        FileInfo fileInfo = new FileInfo(path);
                        if (fileInfo.Name.StartsWith("nuget-plugin-", StringComparison.CurrentCultureIgnoreCase))
                        {
                            // A DotNet tool plugin
                            if (IsValidPluginFile(fileInfo))
                            {
                                PluginFile pluginFile = new PluginFile(fileInfo.FullName, new Lazy<PluginFileState>(() => PluginFileState.Valid), isDotnetToolsPlugin: true);
                                pluginFiles.Add(pluginFile);
                            }
                        }
                        else
                        {
                            // A non DotNet tool plugin file
                            var state = new Lazy<PluginFileState>(() => _verifier.IsValid(fileInfo.FullName) ? PluginFileState.Valid : PluginFileState.InvalidEmbeddedSignature);
                            pluginFiles.Add(new PluginFile(fileInfo.FullName, state));
                        }
                    }
                    else if (Directory.Exists(path))
                    {
                        var directoryInfo = new DirectoryInfo(path);
                        var files = directoryInfo.GetFiles("nuget-plugin-*");

                        foreach (var file in files)
                        {
                            if (IsValidPluginFile(file))
                            {
                                // .NET SDK recently package signature verification for .NET tools, as a result we expect them to be valid.
                                // As a result the plugin created here has PluginFileState.Valid.
                                PluginFile pluginFile = new PluginFile(file.FullName, new Lazy<PluginFileState>(() => PluginFileState.Valid), isDotnetToolsPlugin: true);
                                pluginFiles.Add(pluginFile);
                            }
                        }
                    }
                }
                else
                {
                    pluginFiles.Add(new PluginFile(path, new Lazy<PluginFileState>(() => PluginFileState.InvalidFilePath)));
                }
            }

            return pluginFiles;
        }

        /// <summary>
        /// Retrieves .NET tools authentication plugins by searching through directories specified in `PATH` 
        /// </summary>
        /// <returns>A list of valid <see cref="PluginFile"/> objects representing the discovered plugins.</returns>
        internal List<PluginFile> GetPluginsInPATH()
        {
            var pluginFiles = new List<PluginFile>();
            var nugetPluginPaths = _environmentVariableReader.GetEnvironmentVariable("PATH");
            string[] paths = nugetPluginPaths?.Split(Path.PathSeparator) ?? Array.Empty<string>();

            foreach (var path in paths)
            {
                if (PathValidator.IsValidLocalPath(path) || PathValidator.IsValidUncPath(path))
                {
                    if (Directory.Exists(path))
                    {
                        var directoryInfo = new DirectoryInfo(path);
                        var files = directoryInfo.GetFiles("nuget-plugin-*");

                        foreach (var file in files)
                        {
                            if (IsValidPluginFile(file))
                            {
                                // .NET SDK recently package signature verification for .NET tools, as a result we expect them to be valid.
                                PluginFile pluginFile = new PluginFile(file.FullName, new Lazy<PluginFileState>(() => PluginFileState.Valid), isDotnetToolsPlugin: true);
                                pluginFiles.Add(pluginFile);
                            }
                        }
                    }
                }
                else
                {
                    pluginFiles.Add(new PluginFile(path, new Lazy<PluginFileState>(() => PluginFileState.InvalidFilePath)));
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
        internal static bool IsValidPluginFile(FileInfo fileInfo)
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
                return fileInfo.Exists && IsExecutable(fileInfo);
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
        internal static bool IsExecutable(FileInfo fileInfo)
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                string output;
                using (var process = new System.Diagnostics.Process())
                {
                    // Use a shell command to check if the file is executable
                    process.StartInfo.FileName = "/bin/bash";
                    process.StartInfo.Arguments = $" -c \"if [ -x {fileInfo.FullName} ]; then echo yes; else echo no; fi\"";
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
    }
}
