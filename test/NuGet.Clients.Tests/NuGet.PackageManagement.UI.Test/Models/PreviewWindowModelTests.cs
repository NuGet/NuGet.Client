// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using Xunit;

namespace NuGet.PackageManagement.UI
{
    public class PreviewWindowModelTests
    {
        class MockProject : ProjectManagement.NuGetProject
        {
            public override Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
            {
                throw new NotImplementedException();
            }

            public override Task<bool> InstallPackageAsync(PackageIdentity packageIdentity, DownloadResourceResult downloadResourceResult, INuGetProjectContext nuGetProjectContext, CancellationToken token)
            {
                throw new NotImplementedException();
            }

            public override Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public void PreviewWindowModelToString_Test()
        {
            var added = new List<PackageIdentity>();
            var deleted = new List<PackageIdentity>();
            var updated = new List<UpdatePreviewResult>();

            added.Add(new PackageIdentity("PkgA", new Versioning.NuGetVersion("1.2.3")));
            deleted.Add(new PackageIdentity("PkgB", new Versioning.NuGetVersion("3.2.1")));
            updated.Add(new UpdatePreviewResult(
                new PackageIdentity("PkgC", new Versioning.NuGetVersion("1.0.0")),
                new PackageIdentity("PkgC", new Versioning.NuGetVersion("2.0.0"))
            ));

            var ProjA = new MockProject();
            var previewResult = new PreviewResult(ProjA, added, deleted, updated);
            var allResults = new List<PreviewResult>();
            allResults.Add(previewResult);
            var model = new PreviewWindowModel(allResults);

            var sb = new StringBuilder();
            sb.AppendLine("Unknown Project");
            sb.AppendLine();
            sb.AppendLine(Resources.Label_UninstalledPackages);
            sb.AppendLine();
            foreach (var p in deleted)
            {
                sb.AppendLine(p.ToString());
            }
            sb.AppendLine();
            sb.AppendLine(Resources.Label_UpdatedPackages);
            sb.AppendLine();
            foreach (var p in updated)
            {
                sb.AppendLine(p.ToString());
            }
            sb.AppendLine();
            sb.AppendLine(Resources.Label_InstalledPackages);
            sb.AppendLine();
            foreach (var p in added)
            {
                sb.AppendLine(p.ToString());
            }
            sb.AppendLine();

            Assert.Equal(sb.ToString(), model.ToString());
        }
    }
}
