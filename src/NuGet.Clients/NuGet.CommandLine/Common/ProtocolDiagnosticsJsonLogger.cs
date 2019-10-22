// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using NuGet.Protocol.Utility;

namespace NuGet.CommandLine
{
    internal sealed class ProtocolDiagnosticsJsonLogger : IDisposable
    {
        private object _lock;
        private StreamWriter _streamWriter;
        private JsonWriter _jsonWriter;

        internal ProtocolDiagnosticsJsonLogger(string fileName)
        {
            _lock = new object();
            _streamWriter = new StreamWriter(fileName);
            _jsonWriter = new JsonTextWriter(_streamWriter);
            _jsonWriter.Formatting = Formatting.Indented;
            _jsonWriter.WriteStartArray();
        }

        internal void OnEvent(ProtocolDiagnosticEvent pde)
        {
            if (_lock == null)
            {
                throw new ObjectDisposedException(nameof(ProtocolDiagnosticsJsonLogger));
            }

            var timestamp = pde.Timestamp.ToString("o", CultureInfo.InvariantCulture);

            lock (_lock)
            {
                _jsonWriter.WriteStartObject();

                _jsonWriter.WritePropertyName("timestamp");
                _jsonWriter.WriteValue(timestamp);

                _jsonWriter.WritePropertyName("source");
                _jsonWriter.WriteValue(pde.Source);

                _jsonWriter.WritePropertyName("url");
                _jsonWriter.WriteValue(pde.Url);

                if (pde.HeaderDuration.HasValue)
                {
                    _jsonWriter.WritePropertyName("headerDuration");
                    _jsonWriter.WriteValue(pde.HeaderDuration.Value.TotalMilliseconds);
                }

                _jsonWriter.WritePropertyName("duration");
                _jsonWriter.WriteValue(pde.EventDuration.TotalMilliseconds);

                if (pde.HttpStatusCode.HasValue)
                {
                    _jsonWriter.WritePropertyName("httpStatusCode");
                    _jsonWriter.WriteValue(pde.HttpStatusCode.Value);
                }

                _jsonWriter.WritePropertyName("bytes");
                _jsonWriter.WriteValue(pde.Bytes);

                _jsonWriter.WritePropertyName("isSuccess");
                _jsonWriter.WriteValue(pde.IsSuccess);

                _jsonWriter.WritePropertyName("isRetry");
                _jsonWriter.WriteValue(pde.IsRetry);

                _jsonWriter.WritePropertyName("isCancelled");
                _jsonWriter.WriteValue(pde.IsCancelled);

                _jsonWriter.WriteEndObject();

                // In case process crashes or ctrl-c, make sure log is as usable as possible.
                _jsonWriter.Flush();
            }
        }

        public void Dispose()
        {
            _jsonWriter?.WriteEndArray();
            _jsonWriter?.Flush();
            _jsonWriter = null;

            _streamWriter?.Flush();
            _streamWriter?.Dispose();
            _streamWriter = null;

            _lock = null;
        }
    }
}
