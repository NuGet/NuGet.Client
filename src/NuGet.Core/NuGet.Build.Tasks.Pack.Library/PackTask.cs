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
        public ITaskItem[] PackageReferences { get; set; }
        public ITaskItem[] ProjectReferences { get; set; }
        public bool ContinuePackingAfterGeneratingNuspec { get; set; }
        public string NuspecOutputPath { get; set; }
        public bool IncludeBuildOutput { get; set; }
        public string BuildOutputFolder { get; set; }

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
                AssemblyName = MSBuildUtility.TrimAndGetNullForEmpty(AssemblyName),
                AssemblyReferences = MSBuildUtility.WrapMSBuildItem(AssemblyReferences),
                Authors = MSBuildUtility.TrimAndExcludeNullOrEmpty(Authors),
                BuildOutputFolder = MSBuildUtility.TrimAndGetNullForEmpty(BuildOutputFolder),
                ContinuePackingAfterGeneratingNuspec = ContinuePackingAfterGeneratingNuspec,
                Copyright = MSBuildUtility.TrimAndGetNullForEmpty(Copyright),
                Description = MSBuildUtility.TrimAndGetNullForEmpty(Description),
                IconUrl = MSBuildUtility.TrimAndGetNullForEmpty(IconUrl),
                IncludeBuildOutput = IncludeBuildOutput,
                IncludeSource = IncludeSource,
                IncludeSymbols = IncludeSymbols,
                IsTool = IsTool,
                LicenseUrl = MSBuildUtility.TrimAndGetNullForEmpty(LicenseUrl),
                Logger = Logger,
                MinClientVersion = MSBuildUtility.TrimAndGetNullForEmpty(MinClientVersion),
                NoPackageAnalysis = NoPackageAnalysis,
                NuspecOutputPath = MSBuildUtility.TrimAndGetNullForEmpty(NuspecOutputPath),
                PackageFiles = MSBuildUtility.WrapMSBuildItem(PackageFiles),
                PackageFilesToExclude = MSBuildUtility.WrapMSBuildItem(PackageFilesToExclude),
                PackageId = MSBuildUtility.TrimAndGetNullForEmpty(PackageId),
                PackageOutputPath = MSBuildUtility.TrimAndGetNullForEmpty(PackageOutputPath),
                PackageReferences = MSBuildUtility.WrapMSBuildItem(PackageReferences),
                PackageTypes = MSBuildUtility.TrimAndExcludeNullOrEmpty(PackageTypes),
                PackageVersion = MSBuildUtility.TrimAndGetNullForEmpty(PackageVersion),
                PackItem = MSBuildUtility.WrapMSBuildItem(PackItem),
                ProjectReferences = MSBuildUtility.WrapMSBuildItem(ProjectReferences),
                ProjectUrl = MSBuildUtility.TrimAndGetNullForEmpty(ProjectUrl),
                ReleaseNotes = MSBuildUtility.TrimAndGetNullForEmpty(ReleaseNotes),
                RepositoryType = MSBuildUtility.TrimAndGetNullForEmpty(RepositoryType),
                RepositoryUrl = MSBuildUtility.TrimAndGetNullForEmpty(RepositoryUrl),
                RequireLicenseAcceptance = RequireLicenseAcceptance,
                Serviceable = Serviceable,
                SourceFiles = MSBuildUtility.WrapMSBuildItem(SourceFiles),
                Tags = MSBuildUtility.TrimAndExcludeNullOrEmpty(Tags),
                TargetFrameworks = MSBuildUtility.TrimAndExcludeNullOrEmpty(TargetFrameworks),
                TargetPathsToAssemblies = MSBuildUtility.TrimAndExcludeNullOrEmpty(TargetPathsToAssemblies),
                TargetPathsToSymbols = MSBuildUtility.TrimAndExcludeNullOrEmpty(TargetPathsToSymbols),
                VersionSuffix = MSBuildUtility.TrimAndGetNullForEmpty(VersionSuffix)
            };
        }
    }
}
