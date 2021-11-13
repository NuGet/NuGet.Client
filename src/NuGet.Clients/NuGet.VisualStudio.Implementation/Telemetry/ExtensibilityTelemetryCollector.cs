// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Diagnostics.Tracing;
using NuGet.Common;
using NuGet.SolutionRestoreManager;
using NuGet.VisualStudio.Contracts;
using NuGet.VisualStudio.Etw;

namespace NuGet.VisualStudio.Telemetry
{
    internal sealed class ExtensibilityTelemetryCollector : IDisposable
    {
        // While we could use ConcurrentDictionary to avoid needing to pre-create all the keys and counters, I've
        // chosen to keep IReadOnlyDictionary for two reasons:
        // * To be more transparent to customers, and other Microsoft employees, exactly what telemetry is being
        //     collected. Every property/measure that will get reported is clearly defined here and easy to determine.
        // * ConcurrentDictionary can be quite slow to add new keys. ETW events are supposed to be fast, so they can
        //     be used liberally. EventSource synchronously blocks on every subscribed EventListener's OnEventWritten
        //     callback. Hence, I want to ensure that raising ETW events is always as fast as possible.

        private IReadOnlyDictionary<string, Count> _counts;
        private ExtensibilityEventListener _eventListener;

        public ExtensibilityTelemetryCollector()
        {
            _eventListener = new ExtensibilityEventListener(this);
            _counts = new Dictionary<string, Count>()
            {
                // INuGetProjectService
                [nameof(INuGetProjectService) + "." + nameof(INuGetProjectService.GetInstalledPackagesAsync)] = new Count(),

                // IVsFrameworkCompatibility
                [nameof(IVsFrameworkCompatibility) + "." + nameof(IVsFrameworkCompatibility.GetNetStandardFrameworks)] = new Count(),
                [nameof(IVsFrameworkCompatibility) + "." + nameof(IVsFrameworkCompatibility.GetFrameworksSupportingNetStandard)] = new Count(),
                [nameof(IVsFrameworkCompatibility) + "." + nameof(IVsFrameworkCompatibility.GetNearest)] = new Count(),

                // IVsFrameworkCompatibility2
                [nameof(IVsFrameworkCompatibility2) + "." + nameof(IVsFrameworkCompatibility2.GetNearest)] = new Count(),

                // IVsFrameworkCompatibility2
                [nameof(IVsFrameworkCompatibility3) + "." + nameof(IVsFrameworkCompatibility3.GetNearest) + ".2"] = new Count(),
                [nameof(IVsFrameworkCompatibility3) + "." + nameof(IVsFrameworkCompatibility3.GetNearest) + ".3"] = new Count(),

                // IVsFrameworkParser
                [nameof(IVsFrameworkParser) + "." + nameof(IVsFrameworkParser.ParseFrameworkName)] = new Count(),
                [nameof(IVsFrameworkParser) + "." + nameof(IVsFrameworkParser.GetShortFrameworkName)] = new Count(),

                // IVsFrameworkParser2
                [nameof(IVsFrameworkParser2) + "." + nameof(IVsFrameworkParser2.TryParse)] = new Count(),

                // IVsGlobalPackagesInitScriptExecutor
                [nameof(IVsGlobalPackagesInitScriptExecutor) + "." + nameof(IVsGlobalPackagesInitScriptExecutor.ExecuteInitScriptAsync)] = new Count(),

                // IVsPackageInstaller
                [nameof(IVsPackageInstaller) + "." + nameof(IVsPackageInstaller.InstallPackage) + ".1"] = new Count(),
                [nameof(IVsPackageInstaller) + "." + nameof(IVsPackageInstaller.InstallPackage) + ".2"] = new Count(),
                [nameof(IVsPackageInstaller) + "." + nameof(IVsPackageInstaller.InstallPackagesFromRegistryRepository) + ".1"] = new Count(),
                [nameof(IVsPackageInstaller) + "." + nameof(IVsPackageInstaller.InstallPackagesFromRegistryRepository) + ".2"] = new Count(),
                [nameof(IVsPackageInstaller) + "." + nameof(IVsPackageInstaller.InstallPackagesFromVSExtensionRepository) + ".1"] = new Count(),
                [nameof(IVsPackageInstaller) + "." + nameof(IVsPackageInstaller.InstallPackagesFromVSExtensionRepository) + ".2"] = new Count(),

                // IVsPackageInstaller2
                [nameof(IVsPackageInstaller2) + "." + nameof(IVsPackageInstaller2.InstallLatestPackage)] = new Count(),

                // IVsPackageInstallerEvents
                [nameof(IVsPackageInstallerEvents) + "." + nameof(IVsPackageInstallerEvents.PackageInstalled)] = new Count(),
                [nameof(IVsPackageInstallerEvents) + "." + nameof(IVsPackageInstallerEvents.PackageInstalling)] = new Count(),
                [nameof(IVsPackageInstallerEvents) + "." + nameof(IVsPackageInstallerEvents.PackageReferenceAdded)] = new Count(),
                [nameof(IVsPackageInstallerEvents) + "." + nameof(IVsPackageInstallerEvents.PackageReferenceRemoved)] = new Count(),
                [nameof(IVsPackageInstallerEvents) + "." + nameof(IVsPackageInstallerEvents.PackageUninstalled)] = new Count(),
                [nameof(IVsPackageInstallerEvents) + "." + nameof(IVsPackageInstallerEvents.PackageUninstalling)] = new Count(),

                // IVsPackageInstallerProjectEvents
                [nameof(IVsPackageInstallerProjectEvents) + "." + nameof(IVsPackageInstallerProjectEvents.BatchStart)] = new Count(),
                [nameof(IVsPackageInstallerProjectEvents) + "." + nameof(IVsPackageInstallerProjectEvents.BatchEnd)] = new Count(),

                // IVsPackageInstallerServices
#pragma warning disable CS0618 // Type or member is obsolete
                [nameof(IVsPackageInstallerServices) + "." + nameof(IVsPackageInstallerServices.GetInstalledPackages)] = new Count(),
                [nameof(IVsPackageInstallerServices) + "." + nameof(IVsPackageInstallerServices.GetInstalledPackages) + ".1"] = new Count(),
                [nameof(IVsPackageInstallerServices) + "." + nameof(IVsPackageInstallerServices.IsPackageInstalled) + ".2"] = new Count(),
                [nameof(IVsPackageInstallerServices) + "." + nameof(IVsPackageInstallerServices.IsPackageInstalled) + ".3"] = new Count(),
                [nameof(IVsPackageInstallerServices) + "." + nameof(IVsPackageInstallerServices.IsPackageInstalledEx)] = new Count(),
#pragma warning restore CS0618 // Type or member is obsolete

                // IVsPackageRestorer
                [nameof(IVsPackageRestorer) + "." + nameof(IVsPackageRestorer.IsUserConsentGranted)] = new Count(),
                [nameof(IVsPackageRestorer) + "." + nameof(IVsPackageRestorer.RestorePackages)] = new Count(),

                // IVsPackageSourceProvider
                [nameof(IVsPackageSourceProvider) + "." + nameof(IVsPackageSourceProvider.GetSources)] = new Count(),
                [nameof(IVsPackageSourceProvider) + "." + nameof(IVsPackageSourceProvider.SourcesChanged)] = new Count(),

                // IVsPackageUninstaller
                [nameof(IVsPackageUninstaller) + "." + nameof(IVsPackageUninstaller.UninstallPackage)] = new Count(),

                // IVsPathContext
                [nameof(IVsPathContext) + "." + nameof(IVsPathContext.TryResolvePackageAsset)] = new Count(),

                // IVsPathContextProvider
                [nameof(IVsPathContextProvider) + "." + nameof(IVsPathContextProvider.TryCreateContext)] = new Count(),

                // IVsPathContextProvider2
                [nameof(IVsPathContextProvider2) + "." + nameof(IVsPathContextProvider2.TryCreateSolutionContext) + ".1"] = new Count(),
                [nameof(IVsPathContextProvider2) + "." + nameof(IVsPathContextProvider2.TryCreateSolutionContext) + ".2"] = new Count(),
                [nameof(IVsPathContextProvider2) + "." + nameof(IVsPathContextProvider2.TryCreateNoSolutionContext)] = new Count(),

                // IVsProjectJsonToPackageReferenceMigrator
                [nameof(IVsProjectJsonToPackageReferenceMigrator) + "." + nameof(IVsProjectJsonToPackageReferenceMigrator.MigrateProjectJsonToPackageReferenceAsync)] = new Count(),

                // IVsSemanticVersionComparer
                [nameof(IVsSemanticVersionComparer) + "." + nameof(IVsSemanticVersionComparer.Compare)] = new Count(),

                // IVsSolutionRestoreService
                [nameof(IVsSolutionRestoreService) + "." + nameof(IVsSolutionRestoreService.CurrentRestoreOperation)] = new Count(),
                [nameof(IVsSolutionRestoreService) + "." + nameof(IVsSolutionRestoreService.NominateProjectAsync)] = new Count(),

                // IVsSolutionRestoreService2
                [nameof(IVsSolutionRestoreService2) + "." + nameof(IVsSolutionRestoreService2.NominateProjectAsync)] = new Count(),

                // IVsSolutionRestoreService3
                [nameof(IVsSolutionRestoreService3) + "." + nameof(IVsSolutionRestoreService3.CurrentRestoreOperation)] = new Count(),
                [nameof(IVsSolutionRestoreService3) + "." + nameof(IVsSolutionRestoreService3.NominateProjectAsync)] = new Count(),

                // IVsSolutionRestoreService4
                [nameof(IVsSolutionRestoreService4) + "." + nameof(IVsSolutionRestoreService4.RegisterRestoreInfoSourceAsync)] = new Count(),
            };
        }

