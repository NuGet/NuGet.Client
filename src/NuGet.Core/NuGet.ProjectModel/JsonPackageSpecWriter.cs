using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.RuntimeModel;
using NuGet.LibraryModel;
using NuGet.Frameworks;

namespace NuGet.ProjectModel
{
    public class JsonPackageSpecWriter
    {
        public static void WritePackageSpec(PackageSpec packageSpec, string fileName = null)
        {
            // Will write to packageSpec.FilePath if fileName is null

            JObject json = new JObject();

            SetValue(json, "title", packageSpec.Title);
            SetValue(json, "version", packageSpec.Version.ToNormalizedString());
            SetValue(json, "description", packageSpec.Description);
            SetArrayValue(json, "authors", packageSpec.Authors);
            SetArrayValue(json, "owners", packageSpec.Owners);
            SetArrayValue(json, "tags", packageSpec.Tags);
            SetValue(json, "projectUrl", packageSpec.ProjectUrl);
            SetValue(json, "iconUrl", packageSpec.IconUrl);
            SetValue(json, "licenseUrl", packageSpec.LicenseUrl);
            SetValue(json, "copyright", packageSpec.Copyright);
            SetValue(json, "language", packageSpec.Language);
            SetValue(json, "summary", packageSpec.Summary);
            SetValue(json, "releaseNotes", packageSpec.ReleaseNotes);
            SetValue(json, "requireLicenseAcceptance", packageSpec.RequireLicenseAcceptance.ToString());
            SetArrayValue(json, "contentFiles", packageSpec.ContentFiles);
            SetDictionaryValue(json, "packInclude", packageSpec.PackInclude);
            SetDictionaryValues(json, "scripts", packageSpec.Scripts);

            if (packageSpec.Dependencies.Any())
            {
                SetDependencies(json, packageSpec.Dependencies);
            }

            if (packageSpec.Tools.Any())
            {
                JObject tools = new JObject();
                foreach (var tool in packageSpec.Tools)
                {
                    JObject toolObject = new JObject();
                    toolObject["version"] = tool.LibraryRange.VersionRange.ToNormalizedString();

                    if (tool.Imports.Any())
                    {
                        SetImports(toolObject, tool.Imports);
                    }
                    tools[tool.LibraryRange.Name] = toolObject;
                }

                SetValue(json, "tools", tools);
            }

            if (packageSpec.TargetFrameworks.Any())
            {
                JObject frameworks = new JObject();
                foreach (var framework in packageSpec.TargetFrameworks)
                {
                    JObject frameworkObject = new JObject();

                    SetDependencies(frameworkObject, framework.Dependencies);
                    SetImports(frameworkObject, framework.Imports);

                    frameworks[framework.FrameworkName] = frameworkObject;
                }                

                SetValue(json, "frameworks", frameworks);
            }

            if (packageSpec.RuntimeGraph.Runtimes.Any())
            {
                JObject runtimes = new JObject();

                foreach (var runtime in packageSpec.RuntimeGraph.Runtimes)
                {
                    runtime.Value.InheritedRuntimes
                    runtimes[runtime.Key] = runtime.Value.;
                }

                SetValue(json, "runtimes", runtimes);
            }
            if (packageSpec.RuntimeGraph.Supports.Any())
            {
                JObject supports = new JObject();

                SetValue(json, "supports", supports);
            }
        }

        private static void SetDependencies(JObject json, IList<LibraryDependency> libraryDependencies)
        {
            JObject dependencies = new JObject();
            JObject frameworkAssemblies = new JObject();
            foreach (var dependency in libraryDependencies)
            {
                JObject dependencyObject = new JObject();

                SetValue(dependencyObject, "include", dependency.IncludeType.ToString());
                SetValue(dependencyObject, "version", dependency.LibraryRange.VersionRange.ToNormalizedString());
                SetValue(dependencyObject, "suppressParent", dependency.SuppressParent.ToString());
                SetValue(dependencyObject, "type", dependency.Type.ToString());

                if (dependency.LibraryRange.TypeConstraint != LibraryDependencyTarget.Reference)
                {
                    SetValue(dependencyObject, "target", dependency.LibraryRange.TypeConstraint.ToString());
                    dependencies[dependency.Name] = dependencyObject;
                }
                else
                {
                    frameworkAssemblies[dependency.Name] = dependencyObject;
                }
            }

            if (dependencies.HasValues)
            {
                SetValue(json, "dependencies", dependencies);
            }
            if (frameworkAssemblies.HasValues)
            {
                SetValue(json, "frameworkAssemblies", frameworkAssemblies);
            }
        }

        private static void SetImports(JObject json, IReadOnlyList<NuGetFramework> frameworks)
        {
            JArray imports = new JArray();
            foreach (var import in frameworks)
            {
                imports.Add(import.ToString());
            }
            json["imports"] = imports;
        }

        private static void SetValue(JObject json, string name, string value)
        {
            if (value != null)
            {
                json[name] = value;
            }
        }

        private static void SetValue(JObject json, string name, JObject value)
        {
            if (value != null)
            {
                json[name] = value;
            }
        }

        private static void SetArrayValue(JObject json, string name, IEnumerable<string> values)
        {
            if (values != null && values.Any())
            {
                json[name] = new JArray(values);
            }
        }

        private static void SetDictionaryValue(JObject json, string name, IDictionary<string, string> values)
        {
            if (values != null && values.Any())
            {
                json[name] = new JObject();
                foreach (var pair in values)
                {
                    json[name][pair.Key] = pair.Value;
                }
            }
        }

        private static void SetDictionaryValues(JObject json, string name, IDictionary<string, IEnumerable<string>> values)
        {
            if (values != null && values.Any())
            {
                json[name] = new JObject();
                foreach (var pair in values)
                {
                    json[name][pair.Key] = new JArray(pair.Value);
                }
            }
        }
    }
}
