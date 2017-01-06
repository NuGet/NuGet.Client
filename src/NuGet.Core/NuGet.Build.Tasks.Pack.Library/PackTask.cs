// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Build.Framework;
using NuGet.Commands;
using ILogger = NuGet.Common.ILogger;

namespace NuGet.Build.Tasks.Pack
{
    public class PackTask : Microsoft.Build.Utilities.Task, IPackTaskRequest<ITaskItem>
    {
        [Required]
        public ITaskItem PackItem { get; set; }

        public ITaskItem[] PackageFiles { get; set; }
        public ITaskItem[] PackageFilesToExclude { get; set; }
        public string[] TargetFrameworks { get; set; }
        public string[] PackageTypes { get; set; }
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public string[] Authors { get; set; }
        public string Description { get; set; }
        public string Copyright { get; set; }
        public bool RequireLicenseAcceptance { get; set; }
        public string RestoreOutputPath { get; set; }
        public string LicenseUrl { get; set; }
        public string ProjectUrl { get; set; }
        public string IconUrl { get; set; }
        public string[] Tags { get; set; }
        public string ReleaseNotes { get; set; }
        public string[] TargetPathsToAssemblies { get; set; }
        public string[] TargetPathsToSymbols { get; set; }
        public string AssemblyName { get; set; }
        public string PackageOutputPath { get; set; }
        public bool IsTool { get; set; }
        public bool IncludeSymbols { get; set; }
        public bool IncludeSource { get; set; }
        public string RepositoryUrl { get; set; }
        public string RepositoryType { get; set; }
        public ITaskItem[] SourceFiles { get; set; }
        public bool NoPackageAnalysis { get; set; }
        public string MinClientVersion { get; set; }
        public bool Serviceable { get; set; }
        public string VersionSuffix { get; set; }
        public ITaskItem[] AssemblyReferences { get; set; }
        public bool ContinuePackingAfterGeneratingNuspec { get; set; }
        public string NuspecOutputPath { get; set; }
        public bool IncludeBuildOutput { get; set; }
        public string BuildOutputFolder { get; set; }
        public string[] ContentTargetFolders { get; set; }

        public ILogger Logger => new MSBuildLogger(Log);

        private IPackTaskLogic _packTaskLogic;

        /// <summary>
        /// This property is only used for testing.
        /// </summary>
        public IPackTaskLogic PackTaskLogic
        {
            get
            {
                if (_packTaskLogic == null)
                {
                    _packTaskLogic = new PackTaskLogic();
                }

                return _packTaskLogic;
            }

            set
            {
                _packTaskLogic = value;
            }
        }

        public override bool Execute()
        {
            var request = GetRequest();
            var logic = PackTaskLogic;

            var packArgs = logic.GetPackArgs(request);
            var packageBuilder = logic.GetPackageBuilder(request);
            var packRunner = logic.GetPackCommandRunner(request, packArgs, packageBuilder);
            logic.BuildPackage(packRunner);

            return true;
        }

        /// <summary>
        /// This method does two important things:
        /// 1. Normalizes string parameters, trimming whitespace and coalescing empty strings to null.
        /// 2. Wrap <see cref="ITaskItem"/> instances to facility unit testing.
        /// </summary>
        private IPackTaskRequest<IMSBuildItem> GetRequest()
        {
            return new PackTaskRequest
            {
                AssemblyName = MSBuildStringUtility.TrimAndGetNullForEmpty(AssemblyName),
                AssemblyReferences = MSBuildUtility.WrapMSBuildItem(AssemblyReferences),
                Authors = MSBuildStringUtility.TrimAndExcludeNullOrEmpty(Authors),
                BuildOutputFolder = MSBuildStringUtility.TrimAndGetNullForEmpty(BuildOutputFolder),
                ContinuePackingAfterGeneratingNuspec = ContinuePackingAfterGeneratingNuspec,
                ContentTargetFolders = MSBuildStringUtility.TrimAndExcludeNullOrEmpty(ContentTargetFolders),
                Copyright = MSBuildStringUtility.TrimAndGetNullForEmpty(Copyright),
                Description = MSBuildStringUtility.TrimAndGetNullForEmpty(Description),
                IconUrl = MSBuildStringUtility.TrimAndGetNullForEmpty(IconUrl),
                IncludeBuildOutput = IncludeBuildOutput,
                IncludeSource = IncludeSource,
                IncludeSymbols = IncludeSymbols,
                IsTool = IsTool,
                LicenseUrl = MSBuildStringUtility.TrimAndGetNullForEmpty(LicenseUrl),
                Logger = Logger,
                MinClientVersion = MSBuildStringUtility.TrimAndGetNullForEmpty(MinClientVersion),
                NoPackageAnalysis = NoPackageAnalysis,
                NuspecOutputPath = MSBuildStringUtility.TrimAndGetNullForEmpty(NuspecOutputPath),
                PackageFiles = MSBuildUtility.WrapMSBuildItem(PackageFiles),
                PackageFilesToExclude = MSBuildUtility.WrapMSBuildItem(PackageFilesToExclude),
                PackageId = MSBuildStringUtility.TrimAndGetNullForEmpty(PackageId),
                PackageOutputPath = MSBuildStringUtility.TrimAndGetNullForEmpty(PackageOutputPath),
                PackageTypes = MSBuildStringUtility.TrimAndExcludeNullOrEmpty(PackageTypes),
                PackageVersion = MSBuildStringUtility.TrimAndGetNullForEmpty(PackageVersion),
                PackItem = MSBuildUtility.WrapMSBuildItem(PackItem),
                ProjectUrl = MSBuildStringUtility.TrimAndGetNullForEmpty(ProjectUrl),
                ReleaseNotes = MSBuildStringUtility.TrimAndGetNullForEmpty(ReleaseNotes),
                RepositoryType = MSBuildStringUtility.TrimAndGetNullForEmpty(RepositoryType),
                RepositoryUrl = MSBuildStringUtility.TrimAndGetNullForEmpty(RepositoryUrl),
                RequireLicenseAcceptance = RequireLicenseAcceptance,
                RestoreOutputPath = MSBuildStringUtility.TrimAndGetNullForEmpty(RestoreOutputPath),
                Serviceable = Serviceable,
                SourceFiles = MSBuildUtility.WrapMSBuildItem(SourceFiles),
                Tags = MSBuildStringUtility.TrimAndExcludeNullOrEmpty(Tags),
                TargetFrameworks = MSBuildStringUtility.TrimAndExcludeNullOrEmpty(TargetFrameworks),
                TargetPathsToAssemblies = MSBuildStringUtility.TrimAndExcludeNullOrEmpty(TargetPathsToAssemblies),
                TargetPathsToSymbols = MSBuildStringUtility.TrimAndExcludeNullOrEmpty(TargetPathsToSymbols),
                VersionSuffix = MSBuildStringUtility.TrimAndGetNullForEmpty(VersionSuffix)
            };
        }
    }
}