        public TelemetryEvent ToTelemetryEvent()
        {
            TelemetryEvent data = new("extensibility");

            foreach ((string api, Count count) in _counts)
            {
                if (count.Value > 0)
                {
                    data[api] = count.Value;
                }
            }

            return data;
        }

        public void Dispose()
        {
            _eventListener?.Dispose();
            _eventListener = null;

            GC.SuppressFinalize(this);
        }

        private class Count
        {
            public int Value;
        }

        private class ExtensibilityEventListener : EventListener
        {
            private ExtensibilityTelemetryCollector _collector;

            public ExtensibilityEventListener(ExtensibilityTelemetryCollector collector)
            {
                _collector = collector;
            }

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name == "NuGet-VS-Extensibility")
                {
                    EnableEvents(eventSource, EventLevel.LogAlways);
                }
                Debug.WriteLine(nameof(ExtensibilityEventListener) + " found " + eventSource.Name);
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                var opcode = eventData.Opcode;
                if (opcode == EventOpcode.Start || opcode == NuGetExtensibilityEtw.CustomOpcodes.Add || opcode == EventOpcode.Info)
                {
                    if (_collector._counts.TryGetValue(eventData.EventName, out Count count))
                    {
                        Interlocked.Increment(ref count.Value);
                    }
                    else
                    {
                        Debug.Assert(false, "VS Extensibility API without counter");
                    }
                }
            }
        }
    }
}
