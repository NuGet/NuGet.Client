// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace NuGet.Protocol.Plugins
{
    internal sealed class ProcessLogMessage : PluginLogMessage
    {
        private readonly int _processId;
        private readonly string _processName;

        internal ProcessLogMessage()
        {
            using (var process = Process.GetCurrentProcess())
            {
                _processId = process.Id;
                _processName = process.ProcessName;
            }
        }

        public override string ToString()
        {
            var message = new JObject(
                new JProperty("process ID", _processId),
                new JProperty("process name", _processName));

            return ToString("process", message);
        }
    }
}