// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using NuGet.Common;

namespace NuGet.Protocol.Plugins
{
    internal sealed class PluginLogger : IPluginLogger
    {
        private bool _isDisposed;
        private readonly Lazy<StreamWriter> _streamWriter;
        private readonly object _streamWriterLock;

        internal static PluginLogger DefaultInstance { get; } = new PluginLogger(new EnvironmentVariableWrapper());

        public bool IsEnabled { get; }

        internal PluginLogger(IEnvironmentVariableReader environmentVariableReader)
        {
            if (environmentVariableReader == null)
            {
                throw new ArgumentNullException(nameof(environmentVariableReader));
            }

            var value = environmentVariableReader.GetEnvironmentVariable(EnvironmentVariableConstants.EnableLog);

            IsEnabled = bool.TryParse(value, out var enable) && enable;

            _streamWriter = new Lazy<StreamWriter>(CreateStreamWriter);
            _streamWriterLock = new object();
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                if (_streamWriter.IsValueCreated)
                {
                    _streamWriter.Value.Dispose();
                }

                GC.SuppressFinalize(this);
                _isDisposed = true;
            }
        }

        public void Write(IPluginLogMessage message)
        {
            if (!IsEnabled)
            {
                return;
            }

            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(PluginLogger));
            }

            if (message == null)
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(message));
            }

            lock (_streamWriterLock)
            {
                _streamWriter.Value.WriteLine(message.ToString());
            }
        }

        private StreamWriter CreateStreamWriter()
        {
            if (IsEnabled)
            {
                FileInfo file;

                using (var process = Process.GetCurrentProcess())
                {
                    file = new FileInfo(process.MainModule.FileName);
                }

                var fileName = $"NuGet_PluginLogFor_{Path.GetFileNameWithoutExtension(file.Name)}.log";
                var stream = File.OpenWrite(fileName);

                try
                {
                    var streamWriter = new StreamWriter(stream);

                    streamWriter.AutoFlush = true;

                    return streamWriter;
                }
                catch (Exception)
                {
                    stream.Dispose();

                    throw;
                }
            }

            return StreamWriter.Null;
        }
    }
}