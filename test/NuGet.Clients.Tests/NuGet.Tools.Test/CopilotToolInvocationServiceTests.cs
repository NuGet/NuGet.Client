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
    public class CopilotToolInvocationServiceTests
    {
        private const string ToolName = McpServerConstants.NuGetSolverToolName;

        private static readonly IReadOnlyCollection<string> AcceptableGroups = McpServerConstants.NuGetMcpServerNames;

        private static readonly ServiceMoniker TestServiceMoniker = new("test.moniker");

        private static CopilotMcpFunctionDescriptor CreateMcpDescriptor(
            string serverNameOfFunction = ToolName,
            string group = McpServerConstants.NuGetMcpServerName,
            string? name = null)
        {
            return new CopilotMcpFunctionDescriptor(
                providerMoniker: TestServiceMoniker,
                serverNameOfFunction: serverNameOfFunction,
                configurationPath: string.Empty,
                name: name ?? $"mcp_{group}_{serverNameOfFunction}",
                displayName: "test display name",
                description: "desc",
                confirmation: CopilotConfirmationRequirement.NotRequired)
            {
                Group = group,
            };
        }

        [Theory]
        // Group matching
        [InlineData(McpServerConstants.NuGetMcpServerName, ToolName, null, true)]   // Visual Studio group
        [InlineData(McpServerConstants.ComMicrosoftNuGetMcpServerName, ToolName, null, true)]   // MCP registry group
        [InlineData("nuget", ToolName, null, true)]   // Group match is case-insensitive
        [InlineData("someone.else/nuget", ToolName, null, false)]  // Unknown group
        [InlineData(null, ToolName, null, false)]  // Null group
        // ServerNameOfFunction matching
        [InlineData(McpServerConstants.NuGetMcpServerName, "some_other_tool", null, false)]  // Wrong ServerNameOfFunction
        [InlineData(McpServerConstants.NuGetMcpServerName, "FIX_VULNERABLE_PACKAGES", null, true)]  // ServerNameOfFunction match is case-insensitive
        // Regression guard: previous impl matched on CopilotFunctionDescriptor.Name; now we match on ServerNameOfFunction.
        [InlineData(McpServerConstants.NuGetMcpServerName, "some_other_tool", ToolName, false)]  // Name matches but ServerNameOfFunction does not
        public void IsAvailable_SingleMcpDescriptor_ReturnsExpected(
            string? group,
            string serverNameOfFunction,
            string? name,
            bool expected)
        {
            var functions = new List<CopilotFunctionDescriptor>
            {
                CreateMcpDescriptor(serverNameOfFunction: serverNameOfFunction, group: group!, name: name),
            };

            Assert.Equal(expected, CopilotToolInvocationService.IsAvailable(functions, ToolName, AcceptableGroups));
        }

        [Fact]
        public void IsAvailable_NullOrEmptyFunctions_ReturnsFalse()
        {
            Assert.False(CopilotToolInvocationService.IsAvailable(functions: null, ToolName, AcceptableGroups));
            Assert.False(CopilotToolInvocationService.IsAvailable(new List<CopilotFunctionDescriptor>(), ToolName, AcceptableGroups));
        }

        [Fact]
        public void IsAvailable_NonMcpDescriptorWithMatchingName_ReturnsFalse()
        {
            // A non-MCP descriptor (e.g. a local Copilot function) must not match even if its Name
            // collides with the composed fully-qualified MCP tool name.
            var localFn = new CopilotLocalFunctionDescriptor(
                name: $"mcp_{McpServerConstants.NuGetMcpServerName}_{ToolName}",
                description: "desc",
                confirmation: CopilotConfirmationRequirement.NotRequired)
            {
                Group = McpServerConstants.NuGetMcpServerName,
            };

            var functions = new List<CopilotFunctionDescriptor> { localFn };

            Assert.False(CopilotToolInvocationService.IsAvailable(functions, ToolName, AcceptableGroups));
        }

        [Fact]
        public void IsAvailable_NoiseBeforeMatch_ReturnsTrue()
        {
            var functions = new List<CopilotFunctionDescriptor>
            {
                CreateMcpDescriptor(serverNameOfFunction: "unrelated_tool"),
                CreateMcpDescriptor(group: McpServerConstants.ComMicrosoftNuGetMcpServerName),
            };

            Assert.True(CopilotToolInvocationService.IsAvailable(functions, ToolName, AcceptableGroups));
        }
    }
}
