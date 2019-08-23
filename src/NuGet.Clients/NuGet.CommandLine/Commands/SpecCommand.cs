using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.CommandLine
{

    [Command(typeof(NuGetCommand), "spec", "SpecCommandDescription", MaxArgs = 1,
            UsageSummaryResourceName = "SpecCommandUsageSummary", UsageExampleResourceName = "SpecCommandUsageExamples")]
    public class SpecCommand : Command
    {
        internal static readonly string SampleProjectUrl = "http://project_url_here_or_delete_this_line/";
        internal static readonly string SampleLicenseUrl = "http://license_url_here_or_delete_this_line/";
        internal static readonly string SampleIconUrl = "http://icon_url_here_or_delete_this_line/";
        internal static readonly string SampleTags = "Tag1 Tag2";
        internal static readonly string SampleReleaseNotes = "Summary of changes made in this release of the package.";
        internal static readonly string SampleDescription = "Package description";
        internal static readonly NuGetFramework SampleTfm = new NuGetFramework("472");
        internal static readonly PackageDependency SampleManifestDependency = new PackageDependency("SampleDependency", new Versioning.VersionRange(new Versioning.NuGetVersion("1.0.0")));

        [Option(typeof(NuGetCommand), "SpecCommandAssemblyPathDescription")]
        public string AssemblyPath
        {
            get;
            set;
        }

        [Option(typeof(NuGetCommand), "SpecCommandForceDescription")]
        public bool Force
        {
            get;
            set;
        }

        public override void ExecuteCommand()
        {
            var manifest = new Manifest(new ManifestMetadata());
            string projectFile = null;
            string fileName = null;
            bool hasProjectFile = false;

            if (!String.IsNullOrEmpty(AssemblyPath))
            {
                // Extract metadata from the assembly
                string path = Path.Combine(CurrentDirectory, AssemblyPath);
                AssemblyMetadata metadata = AssemblyMetadataExtractor.GetMetadata(path);
                manifest.Metadata.Id = metadata.Name;
                manifest.Metadata.Version = Versioning.NuGetVersion.Parse(metadata.Version.ToString());
                manifest.Metadata.Authors = new List<string>() { metadata.Company };
                manifest.Metadata.Description = metadata.Description;
            }
            else
            {
                if (!ProjectHelper.TryGetProjectFile(CurrentDirectory, out projectFile))
                {
                    manifest.Metadata.Id = Arguments.Any() ? Arguments[0] : "Package";
                    manifest.Metadata.Version = Versioning.NuGetVersion.Parse("1.0.0");
                }
                else
                {
                    hasProjectFile = true;
                    fileName = Path.GetFileNameWithoutExtension(projectFile);
                    manifest.Metadata.Id = "mydummyidhere123123123";
                    manifest.Metadata.Title = "$title$";
                    // This is replaced with `$version$` below.
                    manifest.Metadata.Version = new Versioning.NuGetVersion("1.0.0");
                    manifest.Metadata.Description = "$description$";
                    manifest.Metadata.Authors = new List<string>() { "$author$" };
                }
            }

            // Get the file name from the id or the project file
            fileName = fileName ?? manifest.Metadata.Id;

            // If we're using a project file then we want the a minimal nuspec
            if (String.IsNullOrEmpty(projectFile))
            {
                manifest.Metadata.Description = manifest.Metadata.Description ?? SampleDescription;
                if (!manifest.Metadata.Authors.Any() || String.IsNullOrEmpty(manifest.Metadata.Authors.First()))
                {
                    manifest.Metadata.Authors = new List<string>() { Environment.UserName };
                }
                manifest.Metadata.DependencyGroups = new List<PackageDependencyGroup>() {
                    new PackageDependencyGroup(SampleTfm, new List<PackageDependency>() { SampleManifestDependency })
                };
            }

            manifest.Metadata.SetProjectUrl(SampleProjectUrl);
            manifest.Metadata.SetLicenseUrl(SampleLicenseUrl);
            manifest.Metadata.SetIconUrl(SampleIconUrl);
            manifest.Metadata.Tags = SampleTags;
            manifest.Metadata.Copyright = "Copyright " + DateTime.Now.Year;
            manifest.Metadata.ReleaseNotes = SampleReleaseNotes;
            string nuspecFile = fileName + PackagingConstants.ManifestExtension;

            // Skip the creation if the file exists and force wasn't specified
            if (File.Exists(nuspecFile) && !Force)
            {
                Console.WriteLine(LocalizedResourceManager.GetString("SpecCommandFileExists"), nuspecFile);
            }
            else
            {
                try
                {
                    using (var stream = new MemoryStream())
                    {
                        manifest.Save(stream);
                        stream.Seek(0, SeekOrigin.Begin);
                        string content = stream.ReadToEnd();
                        // We have to replace it here because we can't have
                        // arbitrary string versions in ManifestMetadata
                        if (hasProjectFile)
                        {
                            content = content.Replace("<id>mydummyidhere123123123</id>", "<id>$id$</id>");
                            content = content.Replace("<version>1.0.0</version>", "<version>$version$</version>");
                        }
                        File.WriteAllText(nuspecFile, RemoveSchemaNamespace(content));
                    }

                    Console.WriteLine(LocalizedResourceManager.GetString("SpecCommandCreatedNuSpec"), nuspecFile);
                }
                catch
                {
                    // Cleanup the file if it fails to save for some reason
                    File.Delete(nuspecFile);
                    throw;
                }
            }
        }

        public override bool IncludedInHelp(string optionName)
        {
            if (string.Equals(optionName, "ConfigFile", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return base.IncludedInHelp(optionName);
        }

        private static string RemoveSchemaNamespace(string content)
        {
            // This seems to be the only way to clear out xml namespaces.
            return Regex.Replace(content, @"(xmlns:?[^=]*=[""][^""]*[""])", String.Empty, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }
    }
}
