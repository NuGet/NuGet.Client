// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Copilot;

namespace NuGetVSExtension
{
    /// <summary>
    /// Holds an active Copilot thread and the available MCP functions.
    /// Disposing this session releases all underlying Copilot/MCP resources.
    /// </summary>
    internal sealed class CopilotToolSession : IAsyncDisposable
    {
        private readonly IDisposable? _copilotServiceDisposable;

        internal CopilotToolSession(
            CopilotThread thread,
            IReadOnlyList<CopilotFunctionDescriptor> functions,
            IDisposable? copilotServiceDisposable)
        {
            Thread = thread;
            Functions = functions;
            _copilotServiceDisposable = copilotServiceDisposable;
        }

        public CopilotThread Thread { get; }

        public IReadOnlyList<CopilotFunctionDescriptor> Functions { get; }

        public async ValueTask DisposeAsync()
        {
            await Thread.DisposeAsync();
            _copilotServiceDisposable?.Dispose();
        }
    }
}
