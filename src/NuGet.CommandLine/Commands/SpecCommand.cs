using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Common;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "spec", "SpecCommandDescription", MaxArgs = 1,
            UsageSummaryResourceName = "SpecCommandUsageSummary", UsageExampleResourceName = "SpecCommandUsageExamples")]
    public class SpecCommand : Command
    {
        internal static readonly string SampleProjectUrl = "http://PROJECT_URL_HERE_OR_DELETE_THIS_LINE";
        internal static readonly string SampleLicenseUrl = "http://LICENSE_URL_HERE_OR_DELETE_THIS_LINE";
        internal static readonly string SampleIconUrl = "http://ICON_URL_HERE_OR_DELETE_THIS_LINE";
        internal static readonly string SampleTags = "Tag1 Tag2";
        internal static readonly string SampleReleaseNotes = "Summary of changes made in this release of the package.";
        internal static readonly string SampleDescription = "Package description";
        internal static readonly ManifestDependency SampleManifestDependency = new ManifestDependency { Id = "SampleDependency", Version = "1.0" };

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
            var manifest = new Manifest();
            string projectFile = null;
            string fileName = null;

            if (!String.IsNullOrEmpty(AssemblyPath))
            {
                // Extract metadata from the assembly
                string path = Path.Combine(CurrentDirectory, AssemblyPath);
                AssemblyMetadata metadata = AssemblyMetadataExtractor.GetMetadata(path);
                manifest.Metadata.Id = metadata.Name;
                manifest.Metadata.Version = metadata.Version.ToString();
                manifest.Metadata.Authors = metadata.Company;
                manifest.Metadata.Description = metadata.Description;
            }
            else
            {
                if (!ProjectHelper.TryGetProjectFile(CurrentDirectory, out projectFile))
                {
                    manifest.Metadata.Id = Arguments.Any() ? Arguments[0] : "Package";
                    manifest.Metadata.Version = "1.0.0";
                }
                else
                {
                    fileName = Path.GetFileNameWithoutExtension(projectFile);
                    manifest.Metadata.Id = "$id$";
                    manifest.Metadata.Title = "$title$";
                    manifest.Metadata.Version = "$version$";
                    manifest.Metadata.Description = "$description$";
                    manifest.Metadata.Authors = "$author$";
                }
            }

            // Get the file name from the id or the project file
            fileName = fileName ?? manifest.Metadata.Id;

            // If we're using a project file then we want the a minimal nuspec
            if (String.IsNullOrEmpty(projectFile))
            {
                manifest.Metadata.Description = manifest.Metadata.Description ?? SampleDescription;
                if (String.IsNullOrEmpty(manifest.Metadata.Authors))
                {
                    manifest.Metadata.Authors = Environment.UserName;
                }
                manifest.Metadata.DependencySets = new List<ManifestDependencySet>();
                manifest.Metadata.DependencySets.Add(new ManifestDependencySet
                {
                    Dependencies = new List<ManifestDependency> { SampleManifestDependency }
                });
            }

            manifest.Metadata.ProjectUrl = SampleProjectUrl;
            manifest.Metadata.LicenseUrl = SampleLicenseUrl;
            manifest.Metadata.IconUrl = SampleIconUrl;
            manifest.Metadata.Tags = SampleTags;
            manifest.Metadata.Copyright = "Copyright " + DateTime.Now.Year;
            manifest.Metadata.ReleaseNotes = SampleReleaseNotes;
            string nuspecFile = fileName + Constants.ManifestExtension;

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
                        manifest.Save(stream, validate: false);
                        stream.Seek(0, SeekOrigin.Begin);
                        string content = stream.ReadToEnd();
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
