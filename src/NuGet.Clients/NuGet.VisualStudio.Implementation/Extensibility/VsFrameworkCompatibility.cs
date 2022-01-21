// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.Versioning;
using NuGet.Commands;
using NuGet.Frameworks;
using NuGet.VisualStudio.Implementation.Resources;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.VisualStudio.Implementation.Extensibility
{
    [Export(typeof(IVsFrameworkCompatibility))]
#pragma warning disable CS0618 // Type or member is obsolete
    [Export(typeof(IVsFrameworkCompatibility2))]
#pragma warning restore CS0618 // Type or member is obsolete
    [Export(typeof(IVsFrameworkCompatibility3))]
    public class VsFrameworkCompatibility :
#pragma warning disable CS0618 // Type or member is obsolete
        IVsFrameworkCompatibility2,
#pragma warning restore CS0618 // Type or member is obsolete
        IVsFrameworkCompatibility3
    {
        private INuGetTelemetryProvider _telemetryProvider;

        [ImportingConstructor]
        public VsFrameworkCompatibility(INuGetTelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;
        }

        public IEnumerable<FrameworkName> GetNetStandardFrameworks()
        {
            const string eventName = nameof(IVsFrameworkCompatibility) + "." + nameof(GetNetStandardFrameworks);
            NuGetExtensibilityEtw.EventSource.Write(eventName, NuGetExtensibilityEtw.StartEventOptions);

            try
            {
                return DefaultFrameworkNameProvider
                    .Instance
                    .GetNetStandardVersions()
                    .Select(framework => new FrameworkName(framework.DotNetFrameworkName))
                    .ToList();
            }
            catch (Exception exception)
            {
                _telemetryProvider.PostFault(exception, typeof(VsFrameworkCompatibility).FullName);
                throw;
            }
            finally
            {
                NuGetExtensibilityEtw.EventSource.Write(eventName, NuGetExtensibilityEtw.StopEventOptions);
            }
        }

        public IEnumerable<FrameworkName> GetFrameworksSupportingNetStandard(FrameworkName frameworkName)
        {
            const string eventName = nameof(IVsFrameworkCompatibility) + "." + nameof(GetFrameworksSupportingNetStandard);
            NuGetExtensibilityEtw.EventSource.Write(eventName, NuGetExtensibilityEtw.StartEventOptions,
                new
                {
                    Framework = frameworkName.FullName
                });

            try
            {
                if (frameworkName == null)
                {
                    throw new ArgumentNullException(nameof(frameworkName));
                }

                try
                {
                    var nuGetFramework = NuGetFramework.ParseFrameworkName(frameworkName.ToString(), DefaultFrameworkNameProvider.Instance);

                    if (!StringComparer.OrdinalIgnoreCase.Equals(
                        nuGetFramework.Framework,
                        FrameworkConstants.FrameworkIdentifiers.NetStandard))
                    {
                        throw new ArgumentException(string.Format(
                            VsResources.InvalidNetStandardFramework,
                            frameworkName));
                    }

                    return CompatibilityListProvider
                        .Default
                        .GetFrameworksSupporting(nuGetFramework)
                        .Select(framework => new FrameworkName(framework.DotNetFrameworkName))
                        .ToList();
                }
                catch (Exception exception)
                {
                    _telemetryProvider.PostFault(exception, typeof(VsFrameworkCompatibility).FullName);
                    throw;
                }
            }
            finally
            {
                NuGetExtensibilityEtw.EventSource.Write(eventName, NuGetExtensibilityEtw.StopEventOptions);
            }
        }

        public FrameworkName GetNearest(FrameworkName targetFramework, IEnumerable<FrameworkName> frameworks)
        {
            const string eventName = nameof(IVsFrameworkCompatibility) + "." + nameof(GetNearest);
            NuGetExtensibilityEtw.EventSource.Write(eventName, NuGetExtensibilityEtw.StartEventOptions,
                new
                {
                    Target = targetFramework.FullName,
                    Frameworks = string.Join("|", frameworks.Select(f => f.FullName))
                });

            try
            {
                return GetNearest(targetFramework, Enumerable.Empty<FrameworkName>(), frameworks);
            }
            finally
            {
                NuGetExtensibilityEtw.EventSource.Write(eventName, NuGetExtensibilityEtw.StopEventOptions);
            }
        }

        public FrameworkName GetNearest(FrameworkName targetFramework, IEnumerable<FrameworkName> fallbackTargetFrameworks, IEnumerable<FrameworkName> frameworks)
        {
            if (targetFramework == null)
            {
                throw new ArgumentNullException(nameof(targetFramework));
            }

            if (fallbackTargetFrameworks == null)
            {
                throw new ArgumentNullException(nameof(fallbackTargetFrameworks));
            }

            if (frameworks == null)
            {
                throw new ArgumentNullException(nameof(frameworks));
            }

            IEnumerable<NuGetFramework> ParseAllFrameworks(IEnumerable<FrameworkName> frameworks)
            {
                foreach (FrameworkName frameworkName in frameworks)
                {
                    if (frameworkName == null)
                    {
                        throw new ArgumentException(message: VsResourcesFormat.PropertyCannotBeNull(nameof(FrameworkName)), paramName: nameof(frameworks));
                    }

                    var nugetFramework = NuGetFramework.ParseFrameworkName(frameworkName.ToString(), DefaultFrameworkNameProvider.Instance);
                    yield return nugetFramework;
                }
            }

            var nuGetTargetFramework = NuGetFramework.ParseFrameworkName(targetFramework.ToString(), DefaultFrameworkNameProvider.Instance);
            var nuGetFallbackTargetFrameworks = ParseAllFrameworks(fallbackTargetFrameworks).ToList();
            var nuGetFrameworks = ParseAllFrameworks(frameworks).ToList();

            try
            {
                if (nuGetFallbackTargetFrameworks.Any())
                {
                    nuGetTargetFramework = new FallbackFramework(nuGetTargetFramework, nuGetFallbackTargetFrameworks);
                }

                var reducer = new FrameworkReducer();
                var nearest = reducer.GetNearest(nuGetTargetFramework, nuGetFrameworks);

                if (nearest == null)
                {
                    return null;
                }

                return new FrameworkName(nearest.DotNetFrameworkName);
            }
            catch (Exception exception)
            {
                _telemetryProvider.PostFault(exception, typeof(VsFrameworkCompatibility).FullName);
                throw;
            }
        }

        public IVsNuGetFramework GetNearest(IVsNuGetFramework targetFramework, IEnumerable<IVsNuGetFramework> frameworks)
        {
            return GetNearest(targetFramework, Enumerable.Empty<IVsNuGetFramework>(), frameworks);
        }

        public IVsNuGetFramework GetNearest(IVsNuGetFramework targetFramework, IEnumerable<IVsNuGetFramework> fallbackTargetFrameworks, IEnumerable<IVsNuGetFramework> frameworks)
        {
            if (targetFramework == null)
            {
                throw new ArgumentNullException(nameof(targetFramework));
            }

            if (fallbackTargetFrameworks == null)
            {
                throw new ArgumentNullException(nameof(fallbackTargetFrameworks));
            }

            if (frameworks == null)
            {
                throw new ArgumentNullException(nameof(frameworks));
            }

            var inputFrameworks = new Dictionary<NuGetFramework, IVsNuGetFramework>();

            NuGetFramework ToNuGetFramework(IVsNuGetFramework framework, string paramName)
            {
                NuGetFramework nugetFramework = MSBuildProjectFrameworkUtility.GetProjectFramework(
                    projectFilePath: null,
                    targetFrameworkMoniker: framework.TargetFrameworkMoniker,
                    targetPlatformMoniker: framework.TargetPlatformMoniker,
                    targetPlatformMinVersion: framework.TargetPlatformMinVersion);
                if (!nugetFramework.IsSpecificFramework)
                {
                    throw new ArgumentException($"Framework '{framework}' could not be parsed", paramName);
                }
                inputFrameworks[nugetFramework] = framework;
                return nugetFramework;
            }

            List<NuGetFramework> ToNuGetFrameworks(IEnumerable<IVsNuGetFramework> enumerable, string paramName)
            {
                var list = new List<NuGetFramework>();
                foreach (var framework in enumerable)
                {
                    if (framework == null)
                    {
                        throw new ArgumentException("Enumeration contains a null value", paramName);
                    }
                    NuGetFramework nugetFramework = ToNuGetFramework(framework, paramName);
                    list.Add(nugetFramework);
                }
                return list;
            }

            NuGetFramework targetNuGetFramework = ToNuGetFramework(targetFramework, nameof(targetFramework));
            List<NuGetFramework> nugetFallbackTargetFrameworks = ToNuGetFrameworks(fallbackTargetFrameworks, nameof(fallbackTargetFrameworks));
            List<NuGetFramework> nugetFrameworks = ToNuGetFrameworks(frameworks, nameof(frameworks));

            try
            {
                if (nugetFallbackTargetFrameworks.Count > 0)
                {
                    targetNuGetFramework = new FallbackFramework(targetNuGetFramework, nugetFallbackTargetFrameworks);
                }

                var reducer = new FrameworkReducer();
                var nearest = reducer.GetNearest(targetNuGetFramework, nugetFrameworks);

                if (nearest == null)
                {
                    return null;
                }

                var originalFrameworkString = inputFrameworks[nearest];
                return originalFrameworkString;
            }
            catch (Exception exception)
            {
                _telemetryProvider.PostFault(exception, typeof(VsFrameworkCompatibility).FullName);
                throw;
            }
        }
    }
}
