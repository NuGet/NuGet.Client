// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Design;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Copilot;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;

namespace NuGet.Tools.Commands
{
    internal sealed class PackageSourceMapperCommand
    {
        private const string AgentModeResponderServiceMoniker = "Microsoft.VisualStudio.Copilot.AgentModeResponder";
        private const string ServiceName = "Microsoft.VisualStudio.Copilot.SolutionContextProvider";

        private static readonly ServiceRpcDescriptor ProviderDescriptor = CopilotDescriptors.CreateContextProviderDescriptor(ServiceName);
        private static readonly CopilotContextDescriptor ContextDescriptor = new CopilotContextDescriptor(
                    "SolutionFile",
                    "solution file context",
                    CopilotDefaultTypes.StringName);

        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandID = PkgCmdIDList.cmdidOnboardPackageSourceMapping;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = GuidList.guidOnboardPackageSourceMappingCmdSet;

        private readonly OleMenuCommandService _oleMenuCommandService;
        private readonly ICopilotToolInvocationService _toolInvocationService;
        private readonly IVsSolutionManager _solutionManager;

        public PackageSourceMapperCommand(
            OleMenuCommandService oleMenuCommandService,
            ICopilotToolInvocationService toolInvocationService,
            IVsSolutionManager solutionManager)
        {
            _oleMenuCommandService = oleMenuCommandService ?? throw new ArgumentNullException(nameof(oleMenuCommandService));
            _toolInvocationService = toolInvocationService ?? throw new ArgumentNullException(nameof(toolInvocationService));
            _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
        }

        public void Initialize()
        {
            var commandId = new CommandID(CommandSet, CommandID);
            var command = new OleMenuCommand(ExecutePackageSourceMapperCommand, commandId);
            _oleMenuCommandService.AddCommand(command);
        }

        private void ExecutePackageSourceMapperCommand(object sender, EventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(() => ExecuteAsync(CancellationToken.None))
                .PostOnFailure(nameof(NuGetPackage), nameof(ExecutePackageSourceMapperCommand));
        }

        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            CopilotClientId clientId = new("Microsoft.VisualStudio.NuGet.PackageSourceMapper");

            CopilotRequest request = new("Onboard this repo to package source mapping")
            {
                Guidance = "Use absolute paths when invoking MCP Tools.",
                DirectedResponders = [new(AgentModeResponderServiceMoniker, new(CopilotDescriptors.CurrentResponderVersion))]
            };

            CopilotToolSessionResult result = await _toolInvocationService.TryCreateToolSessionAsync(
                clientId,
                request.CorrelationId,
                McpServerConstants.PackageSourceMappingFullyQualifiedToolName,
                cancellationToken);

            if (!result.IsSuccess)
            {
                // TODO: Add telemetry and user-facing error messages
                return;
            }

            await using CopilotToolSession session = result.Session!;

            string solutionPathContext = $"The current solution file path is: {GetSolutionPath()}.";
            CopilotContext context = new CopilotContext(ProviderDescriptor.Moniker, ContextDescriptor, request.CorrelationId, solutionPathContext);
            CopilotRequest requestWithFunctionsAndContext = request.WithFunctions(session.Functions).WithContext(context);

            _ = await session.Thread.Session.SendRequestAsync(requestWithFunctionsAndContext, cancellationToken);
        }

        private string GetSolutionPath() => _solutionManager?.SolutionDirectory ?? string.Empty;
    }
}
