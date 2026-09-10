// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Copilot;
using NuGet.PackageManagement.VisualStudio;
using NuGetVSExtension;
using Xunit;

namespace NuGet.Tools.Test
{
    public class NuGetSdkAgentTests
    {
        private static readonly ServiceMoniker TestServiceMoniker = new("test.moniker");

        [Fact]
        public void SelectPreferredNuGetMcpTools_InBoxAndRegistryTools_PrefersInBoxTools()
        {
            CopilotMcpFunctionDescriptor inBoxTool = CreateMcpDescriptor(
                McpServerConstants.NuGetMcpServerName,
                McpServerConstants.NuGetSolverToolName);
            CopilotMcpFunctionDescriptor registryTool = CreateMcpDescriptor(
                McpServerConstants.ComMicrosoftNuGetMcpServerName,
                McpServerConstants.NuGetSolverToolName);

            IReadOnlyList<CopilotMcpFunctionDescriptor> result = NuGetSdkAgent.SelectPreferredNuGetMcpTools(
                [registryTool, inBoxTool]);

            Assert.Collection(result, tool => Assert.Same(inBoxTool, tool));
        }

        [Fact]
        public void SelectPreferredNuGetMcpTools_OnlyRegistryTools_ReturnsRegistryTools()
        {
            CopilotMcpFunctionDescriptor registryTool = CreateMcpDescriptor(
                McpServerConstants.ComMicrosoftNuGetMcpServerName,
                McpServerConstants.NuGetSolverToolName);

            IReadOnlyList<CopilotMcpFunctionDescriptor> result = NuGetSdkAgent.SelectPreferredNuGetMcpTools(
                [registryTool]);

            Assert.Collection(result, tool => Assert.Same(registryTool, tool));
        }

        [Fact]
        public void SelectPreferredNuGetMcpTools_MultipleInBoxTools_ReturnsAllInBoxTools()
        {
            CopilotMcpFunctionDescriptor fixTool = CreateMcpDescriptor(
                McpServerConstants.NuGetMcpServerName,
                McpServerConstants.NuGetSolverToolName);
            CopilotMcpFunctionDescriptor reviewTool = CreateMcpDescriptor(
                McpServerConstants.NuGetMcpServerName,
                McpServerConstants.PackageSourceMappingToolName);

            IReadOnlyList<CopilotMcpFunctionDescriptor> result = NuGetSdkAgent.SelectPreferredNuGetMcpTools(
                [fixTool, reviewTool]);

            Assert.Equal([fixTool, reviewTool], result);
        }

        [Fact]
        public void SelectPreferredNuGetMcpTools_NoNuGetTools_ReturnsEmpty()
        {
            CopilotMcpFunctionDescriptor unrelatedTool = CreateMcpDescriptor(
                "someone.else/server",
                "unrelated_tool");

            IReadOnlyList<CopilotMcpFunctionDescriptor> result = NuGetSdkAgent.SelectPreferredNuGetMcpTools(
                [unrelatedTool]);

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
    }
}
