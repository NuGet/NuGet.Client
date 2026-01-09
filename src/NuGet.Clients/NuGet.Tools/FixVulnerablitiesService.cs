// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Copilot;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.ServiceBroker;
using NuGet.Common;
using NuGet.PackageManagement.Telemetry;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;

namespace NuGetVSExtension
{
    [Export(typeof(IFixVulnerabilitiesService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class FixVulnerablitiesService : IFixVulnerabilitiesService
    {
        private const string AgentModeResponderServiceMoniker = "Microsoft.VisualStudio.Copilot.AgentModeResponder";
        private const string ChatUiPackageLoaded = "871c3e1c-e58c-4ce9-b6a7-26600555739a";
        //private const string ChatUiPackageAvailable = "a8984974-3a2f-4e50-810a-4cc51f6c1a04";
        //private const string CompletionsPackageAvailable = "a7f179b8-a8e8-4729-86e1-414bb0a103c8";
        //private const string AuthStatusDetermined = "c936efcc-6baa-4ad3-9c2b-7ba750acf18f";

        private static readonly Guid CopilotReadyUIContext = new(ChatUiPackageLoaded);

        //private static readonly string NuGetSolverToolName = "get-nuget-solver";  

        [Import(typeof(SVsFullAccessServiceBroker))]
        public IServiceBroker? ServiceBroker { get; set; }

        [Import(typeof(VisualStudioActivityLogger))]
        public ILogger? ActivityLogger { get; set; }

        public async Task LaunchFixVulnerabilitiesAsync(CancellationToken cancellationToken)
        {
            UIContext copilotReady = UIContext.FromUIContextGuid(CopilotReadyUIContext);
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (!copilotReady.IsActive)
            {
                SendTelemetryEvent(FixVulnerabilitiesWithCopilotErrorType.CopilotNotReady);
                ShowWarningMessage(Resources.Error_CopilotNotReady);
                return;
            }

            if (ServiceBroker == null)
            {
                // Unlikely to occur and would indicate a problem with VS, but should still be handled.
                SendTelemetryEvent(FixVulnerabilitiesWithCopilotErrorType.ServiceBrokerNotAvailable);
                ShowWarningMessage(Resources.Error_ServiceBrokerNotAvailable);
                return;
            }

            ICopilotService? copilotService = await ServiceBroker.GetProxyAsync<ICopilotService>(CopilotDescriptors.CopilotService, cancellationToken);
            using (copilotService as IDisposable)
            {
                if (copilotService is null)
                {
                    SendTelemetryEvent(FixVulnerabilitiesWithCopilotErrorType.CopilotServiceNotAvailable);
                    ShowWarningMessage(Resources.Error_CopilotServiceNotAvailable);
                    return;
                }

                // Create an identifier that will be visible in the session's telemetry
                CopilotClientId clientId = new("Microsoft.VisualStudio.NuGet.VulnerabilitiesInfoBar");
                CopilotThreadOptions options = new(clientId);

                await using (var thread = await copilotService.StartThreadAsync(options, cancellationToken))
                {
                    // Requests from this session will be visible in the Chat window
                    CopilotRequest request = new(Resources.Prompt_FixNuGetPackageVulnerabilities)
                    {
                        DirectedResponders = [new(AgentModeResponderServiceMoniker, new(CopilotDescriptors.CurrentResponderVersion))]
                    };

                    ICopilotFunctionProvider? cfp = await ServiceBroker.GetProxyAsync<ICopilotFunctionProvider>(CopilotDescriptors.McpToolService, cancellationToken);
                    using (cfp as IDisposable)
                    {
                        if (cfp is null)
                        {
                            SendTelemetryEvent(FixVulnerabilitiesWithCopilotErrorType.McpToolServiceNotAvailable);
                            ShowWarningMessage(Resources.Error_McpToolServiceNotAvailable);
                            return;
                        }

                        IReadOnlyList<CopilotFunctionDescriptor> functions = await cfp.GetFunctionsAsync(request.CorrelationId, cancellationToken);
                        CopilotRequest requestWithFunctions = request.WithFunctions(functions);

                        try
                        {
                            _ = await thread.Session.SendRequestAsync(requestWithFunctions, cancellationToken);
                            SendTelemetryEvent(FixVulnerabilitiesWithCopilotErrorType.None);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            SendTelemetryEvent(FixVulnerabilitiesWithCopilotErrorType.CopilotAccessDenied);
                            ActivityLogger?.LogError(ex.Message);
                            ShowWarningMessage(Resources.Error_CopilotAccessDenied);
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

            MessageHelper.ShowWarningMessage(message, Resources.Title_FixVulnerabilitiesWithCopilot);
        }

        private static void SendTelemetryEvent(FixVulnerabilitiesWithCopilotErrorType errorType)
        {
            var evt = NavigatedTelemetryEvent.CreateWithVulnerabilityInfoBarFixWithCopilot(errorType);
            TelemetryActivity.EmitTelemetryEvent(evt);
        }
    }
}
