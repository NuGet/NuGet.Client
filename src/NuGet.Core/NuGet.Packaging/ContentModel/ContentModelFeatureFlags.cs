// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.ContentModel
{
    /// <summary>
    /// Feature flags for ContentModel optimizations.
    /// </summary>
    internal static class ContentModelFeatureFlags
    {
        private static bool? _useOptimizedAssetClassifier;

        /// <summary>
        /// Gets a value indicating whether the optimized asset classifier should be used
        /// instead of the traditional pattern-matching approach.
        /// 
        /// Set the environment variable NUGET_USE_OPTIMIZED_ASSET_CLASSIFIER=true to enable.
        /// 
        /// The optimized classifier uses a decision tree approach with O(n*d) complexity
        /// instead of O(n*m) where n=assets, m=patterns, d=tree depth (~4-5).
        /// </summary>
        public static bool UseOptimizedAssetClassifier
        {
            get
            {
                if (_useOptimizedAssetClassifier == null)
                {
                    var envValue = EnvironmentVariableWrapper.Instance.GetEnvironmentVariable("NUGET_USE_OPTIMIZED_ASSET_CLASSIFIER");
                    _useOptimizedAssetClassifier = MSBuildStringUtility.IsTrue(envValue);
                }
                return _useOptimizedAssetClassifier.Value;
            }
        }

        /// <summary>
        /// Resets the cached feature flag value. Used for testing.
        /// </summary>
        internal static void Reset()
        {
            _useOptimizedAssetClassifier = null;
        }
    }
}
