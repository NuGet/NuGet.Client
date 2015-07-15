// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Frameworks
{
    public static class NuGetFrameworkUtility
    {
        /// <summary>
        /// Find the most compatible group based on target framework
        /// </summary>
        /// <param name="items">framework specific groups or items</param>
        /// <param name="framework">project target framework</param>
        /// <param name="selector">retrieves the framework from the group</param>
        public static T GetNearest<T>(IEnumerable<T> items, NuGetFramework framework, Func<T, NuGetFramework> selector) where T : class
        {
            return GetNearest<T>(items, framework, DefaultFrameworkNameProvider.Instance, DefaultCompatibilityProvider.Instance, selector);
        }

        /// <summary>
        /// Find the most compatible group based on target framework
        /// </summary>
        /// <param name="items">framework specific groups or items</param>
        /// <param name="framework">project target framework</param>
        /// <param name="selector">retrieves the framework from the group</param>
        /// <param name="frameworkMappings">framework mappings</param>
        public static T GetNearest<T>(IEnumerable<T> items,
            NuGetFramework framework,
            IFrameworkNameProvider frameworkMappings,
            IFrameworkCompatibilityProvider compatibilityProvider,
            Func<T, NuGetFramework> selector) where T : class
        {
            if (framework == null)
            {
                throw new ArgumentNullException(nameof(framework));
            }

            if (frameworkMappings == null)
            {
                throw new ArgumentNullException(nameof(frameworkMappings));
            }

            if (compatibilityProvider == null)
            {
                throw new ArgumentNullException(nameof(compatibilityProvider));
            }

            if (items != null)
            {
                var reducer = new FrameworkReducer(frameworkMappings, compatibilityProvider);

                var mostCompatibleFramework = reducer.GetNearest(framework, items.Select(selector));
                if (mostCompatibleFramework != null)
                {
                    return items.FirstOrDefault(item => NuGetFramework.Comparer.Equals(selector(item), mostCompatibleFramework));
                }
            }

            return null;
        }

        /// <summary>
        /// Find the most compatible group based on target framework
        /// </summary>
        /// <param name="items">framework specific groups or items</param>
        /// <param name="framework">project target framework</param>
        public static T GetNearest<T>(IEnumerable<T> items, NuGetFramework framework) where T : IFrameworkSpecific
        {
            return GetNearest<T>(items, framework, DefaultFrameworkNameProvider.Instance, DefaultCompatibilityProvider.Instance);
        }

        /// <summary>
        /// Find the most compatible group based on target framework
        /// </summary>
        /// <param name="items">framework specific groups or items</param>
        /// <param name="framework">project target framework</param>
        public static T GetNearest<T>(IEnumerable<T> items,
                                        NuGetFramework framework,
                                        IFrameworkNameProvider frameworkMappings,
                                        IFrameworkCompatibilityProvider compatibilityProvider) where T : IFrameworkSpecific
        {
            if (framework == null)
            {
                throw new ArgumentNullException(nameof(framework));
            }

            if (frameworkMappings == null)
            {
                throw new ArgumentNullException(nameof(frameworkMappings));
            }

            if (compatibilityProvider == null)
            {
                throw new ArgumentNullException(nameof(compatibilityProvider));
            }

            if (items != null)
            {
                var reducer = new FrameworkReducer(frameworkMappings, compatibilityProvider);

                var mostCompatibleFramework = reducer.GetNearest(framework, items.Select(item => item.TargetFramework));
                if (mostCompatibleFramework != null)
                {
                    return items.FirstOrDefault(item => NuGetFramework.Comparer.Equals(item.TargetFramework, mostCompatibleFramework));
                }
            }

            return default(T);
        }

        /// <summary>
        /// Check compatibility with additional checks for the fallback framework.
        /// </summary>
        public static bool IsCompatibleWithFallbackCheck(NuGetFramework projectFramework, NuGetFramework candidate)
        {
            var compatible = DefaultCompatibilityProvider.Instance.IsCompatible(projectFramework, candidate);

            if (!compatible)
            {
                var fallbackFramework = projectFramework as FallbackFramework;

                if (fallbackFramework != null && fallbackFramework.Fallback != null)
                {
                    compatible = DefaultCompatibilityProvider.Instance.IsCompatible(fallbackFramework.Fallback, candidate);
                }
            }

            return compatible;
        }
    }
}
