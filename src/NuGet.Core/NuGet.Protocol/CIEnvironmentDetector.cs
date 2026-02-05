// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Detects CI/CD environment from environment variables.
    /// </summary>
    public static class CIEnvironmentDetector
    {
        private static readonly EnvironmentDetectionRule[] DetectionRules =
        {
            // GitHub Actions
            new BooleanEnvironmentRule("GitHub Actions", "GITHUB_ACTIONS"),

            // Azure DevOps
            new BooleanEnvironmentRule("Azure DevOps", "TF_BUILD"),

            // AppVeyor
            new BooleanEnvironmentRule("AppVeyor", "APPVEYOR"),

            // Travis CI
            new BooleanEnvironmentRule("Travis CI", "TRAVIS"),

            // CircleCI
            new BooleanEnvironmentRule("CircleCI", "CIRCLECI"),

            // AWS CodeBuild
            new AnyPresentEnvironmentRule("AWS CodeBuild", "CODEBUILD_BUILD_ID"),

            // Jenkins - requires both BUILD_ID and BUILD_URL
            new AllPresentEnvironmentRule("Jenkins", "BUILD_ID", "BUILD_URL"),

            // Google Cloud Build - requires both BUILD_ID and PROJECT_ID
            new AllPresentEnvironmentRule("Google Cloud", "BUILD_ID", "PROJECT_ID"),

            // TeamCity
            new AnyPresentEnvironmentRule("TeamCity", "TEAMCITY_VERSION"),

            // JetBrains Space
            new AnyPresentEnvironmentRule("JetBrains Space", "JB_SPACE_API_URL"),

            // Generic CI - must be last as it's the most general
            new BooleanEnvironmentRule("other", "CI")
        };

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
            foreach (EnvironmentDetectionRule rule in DetectionRules)
            {
                if (rule.IsMatch(environmentVariableReader))
                {
                    return rule.Name;
                }
            }

            return null;
        }
    }
}
