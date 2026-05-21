// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Copilot;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.ServiceBroker;
using NuGet.VisualStudio;

namespace NuGetVSExtension
{
    [Export(typeof(ICopilotToolInvocationService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class CopilotToolInvocationService : ICopilotToolInvocationService
    {
        private const string AuthStatusDetermined = "c936efcc-6baa-4ad3-9c2b-7ba750acf18f";
        private static readonly Guid CopilotReadyUIContext = new(AuthStatusDetermined);

        [Import(typeof(SVsFullAccessServiceBroker))]
        public IServiceBroker? ServiceBroker { get; set; }

        public async Task<CopilotToolSessionResult> TryCreateToolSessionAsync(
            CopilotClientId clientId,
            Guid correlationId,
            string requiredToolName,
            CancellationToken cancellationToken)
        {
            // 1. Check if the user is signed-in to GitHub Copilot
            UIContext copilotReady = UIContext.FromUIContextGuid(CopilotReadyUIContext);
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (!copilotReady.IsActive)
            {
                return CopilotToolSessionResult.Failure(CopilotToolSessionError.CopilotNotReady);
            }

            // 2. Verify service broker is available
            if (ServiceBroker == null)
            {
                return CopilotToolSessionResult.Failure(CopilotToolSessionError.ServiceBrokerNotAvailable);
            }

            // 3. Acquire Copilot service
            ICopilotService? copilotService = await ServiceBroker.GetProxyAsync<ICopilotService>(CopilotDescriptors.CopilotService, cancellationToken);
            if (copilotService is null)
            {
                return CopilotToolSessionResult.Failure(CopilotToolSessionError.CopilotServiceNotAvailable);
            }

            // 4. Acquire MCP tool function provider
            ICopilotFunctionProvider? cfp = await ServiceBroker.GetProxyAsync<ICopilotFunctionProvider>(CopilotDescriptors.McpToolService, cancellationToken);
            if (cfp is null)
            {
                (copilotService as IDisposable)?.Dispose();
                return CopilotToolSessionResult.Failure(CopilotToolSessionError.McpToolServiceNotAvailable);
            }

            // 5. Verify the required tool is available
            IReadOnlyList<CopilotFunctionDescriptor> functions = await cfp.GetFunctionsAsync(correlationId, cancellationToken);
            if (functions is null || !functions.Any(f => string.Equals(f.Name, requiredToolName, StringComparison.OrdinalIgnoreCase)))
            {
                (cfp as IDisposable)?.Dispose();
                (copilotService as IDisposable)?.Dispose();
                return CopilotToolSessionResult.Failure(CopilotToolSessionError.ToolNotAvailable);
            }

            // 6. Start Copilot thread
            CopilotThreadOptions options = new(clientId);
            ICopilotThread thread = await copilotService.StartThreadAsync(options, cancellationToken);

            return CopilotToolSessionResult.Success(
                new CopilotToolSession(
                    thread,
                    functions,
                    copilotServiceDisposable: copilotService as IDisposable,
                    functionProviderDisposable: cfp as IDisposable));
        }
    }
}
