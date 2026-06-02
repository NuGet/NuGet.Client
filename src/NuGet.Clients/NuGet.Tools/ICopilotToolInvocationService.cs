// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Copilot;

namespace NuGetVSExtension
{
    /// <summary>
    /// Handles shared pre-flight checks for invoking NuGet MCP tools via Copilot.
    /// Verifies Copilot readiness, acquires required services, and validates tool availability.
    /// </summary>
    internal interface ICopilotToolInvocationService
    {
        /// <summary>
        /// Attempts to create a ready-to-use Copilot tool session.
        /// On success, the returned session owns all underlying resources and must be disposed by the caller.
        /// </summary>
        /// <param name="clientId">The Copilot client identity for telemetry attribution.</param>
        /// <param name="correlationId">The correlation ID from the caller's <see cref="CopilotRequest"/> (used for function discovery).</param>
        /// <param name="requiredServerNameOfFunction">The MCP server's name for the tool (e.g. <c>fix_vulnerable_packages</c>) that must be available.</param>
        /// <param name="acceptableGroups">The set of MCP groups under which the tool is considered acceptable (e.g. the in-VS group and the MCP registry group).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A result indicating success (with session) or failure (with error type).</returns>
        Task<CopilotToolSessionResult> TryCreateToolSessionAsync(
            CopilotClientId clientId,
            CopilotCorrelationId correlationId,
            string requiredServerNameOfFunction,
            IReadOnlyCollection<string> acceptableGroups,
            CancellationToken cancellationToken);
    }
}
