using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    internal static class ContentFileUtils
    {
        private const string ContentFilesFolderName = "contentFiles/";
        private const string BuildAction = "buildAction";
        private const string CopyToOutput = "copyToOutput";
        private const string CodeLanguage = "codeLanguage";
        private const string None = "None";

        /// <summary>
        /// Normalizes build action casing and validates the action string.
        /// Returns null if the action is an unknown string.
        /// </summary>
        internal static string GetBuildActionOrNull(string action)
        {
            // Case sensitive compare
            if (BuildActionList.Contains(action))
            {
                return action;
            }

            // Find the correct casing
            var correctCase = BuildActionList.FirstOrDefault(buildAction => string.Equals(
                action,
                buildAction,
                StringComparison.OrdinalIgnoreCase));

            return correctCase;
        }

        private static HashSet<string> _buildActionList = null;

        /// <summary>
        /// Build action white list used for nuspec data.
        /// </summary>
        private static HashSet<string> BuildActionList
        {
            get
            {
                if (_buildActionList == null)
                {
                    _buildActionList = new HashSet<string>(new string[]
                    {
                        "None",
                        "Compile",
                        "Content",
                        "EmbeddedResource",
                        "ApplicationDefinition",
                        "Page",
                        "Resource",
                        "SplashScreen",
                        "DesignData",
                        "DesignDataWithDesignTimeCreatableTypes",
                        "CodeAnalysisDictionary"
                    }, StringComparer.Ordinal);
                }

                return _buildActionList;
            }
        }

        /// <summary>
        /// Get all content groups that have the nearest TxM
        /// </summary>
        internal static List<ContentItemGroup> GetContentGroupsForFramework(
            LockFileTargetLibrary lockFileLib,
            NuGetFramework framework,
            IEnumerable<ContentItemGroup> contentGroups)
        {
            var groups = new List<ContentItemGroup>();

            // Group by content by code language and find the nearest TxM under each language.
            var groupsByLanguage = new Dictionary<string, List<ContentItemGroup>>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in contentGroups)
            {
                var codeLanguage = (string)group.Properties[ManagedCodeConventions.PropertyNames.CodeLanguage];

                List<ContentItemGroup> index;
                if (!groupsByLanguage.TryGetValue(codeLanguage, out index))
                {
                    index = new List<ContentItemGroup>(1);
                    groupsByLanguage.Add(codeLanguage, index);
                }

                index.Add(group);
            }

            // Find the nearest TxM within each language
            foreach (var codeLanguagePair in groupsByLanguage)
            {
                var languageGroups = codeLanguagePair.Value;

                var nearestGroup = NuGetFrameworkUtility.GetNearest<ContentItemGroup>(languageGroups, framework, 
                    group =>
                       (NuGetFramework)group.Properties[ManagedCodeConventions.PropertyNames.TargetFrameworkMoniker]);

                // If a compatible group exists within the code language add it to the results
                if (nearestGroup != null)
                {
                    groups.Add(nearestGroup);
                }
            }

            return groups;
        }

        /// <summary>
        /// Apply build actions from the nuspec to items from the contentFiles folder.
        /// </summary>
        internal static List<LockFileItem> GetContentFileGroup(
            NuGetFramework framework,
            NuspecReader nuspec,
            List<ContentItemGroup> contentFileGroups)
        {
            var results = new List<LockFileItem>(contentFileGroups.Count);
            var rootFolderPathLength = ContentFilesFolderName.Length;

            // Read the contentFiles section of the nuspec
            // Read the entries so that the bottom entry has priority
            var nuspecContentFiles = nuspec.GetContentFiles().ToList();

            // Initialize mappings
            var entryMappings = new Dictionary<string, List<ContentFilesEntry>>(StringComparer.OrdinalIgnoreCase);
            var languageMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in contentFileGroups)
            {
                var codeLanguage = group.Properties[ManagedCodeConventions.PropertyNames.CodeLanguage] as string;

                foreach (var item in group.Items)
                {
                    if (!entryMappings.ContainsKey(item.Path))
                    {
                        entryMappings.Add(item.Path, new List<ContentFilesEntry>());
                        languageMappings.Add(item.Path, codeLanguage);
                    }
                }
            }

            // Virtual root for file globbing
            var rootDirectory = new VirtualFileInfo(VirtualFileProvider.RootDir, isDirectory: true);

            // Apply all nuspec property mappings to the files returned by content model
            foreach (var filesEntry in nuspecContentFiles)
            {
                // this is validated in the nuspec reader
                Debug.Assert(filesEntry.Include != null, "invalid contentFiles entry");

                // Create a filesystem matcher for globbing patterns
                var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                matcher.AddInclude(filesEntry.Include);

                if (filesEntry.Exclude != null)
                {
                    matcher.AddExclude(filesEntry.Exclude);
                }

                // Check each file against the patterns
                foreach (var file in entryMappings.Keys)
                {
                    // Remove contentFiles/ from the string
                    Debug.Assert(file.StartsWith(ContentFilesFolderName, StringComparison.OrdinalIgnoreCase),
                        "invalid file path: " + file);

                    // All files should begin with the same root folder
                    if (file.Length > rootFolderPathLength)
                    {
                        var relativePath = file.Substring(rootFolderPathLength, file.Length - rootFolderPathLength);

                        // Check if the nuspec group include/exclude patterns apply to the file
                        var virtualDirectory = new VirtualFileProvider(new List<string>() { relativePath });
                        var globbingDirectory = new FileProviderGlobbingDirectory(
                            virtualDirectory,
                            fileInfo: rootDirectory,
                            parent: null);

                        // Currently Matcher only returns the file name not the full path, each file must be
                        // check individually.
                        var matchResults = matcher.Execute(globbingDirectory);

                        if (matchResults.Files.Any())
                        {
                            entryMappings[file].Add(filesEntry);
                        }
                    }
                }
            }

            // Create lock file entries for each item in the contentFiles folder
            foreach (var file in entryMappings.Keys)
            {
                // defaults
                var action = PackagingConstants.ContentFilesDefaultBuildAction;
                var copyToOutput = false;
                var flatten = false;

                // _._ is needed for empty codeLanguage groups
                if (file.EndsWith(PackagingCoreConstants.ForwardSlashEmptyFolder, StringComparison.Ordinal))
                {
                    action = None;
                }
                else
                {
                    // apply each entry
                    // entries may not have all the attributes, if a value is null 
                    // ignore it and continue using the previous value.
                    foreach (var filesEntry in entryMappings[file])
                    {
                        if (!string.IsNullOrEmpty(filesEntry.BuildAction))
                        {
                            action = filesEntry.BuildAction;
                        }

                        if (filesEntry.CopyToOutput.HasValue)
                        {
                            copyToOutput = filesEntry.CopyToOutput.Value;
                        }

                        if (filesEntry.Flatten.HasValue)
                        {
                            flatten = filesEntry.Flatten.Value;
                        }
                    }
                }

                // Add attributes to the lock file item
                var lockFileItem = new LockFileItem(file);

                // Add the language from the directory path
                lockFileItem.Properties.Add(
                    CodeLanguage,
                    languageMappings[file].ToLowerInvariant());

                // normalize and validate the build action name
                // the action will be null if the string was unrecognized
                var normalizedAction = GetBuildActionOrNull(action);

                if (normalizedAction == null)
                {
                    // Throw an error containing the package identity, invalid action, and file where it occurred.
                    var message = Strings.FormatError_UnknownBuildAction(nuspec.GetIdentity(), action, file);
                    throw new PackagingException(message);
                }

                lockFileItem.Properties.Add(BuildAction, normalizedAction);
                lockFileItem.Properties.Add(CopyToOutput, copyToOutput.ToString());

                // Check if this is a .pp transform. If the filename is ".pp" ignore it since it will
                // have no file name after the transform.
                var isPP = lockFileItem.Path.EndsWith(".pp", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(Path.GetFileNameWithoutExtension(lockFileItem.Path));

                if (copyToOutput)
                {
                    string destination = null;

                    if (flatten)
                    {
                        destination = Path.GetFileName(lockFileItem.Path);
                    }
                    else
                    {
                        // Find path relative to the TxM
                        // Ex: contentFiles/cs/net45/config/config.xml -> config/config.xml
                        destination = GetContentFileFolderRelativeToFramework(file);
                    }

                    if (isPP)
                    {
                        // Remove .pp from the output file path
                        destination = destination.Substring(0, destination.Length - 3);
                    }

                    lockFileItem.Properties.Add("outputPath", destination);
                }

                // Add the pp transform file if one exists
                if (isPP)
                {
                    var destination = lockFileItem.Path.Substring(0, lockFileItem.Path.Length - 3);
                    destination = GetContentFileFolderRelativeToFramework(destination);

                    lockFileItem.Properties.Add("ppOutputPath", destination);
                }

                results.Add(lockFileItem);
            }

            return results;
        }

        /// <summary>
        /// Create an empty lock file item for any/any
        /// </summary>
        internal static LockFileItem CreateEmptyItem()
        {
            var item = new LockFileItem("contentFiles/any/any/_._");
            item.Properties.Add(BuildAction, None);
            item.Properties.Add(CopyToOutput, Boolean.FalseString);
            item.Properties.Add(CodeLanguage, "any");

            return item;
        }

        // Find path relative to the TxM
        // Ex: contentFiles/cs/net45/config/config.xml -> config/config.xml
        // Ex: contentFiles/any/any/config/config.xml -> config/config.xml
        private static string GetContentFileFolderRelativeToFramework(string itemPath)
        {
            var parts = itemPath.Split('/');

            if (parts.Length > 3)
            {
                return string.Join("/", parts.Skip(3));
            }

            Debug.Fail("Unable to get relative path: " + itemPath);
            return itemPath;
        }
    }
}
