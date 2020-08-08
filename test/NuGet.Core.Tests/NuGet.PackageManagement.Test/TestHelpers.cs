using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.VisualStudio;
using Test.Utility;
using Xunit;

namespace NuGet.Test
{
    public class TestHelpers
    {
        internal static void Expected(List<Tuple<PackageIdentity, NuGetProjectActionType>> expected, string id, NuGetVersion oldVersion, NuGetVersion newVersion)
        {
            expected.Add(Tuple.Create(new PackageIdentity(id, oldVersion), NuGetProjectActionType.Uninstall));
            expected.Add(Tuple.Create(new PackageIdentity(id, newVersion), NuGetProjectActionType.Install));
        }

        internal static void Expected(List<Tuple<PackageIdentity, NuGetProjectActionType>> expected, string id, NuGetVersion newVersion)
        {
            expected.Add(Tuple.Create(new PackageIdentity(id, newVersion), NuGetProjectActionType.Install));
        }

        internal static bool Compare(
            IEnumerable<Tuple<PackageIdentity, NuGetProjectActionType>> lhs,
            IEnumerable<Tuple<PackageIdentity, NuGetProjectActionType>> rhs)
        {
            var ok = true;
            ok &= RhsContainsAllLhs(lhs, rhs);
            ok &= RhsContainsAllLhs(rhs, lhs);
            return ok;
        }

        internal static bool RhsContainsAllLhs(
            IEnumerable<Tuple<PackageIdentity, NuGetProjectActionType>> lhs,
            IEnumerable<Tuple<PackageIdentity, NuGetProjectActionType>> rhs)
        {
            foreach (var item in lhs)
            {
                if (!rhs.Contains(item, new ActionComparer()))
                {
                    return false;
                }
            }
            return true;
        }

        internal static bool PreviewResultsCompare(
            IEnumerable<Tuple<TestNuGetProject, PackageIdentity>> lhs,
            IEnumerable<Tuple<TestNuGetProject, PackageIdentity>> rhs)
        {
            var ok = true;
            ok &= RhsContainsAllLhs(lhs, rhs);
            ok &= RhsContainsAllLhs(rhs, lhs);
            return ok;
        }

        internal static bool RhsContainsAllLhs(
            IEnumerable<Tuple<TestNuGetProject, PackageIdentity>> lhs,
            IEnumerable<Tuple<TestNuGetProject, PackageIdentity>> rhs)
        {
            foreach (var item in lhs)
            {
                if (!rhs.Contains(item, new PreviewResultComparer()))
                {
                    return false;
                }
            }
            return true;
        }

        internal class ActionComparer : IEqualityComparer<Tuple<PackageIdentity, NuGetProjectActionType>>
        {
            public bool Equals(Tuple<PackageIdentity, NuGetProjectActionType> x, Tuple<PackageIdentity, NuGetProjectActionType> y)
            {
                var f1 = x.Item1.Equals(y.Item1);
                var f2 = x.Item2 == y.Item2;
                return f1 && f2;
            }

            public int GetHashCode(Tuple<PackageIdentity, NuGetProjectActionType> obj)
            {
                return obj.GetHashCode();
            }
        }

        internal class PreviewResultComparer : IEqualityComparer<Tuple<TestNuGetProject, PackageIdentity>>
        {
            public bool Equals(Tuple<TestNuGetProject, PackageIdentity> x, Tuple<TestNuGetProject, PackageIdentity> y)
            {
                var f1 = x.Item1.Metadata[NuGetProjectMetadataKeys.Name].ToString().Equals(
                    y.Item1.Metadata[NuGetProjectMetadataKeys.Name].ToString());
                var f2 = x.Item2.Equals(y.Item2);
                return f1 && f2;
            }

            public int GetHashCode(Tuple<TestNuGetProject, PackageIdentity> obj)
            {
                return obj.GetHashCode();
            }
        }

        internal static void VerifyPreviewActionsTelemetryEvents_PackagesConfig(IEnumerable<string> actual)
        {
            Assert.True(actual.Contains(TelemetryConstants.GatherDependencyStepName));
            Assert.True(actual.Contains(TelemetryConstants.ResolveDependencyStepName));
            Assert.True(actual.Contains(TelemetryConstants.ResolvedActionsStepName));
        }

        internal static void AddToPackagesFolder(PackageIdentity package, string root)
        {
            var dir = Path.Combine(root, $"{package.Id}.{package.Version.ToString()}");
            Directory.CreateDirectory(dir);

            var context = new SimpleTestPackageContext()
            {
                Id = package.Id,
                Version = package.Version.ToString()
            };

            context.AddFile("lib/net45/a.dll");
            SimpleTestPackageUtility.CreateOPCPackage(context, dir);
        }

        internal static SourceRepositoryProvider CreateSource(List<SourcePackageDependencyInfo> packages)
        {
            var resourceProviders = new List<Lazy<INuGetResourceProvider>>();
            resourceProviders.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packages)));
            resourceProviders.Add(new Lazy<INuGetResourceProvider>(() => new TestMetadataProvider(packages)));

            var packageSource = new Configuration.PackageSource("http://temp");
            var packageSourceProvider = new TestPackageSourceProvider(new[] { packageSource });

            return new SourceRepositoryProvider(packageSourceProvider, resourceProviders);
        }


        internal class TestNuGetVSTelemetryService : NuGetVSTelemetryService
        {
            private ITelemetrySession _telemetrySession;
            private XunitLogger _logger;

            public TestNuGetVSTelemetryService(ITelemetrySession telemetrySession, XunitLogger logger)
            {
                _telemetrySession = telemetrySession ?? throw new ArgumentNullException(nameof(telemetrySession));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }

            public override void EmitTelemetryEvent(TelemetryEvent telemetryData)
            {
                if (telemetryData == null)
                {
                    throw new ArgumentNullException(nameof(telemetryData));
                }

                lock (_logger)
                {
                    var operationId = telemetryData["OperationId"];
                    var parentId = telemetryData["ParentId"];

                    _logger.LogInformation("--------------------------");
                    _logger.LogInformation($"Name: {telemetryData.Name}");
                    _logger.LogInformation($"OperationId: {operationId}");
                    _logger.LogInformation($"ParentId: {parentId}");
                    _logger.LogInformation($"Json: {telemetryData.ToJson()}");
                    _logger.LogInformation($"Stack: {Environment.StackTrace}");
                    _logger.LogInformation("--------------------------");
                }

                _telemetrySession.PostEvent(telemetryData);
            }
        }
    }
}
