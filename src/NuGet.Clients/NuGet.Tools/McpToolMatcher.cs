// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Copilot;

namespace NuGetVSExtension
{
    /// <summary>
    /// Identifies a NuGet MCP tool inside the descriptor list returned by
    /// <see cref="ICopilotFunctionProvider.GetFunctionsAsync"/>.
    /// </summary>
    /// <remarks>
    /// The same logical NuGet MCP tool can be exposed under different
    /// <see cref="CopilotFunctionDescriptor.Group"/> values depending on how the MCP server was
    /// installed (in-VS vs. installed via the Anthropic/GitHub MCP registry), so we match on the
    /// MCP descriptor's <see cref="CopilotMcpFunctionDescriptor.ServerNameOfFunction"/> + <c>Group</c>
    /// instead of the composed fully-qualified <see cref="CopilotFunctionDescriptor.Name"/>.
    /// </remarks>
    internal static class McpToolMatcher
    {
        public static bool IsAvailable(
            IReadOnlyList<CopilotFunctionDescriptor>? functions,
            string requiredServerNameOfFunction,
            IReadOnlyCollection<string> acceptableGroups)
        {
            if (functions is null)
            {
                return false;
            }

            return functions
                .OfType<CopilotMcpFunctionDescriptor>()
                .Any(f => !f.IsError
                       && string.Equals(f.ServerNameOfFunction, requiredServerNameOfFunction, StringComparison.Ordinal)
                       && acceptableGroups.Contains(f.Group, StringComparer.Ordinal));
        }
    }
}
