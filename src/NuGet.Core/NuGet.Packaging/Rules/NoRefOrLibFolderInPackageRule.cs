using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using NuGet.Client;
using NuGet.Common;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;

namespace NuGet.Packaging.Rules
{
    internal class NoRefOrLibFolderInPackageRule : IPackageRule
    {
        public string MessageFormat { get; }

        public NoRefOrLibFolderInPackageRule(string messageFormat)
        {
            MessageFormat = messageFormat;
        }

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader package)
        {
            var files = package.GetFiles();
            return Validate(files);
        }

        internal IEnumerable<PackagingLogMessage> Validate(IEnumerable<string> files)
        {
            var managedCodeConventions = new ManagedCodeConventions(new RuntimeGraph());
            var collection = new ContentItemCollection();
            collection.Load(files);

            var libItems = ContentExtractor.GetContentForPattern(collection, managedCodeConventions.Patterns.CompileLibAssemblies);
            var refItems = ContentExtractor.GetContentForPattern(collection, managedCodeConventions.Patterns.CompileRefAssemblies);
            var buildItems = ContentExtractor.GetContentForPattern(collection, managedCodeConventions.Patterns.MSBuildFiles);

            var libFrameworks = ContentExtractor.GetGroupFrameworks(libItems).ToArray();
            var refFrameworks = ContentExtractor.GetGroupFrameworks(refItems).ToArray();
            var buildFrameworks = ContentExtractor.GetGroupFrameworks(buildItems).ToArray();

            if (libFrameworks.Length == 0 && refFrameworks.Length == 0)
            {
                //if you can't find the ref and lib folder, then find the build folder
                if (buildFrameworks.Length != 0)
                {
                    //if you can find any folders other than native or any, raise an NU5127
                    if (buildFrameworks.Any(t => (FrameworkConstants.DotNetAll.Satisfies(t) || FrameworkNameValidatorUtility.IsValidFrameworkName(t))
                                            && t.GetShortFolderName() != FrameworkConstants.FrameworkIdentifiers.DotNet
                                            && t.GetShortFolderName() != FrameworkConstants.FrameworkIdentifiers.Native))
                    {
                        var possibleFrameworks = buildFrameworks.
                            Where(t => t.IsSpecificFramework
                                    && t.GetShortFolderName() != FrameworkConstants.FrameworkIdentifiers.DotNet
                                    && t.GetShortFolderName() != FrameworkConstants.FrameworkIdentifiers.Native).
                            Select(t => t.GetShortFolderName()).ToArray();

                        (var tfmNames, var suggestedDirectories) = GenerateWarningString(possibleFrameworks);
                        
                        var issue = new List<PackagingLogMessage>();
                        issue.Add(PackagingLogMessage.CreateWarning(string.Format(MessageFormat, tfmNames, suggestedDirectories),
                            NuGetLogCode.NU5127));
                        return issue;
                    }
                }
            }

            return Array.Empty<PackagingLogMessage>();
        }

        private (string, string) GenerateWarningString(string[] possibleFrameworks)
        {
            string tfmNames = possibleFrameworks.Length > 1
                ? string.Join(", ", possibleFrameworks)
                : possibleFrameworks[0];

            string suggestedDirectories = possibleFrameworks.Length > 1
                ? CreateDirectoriesMessage(possibleFrameworks)
                : string.Format("-lib/{0}/_._", possibleFrameworks[0]);
            
            return (tfmNames, suggestedDirectories);
        }

        private static string CreateDirectoriesMessage(string[] possibleFrameworks)
        {
            var suggestedDirectories = new StringBuilder();
            foreach (var framework in possibleFrameworks)
            {
                suggestedDirectories.AppendFormat("-lib/{0}/_._", framework).AppendLine();
            }
            return suggestedDirectories.ToString();
        }

    }
}
