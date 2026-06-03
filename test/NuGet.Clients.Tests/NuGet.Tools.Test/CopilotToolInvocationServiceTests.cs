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

        // Default DisplayName used by the test helper deliberately distinct from ToolServerName
        // so any test that accidentally relies on DisplayName matching breaks.
        private const string UnrelatedDisplayName = "User-Facing Fix Vulnerable Packages";

        private static readonly IReadOnlyCollection<string> AcceptableGroups = McpServerConstants.NuGetMCPServerNames;

        private static readonly ServiceMoniker TestServiceMoniker = new("test.moniker");

        private static CopilotMcpFunctionDescriptor CreateMcpDescriptor(
            string serverNameOfFunction = ToolName,
            string group = McpServerConstants.NuGetMCPServerName,
            string? displayName = null,
            string? name = null)
        {
            return new CopilotMcpFunctionDescriptor(
                providerMoniker: TestServiceMoniker,
                serverNameOfFunction: serverNameOfFunction,
                configurationPath: string.Empty,
                name: name ?? $"mcp_{group}_{serverNameOfFunction}",
                displayName: displayName ?? UnrelatedDisplayName,
                description: "desc",
                confirmation: CopilotConfirmationRequirement.NotRequired)
            {
                Group = group,
            };
        }

        [Fact]
        public void IsAvailable_VisualStudioGroup_ReturnsTrue()
        {
            var functions = new List<CopilotFunctionDescriptor>
            {
                CreateMcpDescriptor(group: McpServerConstants.NuGetMCPServerName),
            };

            Assert.True(CopilotToolInvocationService.IsAvailable(functions, ToolName, AcceptableGroups));
        }

        [Fact]
        public void IsAvailable_McpRegistryGroup_ReturnsTrue()
        {
            var functions = new List<CopilotFunctionDescriptor>
            {
                CreateMcpDescriptor(group: McpServerConstants.ComMicrosoftNuGetMCPServerName),
            };

            Assert.True(CopilotToolInvocationService.IsAvailable(functions, ToolName, AcceptableGroups));
        }

        [Fact]
        public void IsAvailable_UnknownGroup_ReturnsFalse()
        {
            var functions = new List<CopilotFunctionDescriptor>
            {
                CreateMcpDescriptor(group: "someone.else/nuget"),
            };

            Assert.False(CopilotToolInvocationService.IsAvailable(functions, ToolName, AcceptableGroups));
        }

        [Fact]
        public void IsAvailable_DifferentCaseGroup_ReturnsTrue()
        {
            // Group comparison is OrdinalIgnoreCase to match VS behavior: "nuget" matches "NuGet".
            var functions = new List<CopilotFunctionDescriptor>
            {
                CreateMcpDescriptor(group: McpServerConstants.NuGetMCPServerName.ToLowerInvariant()),
            };

            Assert.True(CopilotToolInvocationService.IsAvailable(functions, ToolName, AcceptableGroups));
        }

        [Fact]
        public void IsAvailable_WrongServerName_ReturnsFalse()
        {
            var functions = new List<CopilotFunctionDescriptor>
            {
                CreateMcpDescriptor(serverNameOfFunction: "some_other_tool"),
            };

            Assert.False(CopilotToolInvocationService.IsAvailable(functions, ToolName, AcceptableGroups));
        }

        [Fact]
        public void IsAvailable_DisplayNameMatchesButServerNameDoesNot_ReturnsFalse()
        {
            // We match against ServerNameOfFunction, not DisplayName: a function whose
            // DisplayName happens to equal the required tool name but whose ServerNameOfFunction is
            // different must not match.
            var functions = new List<CopilotFunctionDescriptor>
            {
                CreateMcpDescriptor(serverNameOfFunction: "some_other_tool", displayName: ToolName),
            };

            Assert.False(CopilotToolInvocationService.IsAvailable(functions, ToolName, AcceptableGroups));
        }

        [Fact]
        public void IsAvailable_NullGroup_ReturnsFalse()
        {
            // A descriptor with no Group cannot belong to any acceptable group.
            var functions = new List<CopilotFunctionDescriptor>
            {
                CreateMcpDescriptor(group: null!),
            };

            Assert.False(CopilotToolInvocationService.IsAvailable(functions, ToolName, AcceptableGroups));
        }

        [Fact]
        public void IsAvailable_NullFunctions_ReturnsFalse()
        {
            Assert.False(CopilotToolInvocationService.IsAvailable(functions: null, ToolName, AcceptableGroups));
        }

        [Fact]
        public void IsAvailable_EmptyFunctions_ReturnsFalse()
        {
            Assert.False(CopilotToolInvocationService.IsAvailable(new List<CopilotFunctionDescriptor>(), ToolName, AcceptableGroups));
        }

        [Fact]
        public void IsAvailable_NonMcpDescriptorWithMatchingName_ReturnsFalse()
        {
            // A non-MCP descriptor (e.g. a local Copilot function) must not match even if its Name
            // collides with the composed fully-qualified MCP tool name.
            var localFn = new CopilotLocalFunctionDescriptor(
                name: $"mcp_{McpServerConstants.NuGetMCPServerName}_{ToolName}",
                description: "desc",
                confirmation: CopilotConfirmationRequirement.NotRequired)
            {
                Group = McpServerConstants.NuGetMCPServerName,
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
                CreateMcpDescriptor(group: McpServerConstants.ComMicrosoftNuGetMCPServerName),
            };

            Assert.True(CopilotToolInvocationService.IsAvailable(functions, ToolName, AcceptableGroups));
        }
    }
}
