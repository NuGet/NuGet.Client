// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// A plugin factory.
    /// </summary>
    public sealed class PluginFactory : IDisposable
    {
        private bool _isDisposed;
        private readonly TimeSpan _pluginIdleTimeout;
        private readonly ConcurrentDictionary<string, Lazy<Task<IPlugin>>> _plugins;

        /// <summary>
        /// Instantiates a new <see cref="PluginFactory" /> class.
        /// </summary>
        /// <param name="pluginIdleTimeout">The plugin idle timeout.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="pluginIdleTimeout" />
        /// is less than <see cref="Timeout.InfiniteTimeSpan" />.</exception>
        public PluginFactory(TimeSpan pluginIdleTimeout)
        {
            if (pluginIdleTimeout < Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pluginIdleTimeout),
                    pluginIdleTimeout,
                    Strings.Plugin_IdleTimeoutMustBeGreaterThanOrEqualToInfiniteTimeSpan);
            }

            _pluginIdleTimeout = pluginIdleTimeout;
            _plugins = new ConcurrentDictionary<string, Lazy<Task<IPlugin>>>();
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            foreach (var entry in _plugins)
            {
                var lazyTask = entry.Value;

                if (lazyTask.IsValueCreated && lazyTask.Value.Status == TaskStatus.RanToCompletion)
                {
                    var plugin = lazyTask.Value.Result;

                    plugin.Dispose();
                }
            }

            GC.SuppressFinalize(this);

            _isDisposed = true;
        }

        /// <summary>
        /// Asynchronously gets an existing plugin instance or creates a new instance and connects to it.
        /// </summary>
        /// <param name="filePath">The file path of the plugin.</param>
        /// <param name="arguments">Command-line arguments to be supplied to the plugin.</param>
        /// <param name="requestHandlers">Request handlers.</param>
        /// <param name="options">Connection options.</param>
        /// <param name="sessionCancellationToken">A cancellation token for the plugin's lifetime.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a <see cref="Plugin" />
        /// instance.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="filePath" />
        /// is either <c>null</c> or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="arguments" />
        /// is either <c>null</c> or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="requestHandlers" />
        /// is either <c>null</c> or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options" />
        /// is either <c>null</c> or empty.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="sessionCancellationToken" />
        /// is cancelled.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if this object is disposed.</exception>
        /// <remarks>This is intended to be called by NuGet client tools.</remarks>
        public async Task<IPlugin> GetOrCreateAsync(
            string filePath,
            IEnumerable<string> arguments,
            IRequestHandlers requestHandlers,
            ConnectionOptions options,
            CancellationToken sessionCancellationToken)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(PluginFactory));
            }

            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(filePath));
            }

            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            if (requestHandlers == null)
            {
                throw new ArgumentNullException(nameof(requestHandlers));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            sessionCancellationToken.ThrowIfCancellationRequested();

            var lazyTask = _plugins.GetOrAdd(filePath,
                (path) => new Lazy<Task<IPlugin>>(() => CreatePluginAsync(filePath, arguments, requestHandlers, options, sessionCancellationToken)));

            await lazyTask.Value;

            // Manage plugin lifetime by its idleness.  Thus, don't allow callers to prematurely dispose of a plugin.
            return new NoOpDisposePlugin(lazyTask.Value.Result);
        }

        private async Task<IPlugin> CreatePluginAsync(
            string filePath,
            IEnumerable<string> arguments,
            IRequestHandlers requestHandlers,
            ConnectionOptions options,
            CancellationToken sessionCancellationToken)
        {
            var startInfo = new ProcessStartInfo(filePath)
            {
                Arguments = string.Join(" ", arguments),
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            var process = Process.Start(startInfo);
            var sender = new Sender(process.StandardInput);
            var receiver = new StandardOutputReceiver(new PluginProcess(process));
            var messageDispatcher = new MessageDispatcher(requestHandlers, new RequestIdGenerator());
            var connection = new Connection(messageDispatcher, sender, receiver, options);
            var pluginProcess = new PluginProcess(process);

            // Wire up the Fault handler before calling ConnectAsync(...).
            var plugin = new Plugin(
                filePath,
                connection,
                pluginProcess,
                isOwnProcess: false,
                idleTimeout: _pluginIdleTimeout);

            try
            {
                await connection.ConnectAsync(sessionCancellationToken);

                RegisterEventHandlers(plugin);
            }
            catch (ProtocolException ex)
            {
                throw new ProtocolException(
                    string.Format(CultureInfo.CurrentCulture, Strings.Plugin_Exception, plugin.Name, ex.Message));
            }
            catch (Exception)
            {
                plugin.Dispose();

                throw;
            }

            return plugin;
        }

        /// <summary>
        /// Asynchronously creates a plugin instance and connects to it.
        /// </summary>
        /// <param name="requestHandlers">Request handlers.</param>
        /// <param name="options">Connection options.</param>
        /// <param name="sessionCancellationToken">A cancellation token for the plugin's lifetime.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a <see cref="Plugin" />
        /// instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="requestHandlers" />
        /// is either <c>null</c> or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options" />
        /// is either <c>null</c> or empty.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="sessionCancellationToken" />
        /// is cancelled.</exception>
        /// <remarks>This is intended to be called by a plugin.</remarks>
        public static async Task<IPlugin> CreateFromCurrentProcessAsync(
            IRequestHandlers requestHandlers,
            ConnectionOptions options,
            CancellationToken sessionCancellationToken)
        {
            if (requestHandlers == null)
            {
                throw new ArgumentNullException(nameof(requestHandlers));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            sessionCancellationToken.ThrowIfCancellationRequested();

            var standardInput = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
            var standardOutput = new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8);
            var sender = new Sender(standardOutput);
            var receiver = new StandardInputReceiver(standardInput);
            var messageDispatcher = new MessageDispatcher(requestHandlers, new RequestIdGenerator());
            var connection = new Connection(messageDispatcher, sender, receiver, options);

            var process = Process.GetCurrentProcess();
            var filePath = process.MainModule.FileName;
            var pluginProcess = new PluginProcess(process);

            // Wire up event handlers before calling ConnectAsync(...).
            var plugin = new Plugin(
                filePath,
                connection,
                pluginProcess,
                isOwnProcess: true,
                idleTimeout: Timeout.InfiniteTimeSpan);

            try
            {
                await connection.ConnectAsync(sessionCancellationToken);
            }
            catch (Exception)
            {
                plugin.Dispose();

                throw;
            }

            return plugin;
        }

        private void DisposePlugin(IPlugin plugin)
        {
            UnregisterEventHandlers(plugin as Plugin);

            Lazy<Task<IPlugin>> lazyTask;

            if (_plugins.TryRemove(plugin.FilePath, out lazyTask))
            {
                if (lazyTask.IsValueCreated && lazyTask.Value.Status == TaskStatus.RanToCompletion)
                {
                    lazyTask.Value.Result.Dispose();
                }
            }
            else
            {
                plugin.Dispose();
            }
        }

        private void OnPluginFaulted(object sender, PluginEventArgs e)
        {
            DisposePlugin(e.Plugin);
        }

        private void OnPluginExited(object sender, PluginEventArgs e)
        {
            DisposePlugin(e.Plugin);
        }

        private void OnPluginIdle(object sender, PluginEventArgs e)
        {
            DisposePlugin(e.Plugin);
        }

        private void RegisterEventHandlers(Plugin plugin)
        {
            plugin.Exited += OnPluginExited;
            plugin.Faulted += OnPluginFaulted;
            plugin.Idle += OnPluginIdle;
        }

        private void UnregisterEventHandlers(Plugin plugin)
        {
            if (plugin != null)
            {
                plugin.Exited -= OnPluginExited;
                plugin.Faulted -= OnPluginFaulted;
                plugin.Idle -= OnPluginIdle;
            }
        }
    }
}