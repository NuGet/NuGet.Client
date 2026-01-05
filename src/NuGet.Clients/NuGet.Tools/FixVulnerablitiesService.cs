// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

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
using NuGet.Common;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;

namespace NuGetVSExtension
{
    [Export(typeof(IFixVulnerabilitiesService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class FixVulnerablitiesService : IFixVulnerabilitiesService
    {
        private const string LogEntrySource = "NuGet Package Manager";
        private const string AgentModeResponderServiceMoniker = "Microsoft.VisualStudio.Copilot.AgentModeResponder";
        private const string ChatUiPackageLoaded = "871c3e1c-e58c-4ce9-b6a7-26600555739a";
        //private const string ChatUiPackageAvailable = "a8984974-3a2f-4e50-810a-4cc51f6c1a04";
        //private const string CompletionsPackageAvailable = "a7f179b8-a8e8-4729-86e1-414bb0a103c8";
        //private const string AuthStatusDetermined = "c936efcc-6baa-4ad3-9c2b-7ba750acf18f";
        private const string MessageBoxHeader = "Fix Vulnerabilities with GitHub Copilot";

        private static readonly Guid CopilotReadyUIContext = new(ChatUiPackageLoaded);

        private static readonly string NuGetSolverToolName = "get-nuget-solver";
        private static readonly string FixVulnerabilitiesCopilotPrompt = "fix my vulnerabilities";

        [Import(typeof(SVsFullAccessServiceBroker))]
        public IServiceBroker? ServiceBroker { get; set; }

        [Import(typeof(VisualStudioActivityLogger))]
        public ILogger? ActivityLogger { get; set; }

        public async Task LaunchFixVulnerabilitiesAsync(CancellationToken cancellationToken)
        {
            // TODO: When not logged in, the Copilot Service is null.We may not need to handle the UI Context
            // which would simplify the process and not require us to switch to the main thread.

            UIContext copilotReady = UIContext.FromUIContextGuid(CopilotReadyUIContext);
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (!copilotReady.IsActive)
            {
                {
                    ShowWarningMessage("GitHub Copilot is not ready. Please ensure GitHub Copilot is installed and signed in.");
                    return;
                }
            }

            if (ServiceBroker == null)
            {
                // Highly unlikely to occur and would indicate a problem with VS, but we should still handle it.
                ActivityLogger?.LogWarning("Service Broker is not available.");
                ShowWarningMessage("Service Broker is not available. Please ensure Visual Studio is running correctly.");
                return;
            }

            ICopilotService? copilotService = await ServiceBroker.GetProxyAsync<ICopilotService>(CopilotDescriptors.CopilotService, cancellationToken);
            using (copilotService as IDisposable)
            {
                if (copilotService is null)
                {
                    ActivityLogger?.LogWarning("GitHub Copilot Service is not available.");
                    ShowWarningMessage("GitHub Copilot Service is not available. Please ensure GitHub Copilot is installed and signed in.");
                    return;
                }

                // create an identifier that will be visible in the session's telemetry
                CopilotClientId clientId = new("Microsoft.VisualStudio.NuGet.VulnerabilitiesInfoBar");
                CopilotThreadOptions options = new(clientId);

                await using (var thread = await copilotService.StartThreadAsync(options, cancellationToken))
                {
                    // Requests from this session will be visible in the Chat window
                    CopilotRequest request = new(FixVulnerabilitiesCopilotPrompt)
                    {
                        DirectedResponders = [new(AgentModeResponderServiceMoniker, new(CopilotDescriptors.CurrentResponderVersion))]
                    };

                    ICopilotFunctionProvider? cfp = await ServiceBroker.GetProxyAsync<ICopilotFunctionProvider>(CopilotDescriptors.McpToolService, cancellationToken);
                    using (cfp as IDisposable)
                    {
                        if (cfp is null)
                        {
                            ActivityLogger?.LogWarning("MCP Tool Service is not available.");
                            ShowWarningMessage("MCP Tool Service is not available. Please ensure GitHub Copilot is installed and signed in.");
                            return;
                        }

                        IReadOnlyList<CopilotFunctionDescriptor> functions = await cfp.GetFunctionsAsync(request.CorrelationId, cancellationToken);
                        ActivityLogger?.LogInformation($"Retrieved {functions.Count} functions from MCP Tool Service. \n{string.Join(", ", functions.Select(f => f.Name))}");
                        CopilotRequest requestWithFunctions = request.WithFunctions(functions);

                        //var requestOptions = new CopilotRequestOptions()
                        //{
                        //    ToolMode = CopilotToolMode.RequireSpecific(NuGetSolverToolName)
                        //};

                        try
                        {
                            _ = await thread.Session.SendRequestAsync(requestWithFunctions, cancellationToken);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            ActivityLog.LogError(LogEntrySource, ex.Message);
                            ShowWarningMessage("Access denied. Please ensure you are signed in to Copilot.");
                        }
                    }
                }
            }
        }

        private static void ShowWarningMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            MessageHelper.ShowWarningMessage(message, MessageBoxHeader);
        }
    }
}
