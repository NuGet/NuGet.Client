// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Copilot;
using Microsoft.VisualStudio.Copilot.Sdk;

namespace NuGetVSExtension
{
#pragma warning disable VSCOPILOT_BACKEND // Experimental SDK harness contracts.
    [CopilotSdkAgent(
        AgentName,
        DisplayName = "NuGet",
        Description = "Helps resolve NuGet package management issues.",
        IncludeBuiltInContributions = true,
        Usage = AgentUsage.Programmatic)]
    internal sealed class NuGetSdkAgent : ICopilotSdkAgent
    {
        internal const string AgentName = "nuget";

        public Task<string> GetSystemPromptAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(
                "Use the available NuGet MCP tools to address the user's NuGet package management request. " +
                "Use absolute paths when invoking MCP tools.");
        }

        public Task<IReadOnlyList<CopilotFunctionDescriptor>> GetToolsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<CopilotFunctionDescriptor>>([]);
        }
    }
#pragma warning restore VSCOPILOT_BACKEND
}
