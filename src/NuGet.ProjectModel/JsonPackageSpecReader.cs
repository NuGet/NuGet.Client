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
    public class JsonPackageSpecReader
    {
        public static PackageSpec GetPackageSpec(string json, string name, string packageSpecPath)
        {
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            return GetPackageSpec(ms, name, packageSpecPath);
        }

        public static PackageSpec GetPackageSpec(Stream stream, string name, string packageSpecPath)
        {
            // Load the raw JSON into the package spec object
            var reader = new JsonTextReader(new StreamReader(stream));
            var rawPackageSpec = JObject.Load(reader);
            var packageSpec = new PackageSpec(rawPackageSpec);

            // Parse properties we know about
            var version = rawPackageSpec["version"];
            var authors = rawPackageSpec["authors"];
            var owners = rawPackageSpec["owners"];
            var tags = rawPackageSpec["tags"];
            var buildVersion = Environment.GetEnvironmentVariable("DNX_BUILD_VERSION");

            packageSpec.Name = name;
            packageSpec.FilePath = Path.GetFullPath(packageSpecPath);

            if (version == null)
            {
                packageSpec.Version = new NuGetVersion("1.0.0");
            }
            else
            {
                try
                {
                    packageSpec.Version = SpecifySnapshot(version.Value<string>(), buildVersion);
                }
                catch (Exception ex)
                {
                    var lineInfo = (IJsonLineInfo)version;

                    throw PackageSpecFormatException.Create(ex, version, packageSpec.FilePath);
                }
            }

            packageSpec.Description = rawPackageSpec.GetValue<string>("description");
            packageSpec.Authors = authors == null ? new string[] { } : authors.ValueAsArray<string>();
            packageSpec.Owners = owners == null ? new string[] { } : owners.ValueAsArray<string>();
            packageSpec.Dependencies = new List<LibraryDependency>();
            packageSpec.ProjectUrl = rawPackageSpec.GetValue<string>("projectUrl");
            packageSpec.IconUrl = rawPackageSpec.GetValue<string>("iconUrl");
            packageSpec.LicenseUrl = rawPackageSpec.GetValue<string>("licenseUrl");
            packageSpec.Copyright = rawPackageSpec.GetValue<string>("copyright");
            packageSpec.Language = rawPackageSpec.GetValue<string>("language");
            packageSpec.RequireLicenseAcceptance = rawPackageSpec.GetValue<bool?>("requireLicenseAcceptance") ?? false;
            packageSpec.Tags = tags == null ? new string[] { } : tags.ValueAsArray<string>();

            var scripts = rawPackageSpec["scripts"] as JObject;
            if (scripts != null)
            {
                foreach (var script in scripts)
                {
                    var value = script.Value;
                    if (value.Type == JTokenType.String)
                    {
                        packageSpec.Scripts[script.Key] = new string[] { value.Value<string>() };
                    }
                    else if (value.Type == JTokenType.Array)
                    {
                        packageSpec.Scripts[script.Key] = script.Value.ValueAsArray<string>();
                    }
                    else
                    {
                        throw PackageSpecFormatException.Create(
                            string.Format("The value of a script in '{0}' can only be a string or an array of strings", PackageSpec.PackageSpecFileName),
                            value,
                            packageSpec.FilePath);
                    }
                }
            }

            BuildTargetFrameworks(packageSpec, rawPackageSpec);

            PopulateDependencies(
                packageSpec.FilePath,
                packageSpec.Dependencies,
                rawPackageSpec,
                "dependencies",
                isGacOrFrameworkReference: false);

            return packageSpec;
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
            string packageSpecPath,
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

                        throw PackageSpecFormatException.Create(
                            "Unable to resolve dependency ''.",
                            dependency.Value,
                            packageSpecPath);
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

                    VersionRange dependencyVersionRange = null;

                    if (!string.IsNullOrEmpty(dependencyVersionValue))
                    {
                        try
                        {
                            dependencyVersionRange = VersionRange.Parse(dependencyVersionValue);
                        }
                        catch (Exception ex)
                        {
                            throw PackageSpecFormatException.Create(
                                ex,
                                dependencyVersionToken,
                                packageSpecPath);
                        }
                    }

                    results.Add(new LibraryDependency()
                    {
                        LibraryRange = new LibraryRange()
                        {
                            Name = dependency.Key,
                            TypeConstraint = isGacOrFrameworkReference ? LibraryTypes.Reference : null,
                            VersionRange = dependencyVersionRange,
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

        private static void BuildTargetFrameworks(PackageSpec packageSpec, JObject rawPackageSpec)
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

            var frameworks = rawPackageSpec["frameworks"] as JObject;
            if (frameworks != null)
            {
                foreach (var framework in frameworks)
                {
                    try
                    {
                        BuildTargetFrameworkNode(packageSpec, framework);
                    }
                    catch (Exception ex)
                    {
                        throw PackageSpecFormatException.Create(ex, framework.Value, packageSpec.FilePath);
                    }
                }
            }
        }

        private static bool BuildTargetFrameworkNode(PackageSpec packageSpec, KeyValuePair<string, JToken> targetFramework)
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
                packageSpec.FilePath,
                targetFrameworkInformation.Dependencies,
                properties,
                "dependencies",
                isGacOrFrameworkReference: false);

            var frameworkAssemblies = new List<LibraryDependency>();
            PopulateDependencies(
                packageSpec.FilePath,
                frameworkAssemblies,
                properties,
                "frameworkAssemblies",
                isGacOrFrameworkReference: true);

            frameworkAssemblies.ForEach(d => targetFrameworkInformation.Dependencies.Add(d));


            packageSpec.TargetFrameworks.Add(targetFrameworkInformation);

            return true;
        }

        private static NuGetFramework GetFramework(string key)
        {
            return NuGetFramework.Parse(key);
        }

        private static string GetDirectoryName(string path)
        {
            path = path.TrimEnd(Path.DirectorySeparatorChar);
            return path.Substring(Path.GetDirectoryName(path).Length).Trim(Path.DirectorySeparatorChar);
        }
    }
}