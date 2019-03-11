// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;

namespace NuGet.Protocol.Plugins
{
    internal sealed class PluginInstanceLogMessage : PluginLogMessage
    {
        private readonly int _processId;

        internal PluginInstanceLogMessage(int processId)
        {
            _processId = processId;
        }

        public override string ToString()
        {
            var message = new JObject(new JProperty("process ID", _processId));

            return ToString("plugin instance", message);
        }
    }
}