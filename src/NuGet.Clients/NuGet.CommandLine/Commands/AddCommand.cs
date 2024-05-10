// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Packaging;
using NuGet.Packaging.PackageExtraction;
using NuGet.Packaging.Signing;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "add", "AddCommandDescription",
        MinArgs = 1, MaxArgs = 1, UsageDescriptionResourceName = "AddCommandUsageDescription",
        UsageSummaryResourceName = "AddCommandUsageSummary", UsageExampleResourceName = "AddCommandUsageExamples")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class AddCommand : Command
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        [Option(typeof(NuGetCommand), "AddCommandSourceDescription", AltName = "src")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Source { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "ExpandDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool Expand { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override async Task ExecuteCommandAsync()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            // Arguments[0] will not be null at this point.
            // Because, this command has MinArgs set to 1.
            var packagePath = Arguments[0];

            if (string.IsNullOrEmpty(Source))
            {
                throw new CommandException(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.AddCommand_SourceNotProvided)));
            }

            OfflineFeedUtility.ThrowIfInvalidOrNotFound(
                packagePath,
                isDirectory: false,
                resourceString: LocalizedResourceManager.GetString(nameof(NuGetResources.NupkgPath_NotFound)));

            // If the Source Feed Folder does not exist, it will be created.
            OfflineFeedUtility.ThrowIfInvalid(Source);

            var packageExtractionContext = new PackageExtractionContext(
                Expand ? PackageSaveMode.Defaultv3 : PackageSaveMode.Nuspec | PackageSaveMode.Nupkg,
                PackageExtractionBehavior.XmlDocFileSaveMode,
                ClientPolicyContext.GetClientPolicy(Settings, Console),
                Console);

            var offlineFeedAddContext = new OfflineFeedAddContext(
                packagePath,
                Source,
                Console, // IConsole is an ILogger
                throwIfSourcePackageIsInvalid: true,
                throwIfPackageExistsAndInvalid: true,
                throwIfPackageExists: false,
                extractionContext: packageExtractionContext);

            await OfflineFeedUtility.AddPackageToSource(offlineFeedAddContext, CancellationToken.None);
        }
    }
}
