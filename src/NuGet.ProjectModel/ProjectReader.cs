using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class ProjectReader
    {
        public static bool HasProjectFile(string path)
        {
            string projectPath = Path.Combine(path, Project.ProjectFileName);

            return File.Exists(projectPath);
        }

        public static bool TryReadProject(string path, out Project project)
        {
            project = null;

            string projectPath = null;

            if (string.Equals(Path.GetFileName(path), Project.ProjectFileName, StringComparison.OrdinalIgnoreCase))
            {
                projectPath = path;
                path = Path.GetDirectoryName(path);
            }
            else if (!HasProjectFile(path))
            {
                return false;
            }
            else
            {
                projectPath = Path.Combine(path, Project.ProjectFileName);
            }

            // Assume the directory name is the project name if none was specified
            var projectName = GetDirectoryName(path);
            projectPath = Path.GetFullPath(projectPath);

            try
            {
                using (var stream = File.OpenRead(projectPath))
                {
                    project = GetProject(stream, projectName, projectPath);
                }
            }
            catch (JsonReaderException ex)
            {
                throw ProjectFormatException.Create(ex, projectPath);
            }

            return true;
        }

        public static Project GetProject(string json, string projectName, string projectPath)
        {
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            return GetProject(ms, projectName, projectPath);
        }

        public static Project GetProject(Stream stream, string projectName, string projectPath)
        {
            var project = new Project();

            var reader = new JsonTextReader(new StreamReader(stream));
            var rawProject = JObject.Load(reader);

            // Metadata properties
            var version = rawProject["version"];
            var authors = rawProject["authors"];
            var owners = rawProject["owners"];
            var tags = rawProject["tags"];
            var buildVersion = Environment.GetEnvironmentVariable("K_BUILD_VERSION");

            project.Name = projectName;
            project.ProjectFilePath = Path.GetFullPath(projectPath);

            if (version == null)
            {
                project.Version = new NuGetVersion("1.0.0");
            }
            else
            {
                try
                {
                    project.Version = SpecifySnapshot(version.Value<string>(), buildVersion);
                }
                catch (Exception ex)
                {
                    var lineInfo = (IJsonLineInfo)version;

                    throw ProjectFormatException.Create(ex, version, project.ProjectFilePath);
                }
            }

            project.Description = rawProject.GetValue<string>("description");
            project.Authors = authors == null ? new string[] { } : authors.Value<string[]>();
            project.Owners = owners == null ? new string[] { } : owners.Value<string[]>();
            project.Dependencies = new List<LibraryDependency>();
            project.ProjectUrl = rawProject.GetValue<string>("projectUrl");
            project.IconUrl = rawProject.GetValue<string>("iconUrl");
            project.LicenseUrl = rawProject.GetValue<string>("licenseUrl");
            project.Copyright = rawProject.GetValue<string>("copyright");
            project.Language = rawProject.GetValue<string>("language");
            project.RequireLicenseAcceptance = rawProject.GetValue<bool?>("requireLicenseAcceptance") ?? false;
            project.Tags = tags == null ? new string[] { } : tags.ValueAsArray<string>();

            var scripts = rawProject["scripts"] as JObject;
            if (scripts != null)
            {
                foreach (var script in scripts)
                {
                    var value = script.Value;
                    if (value.Type == JTokenType.String)
                    {
                        project.Scripts[script.Key] = new string[] { value.Value<string>() };
                    }
                    else if (value.Type == JTokenType.Array)
                    {
                        project.Scripts[script.Key] = script.Value.ValueAsArray<string>();
                    }
                    else
                    {
                        throw ProjectFormatException.Create(
                            string.Format("The value of a script in '{0}' can only be a string or an array of strings", Project.ProjectFileName),
                            value,
                            project.ProjectFilePath);
                    }
                }
            }

            BuildTargetFrameworks(project, rawProject);

            PopulateDependencies(
                project.ProjectFilePath,
                project.Dependencies,
                rawProject,
                "dependencies",
                isGacOrFrameworkReference: false);

            return project;
        }

        private static NuGetVersion SpecifySnapshot(string version, string snapshotValue)
        {
            if (version.EndsWith("-*"))
            {
                if (string.IsNullOrEmpty(snapshotValue))
                {
                    version = version.Substring(0, version.Length - 2);
                }
                else
                {
                    version = version.Substring(0, version.Length - 1) + snapshotValue;
                }
            }

            return new NuGetVersion(version);
        }

        private static void PopulateDependencies(
            string projectPath,
            IList<LibraryDependency> results,
            JObject settings,
            string propertyName,
            bool isGacOrFrameworkReference)
        {
            var dependencies = settings[propertyName] as JObject;
            if (dependencies != null)
            {
                foreach (var dependency in dependencies)
                {
                    if (string.IsNullOrEmpty(dependency.Key))
                    {

                        throw ProjectFormatException.Create(
                            "Unable to resolve dependency ''.",
                            dependency.Value,
                            projectPath);
                    }

                    // Support 
                    // "dependencies" : {
                    //    "Name" : "1.0"
                    // }

                    var dependencyValue = dependency.Value;
                    var dependencyTypeValue = LibraryDependencyType.Default;

                    string dependencyVersionValue = null;
                    JToken dependencyVersionToken = dependencyValue;

                    if (dependencyValue.Type == JTokenType.String)
                    {
                        dependencyVersionValue = dependencyValue.Value<string>();
                    }
                    else
                    {
                        if (dependencyValue.Type == JTokenType.Object)
                        {
                            dependencyVersionToken = dependencyValue["version"];
                            if (dependencyVersionToken != null && dependencyVersionToken.Type == JTokenType.String)
                            {
                                dependencyVersionValue = dependencyVersionToken.Value<string>();
                            }
                        }

                        IEnumerable<string> strings;
                        if (TryGetStringEnumerable(dependencyValue["type"], out strings))
                        {
                            dependencyTypeValue = LibraryDependencyType.Parse(strings);
                        }
                    }

                    NuGetVersionRange dependencyVersionRange = null;

                    if (!string.IsNullOrEmpty(dependencyVersionValue))
                    {
                        try
                        {
                            dependencyVersionRange = NuGetVersionRange.Parse(dependencyVersionValue);
                        }
                        catch (Exception ex)
                        {
                            throw ProjectFormatException.Create(
                                ex,
                                dependencyVersionToken,
                                projectPath);
                        }
                    }

                    results.Add(new LibraryDependency
                    {
                        LibraryRange = new LibraryRange
                        {
                            Name = dependency.Key,
                            VersionRange = dependencyVersionRange,
                            Type = isGacOrFrameworkReference ? LibraryTypes.FrameworkOrGacAssembly : null,
                        },
                        Type = dependencyTypeValue
                    });
                }
            }
        }

        private static bool TryGetStringEnumerable(JToken token, out IEnumerable<string> result)
        {
            IEnumerable<string> values;
            if (token == null)
            {
                result = null;
                return false;
            }
            else if (token.Type == JTokenType.String)
            {
                values = new[]
                {
                    token.Value<string>()
                };
            }
            else
            {
                values = token.Value<string[]>();
            }
            result = values
                .SelectMany(value => value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries));
            return true;
        }

        private static void BuildTargetFrameworks(Project project, JObject rawProject)
        {
            // The frameworks node is where target frameworks go
            /*
                {
                    "frameworks": {
                        "net45": {
                        },
                        "aspnet50": {
                        }
                    }
                }
            */

            var frameworks = rawProject["frameworks"] as JObject;
            if (frameworks != null)
            {
                foreach (var framework in frameworks)
                {
                    try
                    {
                        BuildTargetFrameworkNode(project, framework);
                    }
                    catch (Exception ex)
                    {
                        throw ProjectFormatException.Create(ex, framework.Value, project.ProjectFilePath);
                    }
                }
            }
        }

        private static bool BuildTargetFrameworkNode(Project project, KeyValuePair<string, JToken> targetFramework)
        {
            var frameworkName = GetFramework(targetFramework.Key);

            // If it's not unsupported then keep it
            if (frameworkName == NuGetFramework.UnsupportedFramework)
            {
                // REVIEW: Should we skip unsupported target frameworks
                return false;
            }

            var targetFrameworkInformation = new TargetFrameworkInformation
            {
                FrameworkName = frameworkName,
                Dependencies = new List<LibraryDependency>()
            };

            var properties = targetFramework.Value.Value<JObject>();

            PopulateDependencies(
                project.ProjectFilePath,
                targetFrameworkInformation.Dependencies,
                properties,
                "dependencies",
                isGacOrFrameworkReference: false);

            var frameworkAssemblies = new List<LibraryDependency>();
            PopulateDependencies(
                project.ProjectFilePath,
                frameworkAssemblies,
                properties,
                "frameworkAssemblies",
                isGacOrFrameworkReference: true);

            frameworkAssemblies.ForEach(d => targetFrameworkInformation.Dependencies.Add(d));


            project.TargetFrameworks.Add(targetFrameworkInformation);

            return true;
        }

        private static NuGetFramework GetFramework(string key)
        {
            //if (key == "aspnet50")
            //{
            //    return new NuGetFramework("Asp.Net", new Version(5, 0));
            //}
            //else if (key == "aspnetcore50")
            //{
            //    return new NuGetFramework("Asp.NetCore", new Version(5, 0));
            //}

            return NuGetFramework.Parse(key);
        }

        private static string GetDirectoryName(string path)
        {
            path = path.TrimEnd(Path.DirectorySeparatorChar);
            return path.Substring(Path.GetDirectoryName(path).Length).Trim(Path.DirectorySeparatorChar);
        }
    }
}