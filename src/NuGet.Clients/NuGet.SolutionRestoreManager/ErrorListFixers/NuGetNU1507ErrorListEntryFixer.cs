// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Threading;
using Microsoft.Internal.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Utilities;
using NuGet.Common;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.SolutionRestoreManager.ErrorListFixers
{
#pragma warning disable CS0618 // Obsolete in VS because it "may change without warning". It remains the only Error List fixer extensibility point today.
    [Export(typeof(IErrorListEntryFixer))]
#pragma warning restore CS0618
    [DataSource(StandardTableDataSources.ErrorTableDataSource)]
    [Name(NuGetErrorListFixerConstants.NU1507FixerName)]
    [Order(Before = NuGetErrorListFixerConstants.CopilotFixerName)]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class NuGetNU1507ErrorListEntryFixer : NuGetErrorListEntryFixerBase
    {
        private static readonly HashSet<string> SupportedCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            NuGetLogCode.NU1507.ToString(),
        };

        [Import(typeof(IResolveSupplyChainSecurityService), AllowDefault = true)]
        public Lazy<IResolveSupplyChainSecurityService>? ResolveSupplyChainSecurityService { get; set; }

        public override string Tooltip => Resources.Title_ResolveNU1507WithCopilot;

        protected override IErrorListEntryInspector EntryInspector { get; } = new SupportedCodesErrorListInspector(SupportedCodes);

        protected override bool TryFixCore(ITableEntryHandle entry)
        {
            if (ResolveSupplyChainSecurityService == null)
            {
                return false;
            }

            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ResolveSupplyChainSecurityService.Value.LaunchResolveAsync(
                    ResolveSupplyChainSecuritySource.NU1507ErrorList,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Resources.Prompt_ResolveSupplyChainSecurityNUCode,
                        NuGetLogCode.NU1507),
                    CancellationToken.None);
            }).PostOnFailure(nameof(NuGetNU1507ErrorListEntryFixer), nameof(TryFixCore));

            return true;
        }
    }
}
