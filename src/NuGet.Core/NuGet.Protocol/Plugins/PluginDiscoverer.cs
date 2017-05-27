// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Discovers plugins and their operation claims.
    /// </summary>
    public sealed class PluginDiscoverer : IDisposable
    {
        private bool _isDisposed;
        private List<PluginFile> _pluginFiles;
        private readonly string _rawPluginPaths;
        private IEnumerable<PluginDiscoveryResult> _results;
        private readonly SemaphoreSlim _semaphore;
        private readonly EmbeddedSignatureVerifier _verifier;

        /// <summary>
        /// Instantiates a new <see cref="PluginDiscoverer" /> class.
        /// </summary>
        /// <param name="rawPluginPaths">The raw semicolon-delimited list of supposed plugin file paths.</param>
        /// <param name="verifier">An embedded signature verifier.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="verifier" /> is <c>null</c>.</exception>
        public PluginDiscoverer(string rawPluginPaths, EmbeddedSignatureVerifier verifier)
        {
            if (verifier == null)
            {
                throw new ArgumentNullException(nameof(verifier));
            }

            _rawPluginPaths = rawPluginPaths;
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

                _pluginFiles = GetPluginFiles(cancellationToken);
                var results = new List<PluginDiscoveryResult>();

                for (var i = 0; i < _pluginFiles.Count; ++i)
                {
                    var pluginFile = _pluginFiles[i];
                    string message = null;

                    switch (pluginFile.State)
                    {
                        case PluginFileState.Valid:
                            break;

                        case PluginFileState.NotFound:
                            message = string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.Plugin_FileNotFound,
                                pluginFile.Path);
                            break;

                        case PluginFileState.InvalidFilePath:
                            message = string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.Plugin_InvalidPluginFilePath,
                                pluginFile.Path);
                            break;

                        case PluginFileState.InvalidEmbeddedSignature:
                            message = string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.Plugin_InvalidEmbeddedSignature,
                                pluginFile.Path);
                            break;

                        default:
                            throw new NotImplementedException();
                    }

                    var result = new PluginDiscoveryResult(pluginFile, message);

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
                cancellationToken.ThrowIfCancellationRequested();

                if (PathValidator.IsValidLocalPath(filePath) || PathValidator.IsValidUncPath(filePath))
                {
                    if (File.Exists(filePath))
                    {
                        var isValid = _verifier.IsValid(filePath);
                        var state = isValid ? PluginFileState.Valid : PluginFileState.InvalidEmbeddedSignature;

                        files.Add(new PluginFile(filePath, state));
                    }
                    else
                    {
                        files.Add(new PluginFile(filePath, PluginFileState.NotFound));
                    }
                }
                else
                {
                    files.Add(new PluginFile(filePath, PluginFileState.InvalidFilePath));
                }
            }

            return files;
        }

        private IEnumerable<string> GetPluginFilePaths()
        {
            if (string.IsNullOrEmpty(_rawPluginPaths))
            {
                return Enumerable.Empty<string>();
            }

            return _rawPluginPaths.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}