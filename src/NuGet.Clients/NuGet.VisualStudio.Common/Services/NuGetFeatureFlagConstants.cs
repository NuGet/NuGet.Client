// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.VisualStudio
{
    public class NuGetFeatureFlagConstants
    {
        internal NuGetFeatureFlagConstants(string featureFlagName, string featureEnvironmentVariable, bool defaultFeatureFlag)
        {
            FeatureFlagName = featureFlagName ?? throw new ArgumentNullException(nameof(featureFlagName));
            FeatureEnvironmentVariable = featureEnvironmentVariable;
            DefaultFeatureFlag = defaultFeatureFlag;
        }

        /// <summary>
        /// The value defined for the VS feature flag service.
        /// </summary>
        internal string FeatureFlagName { get; }

        /// <summary>
        /// The environment variable means of enabled this feature.
        /// Might be <see cref="null"/>.
        /// </summary>
        internal string FeatureEnvironmentVariable { get; }

        /// <summary>
        /// Default feature state, if the Feature Flag is not specified.
        /// </summary>
        internal bool DefaultFeatureFlag { get; }

        public static readonly NuGetFeatureFlagConstants BulkRestoreCoordination = new("NuGet.BulkRestoreCoordination", "NUGET_BULK_RESTORE_COORDINATION", defaultFeatureFlag: true);
    }
}
