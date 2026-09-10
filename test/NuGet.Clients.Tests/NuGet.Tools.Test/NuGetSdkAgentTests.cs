// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Copilot;
using Microsoft.VisualStudio.Copilot.Internal.Mcp;
using Moq;
using NuGet.PackageManagement.VisualStudio;
using NuGetVSExtension;
using Xunit;

namespace NuGet.Tools.Test
{
    public class NuGetSdkAgentTests
    {
        private static readonly ServiceMoniker TestServiceMoniker = new("test.moniker");

        [Fact]
        public async Task SelectPreferredNuGetMcpToolsAsync_InBoxAndRegistryServersActive_PrefersInBoxTools()
        {
            CopilotMcpFunctionDescriptor inBoxTool = CreateMcpDescriptor(
                McpServerConstants.NuGetMcpServerName,
                McpServerConstants.NuGetSolverToolName);
            CopilotMcpFunctionDescriptor registryTool = CreateMcpDescriptor(
                McpServerConstants.ComMicrosoftNuGetMcpServerName,
                McpServerConstants.NuGetSolverToolName);

            IMcpServerInfoService infoService = CreateInfoService(
                (McpServerConstants.NuGetMcpServerName, McpServerState.Active),
                (McpServerConstants.ComMicrosoftNuGetMcpServerName, McpServerState.Active));

            IReadOnlyList<CopilotMcpFunctionDescriptor> result = await NuGetSdkAgent.SelectPreferredNuGetMcpToolsAsync(
                infoService,
                [registryTool, inBoxTool],
                CancellationToken.None);

            Assert.Collection(result, tool => Assert.Same(inBoxTool, tool));
        }

        [Fact]
        public async Task SelectPreferredNuGetMcpToolsAsync_InBoxDisabled_ReturnsRegistryTools()
        {
            CopilotMcpFunctionDescriptor inBoxTool = CreateMcpDescriptor(
                McpServerConstants.NuGetMcpServerName,
                McpServerConstants.NuGetSolverToolName);
            CopilotMcpFunctionDescriptor registryTool = CreateMcpDescriptor(
                McpServerConstants.ComMicrosoftNuGetMcpServerName,
                McpServerConstants.NuGetSolverToolName);

            IMcpServerInfoService infoService = CreateInfoService(
                (McpServerConstants.NuGetMcpServerName, McpServerState.Disabled),
                (McpServerConstants.ComMicrosoftNuGetMcpServerName, McpServerState.Suspended));

            IReadOnlyList<CopilotMcpFunctionDescriptor> result = await NuGetSdkAgent.SelectPreferredNuGetMcpToolsAsync(
                infoService,
                [inBoxTool, registryTool],
                CancellationToken.None);

            Assert.Collection(result, tool => Assert.Same(registryTool, tool));
        }

        [Fact]
        public async Task SelectPreferredNuGetMcpToolsAsync_MultipleInBoxTools_ReturnsAllInBoxTools()
        {
            CopilotMcpFunctionDescriptor fixTool = CreateMcpDescriptor(
                McpServerConstants.NuGetMcpServerName,
                McpServerConstants.NuGetSolverToolName);
            CopilotMcpFunctionDescriptor reviewTool = CreateMcpDescriptor(
                McpServerConstants.NuGetMcpServerName,
                McpServerConstants.PackageSourceMappingToolName);

            IMcpServerInfoService infoService = CreateInfoService(
                (McpServerConstants.NuGetMcpServerName, McpServerState.Active));

            IReadOnlyList<CopilotMcpFunctionDescriptor> result = await NuGetSdkAgent.SelectPreferredNuGetMcpToolsAsync(
                infoService,
                [fixTool, reviewTool],
                CancellationToken.None);

            Assert.Equal([fixTool, reviewTool], result);
        }

        [Fact]
        public async Task SelectPreferredNuGetMcpToolsAsync_NoActiveNuGetServer_ReturnsEmpty()
        {
            CopilotMcpFunctionDescriptor inBoxTool = CreateMcpDescriptor(
                McpServerConstants.NuGetMcpServerName,
                McpServerConstants.NuGetSolverToolName);
            CopilotMcpFunctionDescriptor registryTool = CreateMcpDescriptor(
                McpServerConstants.ComMicrosoftNuGetMcpServerName,
                McpServerConstants.NuGetSolverToolName);

            IMcpServerInfoService infoService = CreateInfoService(
                (McpServerConstants.NuGetMcpServerName, McpServerState.Disabled),
                (McpServerConstants.ComMicrosoftNuGetMcpServerName, McpServerState.Failed));

            IReadOnlyList<CopilotMcpFunctionDescriptor> result = await NuGetSdkAgent.SelectPreferredNuGetMcpToolsAsync(
                infoService,
                [inBoxTool, registryTool],
                CancellationToken.None);

            Assert.Empty(result);
        }

        private static CopilotMcpFunctionDescriptor CreateMcpDescriptor(string group, string toolName)
        {
            return new CopilotMcpFunctionDescriptor(
                providerMoniker: TestServiceMoniker,
                serverNameOfFunction: toolName,
                configurationPath: string.Empty,
                name: $"mcp_{group}_{toolName}",
                displayName: toolName,
                description: "desc",
                confirmation: CopilotConfirmationRequirement.NotRequired)
            {
                Group = group,
            };
        }

        private static IMcpServerInfoService CreateInfoService(params (string ServerName, McpServerState? State)[] states)
        {
            Dictionary<string, McpServerState?> map = states.ToDictionary(state => state.ServerName, state => state.State);

            var mock = new Mock<IMcpServerInfoService>();
            mock.Setup(service => service.GetServerStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string name, CancellationToken _) =>
                    new ValueTask<McpServerState?>(map.TryGetValue(name, out McpServerState? state) ? state : null));

            return mock.Object;
        }
    }
}
