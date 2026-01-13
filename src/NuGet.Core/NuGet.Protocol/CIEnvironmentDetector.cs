// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Detects CI/CD environment from environment variables.
    /// Currently supports GitHub Actions, Azure DevOps, and generic CI.
    /// </summary>
    public static class CIEnvironmentDetector
    {
        /// <summary>
        /// Environment variable set to "true" when running in GitHub Actions.
        /// </summary>
        public const string GitHubActionsEnvVar = "GITHUB_ACTIONS";

        /// <summary>
        /// Environment variable set to "True" when running in Azure DevOps pipelines.
        /// </summary>
        public const string AzureDevOpsEnvVar = "TF_BUILD";

        /// <summary>
        /// Client ID for GitHub Actions environment.
        /// </summary>
        public const string GitHubActionsClientId = "GitHub Actions";

        /// <summary>
        /// Client ID for Azure DevOps environment.
        /// </summary>  
        public const string AzureDevOpsClientId = "AzureDevOps";

        /// <summary>
        /// Detects the CI environment based on environment variables.
        /// </summary>
        /// <returns>A <see cref="string"/> if a CI environment is detected, null otherwise.</returns>
        public static string? Detect()
        {
            return Detect(EnvironmentVariableWrapper.Instance);
        }

        /// <summary>
        /// Detects the CI environment based on environment variables.
        /// </summary>
        /// <param name="environmentVariableReader">The environment variable reader to use.</param>
        /// <returns>A <see cref="string"/> if a CI environment is detected, null otherwise.</returns>
        internal static string? Detect(IEnvironmentVariableReader environmentVariableReader)
        {
            // Check for GitHub Actions
            if (IsGitHubActions(environmentVariableReader))
            {
                return GitHubActionsClientId;
            }

            // Check for Azure DevOps
            if (IsAzureDevOps(environmentVariableReader))
            {
                return AzureDevOpsClientId;
            }

            return null;
        }

        /// <summary>
        /// Checks if the current environment is GitHub Actions.
        /// </summary>
        /// <param name="environmentVariableReader">The environment variable reader to use.</param>
        /// <returns>True if running in GitHub Actions, false otherwise.</returns>
        internal static bool IsGitHubActions(IEnvironmentVariableReader environmentVariableReader)
        {
            string? value = environmentVariableReader.GetEnvironmentVariable(GitHubActionsEnvVar);
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the current environment is Azure DevOps.
        /// </summary>
        /// <param name="environmentVariableReader">The environment variable reader to use.</param>
        /// <returns>True if running in Azure DevOps, false otherwise.</returns>
        internal static bool IsAzureDevOps(IEnvironmentVariableReader environmentVariableReader)
        {
            string? value = environmentVariableReader.GetEnvironmentVariable(AzureDevOpsEnvVar);
            return string.Equals(value, "True", StringComparison.OrdinalIgnoreCase);
        }
    }
}
