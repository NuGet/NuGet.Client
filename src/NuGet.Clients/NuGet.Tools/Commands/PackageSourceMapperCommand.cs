// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Design;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Telemetry;
using NuGetVSExtension;

namespace NuGet.Tools.Commands
{
    internal sealed class PackageSourceMapperCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandID = PkgCmdIDList.cmdidReviewPackageSourceMapping;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = GuidList.guidReviewPackageSourceMappingCmdSet;

        private readonly OleMenuCommandService _oleMenuCommandService;
        private readonly IPackageSourceMappingService _packageSourceMappingService;

        public PackageSourceMapperCommand(
            OleMenuCommandService oleMenuCommandService,
            IPackageSourceMappingService packageSourceMappingService)
        {
            _oleMenuCommandService = oleMenuCommandService ?? throw new ArgumentNullException(nameof(oleMenuCommandService));
            _packageSourceMappingService = packageSourceMappingService ?? throw new ArgumentNullException(nameof(packageSourceMappingService));
        }

        public void Initialize()
        {
            var commandId = new CommandID(CommandSet, CommandID);
            var command = new OleMenuCommand(ExecutePackageSourceMapperCommand, commandId);
            _oleMenuCommandService.AddCommand(command);
        }

        private void ExecutePackageSourceMapperCommand(object sender, EventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(() =>
                _packageSourceMappingService.LaunchReviewPackageSourceMappingAsync(CancellationToken.None))
                .PostOnFailure(nameof(NuGetPackage), nameof(ExecutePackageSourceMapperCommand));
        }
    }
}
